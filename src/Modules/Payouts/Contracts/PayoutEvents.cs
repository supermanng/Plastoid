using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Messaging;

namespace Plastoids.Modules.Payouts.Contracts;

/// <summary>payouts.batch-approved.v1 (build prompt §2.8): the checker approved the maker's batch.</summary>
[IntegrationEvent("payouts", "batch-approved", 1)]
public sealed record BatchApproved(Guid AggregateId, Guid BatchId, int Count, Money TotalAmount) : IIntegrationEvent;

/// <summary>
/// payouts.payout-settled.v1: the bank confirmed the transfer; ledger entry 4 was posted. PayeeType "tutor" | "organisation" (ADR 0019) with
/// PayeeId; TutorUserId stays for older consumers (the payee id for tutors, Guid.Empty for organisations).
/// </summary>
[IntegrationEvent("payouts", "payout-settled", 1)]
public sealed record PayoutSettled(Guid AggregateId, Guid PayoutId, Guid TutorUserId, Money Amount, string BankRef, string PayeeType = "tutor", Guid PayeeId = default) : IIntegrationEvent;

/// <summary>payouts.payout-failed.v1: the transfer failed or was reversed; funds are back on the payee's balance. Payee fields as on PayoutSettled.</summary>
[IntegrationEvent("payouts", "payout-failed", 1)]
public sealed record PayoutFailed(Guid AggregateId, Guid PayoutId, Guid TutorUserId, Money Amount, string Reason, bool Reversed, string PayeeType = "tutor", Guid PayeeId = default) : IIntegrationEvent;
