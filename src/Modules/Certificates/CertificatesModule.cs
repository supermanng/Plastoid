using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Plastoids.Infrastructure.Messaging;
using Plastoids.Infrastructure.Messaging.Kafka;
using Plastoids.Infrastructure.Modules;
using Plastoids.Modules.Certificates.Consumers;
using Plastoids.Modules.Certificates.Contracts;
using Plastoids.Modules.Certificates.Features.Verification.ListMyCertificates;
using Plastoids.Modules.Certificates.Features.Verification.RevokeCertificate;
using Plastoids.Modules.Certificates.Features.Verification.VerifyCertificate;
using Plastoids.Modules.Certificates.Persistence;
using Plastoids.Modules.Lms.Contracts;

namespace Plastoids.Modules.Certificates;

public sealed class CertificatesModule : IModule
{
    public string Name => "certificates";
    public Assembly Assembly => typeof(CertificatesModule).Assembly;
    public Type DbContextType => typeof(CertificatesDbContext);

    public IEnumerable<TopicDefinition> ProducedTopics =>
    [
        new(EnvelopeSerializer.GetAttribute(typeof(CertificateIssued)).Topic),
        new(EnvelopeSerializer.GetAttribute(typeof(NotificationRequested)).Topic),
    ];

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        VerifyCertificateEndpoint.Map(endpoints.MapGroup("/").WithTags("Certificates"));
        var certificates = endpoints.MapGroup("/certificates").WithTags("Certificates");
        ListMyCertificatesEndpoint.Map(certificates);
        var admin = endpoints.MapGroup("/admin/certificates").WithTags("Certificates (staff)");
        RevokeCertificateEndpoint.Map(admin);
    }

    public IEnumerable<ConsumerRegistration> Consumers() =>
    [
        ConsumerRegistry.For<EnrolmentCompleted, EnrolmentCompletedConsumer, CertificatesDbContext>(Name),
    ];
}
