using System.Text.Json;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Lms.Domain;

public enum AttemptStatus
{
    InProgress = 0,
    Submitted = 1,
    Expired = 2,
}

/// <summary>A quiz/exam attempt with server-side timing (blueprint §12): answers after the deadline are refused and the attempt expires.</summary>
public sealed class Attempt : Entity
{
    public const int DefaultTimeLimitMinutes = 30;

    private Attempt()
    {
    }

    private Attempt(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid EnrolmentId { get; private set; }
    public Guid LessonId { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime DeadlineAt { get; private set; }
    public DateTime? SubmittedAt { get; private set; }
    public AttemptStatus Status { get; private set; }
    public string AnswersJson { get; private set; } = "{}";
    public decimal Score { get; private set; }
    public decimal MaxScore { get; private set; }
    public bool Passed { get; private set; }
    public int Number { get; private set; }

    public static Attempt Start(Guid tenantId, Guid enrolmentId, Guid lessonId, int number, int timeLimitMinutes, DateTime utcNow)
        => new(NewId(), tenantId) { EnrolmentId = enrolmentId, LessonId = lessonId, Number = number, StartedAt = utcNow, DeadlineAt = utcNow.AddMinutes(timeLimitMinutes), Status = AttemptStatus.InProgress };

    public Result SaveAnswers(string answersJson, DateTime utcNow)
    {
        if (Status != AttemptStatus.InProgress)
        {
            return Error.Conflict("attempt_closed", "This attempt is already submitted or expired.");
        }

        if (utcNow > DeadlineAt)
        {
            Status = AttemptStatus.Expired;
            return Error.Conflict("attempt_expired", "The time limit has passed; the attempt is closed.");
        }

        AnswersJson = answersJson;
        return Result.Success();
    }

    public Result Submit(GradeResult grade, DateTime utcNow)
    {
        if (Status != AttemptStatus.InProgress)
        {
            return Error.Conflict("attempt_closed", "This attempt is already submitted or expired.");
        }

        // A small grace for network latency; anything later is graded as it stands but marked expired.
        if (utcNow > DeadlineAt.AddSeconds(30))
        {
            Status = AttemptStatus.Expired;
        }
        else
        {
            Status = AttemptStatus.Submitted;
        }

        SubmittedAt = utcNow;
        Score = grade.Score;
        MaxScore = grade.MaxScore;
        Passed = Status == AttemptStatus.Submitted && grade.Passed;
        return Result.Success();
    }
}

public sealed record GradeResult(decimal Score, decimal MaxScore, bool Passed, int Correct, int Total);

/// <summary>Auto-grading of the quiz content stored by Catalogue: { passMarkPercent, questions: [{ prompt, options[], correctIndex | correctIndexes[] | answer }] }.</summary>
public static class Grader
{
    public static GradeResult Grade(JsonElement quizContent, JsonElement answers)
    {
        var passMark = quizContent.TryGetProperty("passMarkPercent", out var pm) && pm.ValueKind == JsonValueKind.Number ? pm.GetInt32() : 70;
        if (!quizContent.TryGetProperty("questions", out var questions) || questions.ValueKind != JsonValueKind.Array)
        {
            return new GradeResult(0, 0, false, 0, 0);
        }

        var total = questions.GetArrayLength();
        var correct = 0;
        var index = 0;
        foreach (var q in questions.EnumerateArray())
        {
            var key = index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (answers.ValueKind == JsonValueKind.Object && answers.TryGetProperty(key, out var given) && IsCorrect(q, given))
            {
                correct++;
            }

            index++;
        }

        var score = total == 0 ? 0 : Math.Round(100m * correct / total, 2);
        return new GradeResult(score, 100m, score >= passMark, correct, total);
    }

    private static bool IsCorrect(JsonElement question, JsonElement given)
    {
        // Multi-select: correctIndexes [] must match the given set exactly.
        if (question.TryGetProperty("correctIndexes", out var multi) && multi.ValueKind == JsonValueKind.Array)
        {
            if (given.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var expected = multi.EnumerateArray().Select(e => e.GetInt32()).ToHashSet();
            var actual = given.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Number).Select(e => e.GetInt32()).ToHashSet();
            return expected.SetEquals(actual);
        }

        // Single choice.
        if (question.TryGetProperty("correctIndex", out var single) && single.ValueKind == JsonValueKind.Number)
        {
            return given.ValueKind == JsonValueKind.Number && given.GetInt32() == single.GetInt32();
        }

        // Short answer: exact match after trimming and case folding.
        if (question.TryGetProperty("answer", out var text) && text.ValueKind == JsonValueKind.String)
        {
            return given.ValueKind == JsonValueKind.String && string.Equals(given.GetString()?.Trim(), text.GetString()?.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}

public enum SubmissionStatus
{
    Submitted = 0,
    Graded = 1,
}

/// <summary>Assignment file upload graded by the tutor against a rubric (story E1).</summary>
public sealed class AssignmentSubmission : Entity
{
    private AssignmentSubmission()
    {
    }

    private AssignmentSubmission(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid EnrolmentId { get; private set; }
    public Guid LessonId { get; private set; }
    public string BlobKey { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public DateTime SubmittedAt { get; private set; }
    public SubmissionStatus Status { get; private set; }
    public decimal? Score { get; private set; }
    public decimal? MaxScore { get; private set; }
    public string? RubricJson { get; private set; }
    public string? Feedback { get; private set; }
    public Guid? GradedBy { get; private set; }
    public DateTime? GradedAt { get; private set; }
    public bool Passed { get; private set; }

    public static AssignmentSubmission Create(Guid tenantId, Guid enrolmentId, Guid lessonId, string blobKey, string contentType, DateTime utcNow)
        => new(NewId(), tenantId) { EnrolmentId = enrolmentId, LessonId = lessonId, BlobKey = blobKey, ContentType = contentType, SubmittedAt = utcNow, Status = SubmissionStatus.Submitted };

    public Result Grade(decimal score, decimal maxScore, int passMarkPercent, string rubricJson, string? feedback, Guid gradedBy, DateTime utcNow)
    {
        if (maxScore <= 0 || score < 0 || score > maxScore)
        {
            return Error.Validation("grade_out_of_range", "Score must be between 0 and maxScore.");
        }

        Score = score;
        MaxScore = maxScore;
        RubricJson = rubricJson;
        Feedback = feedback?.Trim();
        GradedBy = gradedBy;
        GradedAt = utcNow;
        Status = SubmissionStatus.Graded;
        Passed = 100m * score / maxScore >= passMarkPercent;
        return Result.Success();
    }

    public decimal? ScorePercent => Score is null || MaxScore is null or 0 ? null : Math.Round(100m * Score.Value / MaxScore.Value, 2);
}
