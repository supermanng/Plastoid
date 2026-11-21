using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Booking.Domain;
using Plastoids.Modules.Booking.Persistence;
using Plastoids.Modules.Booking.Services;
using Plastoids.Modules.Payments.Contracts;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Booking.Features.Sessions.CreateBooking;

/// <summary>Creates the booking and its payment intent (or consumes a package session). Money write → Idempotency-Key.</summary>
public sealed record CreateBookingCommand(Guid TutorId, DateTime SlotStart, int DurationMinutes, string Format, Guid? PackageId, string? Note, string? LearnerSource, string IdempotencyKey)
    : ICommand<CreateBookingResponse>, IIdempotentCommand;

public sealed record CreateBookingRequest(Guid TutorId, DateTime SlotStart, int DurationMinutes, string Format, Guid? PackageId, string? Note, string? LearnerSource);

public sealed record CreateBookingResponse(Guid BookingId, string Status, Money Price, Guid? PaymentIntentId, Uri? CheckoutUrl, DateTime SlotStart, DateTime SlotEnd);

internal sealed class CreateBookingValidator : AbstractValidator<CreateBookingCommand>
{
    public CreateBookingValidator()
    {
        RuleFor(x => x.TutorId).NotEmpty();
        RuleFor(x => x.DurationMinutes).Must(d => BookingPricing.AllowedDurations.Contains(d)).WithMessage("Duration must be 30, 45, 60, 90 or 120 minutes.");
        RuleFor(x => x.Format).Must(f => f is "one_to_one" or "group").WithMessage("format must be one_to_one or group.");
        RuleFor(x => x.Note).MaximumLength(500);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

internal sealed class CreateBookingHandler(BookingDbContext db, BookingPricing pricing, SlotLocks locks, BookingLifecycle lifecycle, IPaymentOperations payments, ICurrentUser currentUser, ICurrentTenant tenant, IClock clock)
    : ICommandHandler<CreateBookingCommand, CreateBookingResponse>
{
    public async Task<Result<CreateBookingResponse>> Handle(CreateBookingCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } learnerId)
        {
            return Error.Unauthorized("not_signed_in", "Sign in first.");
        }

        var quote = await pricing.QuoteAsync(request.TutorId, request.Format, request.SlotStart, request.DurationMinutes, cancellationToken);
        if (quote.IsFailure)
        {
            return Result.Failure<CreateBookingResponse>(quote.Error);
        }

        var q = quote.Value;
        if (!await locks.TryLockAsync(q.TutorId, q.SlotStart, learnerId))
        {
            return Error.Conflict("slot_locked", "Another learner is checking out this slot; try again in a few minutes.");
        }

        SessionPackage? package = null;
        if (request.PackageId is { } packageId)
        {
            package = await db.Packages.SingleOrDefaultAsync(p => p.Id == packageId && p.LearnerId == learnerId && p.TutorId == q.TutorId, cancellationToken);
            if (package is null)
            {
                return Error.NotFound("package_not_found", "No such package for this tutor.");
            }

            if (package.Format != request.Format || package.DurationMinutes != request.DurationMinutes)
            {
                return Error.Validation("package_mismatch", "The package covers a different format or duration.");
            }
        }

        var price = package?.EffectivePricePerSession ?? q.Price;
        var created = Domain.Booking.Create(tenant.TenantId, q.TutorId, q.TutorUserId, learnerId, request.Format, q.SlotStart, request.DurationMinutes, price, package?.Id, request.Note, clock.UtcNow);
        if (created.IsFailure)
        {
            return Result.Failure<CreateBookingResponse>(created.Error);
        }

        var booking = created.Value;
        db.Bookings.Add(booking);

        if (package is not null)
        {
            var used = package.UseSession();
            if (used.IsFailure)
            {
                return Result.Failure<CreateBookingResponse>(used.Error);
            }

            booking.AttachPayment(package.PaymentIntentId!.Value);
            lifecycle.Confirm(booking);
            await locks.ReleaseAsync(q.TutorId, q.SlotStart);
            return new CreateBookingResponse(booking.Id, booking.Status.ToString().ToLowerInvariant(), price, booking.PaymentIntentId, null, booking.SlotStart, booking.SlotEnd);
        }

        var intent = await payments.CreateIntentAsync(new IntentRequest(learnerId, PaymentPurposes.Booking, booking.Id, price, q.TutorUserId, $"{request.DurationMinutes}-minute {request.Format} session", request.LearnerSource ?? LearnerSources.Platform), cancellationToken);
        if (intent.IsFailure)
        {
            return Result.Failure<CreateBookingResponse>(intent.Error);
        }

        booking.AttachPayment(intent.Value.PaymentIntentId);
        return new CreateBookingResponse(booking.Id, booking.Status.ToString().ToLowerInvariant(), price, intent.Value.PaymentIntentId, intent.Value.CheckoutUrl, booking.SlotStart, booking.SlotEnd);
    }
}

internal static class CreateBookingEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/", async (CreateBookingRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new CreateBookingCommand(body.TutorId, body.SlotStart, body.DurationMinutes, body.Format, body.PackageId, body.Note, body.LearnerSource, key), ct))
                    .ToCreatedResult(r => $"/api/v1/bookings/{r.BookingId}");
            })
            .WithName("CreateBooking")
            .WithSummary("Book a slot: returns a checkout URL (or confirms at once from a package). Requires Idempotency-Key.")
            .RequireAuthorization(Policies.Authenticated);
    }
}
