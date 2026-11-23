using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Identity.Authorization;
using Plastoids.Modules.Identity.Contracts;
using Plastoids.Modules.Sponsorship.Domain;
using Plastoids.Modules.Sponsorship.Persistence;
using Plastoids.Modules.Sponsorship.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Sponsorship.Features.Seats.AllocateSeat;

/// <summary>
/// ADR 0021 §3: give one seat to a learner named by phone or email. An existing account is enrolled at once (entry 15,
/// seat-allocated event); otherwise the seat waits as "invited" under the phone's blind index or the lower-case email until that contact registers.
/// </summary>
public sealed record AllocateSeatCommand(Guid OrganisationId, Guid BlockId, string? Phone, string? Email, string? DisplayName, string IdempotencyKey) : ICommand<SeatAllocationView>, IIdempotentCommand;

public sealed record AllocateSeatRequest(string? Phone, string? Email, string? DisplayName);

internal sealed class AllocateSeatValidator : AbstractValidator<AllocateSeatCommand>
{
    public AllocateSeatValidator()
    {
        RuleFor(x => x.OrganisationId).NotEmpty();
        RuleFor(x => x.BlockId).NotEmpty();
        RuleFor(x => x.Phone).MaximumLength(32);
        RuleFor(x => x.Email).MaximumLength(254);
        RuleFor(x => x.DisplayName).MaximumLength(120);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

internal sealed class AllocateSeatHandler(SponsorshipDbContext db, IUserDirectory users, IFieldEncryptor encryptor, SeatAllocator allocator, ICurrentUser currentUser, ICurrentTenant tenant, IClock clock)
    : ICommandHandler<AllocateSeatCommand, SeatAllocationView>
{
    public async Task<Result<SeatAllocationView>> Handle(AllocateSeatCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized("not_signed_in", "Sign in first.");
        }

        var hasPhone = !string.IsNullOrWhiteSpace(request.Phone);
        var email = ContactNormaliser.NormaliseEmail(request.Email);
        if (!hasPhone && email is null)
        {
            return Error.Validation("contact_required", "Name the learner by phone or email.");
        }

        string? e164 = null;
        if (hasPhone && !ContactNormaliser.TryNormalisePhone(request.Phone, out e164))
        {
            return Error.Validation("phone_invalid", "The phone number is not valid.");
        }

        var block = await db.SeatBlocks.SingleOrDefaultAsync(b => b.Id == request.BlockId && b.OrganisationId == request.OrganisationId, cancellationToken);
        if (block is null)
        {
            return Error.NotFound("block_not_found", "No such seat block for this organisation.");
        }

        if (block.Status != SeatBlockStatus.Active)
        {
            return Error.Conflict("block_closed", "The seat block is closed.");
        }

        if (block.Open <= 0)
        {
            return Error.Conflict("no_open_seats", "Every seat in this block has been allocated or released.");
        }

        var now = clock.UtcNow;
        var account = await users.FindByContactAsync(e164, email, cancellationToken);
        var phoneHash = e164 is null ? null : encryptor.BlindIndex(e164);
        var contactKind = e164 is null ? ContactKinds.Email : ContactKinds.Phone;
        var masked = e164 is null ? email! : ContactNormaliser.MaskPhone(e164);

        var taken = account is null
            ? await db.SeatAllocations.AnyAsync(a => a.BlockId == block.Id && ((phoneHash != null && a.PhoneHash == phoneHash) || (email != null && a.Email == email)), cancellationToken)
            : await db.SeatAllocations.AnyAsync(a => a.BlockId == block.Id && (a.LearnerId == account.UserId || (phoneHash != null && a.PhoneHash == phoneHash) || (email != null && a.Email == email)), cancellationToken);
        if (taken)
        {
            return Error.Conflict("seat_already_allocated", "This learner already holds a seat in the block.");
        }

        var seatNo = block.TakeSeat();
        if (seatNo.IsFailure)
        {
            return Result.Failure<SeatAllocationView>(seatNo.Error);
        }

        var displayName = string.IsNullOrWhiteSpace(request.DisplayName) ? account?.DisplayName ?? masked : request.DisplayName.Trim();
        var allocation = SeatAllocation.Invite(tenant.TenantId, block, seatNo.Value, displayName, contactKind, phoneHash, email, masked, userId, now);
        if (account is not null)
        {
            var enrolled = await allocator.EnrolAsync(block, allocation, account.UserId, now, cancellationToken);
            if (enrolled.IsFailure)
            {
                return Result.Failure<SeatAllocationView>(enrolled.Error);
            }
        }

        db.SeatAllocations.Add(allocation);
        return SeatViews.Allocation(allocation);
    }
}

internal static class AllocateSeatEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/{blockId:guid}/allocations", async (Guid organisationId, Guid blockId, AllocateSeatRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new AllocateSeatCommand(organisationId, blockId, body.Phone, body.Email, body.DisplayName, key), ct)).ToCreatedResult(v => $"/api/v1/sponsorship/organisations/{organisationId}/seat-blocks/{blockId}");
            })
            .WithName("AllocateSeat")
            .WithSummary("Sponsor owner/finance: give a seat to a learner by phone or email; enrolled at once when the account exists, invited otherwise. Requires Idempotency-Key.")
            .RequireAuthorization(IdentityPolicies.OrganisationFinance);
    }
}
