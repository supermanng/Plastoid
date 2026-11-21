using FluentValidation;
using Plastoids.Modules.Payments.Contracts;

namespace Plastoids.Modules.Payments.Features.Checkout.CreatePaymentIntent;

internal sealed class CreatePaymentIntentValidator : AbstractValidator<CreatePaymentIntentCommand>
{
    public CreatePaymentIntentValidator()
    {
        RuleFor(x => x.Purpose).Must(PaymentPurposes.IsValid).WithMessage("purpose must be course, booking or co_pay.");
        RuleFor(x => x.QuoteId).NotEmpty().MaximumLength(64).When(x => x.Purpose == PaymentPurposes.Course).WithMessage("quoteId is required for course purchases.");
        When(x => x.Purpose != PaymentPurposes.Course, () =>
        {
            RuleFor(x => x.ReferenceId).NotNull().NotEqual(Guid.Empty);
            RuleFor(x => x.Amount).NotNull();
            RuleFor(x => x.Amount!.Value.AmountKobo).GreaterThan(0).LessThanOrEqualTo(5_000_000_000).When(x => x.Amount is not null);
            RuleFor(x => x.PayeeUserId).NotNull().When(x => x.Purpose == PaymentPurposes.Booking);
        });
        RuleFor(x => x.LearnerSource).Must(s => s is null or LearnerSources.Platform or LearnerSources.TutorReferred).WithMessage("learnerSource must be platform or tutor_referred.");
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}
