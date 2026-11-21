using System.Reflection;
using System.Text.Json;
using Plastoids.Modules.Applications.Domain;
using Plastoids.Modules.Applications.Services;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Sponsorship.Tests;

public class ApplicationRulesTests
{
    private static readonly DateTime Now = new(2026, 11, 21, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Protected_characteristics_never_reach_the_scoring_input()
    {
        // The scorer's input type has no field that could carry a protected characteristic, and the scorer never sees form answers.
        var properties = typeof(ScoringInput).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).ToList();
        properties.Should().BeEquivalentTo(["AssessmentPercent", "CriterionScores"]);
        properties.Should().NotContain(p => AnswerFields.ProtectedCharacteristics.Contains(p));
        typeof(Scorer).GetMethods(BindingFlags.Public | BindingFlags.Static).SelectMany(m => m.GetParameters()).Select(p => p.ParameterType)
            .Should().NotContain(typeof(JsonElement), "answers (JSON) are never a scoring parameter");

        using var answers = JsonDocument.Parse("""{"gender":"female","religion":"x","motivation":"I want to learn"}""");
        AnswerFields.Validate(answers.RootElement).Error.Code.Should().Be("protected_characteristic_field");
        using var tooMany = JsonDocument.Parse("""{"a":1,"b":2,"c":3,"d":4,"e":5,"f":6,"g":7,"h":8,"i":9}""");
        AnswerFields.Validate(tooMany.RootElement).Error.Code.Should().Be("too_many_fields");
        using var fine = JsonDocument.Parse("""{"fullName":"Ada","nin":"12345678901","countryOfResidence":"NG","educationLevel":"graduate","motivation":"..."}""");
        AnswerFields.Validate(fine.RootElement).IsSuccess.Should().BeTrue();
        var blind = AnswerFields.Blind(fine.RootElement.GetRawText());
        blind.TryGetProperty("fullName", out _).Should().BeFalse();
        blind.TryGetProperty("nin", out _).Should().BeFalse();
        blind.GetProperty("motivation").GetString().Should().Be("...");
    }

    [Fact]
    public void Scorer_combines_assessment_and_rubric_by_weight()
    {
        using var rubricJson = JsonDocument.Parse("""{"assessmentWeight":60,"criteria":[{"key":"motivation","weight":2,"max":5},{"key":"experience","weight":1,"max":10}]}""");
        var rubric = RubricParser.Parse(rubricJson.RootElement);
        var outcome = Scorer.Compute(new ScoringInput(80m, new Dictionary<string, decimal> { ["motivation"] = 4, ["experience"] = 5 }), rubric);
        outcome.IsSuccess.Should().BeTrue();
        outcome.Value.RubricPercent.Should().Be(70m); // (4/5×2 + 5/10×1) / 3 = 0.7
        outcome.Value.TotalPercent.Should().Be(76m); // 0.6×80 + 0.4×70
        Scorer.Compute(new ScoringInput(80m, new Dictionary<string, decimal> { ["motivation"] = 4 }), rubric).Error.Code.Should().Be("rubric_score_missing");
        Scorer.Compute(new ScoringInput(80m, new Dictionary<string, decimal> { ["motivation"] = 9, ["experience"] = 5 }), rubric).Error.Code.Should().Be("rubric_score_out_of_range");
        Scorer.Compute(new ScoringInput(55m, new Dictionary<string, decimal>()), RubricParser.Parse(null)).Value.TotalPercent.Should().Be(55m, "without a rubric the assessment is the score");
    }

    [Fact]
    public void Application_moves_through_offer_co_pay_and_expiry()
    {
        var a = Application.Start(Tenants.Marketplace, Guid.NewGuid(), Guid.NewGuid());
        a.SaveAnswers("""{"motivation":"x"}""").IsSuccess.Should().BeTrue();
        a.Submit("nin-hash", "phone-hash", Now).IsSuccess.Should().BeTrue();
        a.SaveAnswers("{}").Error.Code.Should().Be("application_already_submitted");
        a.SendOffer(Guid.NewGuid(), Now).Error.Code.Should().Be("application_not_accepted");
        a.Decide("maybe", Now).Error.Code.Should().Be("decision_invalid");
        a.Decide(DecisionKinds.Accepted, Now).IsSuccess.Should().BeTrue();
        var seat = Guid.NewGuid();
        a.SendOffer(seat, Now).IsSuccess.Should().BeTrue();
        a.OfferExpiresAt.Should().Be(Now.AddDays(5));
        a.Decide(DecisionKinds.Rejected, Now).Error.Code.Should().Be("application_not_decidable");
        a.ExpireIfDue(Now.AddDays(4)).Should().BeFalse();
        a.RequireCoPay(Guid.NewGuid(), Money.NairaAmount(13_500), Now.AddDays(1)).IsSuccess.Should().BeTrue();
        a.Status.Should().Be(ApplicationStatus.PendingCoPay);
        a.ExpireIfDue(Now.AddDays(7)).Should().BeFalse("the seat is held 7 days after acceptance");
        a.ConfirmCoPay().Should().BeTrue();
        a.Status.Should().Be(ApplicationStatus.Enrolled);
        a.ConfirmCoPay().Should().BeFalse();
        a.Withdraw().Error.Code.Should().Be("application_not_withdrawable");

        var b = Application.Start(Tenants.Marketplace, Guid.NewGuid(), Guid.NewGuid());
        b.Submit(null, null, Now);
        b.Decide(DecisionKinds.Accepted, Now);
        b.SendOffer(Guid.NewGuid(), Now);
        b.Accept(Now.AddDays(6)).Error.Code.Should().Be("offer_expired");
        b.Status.Should().Be(ApplicationStatus.Expired);

        var c = Application.Start(Tenants.Marketplace, Guid.NewGuid(), Guid.NewGuid());
        c.Submit(null, null, Now);
        c.Decide(DecisionKinds.Rejected, Now);
        c.CanAppeal(Now.AddDays(6)).Should().BeTrue();
        c.CanAppeal(Now.AddDays(8)).Should().BeFalse();
        c.Decide(DecisionKinds.Waitlisted, Now.AddDays(2)).IsSuccess.Should().BeTrue("an upheld appeal returns it to the pool");
    }

    [Fact]
    public void Entry_assessment_enforces_timing_server_side()
    {
        var attempt = EntryAttempt.Start(Tenants.Marketplace, Guid.NewGuid(), 20, Now);
        attempt.DeadlineAt.Should().Be(Now.AddMinutes(20));
        attempt.Submit("{}", 80, 100, true, Now.AddMinutes(21)).IsSuccess.Should().BeTrue();
        attempt.Status.Should().Be(EntryAttemptStatus.Expired);
        attempt.Passed.Should().BeFalse("after the grace period a submission cannot pass");
        attempt.Submit("{}", 80, 100, true, Now).Error.Code.Should().Be("assessment_closed");

        var onTime = EntryAttempt.Start(Tenants.Marketplace, Guid.NewGuid(), 20, Now);
        onTime.Submit("{}", 80, 100, true, Now.AddMinutes(19)).IsSuccess.Should().BeTrue();
        onTime.Passed.Should().BeTrue();
    }
}
