# ADR 0009: Sprint 3 decisions — ledger write path, payment lifecycle, fees and reconciliation

**Status:** Accepted (2026-11-21)

## Context

Sprint 3 introduces real money: the Ledger module (blueprint §18) and the Payments module (stories G1, G2). The
blueprint fixes the chart of accounts and the ten illustrative entries; this ADR records how the code realises them.

## Decisions

1. **One write path.** `ILedgerService.PostAsync` (Ledger.Contracts) is the only way rows reach `ledger_journal_entries`
   and `ledger_postings`. It validates in memory (balanced, one-sided, non-negative, single currency, known accounts),
   enlists the Ledger context in the caller's unit of work, and is idempotent on the draft's `Reference`
   (`payment:<id>:charge`, `refund:<id>`, …), so a retried command can never double-post. Both tables are append-only
   through BEFORE UPDATE/DELETE triggers and a CHECK constraint keeps each posting one-sided.
2. **Sub-ledgers by owner id.** 2010 is keyed by the tutor's *user id* (the only identity every module knows), 2020 by
   programme id (organisation id until Sponsorship creates programmes), 2030 by learner id. `GetBalance(code, ownerId)`
   returns the natural balance (assets/expenses = Dr − Cr, liabilities/revenue = Cr − Dr).
3. **Entries 1–6 live in `PaymentPostings`.** Charge (1), settlement net of fee (2), sweep of commission less fees (3),
   pro-rata refund (5, crediting 1030 if not yet settled) and invoice payment with VAT credited last on partial
   payments (6). Entry 4 is Payouts' (Sprint 7); entries 7–10 are Sponsorship's (Sprint 6).
4. **Payment lifecycle.** A `PaymentIntent` is created with the quoted price (30-minute Redis quote from Catalogue via
   `IPriceQuoteReader`), the commission resolved by `IFeeRules` (20 % platform-sourced, 8 % tutor-referred, 15 % live
   tutoring, 0 % co-pay) and stored on the row. Confirmation happens exactly once from either the signed webhook or the
   client-side verify, whichever arrives first; duplicates are detected by the gateway-event inbox and the intent
   state. The webhook endpoint runs as the system actor in tenant zero.
5. **Refunds.** Up to ₦100,000 a staff member issues immediately; above that the refund waits for a *different* staff
   member (`Refund.Approve` enforces `approved_by != requested_by`).
6. **Invoices.** Lines are tuition, management fee and seat fee; VAT (7.5 %) is computed on the fee lines only, pending
   tax advice. Each invoice gets a dedicated virtual account from the gateway; a credit webhook auto-matches it: exact
   → paid (+ `payments.invoice-paid.v1`), less → partially paid, more → held for finance.
7. **Reconciliation.** `ReconciliationJob` compares bank balance (IBankStatement) + gateway in-transit
   (IGatewaySettlementReport) with 2010 + 2020 + 2030. A shortfall sets `payouts_frozen` (readable through
   `IPayoutControls`) and emits `ledger.shortfall-detected.v1`. The fakes mirror the ledger unless overridden, so the
   check is a no-op locally until a real statement adapter exists. `BalanceVerificationJob` runs nightly and raises
   `ledger.integrity-alert.v1` for any entry that does not net to zero.
8. **Gateway adapters.** `IPaymentGateway` has a deterministic fake (Paystack-style fees, HMAC-SHA512 webhooks), a
   Paystack adapter whose initialize/verify/webhook-parsing are implemented against the public API shape (virtual
   accounts and refunds are stubs until a live account exists) and a Flutterwave stub.

## Consequences

- Money arithmetic is exact (kobo) and every posting carries a journal entry; integration tests assert the blueprint's
  numbers (36,000 / 9,000; 44,225 / 775; 8,225; 90,000,000 / 1,125,000).
- The sweep waits `Payments:SweepHoldDays` (7) after a successful payment; tests set it to 0.
- Entry 4 and payouts, and entries 7–10, are deliberately left to later sprints; a test posts entry 4 directly through
  the contract to prove the path.
