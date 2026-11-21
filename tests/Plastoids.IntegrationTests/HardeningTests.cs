using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Plastoids.IntegrationTests.ApiClientExtensions;

namespace Plastoids.IntegrationTests;

/// <summary>Sprint 9 security pass: problem details never leak stack traces; unknown routes and bad JSON are shaped the same way.</summary>
[Collection(ApiCollection.Name)]
public class HardeningTests(PlastoidsApiFactory factory)
{
    [Fact]
    public async Task Unhandled_exceptions_become_problem_details_without_stack_traces()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/diagnostics/throw");
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("   at ").And.NotContain("Exception").And.NotContain("Program.cs");
        var problem = JsonSerializer.Deserialize<JsonElement>(body, Json);
        problem.GetProperty("code").GetString().Should().Be("internal_error");
        problem.GetProperty("status").GetInt32().Should().Be(500);
        problem.GetProperty("correlationId").GetString().Should().NotBeNullOrEmpty();
        response.Headers.Should().ContainKey("X-Correlation-Id");
    }

    [Fact]
    public async Task Validation_failures_are_problem_details_with_field_errors()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/identity/auth/otp/request", new { phone = "not-a-phone" });
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        problem.GetProperty("code").GetString().Should().Be("validation_failed");
        problem.GetProperty("errors").ValueKind.Should().Be(JsonValueKind.Object);
    }
}
