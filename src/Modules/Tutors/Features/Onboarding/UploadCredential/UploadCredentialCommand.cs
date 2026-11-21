using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Tutors.Features.Onboarding.UploadCredential;

/// <summary>Registers a credential and returns a presigned upload URL for its evidence file; a review item is queued (3 working days).</summary>
public sealed record UploadCredentialCommand(string Type, string Issuer, string Title, string ContentType) : ICommand<UploadCredentialResponse>;

public sealed record UploadCredentialResponse(Guid CredentialId, Uri UploadUrl, DateTime ReviewDueAt);
