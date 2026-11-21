using Microsoft.EntityFrameworkCore;
using Plastoids.Modules.Catalogue.Domain;
using Plastoids.Modules.Catalogue.Persistence;
using Plastoids.Modules.Tutors.Contracts;
using Plastoids.SharedKernel.Messaging;

namespace Plastoids.Modules.Catalogue.Consumers;

/// <summary>Keeps the local verified-tutor projection current (publish checks read it).</summary>
internal sealed class TutorVerifiedConsumer(CatalogueDbContext db) : IIntegrationEventHandler<TutorVerified>
{
    public async Task HandleAsync(TutorVerified @event, IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        var existing = await db.VerifiedTutors.SingleOrDefaultAsync(t => t.TutorId == @event.TutorId, cancellationToken);
        if (existing is null)
        {
            db.VerifiedTutors.Add(VerifiedTutor.Record(envelope.TenantId, @event.TutorId, @event.UserId, @event.Level, envelope.OccurredAt));
        }
        else
        {
            existing.UpdateLevel(@event.Level);
        }
    }
}
