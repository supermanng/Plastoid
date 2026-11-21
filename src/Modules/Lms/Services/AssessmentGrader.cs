using System.Text.Json;
using System.Text.Json.Nodes;
using Plastoids.Modules.Lms.Contracts;
using Plastoids.Modules.Lms.Domain;

namespace Plastoids.Modules.Lms.Services;

/// <summary>Exposes the Lms grader and timing rules through Lms.Contracts (build prompt Sprint 6: Applications reuses the assessment engine).</summary>
internal sealed class AssessmentGrader : IAssessmentGrader
{
    public AssessmentGrade Grade(JsonElement quiz, JsonElement answers)
    {
        var g = Grader.Grade(quiz, answers);
        return new AssessmentGrade(g.Score, g.MaxScore, g.Passed, g.Correct, g.Total);
    }

    public int TimeLimitMinutes(JsonElement quiz)
        => quiz.ValueKind == JsonValueKind.Object && quiz.TryGetProperty("timeLimitMinutes", out var t) && t.ValueKind == JsonValueKind.Number ? Math.Clamp(t.GetInt32(), 1, 600) : Attempt.DefaultTimeLimitMinutes;

    public JsonElement Redact(JsonElement quiz)
    {
        if (quiz.ValueKind != JsonValueKind.Object)
        {
            return JsonDocument.Parse("{}").RootElement.Clone();
        }

        var node = JsonNode.Parse(quiz.GetRawText())!.AsObject();
        if (node["questions"] is JsonArray questions)
        {
            foreach (var q in questions.OfType<JsonObject>())
            {
                q.Remove("correctIndex");
                q.Remove("correctIndexes");
                q.Remove("answer");
            }
        }

        return JsonDocument.Parse(node.ToJsonString()).RootElement.Clone();
    }
}
