using System.Text;
using Plastoids.Infrastructure.Observability;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Plastoids.ArchitectureTests;

/// <summary>Build prompt Sprint 9 security pass: PII never reaches logs (Serilog destructuring policy).</summary>
public class PiiLoggingTests
{
    private sealed class CapturingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    private sealed record SignUp(string Phone, string Email, string DisplayName, string Nin, DateOnly DateOfBirth, string CareerGoal, int Attempts);

    [Fact]
    public void Destructured_objects_have_their_personal_data_masked()
    {
        var sink = new CapturingSink();
        using var logger = new LoggerConfiguration()
            .Destructure.With<PiiDestructuringPolicy>()
            .Destructure.ToMaximumDepth(2)
            .WriteTo.Sink(sink)
            .CreateLogger();

        var request = new SignUp("+2348031234567", "ada@example.com", "Ada Okafor", "12345678901", new DateOnly(1999, 5, 5), "data analyst", 3);
        logger.Information("Sign-up received {@Request} with headers {@Headers}", request, new Dictionary<string, string> { ["x-phone"] = "+2348031234567", ["x-correlation"] = "abc" });

        var rendered = new StringBuilder();
        using (var writer = new StringWriter(rendered))
        {
            new RenderedCompactJsonFormatter().Format(sink.Events.Single(), writer);
        }

        var json = rendered.ToString();
        json.Should().NotContain("2348031234567").And.NotContain("ada@example.com").And.NotContain("Ada Okafor").And.NotContain("12345678901").And.NotContain("1999");
        json.Should().Contain("data analyst", "non-personal fields still log").And.Contain("\"Attempts\":3").And.Contain("\"x-correlation\":\"abc\"");
        json.Should().Contain(PiiDestructuringPolicy.Mask);
    }

    [Theory]
    [InlineData("PhoneNumber", true)]
    [InlineData("phone_hash", true)]
    [InlineData("Email", true)]
    [InlineData("AccountNumberMasked", true)]
    [InlineData("MfaSecretCiphertext", true)]
    [InlineData("RefreshToken", true)]
    [InlineData("Nin", true)]
    [InlineData("DateOfBirth", true)]
    [InlineData("CourseTitle", false)]
    [InlineData("AmountKobo", false)]
    [InlineData("Status", false)]
    [InlineData("Zip", false)]
    public void Sensitive_member_names_are_recognised(string name, bool sensitive)
    {
        PiiDestructuringPolicy.IsSensitive(name).Should().Be(sensitive);
    }
}
