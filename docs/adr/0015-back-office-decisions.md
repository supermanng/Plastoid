# ADR 0015: Sprint 8 decisions — Back office (story I1)

**Status:** Accepted (2026-11-21)

## Decisions

1. **The dashboard never recomputes on screen.** `GET /admin/backoffice/dashboard` reads ledger balances through
   `ILedgerService` (revenue accounts 4010/4020/4030/4040 less 5010; liabilities 1020/1030/2010/2020/2030/2040) and
   BackOffice's own `backoffice_metrics` counters, which consumers bump from payment-succeeded, refund-issued,
   user-registered, organisation-verified, tutor-verified, enrolment-created/completed, programme-funded/published,
   batch-approved, payout-settled/failed and tutor-strike-recorded. Risk alerts (`backoffice_risk_alerts`) come from
   ledger.shortfall-detected, review-flagged strikes and failed/reversed payouts; `payouts_frozen` is read live from
   `IPayoutControls`.
2. **Queues are views; decisions stay where the rules live.** Verification (Tutors + Identity readers), course moderation
   (`ICourseModeration`, read-only), refunds (`IPaymentsAdminQueues`, four-eyes stays in Payments), disputes
   (`IDisputeQueue` over Payouts holds + strike alerts) and NDPA data requests are read through Contracts. Each queue view
   names the endpoint where the decision is taken, so there is exactly one write path per rule.
3. **Account administration through Identity.Contracts.** `IUserAdministration` searches, suspends and reinstates
   accounts; suspension changes `User.Status` and revokes refresh tokens inside the caller's transaction, so the
   `[Audited]` aggregate yields before/after snapshots on the staff command. Staff cannot suspend themselves.
4. **Four-eyes is enforced in the domain, not the UI.** `FeeRule` (Payments), `RoleGrantProposal` (Identity) and
   `Refund` (existing) refuse the proposer as approver with `maker_checker_violation`; role grants also refuse the
   beneficiary. Fee rules are versioned per rule code with an `EffectiveFrom` date; `IFeeRules` resolves the approved
   rule in force at the sale instant (highest version on ties) and falls back to the `Fees` configuration section, so
   past sales keep the rate stored on them (blueprint §18). Rules cannot be back-dated.
5. **Feature flags live in BackOffice** (`backoffice_feature_flags`, kebab-case keys, audited) behind
   `IFeatureFlags.IsEnabledAsync`; unknown flags are off.
6. **Audit log viewer through Audit.Contracts** (`IAuditLogReader`): cursor-paged, filtered by actor, entity, entity id
   and date range, backed by the append-only `audit_events` table.
7. **Staff-only, MFA, allow-listed.** Everything is under `/admin/backoffice/*` with `Policies.StaffAdmin`; the admin IP
   allow-list middleware already covers `/api/v1/admin/*`. Idempotency-Key is required on every mutation.

## Consequences

- New migrations: Identity (`identity_role_grants`), Payments (`payments_fee_rules`), BackOffice (initial).
- `IFeeRules` is now scoped (it reads approved rules once per scope) instead of a singleton.
- A second seeded staff account is needed to exercise successful four-eyes approvals end to end; until then the
  success path is unit-tested and the integration test proves the refusal.
