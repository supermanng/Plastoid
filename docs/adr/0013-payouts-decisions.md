# ADR 0013: Sprint 7 decisions — Payouts

**Status:** Accepted (2026-11-21)

## Decisions

1. **The ledger is the cap, earnings are the detail.** Payouts keeps an `Earning` per sale/session/programme month
   (gross, commission, net, refunds, hold) fed by `payments.payment-succeeded.v1` (now carrying `PayeeNet`),
   `booking.earning-released.v1`, `payments.refund-issued.v1` and the new `sponsorship.tutor-earnings-accrued.v1`
   (emitted when entry 9 is posted). What a tutor can be paid is released net − settled − in-flight, never more than
   the 2010 sub-ledger balance less in-flight payouts. The two views are reported side by side in GetMyEarnings.
2. **Holds follow blueprint §18.** Course sales release after `Payouts:CourseSaleHoldDays` (7); session earnings wait
   for Booking's 72-hour release event; sponsored teaching is payable when the programme month is posted (the month is
   the "delivered session" unit at MVP). Dispute holds block a tutor entirely and auto-release after 5 working days.
3. **Maker–checker in the aggregate.** `PayoutBatch.Approve` refuses the creator as checker; the weekly Thursday job
   drafts the batch as the system maker so a single staff member can still approve it, and a staff-drafted batch needs
   a second staff member. Rejecting a batch cancels its payouts and the balances stay payable.
4. **Provider adapter with fallback.** `IPayoutProvider` (Infrastructure) has a fake (tests), a Paystack transfers stub
   and a bank bulk-file stub (`Integrations:PayoutProvider = fake | paystack | bankfile`). Account numbers are decrypted
   only to build the transfer instruction; payouts store the masked number. Webhooks are HMAC-verified and replay-safe
   (`payouts_provider_events`).
5. **Entry 4 on settlement, reversal entry on bounce.** `transfer.success` posts Dr 2010 (tutor) / Cr 1020;
   `transfer.reversed` posts the mirror entry and the payout becomes `reversed`, so the balance is restored by the
   ledger and by the earnings view at the same time. `transfer.failed` before settlement touches no ledger entry.
6. **Freeze is lifted by a person.** The reconciliation job freezes payouts on a shortfall; the new
   `POST /admin/ledger/reconciliation/unfreeze` (staff, Idempotency-Key, reason, audited) is the only way back.
7. **Reads inside transactions enlist.** `ILedgerService.GetBalanceAsync` and `IPayoutControls.ArePayoutsFrozenAsync`
   now join the caller's unit of work, which the close-out and batch-creation handlers rely on.

## Consequences

- `PaymentSucceeded` gained an optional `PayeeNet`; older messages deserialise with null and Payouts falls back to the
  gross (commission 0), which can only happen for events published before this sprint.
- Statement export is CSV (`GET /payouts/me/statement`); PDF statements and withholding-tax lines wait for the tax
  adviser's confirmation (blueprint §18 note).
- Package purchases (several sessions paid at once) create one booking-sourced earning keyed by the package; the
  release follows the first session's 72-hour window. A per-session split is a Phase 2 refinement.
