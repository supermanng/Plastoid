using System.Text.Json;
using Plastoids.Modules.Sponsorship.Contracts;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Sponsorship.Domain;

/// <summary>
/// Deterministic eligibility rules (blueprint §9.13: hard filters are deterministic; protected characteristics are never inputs).
/// Rules document: { minAge, maxAge, residency: ["NG"], states: [...], education: [...], employmentStatus: [...], notPreviouslySponsored: true }.
/// </summary>
public static class EligibilityEngine
{
    public static readonly IReadOnlySet<string> AllowedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "minAge", "maxAge", "residency", "states", "education", "employmentStatus", "notPreviouslySponsored",
    };

    /// <summary>Characteristics that may never appear in rules or scoring inputs (Nigerian Constitution s.42 and blueprint §9.13).</summary>
    public static readonly IReadOnlySet<string> ProtectedCharacteristics = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "gender", "sex", "religion", "ethnicity", "ethnicGroup", "tribe", "disability", "maritalStatus", "politicalOpinion", "politicalAffiliation", "sexualOrientation", "pregnancy", "hivStatus", "placeOfOrigin",
    };

    public static Result ValidateRules(string rulesJson)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(rulesJson);
        }
        catch (JsonException)
        {
            return Error.Validation("eligibility_rules_invalid", "eligibilityRules must be a JSON object.");
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Error.Validation("eligibility_rules_invalid", "eligibilityRules must be a JSON object.");
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (ProtectedCharacteristics.Contains(property.Name))
                {
                    return Error.Validation("eligibility_rule_discriminatory", $"'{property.Name}' is a protected characteristic and cannot be an eligibility rule.");
                }

                if (!AllowedKeys.Contains(property.Name))
                {
                    return Error.Validation("eligibility_rule_unknown", $"Unknown eligibility rule '{property.Name}'. Allowed: {string.Join(", ", AllowedKeys)}.");
                }
            }
        }

        return Result.Success();
    }

    public static EligibilityOutcome Evaluate(JsonElement rules, int? ageYears, ApplicantFacts facts, bool previouslySponsored)
    {
        var reasons = new List<string>();
        if (rules.ValueKind != JsonValueKind.Object)
        {
            return new EligibilityOutcome(true, reasons);
        }

        if (rules.TryGetProperty("minAge", out var min) && min.ValueKind == JsonValueKind.Number && (ageYears is null || ageYears < min.GetInt32()))
        {
            reasons.Add(ageYears is null ? "age_unknown" : $"under_min_age_{min.GetInt32()}");
        }

        if (rules.TryGetProperty("maxAge", out var max) && max.ValueKind == JsonValueKind.Number && (ageYears is null || ageYears > max.GetInt32()))
        {
            reasons.Add(ageYears is null ? "age_unknown" : $"over_max_age_{max.GetInt32()}");
        }

        CheckList(rules, "residency", facts.CountryOfResidence, "residency_not_eligible", reasons);
        CheckList(rules, "states", facts.StateOfResidence, "state_not_eligible", reasons);
        CheckList(rules, "education", facts.EducationLevel, "education_not_eligible", reasons);
        CheckList(rules, "employmentStatus", facts.EmploymentStatus, "employment_status_not_eligible", reasons);
        if (rules.TryGetProperty("notPreviouslySponsored", out var fresh) && fresh.ValueKind == JsonValueKind.True && previouslySponsored)
        {
            reasons.Add("previously_sponsored");
        }

        return new EligibilityOutcome(reasons.Count == 0, reasons.Distinct().ToList());
    }

    private static void CheckList(JsonElement rules, string key, string? value, string reason, List<string> reasons)
    {
        if (!rules.TryGetProperty(key, out var list) || list.ValueKind != JsonValueKind.Array || list.GetArrayLength() == 0)
        {
            return;
        }

        var allowed = list.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!.Trim()).ToList();
        if (value is null || !allowed.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            reasons.Add(reason);
        }
    }
}
