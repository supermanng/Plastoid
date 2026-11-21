using Microsoft.EntityFrameworkCore;
using Plastoids.Modules.Applications.Contracts;
using Plastoids.Modules.Catalogue.Contracts;
using Plastoids.Modules.Lms.Contracts;
using Plastoids.Modules.Lms.Domain;
using Plastoids.Modules.Lms.Persistence;
using Plastoids.Modules.Lms.Services;
using Plastoids.Modules.Payments.Contracts;
using Plastoids.SharedKernel.Messaging;

namespace Plastoids.Modules.Lms.Consumers;

/// <summary>payments.payment-succeeded.v1 with purpose=course → self-funded enrolment on the paid offering (idempotent on the intent).</summary>
internal sealed class CoursePaymentConsumer(EnrolmentService enrolments) : IIntegrationEventHandler<PaymentSucceeded>
{
    public async Task HandleAsync(PaymentSucceeded @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        if (@event.Purpose != PaymentPurposes.Course)
        {
            return;
        }

        var result = await enrolments.EnrolAsync(@event.PayerId, @event.ReferenceId, FundingSources.Self, @event.PaymentIntentId, "payment:" + @event.PaymentIntentId.ToString("N"), null, cancellationToken);
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Enrolment from payment {@event.PaymentIntentId} failed: {result.Error.Code}");
        }
    }
}

/// <summary>applications.offer-accepted.v1 → sponsored enrolment (idempotent on the application).</summary>
internal sealed class OfferAcceptedConsumer(EnrolmentService enrolments) : IIntegrationEventHandler<OfferAccepted>
{
    public async Task HandleAsync(OfferAccepted @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        var result = await enrolments.EnrolAsync(@event.LearnerId, @event.OfferingId, FundingSources.Sponsored, @event.ProgrammeId, "application:" + @event.ApplicationId.ToString("N"), null, cancellationToken);
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Sponsored enrolment for application {@event.ApplicationId} failed: {result.Error.Code}");
        }
    }
}

/// <summary>catalogue.course-published.v1 → local reference of publishable courses and their offerings.</summary>
internal sealed class CoursePublishedConsumer(LmsDbContext db) : IIntegrationEventHandler<CoursePublished>
{
    public async Task HandleAsync(CoursePublished @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        var existing = await db.PublishedCourses.SingleOrDefaultAsync(c => c.CourseId == @event.CourseId, cancellationToken);
        if (existing is null)
        {
            db.PublishedCourses.Add(PublishedCourseRef.Record(envelope.TenantId, @event.CourseId, @event.VersionNo, @event.OwnerId, @event.Slug, @event.OfferingIds));
        }
        else
        {
            existing.Update(@event.VersionNo, @event.OfferingIds);
        }
    }
}
