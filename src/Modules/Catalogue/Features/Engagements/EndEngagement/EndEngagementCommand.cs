using Plastoids.Modules.Catalogue.Services;
using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Catalogue.Features.Engagements.EndEngagement;

/// <summary>The organisation (owner/author of the course) ends an active engagement. Existing offerings keep their tutor; sales after this pay the organisation the whole net.</summary>
public sealed record EndEngagementCommand(Guid EngagementId, string? Reason) : ICommand<EngagementView>;

public sealed record EndEngagementRequest(string? Reason);
