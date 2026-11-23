using System.Globalization;
using Microsoft.Extensions.Options;
using Plastoids.Infrastructure.Integrations;
using Plastoids.Infrastructure.Security;
using Plastoids.Infrastructure.Sms;
using Plastoids.Modules.Sponsorship.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Sponsorship.Services;

/// <summary>Where the public web app lives, for links in messages (ADR 0021 seat invites).</summary>
public sealed class WebOptions
{
    public const string SectionName = "Web";
    public string BaseUrl { get; set; } = "http://localhost:5173";
}

/// <summary>
/// Sends the invite for a seat given to someone without an account (ADR 0021): an SMS to a phone or an email to an address, with the
/// sign-up link. The contact is decrypted only here and never logged; the allocation records that a message went out.
/// </summary>
internal sealed class SeatInviteSender(ISmsSender sms, IEmailSender email, IFieldEncryptor encryptor, IOptions<WebOptions> web)
{
    public async Task<Result> SendAsync(SeatBlock block, SeatAllocation allocation, DateTime now, CancellationToken cancellationToken)
    {
        if (!allocation.CanBeInvited)
        {
            return Error.Conflict("seat_not_invited", "Only an invited seat that is still waiting for its learner can be (re)sent.");
        }

        var contact = encryptor.Decrypt(allocation.ContactCiphertext!);
        var signUpUrl = SignUpUrl(web.Value.BaseUrl);
        if (allocation.ContactKind == ContactKinds.Phone)
        {
            await sms.SendAsync(contact, SmsText(block, signUpUrl), cancellationToken);
        }
        else
        {
            var sent = await email.SendAsync(contact, new OutboundMessage(Guid.Empty, EmailSubject(block), EmailBody(block, signUpUrl), new Dictionary<string, string> { ["blockId"] = block.Id.ToString("D"), ["allocationId"] = allocation.Id.ToString("D") }), cancellationToken);
            if (sent.IsFailure)
            {
                return sent.Error;
            }
        }

        allocation.RecordInviteSent(now);
        return Result.Success();
    }

    public static string SignUpUrl(string baseUrl) => baseUrl.TrimEnd('/') + "/auth/sign-in?mode=signup";

    /// <summary>Short enough for one SMS segment pair; the learner must sign up with the same number for the seat to attach.</summary>
    public static string SmsText(SeatBlock block, string signUpUrl)
        => $"{block.SponsorName} has reserved you a seat in {block.CohortName} ({block.CourseTitle}) on Plastoids, starting {block.StartsAt.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}. Sign up with this phone number to take it: {signUpUrl}";

    public static string EmailSubject(SeatBlock block) => $"{block.SponsorName} has reserved you a seat in {block.CohortName}";

    public static string EmailBody(SeatBlock block, string signUpUrl)
        => $"{block.SponsorName} has paid for a seat for you in {block.CohortName}, a cohort of {block.CourseTitle} on Plastoids running from {block.StartsAt.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)} to {block.EndsAt.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)}.\n\n"
         + $"Sign up with this email address and the seat is yours automatically: {signUpUrl}\n\nIf you were not expecting this, you can ignore this message.";
}
