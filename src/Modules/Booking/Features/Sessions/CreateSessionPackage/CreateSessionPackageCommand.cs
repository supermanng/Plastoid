using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Booking.Domain;
using Plastoids.Modules.Booking.Persistence;
using Plastoids.Modules.Booking.Services;
using Plastoids.Modules.Payments.Contracts;
using Plastoids.Modules.Tutors.Contracts;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Booking.Features.Sessions.CreateSessionPackage;

/// <summary>Buy 5 or 10 sessions up front (10 % off the ten-pack). Money write → Idempotency-Key.</summary>
public sealed record CreateSessionPackageCommand(Guid TutorId, int Sessions, int DurationMinutes, string Format, string IdempotencyKey) : ICommand<CreateSessionPackageResponse>, IIdempotentCommand;

public sealed record CreateSessionPackageRequest(Guid TutorId, int Sessions, int DurationMinutes, string Format);

public sealed record CreateSessionPackageResponse(Guid PackageId, int Sessions, Money PricePerSession, Money Total, Guid PaymentIntentId, Uri CheckoutUrl);

internal sealed class CreateSessionPackageValidator : AbstractValidator<CreateSessionPackageCommand>
{
    public CreateSessionPackageValidator()
    {
        RuleFor(x => x.TutorId).NotEmpty();
        RuleFor(x => x.Sessions).Must(s => SessionPackage.AllowedSizes.Contains(s)).WithMessage("sessions must be 5 or 10.");
        RuleFor(x => x.DurationMinutes).Must(d => BookingPricing.AllowedDurations.Contains(d));
        RuleFor(x => x.Format).Must(f => f is "one_to_one" or "group");
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

internal sealed class CreateSessionPackageHandler(BookingDbContext db, ITutorSchedule tutors, IPaymentOperations payments, ICurrentUser currentUser, ICurrentTenant tenant) : ICommandHandler<CreateSessionPackageCommand, CreateSessionPackageResponse>
{
    public async Task<Result<CreateSessionPackageResponse>> Handle(CreateSessionPackageCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } learnerId)
        {
            return Error.Unauthorized("not_signed_in", "Sign in first.");
        }

        var tutor = await tutors.GetAsync(request.TutorId, cancellationToken);
        if (tutor is null || !tutor.IsVerified)
        {
            return Error.NotFound("tutor_not_found", "No verified tutor with that id.");
        }

        if (!tutor.Formats.Contains(request.Format))
        {
            return Error.Validation("format_not_offered", $"This tutor does not offer {request.Format} sessions.");
        }

        var rule = tutor.PriceRules.FirstOrDefault(r => r.Format == request.Format && r.DurationMinutes == request.DurationMinutes);
        var perSession = rule?.Price ?? new Money((long)Math.Round(tutor.HourlyRate.AmountKobo * request.DurationMinutes / 60m, 0, MidpointRounding.ToEven), tutor.HourlyRate.Currency);
        var created = SessionPackage.Create(tenant.TenantId, tutor.TutorId, tutor.UserId, learnerId, request.Format, request.DurationMinutes, request.Sessions, perSession);
        if (created.IsFailure)
        {
            return Result.Failure<CreateSessionPackageResponse>(created.Error);
        }

        var package = created.Value;
        db.Packages.Add(package);
        var intent = await payments.CreateIntentAsync(new IntentRequest(learnerId, PaymentPurposes.Booking, package.Id, package.Total, tutor.UserId, $"Package of {request.Sessions} {request.DurationMinutes}-minute sessions"), cancellationToken);
        if (intent.IsFailure)
        {
            return Result.Failure<CreateSessionPackageResponse>(intent.Error);
        }

        package.AttachPayment(intent.Value.PaymentIntentId);
        return new CreateSessionPackageResponse(package.Id, package.SessionsTotal, package.PricePerSession, package.Total, intent.Value.PaymentIntentId, intent.Value.CheckoutUrl);
    }
}

internal static class CreateSessionPackageEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/packages", async (CreateSessionPackageRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new CreateSessionPackageCommand(body.TutorId, body.Sessions, body.DurationMinutes, body.Format, key), ct)).ToCreatedResult(r => $"/api/v1/bookings/packages/{r.PackageId}");
            })
            .WithName("CreateSessionPackage")
            .WithSummary("Buy a 5- or 10-session package (10 % off the ten-pack). Requires Idempotency-Key.")
            .RequireAuthorization(Policies.Authenticated);
    }
}
