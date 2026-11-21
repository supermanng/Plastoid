using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Modules.Ledger.Contracts;
using Plastoids.Modules.Ledger.Domain;
using Plastoids.Modules.Ledger.Persistence;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Ledger.Services;

internal sealed class LedgerService(LedgerDbContext db, IUnitOfWork unitOfWork, ICurrentTenant tenant, ICurrentUser user, IClock clock) : ILedgerService, IPayoutControls
{
    public async Task<Result<Guid>> PostAsync(JournalEntryDraft draft, CancellationToken cancellationToken)
    {
        var validation = Validate(draft);
        if (validation.IsFailure)
        {
            return Result.Failure<Guid>(validation.Error);
        }

        if (unitOfWork.IsActive)
        {
            await unitOfWork.EnlistAsync(db, cancellationToken);
        }

        var existing = await db.Entries.AsNoTracking().Where(e => e.Reference == draft.Reference).Select(e => (Guid?)e.Id).SingleOrDefaultAsync(cancellationToken);
        if (existing is { } id)
        {
            return id; // idempotent on reference
        }

        var codes = draft.Postings.Select(p => p.AccountCode).Distinct().ToList();
        var known = await db.Accounts.AsNoTracking().Where(a => codes.Contains(a.Code)).Select(a => a.Code).ToListAsync(cancellationToken);
        var unknown = codes.Except(known).ToList();
        if (unknown.Count > 0)
        {
            return Error.Validation("ledger_unknown_account", $"Unknown account code(s): {string.Join(", ", unknown)}.");
        }

        var currency = draft.Postings[0].Debit.Currency;
        var entry = JournalEntry.Create(tenant.TenantId, draft.Reference, draft.Kind, draft.Description, draft.OccurredAt ?? clock.UtcNow, user.UserId, currency, draft.Postings.Sum(p => p.Debit.AmountKobo));
        db.Entries.Add(entry);
        var line = 0;
        foreach (var p in draft.Postings)
        {
            db.Postings.Add(Posting.Create(tenant.TenantId, entry.Id, ++line, p.AccountCode, p.OwnerType, p.OwnerId, p.Debit, p.Credit));
        }

        if (!unitOfWork.IsActive)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return entry.Id;
    }

    public async Task<Money> GetBalanceAsync(string accountCode, Guid? ownerId, CancellationToken cancellationToken)
    {
        var account = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(a => a.Code == accountCode, cancellationToken)
            ?? throw new InvalidOperationException($"Unknown account {accountCode}.");
        var query = db.Postings.AsNoTracking().Where(p => p.AccountCode == accountCode);
        if (ownerId is { } owner)
        {
            query = query.Where(p => p.OwnerId == owner);
        }

        var debits = await query.SumAsync(p => p.DebitKobo, cancellationToken);
        var credits = await query.SumAsync(p => p.CreditKobo, cancellationToken);
        return Money.Kobo(account.DebitSign * (debits - credits));
    }

    public async Task<bool> ArePayoutsFrozenAsync(CancellationToken cancellationToken)
        => await db.Controls.AsNoTracking().Where(c => c.Id == LedgerControl.SingletonId).Select(c => c.PayoutsFrozen).SingleOrDefaultAsync(cancellationToken);

    /// <summary>Build prompt §2.9: a draft whose postings do not sum to zero is rejected before it reaches the database.</summary>
    public static Result Validate(JournalEntryDraft draft)
    {
        if (string.IsNullOrWhiteSpace(draft.Reference) || draft.Reference.Length > 160)
        {
            return Error.Validation("ledger_reference_invalid", "Reference is required (≤160 chars).");
        }

        if (draft.Postings.Count < 2)
        {
            return Error.Validation("ledger_entry_too_short", "A journal entry needs at least two postings.");
        }

        var currency = draft.Postings[0].Debit.Currency;
        foreach (var p in draft.Postings)
        {
            if (p.Debit.Currency != currency || p.Credit.Currency != currency)
            {
                return Error.Validation("ledger_currency_mismatch", "All postings must use one currency.");
            }

            if (p.Debit.IsNegative || p.Credit.IsNegative)
            {
                return Error.Validation("ledger_negative_posting", "Postings cannot be negative.");
            }

            if (p.Debit.IsZero == p.Credit.IsZero)
            {
                return Error.Validation("ledger_posting_one_side", "Each posting is either a debit or a credit, never both or neither.");
            }
        }

        var net = draft.Postings.Sum(p => p.Debit.AmountKobo) - draft.Postings.Sum(p => p.Credit.AmountKobo);
        return net == 0 ? Result.Success() : Error.Validation("ledger_unbalanced", $"Debits and credits differ by {net} kobo.");
    }
}
