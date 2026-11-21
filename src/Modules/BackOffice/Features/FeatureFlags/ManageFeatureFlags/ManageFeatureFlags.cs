using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.BackOffice.Domain;
using Plastoids.Modules.BackOffice.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.BackOffice.Features.FeatureFlags.ManageFeatureFlags;

public sealed record ListFeatureFlagsQuery : IQuery<IReadOnlyList<FeatureFlagView>>;

/// <summary>Upsert by key. Audited (before/after) like every admin mutation.</summary>
public sealed record SetFeatureFlagCommand(string Key, bool Enabled, string? Description, string IdempotencyKey) : ICommand<FeatureFlagView>, IIdempotentCommand;

public sealed record SetFeatureFlagRequest(bool Enabled, string? Description);

public sealed record FeatureFlagView(string Key, bool Enabled, string Description, Guid UpdatedBy, DateTime UpdatedAt)
{
    public static FeatureFlagView From(FeatureFlag f) => new(f.Key, f.Enabled, f.Description, f.UpdatedBy, f.UpdatedAt);
}

internal sealed class SetFeatureFlagValidator : AbstractValidator<SetFeatureFlagCommand>
{
    public SetFeatureFlagValidator()
    {
        RuleFor(x => x.Key).NotEmpty().Must(FeatureFlag.IsValidKey).WithMessage("Flag keys are kebab-case, 2–64 characters.");
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

internal sealed class ListFeatureFlagsHandler(BackOfficeDbContext db) : IQueryHandler<ListFeatureFlagsQuery, IReadOnlyList<FeatureFlagView>>
{
    public async Task<Result<IReadOnlyList<FeatureFlagView>>> Handle(ListFeatureFlagsQuery request, CancellationToken ct)
        => (await db.FeatureFlags.AsNoTracking().OrderBy(f => f.Key).ToListAsync(ct)).Select(FeatureFlagView.From).ToList();
}

internal sealed class SetFeatureFlagHandler(BackOfficeDbContext db, ICurrentUser currentUser, ICurrentTenant tenant) : ICommandHandler<SetFeatureFlagCommand, FeatureFlagView>
{
    public async Task<Result<FeatureFlagView>> Handle(SetFeatureFlagCommand request, CancellationToken ct)
    {
        var by = currentUser.UserId ?? Guid.Empty;
        var flag = await db.FeatureFlags.SingleOrDefaultAsync(f => f.Key == request.Key, ct);
        if (flag is null)
        {
            var created = FeatureFlag.Create(tenant.TenantId, request.Key, request.Enabled, request.Description ?? string.Empty, by);
            if (created.IsFailure)
            {
                return Result.Failure<FeatureFlagView>(created.Error);
            }

            flag = created.Value;
            db.FeatureFlags.Add(flag);
        }
        else
        {
            flag.Set(request.Enabled, request.Description, by);
        }

        return FeatureFlagView.From(flag);
    }
}

internal static class FeatureFlagEndpoints
{
    public static void Map(IEndpointRouteBuilder admin)
    {
        admin.MapGet("/feature-flags", async (ISender sender, CancellationToken ct) => (await sender.Send(new ListFeatureFlagsQuery(), ct)).ToHttpResult())
            .WithName("ListFeatureFlags")
            .RequireAuthorization(Policies.StaffAdmin);

        admin.MapPut("/feature-flags/{key}", async (string key, SetFeatureFlagRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var idempotencyKey = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new SetFeatureFlagCommand(key, body.Enabled, body.Description, idempotencyKey), ct)).ToHttpResult();
            })
            .WithName("SetFeatureFlag")
            .WithSummary("Staff: create or update a feature flag (audited). Requires Idempotency-Key.")
            .RequireAuthorization(Policies.StaffAdmin);
    }
}
