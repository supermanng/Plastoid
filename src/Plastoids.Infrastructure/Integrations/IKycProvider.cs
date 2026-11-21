using System.Text.RegularExpressions;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Infrastructure.Integrations;

/// <summary>Request for a NIN + BVN + liveness check (blueprint §13 stage 1). Raw identifiers are never persisted by callers.</summary>
public sealed record KycRequest(string Nin, string Bvn, string SelfieBlobKey, string DeclaredFullName);

public sealed record KycResult(bool Passed, string Provider, string Reference, string? VerifiedFullName, string? FailureReason);

/// <summary>Licensed KYC provider (Smile ID, Dojah, Prembly). Fake locally; Smile ID stub needs Secrets:SmileIdApiKey.</summary>
public interface IKycProvider
{
    Task<Result<KycResult>> VerifyAsync(KycRequest request, CancellationToken cancellationToken);
}

/// <summary>Deterministic fake: passes unless the NIN ends in "0"; returns the declared name title-cased as the verified name.</summary>
public sealed partial class FakeKycProvider : IKycProvider
{
    public Task<Result<KycResult>> VerifyAsync(KycRequest request, CancellationToken cancellationToken)
    {
        var reference = "fake-kyc-" + Guid.CreateVersion7().ToString("N")[^10..];
        if (!NinPattern().IsMatch(request.Nin) || !BvnPattern().IsMatch(request.Bvn))
        {
            return Task.FromResult(Result.Success(new KycResult(false, "fake", reference, null, "NIN must be 11 digits and BVN 11 digits.")));
        }

        if (request.Nin.EndsWith('0'))
        {
            return Task.FromResult(Result.Success(new KycResult(false, "fake", reference, null, "Identity mismatch (fake provider rejects NINs ending in 0).")));
        }

        return Task.FromResult(Result.Success(new KycResult(true, "fake", reference, request.DeclaredFullName.Trim(), null)));
    }

    [GeneratedRegex("^[0-9]{11}$")]
    private static partial Regex NinPattern();

    [GeneratedRegex("^[0-9]{11}$")]
    private static partial Regex BvnPattern();
}

internal sealed class SmileIdKycProvider(Secrets.ISecretProvider secrets) : IKycProvider
{
    public Task<Result<KycResult>> VerifyAsync(KycRequest request, CancellationToken cancellationToken)
    {
        _ = secrets.GetRequired("SmileIdApiKey");
        return Task.FromResult(Result.Failure<KycResult>(Error.Failure("kyc_provider_not_implemented", "Smile ID integration is a stub; set Integrations:Kyc to fake outside production.")));
    }
}
