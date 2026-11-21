using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Notifications.Domain;
using Plastoids.Modules.Notifications.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Notifications.Features.Preferences.ManagePreferences;

/// <summary>Channel order, opt-outs and quiet hours (local time; default Africa/Lagos). In-app is always on.</summary>
public sealed record GetPreferencesQuery : IQuery<PreferencesView>;

public sealed record UpdatePreferencesCommand(IReadOnlyList<string>? ChannelOrder, IReadOnlyList<string>? OptedOut, TimeOnly? QuietStart, TimeOnly? QuietEnd, string? TimeZone) : ICommand<PreferencesView>;

public sealed record PreferencesView(IReadOnlyList<string> ChannelOrder, IReadOnlyList<string> OptedOut, TimeOnly? QuietStart, TimeOnly? QuietEnd, string TimeZone, bool QuietNow)
{
    public static PreferencesView From(ChannelPreference p, DateTime now) => new(p.ChannelOrder, p.OptedOut, p.QuietStart, p.QuietEnd, p.TimeZone, p.IsQuiet(now));
}

internal sealed class GetPreferencesValidator : AbstractValidator<GetPreferencesQuery>
{
}

internal sealed class UpdatePreferencesValidator : AbstractValidator<UpdatePreferencesCommand>
{
    public UpdatePreferencesValidator()
    {
        RuleFor(x => x.ChannelOrder).Must(c => c!.Count <= 4).When(x => x.ChannelOrder is not null);
        RuleFor(x => x.TimeZone).MaximumLength(64);
    }
}

internal sealed class GetPreferencesHandler(NotificationsDbContext db, ICurrentUser currentUser, ICurrentTenant tenant, IClock clock) : IQueryHandler<GetPreferencesQuery, PreferencesView>
{
    public async Task<Result<PreferencesView>> Handle(GetPreferencesQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? Guid.Empty;
        var preference = await db.Preferences.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken) ?? ChannelPreference.Default(tenant.TenantId, userId);
        return PreferencesView.From(preference, clock.UtcNow);
    }
}

internal sealed class UpdatePreferencesHandler(NotificationsDbContext db, ICurrentUser currentUser, ICurrentTenant tenant, IClock clock) : ICommandHandler<UpdatePreferencesCommand, PreferencesView>
{
    public async Task<Result<PreferencesView>> Handle(UpdatePreferencesCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? Guid.Empty;
        var preference = await db.Preferences.SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (preference is null)
        {
            preference = ChannelPreference.Default(tenant.TenantId, userId);
            db.Preferences.Add(preference);
        }

        var updated = preference.Update(request.ChannelOrder, request.OptedOut, request.QuietStart, request.QuietEnd, request.TimeZone);
        return updated.IsFailure ? Result.Failure<PreferencesView>(updated.Error) : PreferencesView.From(preference, clock.UtcNow);
    }
}

internal static class PreferencesEndpoints
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/preferences", async (ISender sender, CancellationToken ct) => (await sender.Send(new GetPreferencesQuery(), ct)).ToHttpResult())
            .WithName("GetNotificationPreferences")
            .RequireAuthorization(Policies.Authenticated);

        group.MapPut("/preferences", async (UpdatePreferencesCommand body, ISender sender, CancellationToken ct) => (await sender.Send(body, ct)).ToHttpResult())
            .WithName("UpdateNotificationPreferences")
            .WithSummary("Channel order (push, whatsapp, sms, email), opt-outs and quiet hours (quietStart/quietEnd local time, timeZone IANA).")
            .RequireAuthorization(Policies.Authenticated);
    }
}
