using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Identity.Contracts;

public sealed record UserAdminSummary(Guid UserId, string DisplayName, string? Email, string Status, IReadOnlyList<string> Roles, DateTime CreatedAt, DateTime? LastLoginAt, bool MfaEnabled);

public sealed record OrganisationAdminSummary(Guid OrganisationId, string Name, string Type, string VerificationStatus, string CacNumber, string SignatoryName, DateTime SubmittedAt);

public sealed record DataRequestSummary(Guid RequestId, Guid UserId, string Status, DateTime RequestedAt, DateTime? CompletedAt, string? FailureReason, int Attempts);

/// <summary>Back-office administration of accounts (blueprint §9.20). Writes enlist in the caller's unit of work so they are audited with the caller's command.</summary>
public interface IUserAdministration
{
    Task<IReadOnlyList<UserAdminSummary>> SearchUsersAsync(string? query, string? role, string? status, int limit, CancellationToken cancellationToken);
    Task<UserAdminSummary?> GetUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<Result<UserAdminSummary>> SuspendAsync(Guid userId, string reason, Guid staffId, CancellationToken cancellationToken);
    Task<Result<UserAdminSummary>> ReinstateAsync(Guid userId, Guid staffId, CancellationToken cancellationToken);
    Task<IReadOnlyList<OrganisationAdminSummary>> ListOrganisationsAsync(string? verificationStatus, int limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<DataRequestSummary>> ListDataRequestsAsync(string? status, int limit, CancellationToken cancellationToken);
}
