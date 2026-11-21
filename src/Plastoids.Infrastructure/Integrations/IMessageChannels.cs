using System.Collections.Concurrent;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Infrastructure.Integrations;

/// <summary>A message handed to a channel adapter. Subject applies to email and push titles.</summary>
public sealed record OutboundMessage(Guid RecipientId, string? Subject, string Body, IReadOnlyDictionary<string, string> Data);

public sealed record SentMessage(Guid RecipientId, string Channel, string Destination, string? Subject, string Body, DateTime SentAt, string ProviderReference);

/// <summary>WhatsApp Business via a BSP (blueprint §9.16). Stub until a BSP account exists.</summary>
public interface IWhatsAppSender
{
    Task<Result<string>> SendAsync(string phoneE164, OutboundMessage message, CancellationToken cancellationToken);
}

/// <summary>Transactional email: SMTP (Mailpit locally), a provider later.</summary>
public interface IEmailSender
{
    Task<Result<string>> SendAsync(string email, OutboundMessage message, CancellationToken cancellationToken);
}

/// <summary>Mobile push (FCM). Stub until the apps register device tokens.</summary>
public interface IPushSender
{
    Task<Result<string>> SendAsync(Guid userId, OutboundMessage message, CancellationToken cancellationToken);
}

public sealed class EmailOptions
{
    public const string SectionName = "Email";
    public string SmtpHost { get; set; } = "localhost";
    public int SmtpPort { get; set; } = 1025;
    public string From { get; set; } = "no-reply@plastoids.ng";
    public string FromName { get; set; } = "Plastoids";
}

/// <summary>Captures every message so tests and local runs can read what would have been sent.</summary>
public sealed class FakeMessageChannels(ILogger<FakeMessageChannels> logger, SharedKernel.Time.IClock clock) : IWhatsAppSender, IEmailSender, IPushSender
{
    private readonly ConcurrentQueue<SentMessage> _sent = new();

    public IReadOnlyCollection<SentMessage> Sent => _sent.ToArray();

    public IReadOnlyList<SentMessage> To(Guid recipientId) => _sent.Where(s => s.RecipientId == recipientId).ToList();

    public Task<Result<string>> SendAsync(string phoneE164, OutboundMessage message, CancellationToken cancellationToken) => Record("whatsapp", phoneE164, message);

    Task<Result<string>> IEmailSender.SendAsync(string email, OutboundMessage message, CancellationToken cancellationToken) => Record("email", email, message);

    public Task<Result<string>> SendAsync(Guid userId, OutboundMessage message, CancellationToken cancellationToken) => Record("push", userId.ToString("D"), message);

    private Task<Result<string>> Record(string channel, string destination, OutboundMessage message)
    {
        var reference = $"{channel}_{Guid.CreateVersion7():N}";
        _sent.Enqueue(new SentMessage(message.RecipientId, channel, destination, message.Subject, message.Body, clock.UtcNow, reference));
        logger.LogInformation("[fake-{Channel}] to {Destination}: {Subject} {Body}", channel, destination, message.Subject, message.Body);
        return Task.FromResult(Result.Success(reference));
    }
}

/// <summary>SMTP relay (Mailpit at localhost:1025 in docker-compose).</summary>
public sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    public async Task<Result<string>> SendAsync(string email, OutboundMessage message, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new SmtpClient(options.Value.SmtpHost, options.Value.SmtpPort);
            using var mail = new MailMessage(new MailAddress(options.Value.From, options.Value.FromName), new MailAddress(email)) { Subject = message.Subject ?? "Plastoids", Body = message.Body };
            await client.SendMailAsync(mail, cancellationToken);
            return "smtp_" + Guid.CreateVersion7().ToString("N");
        }
        catch (SmtpException ex)
        {
            return Error.Failure("email_send_failed", ex.Message);
        }
    }
}

/// <summary>WhatsApp BSP adapter stub (Meta Cloud API through a BSP such as Termii/Twilio); needs Secrets:WhatsAppToken.</summary>
internal sealed class BspWhatsAppSender(Secrets.ISecretProvider secrets) : IWhatsAppSender
{
    public Task<Result<string>> SendAsync(string phoneE164, OutboundMessage message, CancellationToken cancellationToken)
    {
        _ = secrets.GetRequired("WhatsAppToken");
        return Task.FromResult(Result.Failure<string>(Error.Failure("whatsapp_not_implemented", "The WhatsApp BSP adapter is a stub.")));
    }
}

/// <summary>Firebase Cloud Messaging stub; needs Secrets:FcmServerKey and device tokens from the apps.</summary>
internal sealed class FcmPushSender(Secrets.ISecretProvider secrets) : IPushSender
{
    public Task<Result<string>> SendAsync(Guid userId, OutboundMessage message, CancellationToken cancellationToken)
    {
        _ = secrets.GetRequired("FcmServerKey");
        return Task.FromResult(Result.Failure<string>(Error.Failure("push_not_implemented", "The FCM adapter is a stub.")));
    }
}
