using Plastoids.Modules.Catalogue.Services;
using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Catalogue.Features.Engagements.ListMyEngagements;

/// <summary>The signed-in tutor's engagements (offers to respond to, active teaching, past), newest offer first.</summary>
public sealed record ListMyEngagementsQuery : IQuery<IReadOnlyList<EngagementView>>;
