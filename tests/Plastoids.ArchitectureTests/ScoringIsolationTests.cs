using NetArchTest.Rules;

namespace Plastoids.ArchitectureTests;

/// <summary>Blueprint §9.13: protected characteristics are never scoring inputs. The scorer cannot read form answers because it cannot read JSON at all.</summary>
public class ScoringIsolationTests
{
    [Fact]
    public void Applications_scorer_cannot_read_form_answers()
    {
        var applications = typeof(Plastoids.Modules.Applications.ApplicationsModule).Assembly;
        var result = Types.InAssembly(applications)
            .That().HaveName("Scorer").Or().HaveName("ScoringInput").Or().HaveName("RubricDefinition")
            .ShouldNot().HaveDependencyOnAny("System.Text.Json", "Plastoids.Modules.Applications.Domain.Application", "Plastoids.Modules.Applications.Domain.AnswerFields")
            .GetResult();
        result.IsSuccessful.Should().BeTrue($"scoring must only see assessment and rubric numbers; offenders: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
