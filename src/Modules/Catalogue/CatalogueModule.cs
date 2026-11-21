using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Plastoids.Infrastructure.Messaging.Kafka;
using Plastoids.Infrastructure.Modules;
using Plastoids.Modules.Catalogue.Consumers;
using Plastoids.Modules.Catalogue.Contracts;
using Plastoids.Modules.Catalogue.Features.Authoring.AddLesson;
using Plastoids.Modules.Catalogue.Features.Authoring.AddQuiz;
using Plastoids.Modules.Catalogue.Features.Authoring.CreateCourse;
using Plastoids.Modules.Catalogue.Features.Authoring.CreateOffering;
using Plastoids.Modules.Catalogue.Features.Authoring.UpdateCourseOutline;
using Plastoids.Modules.Catalogue.Features.Authoring.UploadLessonVideo;
using Plastoids.Modules.Catalogue.Features.Discovery.CompareCourses;
using Plastoids.Modules.Catalogue.Features.Discovery.GetCourse;
using Plastoids.Modules.Catalogue.Features.Discovery.PostReview;
using Plastoids.Modules.Catalogue.Features.Discovery.SearchCourses;
using Plastoids.Modules.Catalogue.Features.Publishing.PublishCourse;
using Plastoids.Modules.Catalogue.Features.Publishing.ReviewCourse;
using Plastoids.Modules.Catalogue.Features.Publishing.SubmitForReview;
using Plastoids.Modules.Catalogue.Persistence;
using Plastoids.Modules.Catalogue.Services;
using Plastoids.Modules.Tutors.Contracts;

namespace Plastoids.Modules.Catalogue;

public sealed class CatalogueModule : IModule
{
    public string Name => "catalogue";
    public Assembly Assembly => typeof(CatalogueModule).Assembly;
    public Type DbContextType => typeof(CatalogueDbContext);

    public IEnumerable<TopicDefinition> ProducedTopics =>
    [
        new(EnvelopeSerializer.GetAttribute(typeof(CoursePublished)).Topic),
    ];

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<CourseAccess>();
        services.AddScoped<OutlineSnapshot>();
        services.AddScoped<AutomatedChecks>();
        services.AddSingleton<PriceQuotes>();
        services.AddScoped<IPriceQuoteReader, PriceQuoteReader>();
        services.AddScoped<ICourseOutlineReader, CourseOutlineReader>();
        services.AddScoped<ICourseModeration, CourseModeration>();
        services.AddSingleton<StubEnrolmentProgress>();
        services.AddSingleton<IEnrolmentProgress>(sp => sp.GetRequiredService<StubEnrolmentProgress>());
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var catalogue = endpoints.MapGroup("/catalogue").WithTags("Catalogue");
        UploadLessonVideoEndpoint.Map(catalogue);

        var courses = catalogue.MapGroup("/courses");
        SearchCoursesEndpoint.Map(courses);
        CompareCoursesEndpoint.Map(courses);
        GetCourseEndpoint.Map(courses);
        CreateCourseEndpoint.Map(courses);
        UpdateCourseOutlineEndpoint.Map(courses);
        AddLessonEndpoint.Map(courses);
        AddQuizEndpoint.Map(courses);
        CreateOfferingEndpoint.Map(courses);
        SubmitForReviewEndpoint.Map(courses);
        PublishCourseEndpoint.Map(courses);
        PostReviewEndpoint.Map(courses);

        var admin = endpoints.MapGroup("/admin/catalogue").WithTags("Catalogue (staff)");
        ReviewCourseEndpoint.Map(admin);
    }

    public IEnumerable<ConsumerRegistration> Consumers() =>
    [
        ConsumerRegistry.For<TutorVerified, TutorVerifiedConsumer, CatalogueDbContext>(Name),
    ];
}
