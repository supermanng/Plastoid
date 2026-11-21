using Plastoids.SharedKernel.Messaging;

namespace Plastoids.Modules.Ledger.Contracts;

/// <summary>ledger.shortfall-detected.v1: daily reconciliation found client funds below liabilities; payouts frozen.</summary>
[IntegrationEvent("ledger", "shortfall-detected", 1)]
public sealed record ShortfallDetected(Guid AggregateId, DateOnly AsOf, long AssetsKobo, long LiabilitiesKobo, long ShortfallKobo) : IIntegrationEvent;

/// <summary>ledger.integrity-alert.v1: nightly verification found an unbalanced journal entry.</summary>
[IntegrationEvent("ledger", "integrity-alert", 1)]
public sealed record IntegrityAlert(Guid AggregateId, Guid JournalEntryId, long NetKobo) : IIntegrationEvent;
