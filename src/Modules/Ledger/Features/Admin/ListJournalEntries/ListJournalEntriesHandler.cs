using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Modules.Ledger.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Ledger.Features.Admin.ListJournalEntries;

internal sealed class ListJournalEntriesHandler(LedgerDbContext db) : IQueryHandler<ListJournalEntriesQuery, Page<JournalEntryItem>>
{
    public async Task<Result<Page<JournalEntryItem>>> Handle(ListJournalEntriesQuery request, CancellationToken cancellationToken)
    {
        var limit = Cursor.ClampLimit(request.Limit);
        var entries = db.Entries.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Kind))
        {
            entries = entries.Where(e => e.Kind == request.Kind);
        }

        if (!string.IsNullOrWhiteSpace(request.AccountCode) || request.OwnerId is not null)
        {
            var postings = db.Postings.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(request.AccountCode))
            {
                postings = postings.Where(p => p.AccountCode == request.AccountCode);
            }

            if (request.OwnerId is { } owner)
            {
                postings = postings.Where(p => p.OwnerId == owner);
            }

            entries = entries.Where(e => postings.Any(p => p.JournalEntryId == e.Id));
        }

        if (Cursor.Decode(request.Cursor) is { } after)
        {
            entries = entries.Where(e => e.Id.CompareTo(after) < 0);
        }

        var rows = await entries.OrderByDescending(e => e.Id).Take(limit + 1).ToListAsync(cancellationToken);
        var hasMore = rows.Count > limit;
        var page = rows.Take(limit).ToList();
        var ids = page.Select(e => e.Id).ToList();
        var allPostings = await db.Postings.AsNoTracking().Where(p => ids.Contains(p.JournalEntryId)).OrderBy(p => p.Line).ToListAsync(cancellationToken);
        var items = page.Select(e => new JournalEntryItem(e.Id, e.Reference, e.Kind, e.Description, e.OccurredAt, e.PostedBy, e.Currency, e.TotalDebitKobo,
            allPostings.Where(p => p.JournalEntryId == e.Id).Select(p => new PostingItem(p.Line, p.AccountCode, p.OwnerType, p.OwnerId, p.DebitKobo, p.CreditKobo)).ToList())).ToList();
        return new Page<JournalEntryItem>(items, hasMore ? Cursor.Encode(items[^1].Id) : null);
    }
}
