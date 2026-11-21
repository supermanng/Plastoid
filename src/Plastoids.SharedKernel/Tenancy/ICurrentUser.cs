namespace Plastoids.SharedKernel.Tenancy;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    IReadOnlyCollection<string> Roles { get; }

    /// <summary>True when the current session completed MFA (amr contains "mfa").</summary>
    bool MfaVerified { get; }
    IReadOnlyCollection<Guid> OrganisationIds { get; }
    string? IpAddress { get; }
    string? CorrelationId { get; }
}
