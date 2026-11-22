using Plastoids.Modules.Catalogue.Services;
using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Catalogue.Features.Engagements.RespondToEngagement;

/// <summary>The offered tutor accepts (→ active) or declines the engagement; the offering member is notified.</summary>
public sealed record RespondToEngagementCommand(Guid EngagementId, bool Accept, string? Note) : ICommand<EngagementView>;

public sealed record RespondToEngagementRequest(bool Accept, string? Note);
