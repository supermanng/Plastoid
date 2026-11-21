namespace Plastoids.SharedKernel.Tenancy;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    IReadOnlyCollection<string> Roles { get; }
    IReadOnlyCollection<Guid> OrganisationIds { get; }
    string? IpAddress { get; }
    string? CorrelationId { get; }
}
