using Plastoids.Modules.Catalogue.Services;
using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Catalogue.Features.Publishing.SubmitForReview;

public sealed record SubmitForReviewCommand(Guid CourseId) : ICommand<SubmitForReviewResponse>;

public sealed record SubmitForReviewResponse(Guid CourseId, string Status, bool RequiresHumanReview, IReadOnlyList<CheckResult> Checks);
