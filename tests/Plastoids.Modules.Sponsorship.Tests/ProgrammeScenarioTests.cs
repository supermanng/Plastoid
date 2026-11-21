using System.Text.Json;
using Plastoids.Modules.Ledger.Contracts;
using Plastoids.Modules.Sponsorship.Contracts;
using Plastoids.Modules.Sponsorship.Domain;
using Plastoids.Modules.Sponsorship.Services;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Sponsorship.Tests;

/// <summary>Blueprint §10 worked example: a bank sponsors 500 graduates on a 3-month programme at ₦150,000 a seat; 480 seats fill, 20 are refunded.</summary>
public class ProgrammeScenarioTests
{
    private static readonly DateTime Now = new(2026, 11, 21, 9, 0, 0, DateTimeKind.Utc);

    private static Programme BankProgramme()
    {
        var p = Programme.Create(Tenants.Marketplace, Guid.NewGuid(), "First Bank CSR", "Data Analytics 2027", Guid.NewGuid());
        p.SaveDraft(null, new ProgrammeDraft("Employ 500 graduates", Guid.NewGuid(), Guid.NewGuid(), "Data Analytics", Guid.NewGuid(), 500, Money.NairaAmount(150_000), 100, Money.NairaAmount(90_000_000),
            """{"minAge":21,"maxAge":30,"residency":["NG"],"education":["graduate"],"notPreviouslySponsored":true}""", null, null, Now.AddDays(7), Now.AddDays(21), Now.AddDays(28), Now.AddDays(28 + 91), SelectionModes.Sponsor, UnusedFundsPolicies.Refund)).IsSuccess.Should().BeTrue();
        return p;
    }

    [Fact]
    public void Quote_matches_the_worked_example()
    {
        var q = QuoteLines.Compute(500, Money.NairaAmount(150_000), 100);
        q.Tuition.Should().Be(Money.NairaAmount(75_000_000));
        q.ManagementFee.Should().Be(Money.NairaAmount(9_000_000));
        q.SeatFee.Should().Be(Money.NairaAmount(6_000_000));
        q.Vat.Should().Be(Money.NairaAmount(1_125_000));
        q.Total.Should().Be(Money.NairaAmount(91_125_000));
        q.ManagementFeePerSeat.Should().Be(Money.NairaAmount(18_000));
        q.RefundPerUnfilledSeat.Should().Be(Money.NairaAmount(180_000));

        var small = QuoteLines.Compute(3, Money.NairaAmount(45_000), 70);
        small.TuitionPerSeatSponsorShare.Should().Be(Money.NairaAmount(31_500));
        small.ManagementFee.Should().Be(Money.NairaAmount(3_000_000), "the management fee has a ₦3m floor");
    }

    [Fact]
    public void Programme_runs_from_quote_to_closeout_with_entries_7_to_10()
    {
        var p = BankProgramme();
        p.Publish(Guid.NewGuid(), Now).Error.Code.Should().Be("quote_not_approved");
        var quote = p.RequestQuote(Now);
        quote.IsSuccess.Should().BeTrue(quote.IsFailure ? quote.Error.Message : string.Empty);
        quote.Value.Total.Should().Be(Money.NairaAmount(91_125_000));
        p.Status.Should().Be(ProgrammeStatus.Quoted);

        p.ApproveQuote(Guid.NewGuid(), Now).IsSuccess.Should().BeTrue();
        p.SaveDraft("renamed", new ProgrammeDraft(null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null)).Error.Code.Should().Be("programme_locked");
        p.Publish(Guid.NewGuid(), Now).Error.Code.Should().Be("programme_not_reviewed");
        p.Review(Guid.NewGuid(), ReviewStatus.Approved, rulesLawful: false, null, null, Now).Error.Code.Should().Be("rules_not_lawful");
        p.Review(Guid.NewGuid(), ReviewStatus.Approved, rulesLawful: true, "ok", null, Now).IsSuccess.Should().BeTrue();
        p.Publish(Guid.NewGuid(), Now).Error.Code.Should().Be("programme_not_funded");

        // 50 % now, 50 % at week 6 (blueprint §10): the first half unlocks seats pro rata, the second completes funding.
        p.RecordFunding(Money.NairaAmount(45_562_500), Now).Should().BeFalse();
        p.SeatsFundable.Should().Be(250);
        p.RecordFunding(Money.NairaAmount(45_562_500), Now).Should().BeTrue();
        p.IsFunded.Should().BeTrue();
        var published = p.Publish(Guid.NewGuid(), Now);
        published.IsSuccess.Should().BeTrue();
        published.Value.Should().Be(500);
        p.IsAcceptingApplications(Now.AddDays(8)).Should().BeTrue();
        p.IsAcceptingApplications(Now.AddDays(22)).Should().BeFalse();
        p.DurationMonths.Should().Be(3);

        var seats = Enumerable.Range(1, 500).Select(n => Seat.Open(Tenants.Marketplace, p.Id, n)).ToList();
        foreach (var seat in seats.Take(480))
        {
            seat.Reserve(Guid.NewGuid(), Guid.NewGuid(), Now).IsSuccess.Should().BeTrue();
            seat.Fill(seat.ApplicationId!.Value, seat.LearnerId!.Value, Now).IsSuccess.Should().BeTrue();
        }

        // Entry 7 per filled seat: 480 × 12,000 = 5,760,000.
        var entry7 = seats.Take(480).Select(s => SponsorshipPostings.Entry7SeatFee(s, Now)).ToList();
        entry7.Should().AllSatisfy(e => Balanced(e));
        entry7.Sum(e => e.Postings.Single(x => x.AccountCode == AccountCodes.PlatformSeatFee).Credit.AmountKobo).Should().Be(Money.NairaAmount(5_760_000).AmountKobo);

        // Entries 8 and 9 each month: 480 × 18,000 ÷ 3 and 480 × 150,000 ÷ 3.
        var (management, tuition) = SponsorshipPostings.MonthlyAmounts(p, 480);
        management.Should().Be(Money.NairaAmount(2_880_000));
        tuition.Should().Be(Money.NairaAmount(24_000_000));
        Balanced(SponsorshipPostings.Entry8ManagementFee(p, 1, management, Now));
        var entry9 = SponsorshipPostings.Entry9TutorEarnings(p, 1, tuition, Now);
        Balanced(entry9);
        entry9.Postings.Single(x => x.AccountCode == AccountCodes.TutorPayable).OwnerId.Should().Be(p.TutorUserId);

        // Entry 10: 20 unfilled seats × 180,000 = 3,600,000.
        var closed = p.Close(Guid.NewGuid(), 20, Now.AddDays(130));
        closed.IsSuccess.Should().BeTrue();
        closed.Value.Should().Be(Money.NairaAmount(3_600_000));
        var entry10 = SponsorshipPostings.Entry10Closeout(p, closed.Value, 20, Now);
        Balanced(entry10);
        entry10.Postings.Single(x => x.AccountCode == AccountCodes.BankClientFunds).Credit.Should().Be(Money.NairaAmount(3_600_000));
        p.Status.Should().Be(ProgrammeStatus.Closed);

        // Programme funds: 90,000,000 held − 5,760,000 − 3 × (2,880,000 + 24,000,000) − 3,600,000 = 0.
        var drawn = 5_760_000L + 3 * (2_880_000L + 24_000_000L) + 3_600_000L;
        drawn.Should().Be(90_000_000L, "the sponsor's invoice (ex VAT) is fully accounted for");
    }

