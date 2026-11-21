using System.Text.Json;

namespace Plastoids.Modules.Lms.Contracts;

public sealed record AssessmentGrade(decimal Score, decimal MaxScore, bool Passed, int Correct, int Total);

/// <summary>
/// The Lms assessment engine for other modules (Applications' entry assessment): the same quiz document format
/// Catalogue stores ({ passMarkPercent, timeLimitMinutes, questions: [...] }) and the same grading rules.
/// </summary>
public interface IAssessmentGrader
{
    AssessmentGrade Grade(JsonElement quiz, JsonElement answers);
    int TimeLimitMinutes(JsonElement quiz);
    /// <summary>The quiz without correct answers, safe to send to the candidate.</summary>
    JsonElement Redact(JsonElement quiz);
}
