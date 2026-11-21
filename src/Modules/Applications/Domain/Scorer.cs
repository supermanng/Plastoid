using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Applications.Domain;

public sealed record RubricCriterion(string Key, decimal Weight, decimal Max);

/// <summary>Programme scoring rubric: assessment weight (percent of the total) and weighted criteria scored by reviewers.</summary>
public sealed record RubricDefinition(decimal AssessmentWeightPercent, IReadOnlyList<RubricCriterion> Criteria)
{
    public static readonly RubricDefinition AssessmentOnly = new(100, []);
}

/// <summary>
/// The only data scoring ever sees: the entry-assessment percentage and the reviewer's criterion scores.
/// Form answers never reach this type, so protected characteristics cannot influence the score (blueprint §9.13); the
/// architecture test keeps <see cref="Scorer"/> free of JSON so it cannot read them by accident.
/// </summary>
public sealed record ScoringInput(decimal? AssessmentPercent, IReadOnlyDictionary<string, decimal> CriterionScores);

public sealed record ScoreOutcome(decimal? RubricPercent, decimal TotalPercent);

public static class Scorer
{
    public static Result<ScoreOutcome> Compute(ScoringInput input, RubricDefinition rubric)
    {
        decimal? rubricPercent = null;
        if (rubric.Criteria.Count > 0)
        {
            var totalWeight = rubric.Criteria.Sum(c => c.Weight);
            if (totalWeight <= 0)
            {
                return Error.Validation("rubric_weights_invalid", "Rubric weights must sum to a positive number.");
            }

            decimal weighted = 0;
            foreach (var criterion in rubric.Criteria)
            {
                if (!input.CriterionScores.TryGetValue(criterion.Key, out var score))
                {
                    return Error.Validation("rubric_score_missing", $"A score for '{criterion.Key}' is required.");
                }

                if (score < 0 || score > criterion.Max)
                {
                    return Error.Validation("rubric_score_out_of_range", $"'{criterion.Key}' must be between 0 and {criterion.Max}.");
                }

                weighted += criterion.Max == 0 ? 0 : score / criterion.Max * criterion.Weight;
            }

            rubricPercent = Math.Round(100m * weighted / totalWeight, 2);
        }

        var assessmentWeight = Math.Clamp(rubric.AssessmentWeightPercent, 0, 100);
        if (rubricPercent is null)
        {
            assessmentWeight = 100;
        }

        var assessment = input.AssessmentPercent ?? 0;
        var total = Math.Round((assessmentWeight * assessment + (100 - assessmentWeight) * (rubricPercent ?? 0)) / 100m, 2);
        return new ScoreOutcome(rubricPercent, total);
    }
}