    [Fact]
    public void Seats_reserve_fill_release_and_refund()
    {
        var seat = Seat.Open(Tenants.Marketplace, Guid.NewGuid(), 1);
        var application = Guid.NewGuid();
        seat.Fill(application, Guid.NewGuid(), Now).IsSuccess.Should().BeTrue("an open seat can be filled directly");
        seat.Fill(application, Guid.NewGuid(), Now).IsSuccess.Should().BeTrue("idempotent for the same application");
        seat.FillCount.Should().Be(1);
        seat.Reserve(Guid.NewGuid(), Guid.NewGuid(), Now).Error.Code.Should().Be("seat_not_open");
        seat.Release("dropped out in week 2", Now).IsSuccess.Should().BeTrue();
        seat.Status.Should().Be(SeatStatus.Open);
        seat.Release("again", Now).Error.Code.Should().Be("seat_not_held");
        seat.Refund();
        seat.Status.Should().Be(SeatStatus.Refunded);
    }

    [Fact]
    public void Eligibility_rules_are_deterministic_and_never_discriminatory()
    {
        EligibilityEngine.ValidateRules("""{"minAge":21,"residency":["NG"]}""").IsSuccess.Should().BeTrue();
        EligibilityEngine.ValidateRules("""{"gender":["female"]}""").Error.Code.Should().Be("eligibility_rule_discriminatory");
        EligibilityEngine.ValidateRules("""{"religion":["x"]}""").Error.Code.Should().Be("eligibility_rule_discriminatory");
        EligibilityEngine.ValidateRules("""{"shoeSize":42}""").Error.Code.Should().Be("eligibility_rule_unknown");
        EligibilityEngine.ValidateRules("[1]").Error.Code.Should().Be("eligibility_rules_invalid");

        using var rules = JsonDocument.Parse("""{"minAge":21,"maxAge":30,"residency":["NG"],"states":["Lagos","Ogun"],"education":["graduate","hnd"],"notPreviouslySponsored":true}""");
        var ok = EligibilityEngine.Evaluate(rules.RootElement, 25, new ApplicantFacts("NG", "lagos", "Graduate", null), false);
        ok.Eligible.Should().BeTrue();
        var no = EligibilityEngine.Evaluate(rules.RootElement, 34, new ApplicantFacts("GH", "Accra", "secondary", null), true);
        no.Eligible.Should().BeFalse();
        no.Reasons.Should().BeEquivalentTo(["over_max_age_30", "residency_not_eligible", "state_not_eligible", "education_not_eligible", "previously_sponsored"]);
        EligibilityEngine.Evaluate(rules.RootElement, null, new ApplicantFacts("NG", "Lagos", "graduate", null), false).Reasons.Should().Contain("age_unknown");
    }

    [Fact]
    public void Impact_report_pdf_is_well_formed()
    {
        var pdf = SimplePdf.FromLines(["Plastoids - Programme impact report", "Seats (filled): 480 of 500", "Refund: ₦3,600,000"]);
        var text = System.Text.Encoding.ASCII.GetString(pdf);
        text.Should().StartWith("%PDF-1.4");
        text.Should().Contain("Seats \\(filled\\): 480 of 500");
        text.Should().Contain("NGN 3,600,000");
        text.Should().EndWith("%%EOF\n");
    }

    private static void Balanced(JournalEntryDraft draft)
    {
        draft.Postings.Sum(p => p.Debit.AmountKobo).Should().Be(draft.Postings.Sum(p => p.Credit.AmountKobo), draft.Reference);
    }
}
