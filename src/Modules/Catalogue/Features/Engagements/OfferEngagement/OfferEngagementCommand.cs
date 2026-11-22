using Plastoids.Modules.Catalogue.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Catalogue.Features.Engagements.OfferEngagement;

/// <summary>Terms offered: kind revenue_share needs shareBps (1..10000 of the sale net of commission); per_enrolment needs a positive naira fixedFee.</summary>
public sealed record EngagementTermsInput(string Kind, int? ShareBps, Money? FixedFee);

/// <summary>ADR 0019 §3: the organisation offers a verified tutor (TutorId) terms to teach one of its courses.</summary>
public sealed record OfferEngagementCommand(Guid OrganisationId, Guid CourseId, Guid TutorId, EngagementTermsInput Terms, string? Note) : ICommand<EngagementView>;

public sealed record OfferEngagementRequest(Guid TutorId, EngagementTermsInput Terms, string? Note);
