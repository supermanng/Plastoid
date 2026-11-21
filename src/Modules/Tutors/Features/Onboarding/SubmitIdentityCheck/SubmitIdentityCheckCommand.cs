using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Tutors.Features.Onboarding.SubmitIdentityCheck;

/// <summary>NIN + BVN + liveness selfie go straight to the KYC provider; only the result and reference are stored.</summary>
public sealed record SubmitIdentityCheckCommand(string Nin, string Bvn, string SelfieBlobKey, string FullName) : ICommand<SubmitIdentityCheckResponse>;

public sealed record SubmitIdentityCheckResponse(bool Passed, string Provider, string Reference, string? FailureReason);
