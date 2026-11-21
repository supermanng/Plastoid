using Plastoids.Modules.BackOffice.Domain;
using Plastoids.Modules.Identity.Domain;
using Plastoids.Modules.Payments.Domain;
using Plastoids.Modules.Payments.Services;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.BackOffice.Tests;

/// <summary>Blueprint §19 controls: every protected back-office action needs two people, enforced in the domain.</summary>
public class FourEyesTests
{
    private static readonly DateTime Now = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Maker = Guid.NewGuid();
    private static readonly Guid Checker = Guid.NewGuid();

    [Fact]
    public void Fee_rule_needs_a_different_approver_and_resolves_by_effective_date()
    {
        var v1 = FeeRule.Propose(Tenants.Marketplace, "course_platform", 1800, Now.AddDays(1), "Launch promo", 1, Maker, Now).Value;
        v1.Approve(Maker, Now).Error.Code.Should().Be("maker_checker_violation");
        v1.Status.Should().Be(FeeRuleStatus.Proposed);
        v1.Approve(Checker, Now).IsSuccess.Should().BeTrue();
        v1.Approve(Checker, Now).Error.Code.Should().Be("fee_rule_not_pending");

        var v2 = FeeRule.Propose(Tenants.Marketplace, "course_platform", 1500, Now.AddDays(10), "Deeper promo", 2, Maker, Now).Value;
        v2.Approve(Checker, Now).IsSuccess.Should().BeTrue();
        var rejected = FeeRule.Propose(Tenants.Marketplace, "course_platform", 100, Now.AddDays(2), "Too low", 3, Maker, Now).Value;
        rejected.Reject(Checker, "Not viable", Now).IsSuccess.Should().BeTrue();
        var rules = new[] { v1, v2, rejected };

        FeeRule.EffectiveAt(rules, "course_platform", Now).Should().BeNull("nothing is effective yet → configured default applies");
        FeeRule.EffectiveAt(rules, "course_platform", Now.AddDays(1)).Should().Be(v1);
        FeeRule.EffectiveAt(rules, "course_platform", Now.AddDays(5)).Should().Be(v1, "the rejected version never applies");
        FeeRule.EffectiveAt(rules, "course_platform", Now.AddDays(10)).Should().Be(v2);
        FeeRule.EffectiveAt(rules, "live_tutoring", Now.AddDays(10)).Should().BeNull();

        FeeRule.Propose(Tenants.Marketplace, "unknown", 100, Now, "x", 1, Maker, Now).Error.Code.Should().Be("fee_rule_code_unknown");
        FeeRule.Propose(Tenants.Marketplace, "co_pay", 6000, Now, "x", 1, Maker, Now).Error.Code.Should().Be("commission_out_of_range");
        FeeRule.Propose(Tenants.Marketplace, "co_pay", 100, Now.AddDays(-1), "x", 1, Maker, Now).Error.Code.Should().Be("effective_from_past");

        new FeeOptions().CoursePlatformBps.Should().Be(2000, "the configured default applies until a rule is effective");
    }

    [Fact]
    public void Role_grant_needs_a_different_approver_and_never_benefits_the_approver()
    {
        var beneficiary = Guid.NewGuid();
        RoleGrantProposal.Propose(Tenants.Marketplace, beneficiary, "admin", "x", Maker, Now).Error.Code.Should().Be("role_not_grantable");
        RoleGrantProposal.Propose(Tenants.Marketplace, Maker, "staff", "x", Maker, Now).Error.Code.Should().Be("self_grant");
        var grant = RoleGrantProposal.Propose(Tenants.Marketplace, beneficiary, "Staff", "New finance analyst", Maker, Now).Value;
        grant.Role.Should().Be("staff");
        grant.Approve(Maker, Now).Error.Code.Should().Be("maker_checker_violation");
        grant.Approve(beneficiary, Now).Error.Code.Should().Be("self_approval");
        grant.Approve(Checker, Now).IsSuccess.Should().BeTrue();
        grant.Status.Should().Be(RoleGrantStatus.Approved);
        grant.Reject(Checker, "late", Now).Error.Code.Should().Be("grant_not_pending");
    }

    [Fact]
    public void Feature_flags_and_risk_alerts_follow_their_rules()
    {
        FeatureFlag.Create(Tenants.Marketplace, "AI Tutor", true, "x", Maker).Error.Code.Should().Be("flag_key_invalid");
        var flag = FeatureFlag.Create(Tenants.Marketplace, "ai-tutor", false, "AI tutor rollout", Maker).Value;
        flag.Set(true, null, Checker);
        flag.Enabled.Should().BeTrue();
        flag.Description.Should().Be("AI tutor rollout");
        flag.UpdatedBy.Should().Be(Checker);

        var alert = RiskAlert.Raise(Tenants.Marketplace, AlertKinds.Shortfall, "critical", "Shortfall", null, Now);
        alert.IsOpen.Should().BeTrue();
        alert.Resolve(Checker, "Bank statement reconciled", Now.AddHours(1)).IsSuccess.Should().BeTrue();
        alert.Resolve(Checker, "again", Now).Error.Code.Should().Be("alert_already_resolved");

        var counter = MetricCounter.Start(Tenants.Marketplace, Metrics.Gmv);
        counter.Bump(Plastoids.SharedKernel.Domain.Money.NairaAmount(45_000), Now);
        counter.Bump(null, Now);
        counter.Count.Should().Be(2);
        counter.Amount.Should().Be(Plastoids.SharedKernel.Domain.Money.NairaAmount(45_000));
    }

    [Fact]
    public void Suspended_users_cannot_be_suspended_twice_and_deleted_users_never()
    {
        var user = User.Register(Tenants.Marketplace, "enc", "hash", "enc-dob", new DateOnly(1999, 5, 5), "Ada", Now).Value;
        user.Suspend(" ").Error.Code.Should().Be("reason_required");
        user.Suspend("Fraud report").IsSuccess.Should().BeTrue();
        user.IsActive.Should().BeFalse();
        user.Suspend("again").Error.Code.Should().Be("user_already_suspended");
        user.Reinstate().IsSuccess.Should().BeTrue();
        user.IsActive.Should().BeTrue();
        user.Reinstate().Error.Code.Should().Be("user_not_suspended");
    }
}
