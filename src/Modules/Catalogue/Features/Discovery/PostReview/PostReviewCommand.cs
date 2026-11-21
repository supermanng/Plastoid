using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Catalogue.Features.Discovery.PostReview;

public sealed record PostReviewCommand(Guid CourseId, Guid EnrolmentId, int Rating, string Body) : ICommand<PostReviewResponse>;

public sealed record PostReviewRequest(Guid EnrolmentId, int Rating, string Body);

public sealed record PostReviewResponse(Guid ReviewId, decimal CourseRatingAverage, int CourseRatingCount);
