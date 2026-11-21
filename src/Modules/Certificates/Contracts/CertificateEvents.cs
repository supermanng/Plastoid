using Plastoids.SharedKernel.Messaging;

namespace Plastoids.Modules.Certificates.Contracts;

/// <summary>certificates.certificate-issued.v1 (build prompt §2.8).</summary>
[IntegrationEvent("certificates", "certificate-issued", 1)]
public sealed record CertificateIssued(Guid AggregateId, Guid CertificateId, Guid EnrolmentId, Guid LearnerId, Guid CourseId, string PublicCode) : IIntegrationEvent;
