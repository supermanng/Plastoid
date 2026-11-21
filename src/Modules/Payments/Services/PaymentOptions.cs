namespace Plastoids.Modules.Payments.Services;

public sealed class PaymentOptions
{
    public const string SectionName = "Payments";
    public string CallbackBaseUrl { get; set; } = "http://localhost:5080/api/v1/payments/return";
    public string PayerEmailFallbackDomain { get; set; } = "payers.plastoids.ng";

    /// <summary>Days after a successful payment before its commission may be swept (blueprint §18: 7-day refund window).</summary>
    public int SweepHoldDays { get; set; } = 7;
}
