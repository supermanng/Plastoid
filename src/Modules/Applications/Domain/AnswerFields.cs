using System.Text.Json;
using System.Text.Json.Nodes;
using Plastoids.Modules.Sponsorship.Contracts;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Applications.Domain;

/// <summary>Form-field rules: at most 8 fields, no protected characteristics, identity fields hidden in blind review.</summary>
public static class AnswerFields
{
    public static readonly IReadOnlySet<string> ProtectedCharacteristics = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "gender", "sex", "religion", "ethnicity", "ethnicGroup", "tribe", "disability", "maritalStatus", "politicalOpinion", "politicalAffiliation", "sexualOrientation", "pregnancy", "hivStatus", "placeOfOrigin",
    };

    public static readonly IReadOnlySet<string> IdentityFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "fullName", "firstName", "lastName", "surname", "name", "nin", "phone", "phoneNumber", "email", "address", "dateOfBirth", "dob", "photo", "photoUrl",
    };

    public static Result Validate(JsonElement answers)
    {
        if (answers.ValueKind != JsonValueKind.Object)
        {
            return Error.Validation("answers_invalid", "answers must be a JSON object keyed by field name.");
        }

        var count = 0;
        foreach (var property in answers.EnumerateObject())
        {
            count++;
            if (ProtectedCharacteristics.Contains(property.Name))
            {
                return Error.Validation("protected_characteristic_field", $"'{property.Name}' is a protected characteristic and is not collected.");
            }

            if (property.Value.ValueKind == JsonValueKind.String && property.Value.GetString()!.Length > 2000)
            {
                return Error.Validation("answer_too_long", $"'{property.Name}' exceeds 2,000 characters.");
            }
        }

        return count > Application.MaxFields ? Error.Validation("too_many_fields", $"Applications take at most {Application.MaxFields} fields.") : Result.Success();
    }

    public static string? Text(JsonElement answers, string key)
        => answers.ValueKind == JsonValueKind.Object && answers.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()?.Trim() : null;

    public static ApplicantFacts Facts(JsonElement answers)
        => new(Text(answers, "countryOfResidence"), Text(answers, "stateOfResidence"), Text(answers, "educationLevel"), Text(answers, "employmentStatus"));

    /// <summary>Blind review: identity fields removed so reviewers judge the answers, not the person.</summary>
    public static JsonElement Blind(string answersJson)
    {
        var node = JsonNode.Parse(answersJson) as JsonObject ?? [];
        foreach (var key in node.Select(p => p.Key).Where(IdentityFields.Contains).ToList())
        {
            node.Remove(key);
        }

        return JsonDocument.Parse(node.ToJsonString()).RootElement.Clone();
    }
}
