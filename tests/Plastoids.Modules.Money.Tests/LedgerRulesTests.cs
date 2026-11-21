using Plastoids.Modules.Ledger.Contracts;
using Plastoids.Modules.Ledger.Services;

namespace Plastoids.Modules.Money.Tests;

public class LedgerRulesTests
{
    private static JournalEntryDraft Draft(params PostingDraft[] postings) => new("ref-1", "test", "test entry", postings);

    [Fact]
    public void Balanced_draft_passes()
    {
        LedgerService.Validate(Draft(PostingDraft.Dr(AccountCodes.GatewayReceivable, Plastoids.SharedKernel.Domain.Money.NairaAmount(45_000)), PostingDraft.Cr(AccountCodes.TutorPayable, Plastoids.SharedKernel.Domain.Money.NairaAmount(36_000)), PostingDraft.Cr(AccountCodes.CourseCommission, Plastoids.SharedKernel.Domain.Money.NairaAmount(9_000))))
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Unbalanced_draft_is_rejected_before_the_database()
    {
        var result = LedgerService.Validate(Draft(PostingDraft.Dr(AccountCodes.GatewayReceivable, Plastoids.SharedKernel.Domain.Money.NairaAmount(45_000)), PostingDraft.Cr(AccountCodes.TutorPayable, Plastoids.SharedKernel.Domain.Money.NairaAmount(36_000))));
        result.Error.Code.Should().Be("ledger_unbalanced");
    }

    [Fact]
    public void Posting_must_be_one_sided_non_negative_and_single_currency()
    {
        LedgerService.Validate(Draft(new PostingDraft(AccountCodes.BankOperating, Plastoids.SharedKernel.Domain.Money.NairaAmount(1), Plastoids.SharedKernel.Domain.Money.NairaAmount(1)), PostingDraft.Cr(AccountCodes.BankClientFunds, Plastoids.SharedKernel.Domain.Money.Zero())))
            .Error.Code.Should().Be("ledger_posting_one_side");
        LedgerService.Validate(Draft(PostingDraft.Dr(AccountCodes.BankOperating, Plastoids.SharedKernel.Domain.Money.Kobo(-5)), PostingDraft.Cr(AccountCodes.BankClientFunds, Plastoids.SharedKernel.Domain.Money.Kobo(-5))))
            .Error.Code.Should().Be("ledger_negative_posting");
        LedgerService.Validate(Draft(PostingDraft.Dr(AccountCodes.BankOperating, new Plastoids.SharedKernel.Domain.Money(100, "USD")), PostingDraft.Cr(AccountCodes.BankClientFunds, Plastoids.SharedKernel.Domain.Money.Kobo(100))))
            .Error.Code.Should().Be("ledger_currency_mismatch");
        LedgerService.Validate(Draft(PostingDraft.Dr(AccountCodes.BankOperating, Plastoids.SharedKernel.Domain.Money.Kobo(100))))
            .Error.Code.Should().Be("ledger_entry_too_short");
    }

    [Fact]
    public void Chart_of_accounts_has_the_thirteen_codes_from_the_blueprint()
    {
        AccountCodes.All.Should().Equal("1010", "1020", "1030", "2010", "2020", "2030", "2040", "4010", "4020", "4030", "4040", "4050", "5010");
    }
}
