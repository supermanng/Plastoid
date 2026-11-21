using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Booking.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Booking.Features.Sessions.LockSlot;

/// <summary>Story D1: lock the slot for 10 minutes while the learner checks out.</summary>
public sealed record LockSlotCommand(Guid TutorId, DateTime SlotStart, int DurationMinutes, string Format) : ICommand<LockSlotResponse>;

public sealed record LockSlotResponse(Guid TutorId, DateTime SlotStart, DateTime LockedUntil, SlotQuote Quote);

internal sealed class LockSlotValidator : AbstractValidator<LockSlotCommand>
{
    public LockSlotValidator()
    {
        RuleFor(x => x.TutorId).NotEmpty();
        RuleFor(x => x.DurationMinutes).Must(d => BookingPricing.AllowedDurations.Contains(d)).WithMessage("Duration must be 30, 45, 60, 90 or 120 minutes.");
        RuleFor(x => x.Format).Must(f => f is "one_to_one" or "group").WithMessage("format must be one_to_one or group.");
    }
}

internal sealed class LockSlotHandler(BookingPricing pricing, SlotLocks locks, ICurrentUser currentUser, IClock clock) : ICommandHandler<LockSlotCommand, LockSlotResponse>
{
    public async Task<Result<LockSlotResponse>> Handle(LockSlotCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } learnerId)
        {
            return Error.Unauthorized("not_signed_in", "Sign in first.");
        }

        var quote = await pricing.QuoteAsync(request.TutorId, request.Format, request.SlotStart, request.DurationMinutes, cancellationToken);
        if (quote.IsFailure)
        {
            return Result.Failure<LockSlotResponse>(quote.Error);
        }

        if (!await locks.TryLockAsync(request.TutorId, quote.Value.SlotStart, learnerId))
        {
            return Error.Conflict("slot_locked", "Another learner is checking out this slot; try again in a few minutes.");
        }

        return new LockSlotResponse(request.TutorId, quote.Value.SlotStart, clock.UtcNow.Add(SlotLocks.Duration), quote.Value);
    }
}

internal static class LockSlotEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/locks", async (LockSlotCommand command, ISender sender, CancellationToken ct) => (await sender.Send(command, ct)).ToHttpResult())
            .WithName("LockSlot")
            .WithSummary("Lock a slot for 10 minutes before paying (409 slot_locked when another learner holds it).")
            .RequireAuthorization(Policies.Authenticated);
    }
}
