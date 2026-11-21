using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Plastoids.Infrastructure.Sms;

/// <summary>Outbound SMS adapter (blueprint §17 integrations: Termii or Africa's Talking). Fake locally.</summary>
public interface ISmsSender
{
    Task SendAsync(string phoneE164, string message, CancellationToken cancellationToken);
}

public sealed class SmsOptions
{
    public const string SectionName = "Sms";

    /// <summary>"fake" (default) or "termii".</summary>
    public string Provider { get; set; } = "fake";
    public string SenderId { get; set; } = "Plastoids";
    public string TermiiBaseUrl { get; set; } = "https://api.ng.termii.com";
}

/// <summary>Sent message captured by <see cref="FakeSmsSender"/> so tests and local runs can read OTP codes.</summary>
public sealed record SentSms(string Phone, string Message, DateTime SentAt);

/// <summary>Records messages instead of sending them. Registered as a singleton; readable by tests.</summary>
public sealed class FakeSmsSender(ILogger<FakeSmsSender> logger, Plastoids.SharedKernel.Time.IClock clock) : ISmsSender
{
    private readonly ConcurrentQueue<SentSms> _sent = new();

    public IReadOnlyCollection<SentSms> Sent => _sent.ToArray();

    public SentSms? LastTo(string phoneE164) => _sent.LastOrDefault(s => s.Phone == phoneE164);

    public Task SendAsync(string phoneE164, string message, CancellationToken cancellationToken)
    {
        _sent.Enqueue(new SentSms(phoneE164, message, clock.UtcNow));
        logger.LogInformation("[fake-sms] to {PhoneMasked}: {Message}", "***" + phoneE164[^Math.Min(4, phoneE164.Length)..], message);
        return Task.CompletedTask;
    }
}

/// <summary>Termii "send message" API. Needs Secrets:TermiiApiKey. Stub until an account exists; shape follows their public docs.</summary>
internal sealed class TermiiSmsSender(HttpClient http, IOptions<SmsOptions> options, Secrets.ISecretProvider secrets) : ISmsSender
{
    public async Task SendAsync(string phoneE164, string message, CancellationToken cancellationToken)
    {
        var payload = new
        {
            api_key = secrets.GetRequired("TermiiApiKey"),
            to = phoneE164,
            from = options.Value.SenderId,
            sms = message,
            type = "plain",
            channel = "generic",
        };
        using var response = await http.PostAsJsonAsync(new Uri(new Uri(options.Value.TermiiBaseUrl), "/api/sms/send"), payload, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
