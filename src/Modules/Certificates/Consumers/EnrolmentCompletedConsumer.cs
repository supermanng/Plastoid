using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Messaging;
using Plastoids.Modules.Catalogue.Contracts;
using Plastoids.Modules.Certificates.Contracts;
using Plastoids.Modules.Certificates.Domain;
using Plastoids.Modules.Certificates.Persistence;
using Plastoids.Modules.Identity.Contracts;
using Plastoids.Modules.Lms.Contracts;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Certificates.Consumers;

/// <summary>lms.enrolment-completed.v1 → one immutable certificate per enrolment (story E4: issued automatically on rule).</summary>
internal sealed class EnrolmentCompletedConsumer(CertificatesDbContext db, ICourseOutlineReader courses, IUserDirectory users, IOutbox outbox, IClock clock) : IIntegrationEventHandler<EnrolmentCompleted>
{
    public async Task HandleAsync(EnrolmentCompleted @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        if (await db.Certificates.AnyAsync(c => c.EnrolmentId == @event.EnrolmentId, cancellationToken))
        {
            return;
        }

        var course = await courses.GetPublishedAsync(@event.CourseId, @event.VersionNo, cancellationToken);
        var learner = await users.GetAsync(@event.LearnerId, cancellationToken);
        var templateKey = @event.FundingSource == FundingSources.Sponsored && @event.FundingSourceId is { } programme ? $"sponsor:{programme:N}" : $"course:{@event.CourseId:N}";
        var certificate = Certificate.Issue(envelope.TenantId, @event.EnrolmentId, @event.LearnerId, learner?.DisplayName ?? "Plastoids learner", @event.CourseId, course?.Title ?? "Course", @event.OfferingId, @event.VersionNo, @event.FinalScore, templateKey, clock.UtcNow);
        db.Certificates.Add(certificate);
        outbox.Enqueue(new CertificateIssued(certificate.Id, certificate.Id, certificate.EnrolmentId, certificate.LearnerId, certificate.CourseId, certificate.PublicCode));
        outbox.Enqueue(NotificationRequested.To(certificate.LearnerId, "certificates.issued", new Dictionary<string, string> { ["certificateId"] = certificate.Id.ToString("D"), ["publicCode"] = certificate.PublicCode, ["courseTitle"] = certificate.CourseTitle }));
    }
}
