using System.Globalization;
using System.Text.RegularExpressions;
using Plastoids.Infrastructure.Integrations;
using Plastoids.Infrastructure.Sms;
using Plastoids.Infrastructure.Tenancy;
using Plastoids.Modules.Booking.Jobs;
using Plastoids.Modules.Identity.Domain;
using Plastoids.Modules.Ledger.Jobs;
using Plastoids.Modules.Notifications.Jobs;
using Plastoids.Modules.Payouts.Jobs;
using Plastoids.Modules.Sponsorship.Jobs;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Api.Dev;

/// <summary>
/// Development-only helpers under /api/v1/dev that let local tooling (scripts/simulate.py) drive the fakes the integration
/// tests reach through the DI container: the captured OTP SMS, signed gateway and payout webhooks, bank-account names for
/// payout matching and the Quartz jobs "as of" a chosen time. Mapped by Program.cs only in Development and Testing.
/// </summary>
internal static partial class DevEndpoints
{
    private const string Tag = "Development only";
    private const string OnlyInDevelopment = " Exists only in Development (and the Testing environment); never mapped elsewhere.";

    public static RouteGroupBuilder MapDevEndpoints(this RouteGroupBuilder v1)
    {
        var dev = v1.MapGroup("/dev").WithTags(Tag).AllowAnonymous();

        dev.MapGet("/sms/last", LastSms)
            .WithName("DevLastSms")
            .WithSummary("Last fake SMS sent to a phone, with the 6-digit OTP code when present." + OnlyInDevelopment);
        dev.MapPost("/gateway/webhook", GatewayWebhook)
            .WithName("DevGatewayWebhook")
            .WithSummary("Builds a signed fake payment-gateway webhook (post it to /payments/webhooks/fake with x-paystack-signature)." + OnlyInDevelopment);
        dev.MapPost("/payouts/webhook", PayoutWebhook)
            .WithName("DevPayoutWebhook")
            .WithSummary("Builds a signed fake payout-provider webhook (post it to /payouts/webhooks/fake with x-payout-signature)." + OnlyInDevelopment);
        dev.MapPost("/jobs/{name}", RunJob)
            .WithName("DevRunJob")
            .WithSummary("Runs a background job now, optionally as of ?at=<ISO time>: " + string.Join(", ", JobNames) + "." + OnlyInDevelopment);
        dev.MapGet("/bank/register", RegisterBankAccount)
            .WithName("DevRegisterBankAccount")
            .WithSummary("Registers an account number → account name on the fake bank resolver so payout accounts can name-match." + OnlyInDevelopment);
        return dev;
    }

    private static readonly string[] JobNames =
    [
        "earnings-release", "weekly-payout-run", "reconciliation", "seat-block-close", "monthly-fees", "booking-release", "booking-reminders", "notifications-delivery",
    ];

    [GeneratedRegex(@"\b(\d{6})\b")]
    private static partial Regex OtpCode();

    private static IResult LastSms(string? phone, IServiceProvider services)
    {
        if (!PhoneNumber.TryNormalise(phone, out var e164))
        {
            return Results.Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "phone_invalid", detail: "phone must be a Nigerian number (0803…, +234803…).");
        }

