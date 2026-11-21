using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Identity.Features.Organisations.RegisterOrganisation;

/// <summary>Submits an organisation (sponsor, institution, employer, hub) for staff verification; the caller becomes its owner.</summary>
public sealed record RegisterOrganisationCommand(string Name, string Type, string CacNumber, string SignatoryName, string SignatoryEmail) : ICommand<RegisterOrganisationResponse>;

public sealed record RegisterOrganisationResponse(Guid OrganisationId, string VerificationStatus);
