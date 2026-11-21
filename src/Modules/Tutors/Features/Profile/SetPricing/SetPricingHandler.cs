using Microsoft.EntityFrameworkCore;
using Plastoids.Modules.Tutors.Domain;
using Plastoids.Modules.Tutors.Persistence;
using Plastoids.Modules.Tutors.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Tutors.Features.Profile.SetPricing;

internal sealed class SetPricingHandler(TutorsDbContext db, TutorAccess access) : ICommandHandler<SetPricingCommand>
{
    public async Task<Result> Handle(SetPricingCommand request, CancellationToken cancellationToken)
    {
        var mine = await access.MineAsync(cancellationToken);
        if (mine.IsFailure)
        {
            return mine.Error;
        }

        var profile = mine.Value;
        var priced = profile.SetPricing(request.HourlyRate);
        if (priced.IsFailure)
        {
            return priced;
        }

        db.PriceRules.RemoveRange(await db.PriceRules.Where(r => r.TutorProfileId == profile.Id).ToListAsync(cancellationToken));
        foreach (var rule in request.Rules.DistinctBy(r => (r.Format, r.DurationMinutes)))
        {
            db.PriceRules.Add(PriceRule.Create(profile.TenantId, profile.Id, rule.Format, rule.DurationMinutes, rule.Price, rule.MaxLearners));
        }

        return Result.Success();
    }
}