        if (services.GetService<FakeSmsSender>() is not { } sms)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "sms_not_fake", detail: "Sms:Provider is not 'fake'; captured messages are unavailable.");
        }

        var last = sms.LastTo(e164);
        if (last is null)
        {
            return Results.NotFound(new { code = "sms_not_found", detail = $"No fake SMS has been sent to {e164}." });
        }

        var match = OtpCode().Match(last.Message);
        return Results.Ok(new DevSmsResponse(e164, last.Message, match.Success ? match.Groups[1].Value : null, last.SentAt));
    }

    private static IResult GatewayWebhook(DevGatewayWebhookRequest request, IPaymentGateway gateway, IClock clock)
    {
        if (gateway is not FakePaymentGateway fake)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "gateway_not_fake", detail: $"Integrations:PaymentGateway is '{gateway.Name}', not the fake.");
        }

        if (string.IsNullOrWhiteSpace(request.Reference) || string.IsNullOrWhiteSpace(request.EventType))
        {
            return Results.Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "validation_failed", detail: "eventType and reference are required.");
        }

        var (payload, signature) = fake.BuildWebhook(request.EventType, request.Reference, Money.Kobo(request.AmountKobo), clock.UtcNow, request.VirtualAccountNumber, request.EventId);
        return Results.Ok(new DevSignedWebhookResponse(payload, signature, "x-paystack-signature", "/api/v1/payments/webhooks/fake"));
    }

    private static IResult PayoutWebhook(DevPayoutWebhookRequest request, IPayoutProvider provider, IClock clock)
    {
        if (provider is not FakePayoutProvider fake)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "payout_provider_not_fake", detail: $"Integrations:PayoutProvider is '{provider.Name}', not the fake.");
        }

        if (string.IsNullOrWhiteSpace(request.Reference) || string.IsNullOrWhiteSpace(request.EventType))
        {
            return Results.Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "validation_failed", detail: "eventType and reference are required.");
        }

        var (payload, signature) = fake.BuildWebhook(request.EventType, request.Reference, Money.Kobo(request.AmountKobo), clock.UtcNow, request.BankReference, request.Reason);
        return Results.Ok(new DevSignedWebhookResponse(payload, signature, "x-payout-signature", "/api/v1/payouts/webhooks/fake"));
    }

    private static IResult RegisterBankAccount(string? accountNumber, string? name, IBankAccountResolver resolver)
    {
        if (resolver is not FakeBankAccountResolver fake)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "bank_resolver_not_fake", detail: "Integrations:BankResolver is not the fake.");
        }

        if (string.IsNullOrWhiteSpace(accountNumber) || accountNumber.Length != 10 || !accountNumber.All(char.IsDigit) || string.IsNullOrWhiteSpace(name))
        {
            return Results.Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "validation_failed", detail: "accountNumber must be 10 digits and name is required.");
        }

        fake.KnownAccounts[accountNumber] = name.Trim();
        return Results.Ok(new { accountNumber, name = name.Trim(), known = fake.KnownAccounts.Count });
    }

    private static async Task<IResult> RunJob(string name, string? at, IServiceScopeFactory scopes, IClock clock, CancellationToken ct)
    {
        var now = clock.UtcNow;
        if (!string.IsNullOrWhiteSpace(at))
        {
            if (!DateTime.TryParse(at, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out now))
            {
                return Results.Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "at_invalid", detail: "at must be an ISO-8601 time, e.g. 2026-12-01T09:00:00Z.");
            }
        }

        using var ambient = AmbientContext.Enter(Tenants.Marketplace, isSystem: true);
        await using var scope = scopes.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        object result;
        switch (name.ToLowerInvariant())
        {
            case "earnings-release":
                {
                    var (released, holdsExpired) = await sp.GetRequiredService<EarningsReleaseJob>().RunAsync(now, ct);
                    result = new { released, holdsExpired };
                    break;
                }

            case "weekly-payout-run":
                {
                    var batchId = await sp.GetRequiredService<WeeklyPayoutRunJob>().RunAsync(now, ct);
                    result = new { batchId };
                    break;
                }

            case "reconciliation":
                {
                    var (assets, liabilities) = await sp.GetRequiredService<ReconciliationJob>().RunAsync(ct);
                    result = new { assetsKobo = assets, liabilitiesKobo = liabilities, shortfall = assets < liabilities };
                    break;
                }

            case "seat-block-close":
                {
                    var closed = await sp.GetRequiredService<SeatBlockCloseJob>().RunAsync(now, ct);
                    result = new { closed };
                    break;
                }

            case "monthly-fees":
                {
                    var posted = await sp.GetRequiredService<MonthlyFeesJob>().RunAsync(now, ct);
                    result = new { posted };
                    break;
                }

            case "booking-release":
                {
                    var (completed, released) = await sp.GetRequiredService<BookingLifecycleJob>().RunAsync(now, ct);
                    result = new { completed, released };
                    break;
                }

            case "booking-reminders":
                {
                    var sent = await sp.GetRequiredService<ReminderDispatcher>().RunAsync(now, ct);
                    result = new { sent };
                    break;
                }

            case "notifications-delivery":
                {
                    var (sent, failed) = await sp.GetRequiredService<DeliveryJob>().RunAsync(now, ct);
                    result = new { sent, failed };
                    break;
                }

            default:
                return Results.NotFound(new { code = "job_unknown", detail = $"Unknown job '{name}'. Known jobs: {string.Join(", ", JobNames)}." });
        }

        return Results.Ok(new { job = name.ToLowerInvariant(), asOf = now, result });
    }

    /// <summary>The last fake SMS to a phone; <c>code</c> is the 6-digit OTP when the message carries one.</summary>
    internal sealed record DevSmsResponse(string Phone, string Message, string? Code, DateTime SentAt);

    /// <summary>charge.success | charge.failed | virtual_account.credit | refund.processed for a gateway reference the fake knows.</summary>
    internal sealed record DevGatewayWebhookRequest(string EventType, string Reference, long AmountKobo, string? VirtualAccountNumber = null, string? EventId = null);

    /// <summary>transfer.success | transfer.failed | transfer.reversed for a payout provider reference.</summary>
    internal sealed record DevPayoutWebhookRequest(string EventType, string Reference, long AmountKobo, string? BankReference = null, string? Reason = null);

    /// <summary>A signed webhook body plus the header and route it must be posted to.</summary>
    internal sealed record DevSignedWebhookResponse(string Payload, string Signature, string Header, string PostTo);
}
