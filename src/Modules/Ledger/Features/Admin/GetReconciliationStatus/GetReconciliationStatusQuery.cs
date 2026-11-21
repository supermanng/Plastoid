using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Ledger.Features.Admin.GetReconciliationStatus;

public sealed record GetReconciliationStatusQuery : IQuery<ReconciliationStatus>;

public sealed record ReconciliationStatus(bool PayoutsFrozen, string? FreezeReason, DateTime? LastReconciledAt, long? LastAssetsKobo, long? LastLiabilitiesKobo, DateTime? LastVerifiedAt, int LastUnbalancedEntries);
