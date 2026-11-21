using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Catalogue.Features.Authoring.AddQuiz;

public sealed record QuizQuestion(string Prompt, IReadOnlyList<string> Options, int CorrectIndex);

public sealed record AddQuizCommand(Guid CourseId, Guid ModuleId, string Title, int Order, int PassMarkPercent, IReadOnlyList<QuizQuestion> Questions) : ICommand<AddQuizResponse>;

public sealed record AddQuizRequest(string Title, int Order, int PassMarkPercent, IReadOnlyList<QuizQuestion> Questions);

public sealed record AddQuizResponse(Guid LessonId, int QuestionCount);
