using Plastoids.Infrastructure.Http;
using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Ledger.Features.Admin.ListJournalEntries;

public sealed record ListJournalEntriesQuery(string? Cursor, int? Limit, string? Kind, string? AccountCode, Guid? OwnerId) : IQuery<Page<JournalEntryItem>>;

public sealed record PostingItem(int Line, string AccountCode, string? OwnerType, Guid? OwnerId, long DebitKobo, long CreditKobo);

public sealed record JournalEntryItem(Guid Id, string Reference, string Kind, string Description, DateTime OccurredAt, Guid? PostedBy, string Currency, long TotalDebitKobo, IReadOnlyList<PostingItem> Postings);
