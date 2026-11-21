# Runbook: payouts frozen (`payouts_frozen`)

`payouts_frozen` is a single-row control flag in the Ledger module. The daily reconciliation sets it when client-money assets fall below liabilities; while it is set, no payout batch can be created. Lifting it is a human finance decision through a staff endpoint; it is never cleared automatically.

Code references (verified):

- Flag and reasons: `src/Modules/Ledger/Domain/LedgerEntities.cs` — `LedgerControl` (`PayoutsFrozen`, `FreezeReason`, `LastReconciledAt`, `LastAssetsKobo`, `LastLiabilitiesKobo`, `LastVerifiedAt`, `LastUnbalancedEntries`). Singleton id `00000000-0000-7000-8000-00000000c0de`. `RecordReconciliation` sets `PayoutsFrozen = true` whenever `assets < liabilities`; `Unfreeze()` clears it.
- Setter: `ReconciliationJob` in `src/Modules/Ledger/Jobs/LedgerJobs.cs` (Quartz job `ledger-reconciliation`, cron `0 0 3 * * ?` = 03:00 UTC daily, registered in `src/Modules/Ledger/LedgerModule.cs`).
- Reader: `LedgerService.ArePayoutsFrozenAsync` (`src/Modules/Ledger/Services/LedgerService.cs`, exposed as `IPayoutControls` in `src/Modules/Ledger/Contracts/ILedgerService.cs`).
- Enforcement: `PayoutRunner` in `src/Modules/Payouts/Services/PayoutServices.cs` returns `409 payouts_frozen` ("Payouts are frozen by the reconciliation control; resolve the shortfall first."). `PayoutRunner` is used by both `POST /api/v1/admin/payouts/batches` (`CreatePayoutBatch`) and the Thursday `payouts-weekly-run` job (`src/Modules/Payouts/Jobs/PayoutJobs.cs`, cron `0 0 8 ? * THU`).
- Staff endpoints (`src/Modules/Ledger/LedgerModule.cs`, group `/admin/ledger`, all `Policies.StaffAdmin` = staff role + MFA, plus the `Admin:IpAllowList` middleware in `src/Plastoids.Infrastructure/Http/AdminIpAllowListMiddleware.cs`):
  - `GET  /api/v1/admin/ledger/reconciliation` — status (`GetReconciliationStatus`)
  - `POST /api/v1/admin/ledger/reconciliation/run` — run the job now (`RunReconciliation`)
  - `POST /api/v1/admin/ledger/reconciliation/unfreeze` — body `{ "reason": "..." }` (10–1000 chars), header `Idempotency-Key` required (`UnfreezePayouts`, `src/Modules/Ledger/Features/Admin/UnfreezePayouts/UnfreezePayouts.cs`)
  - `GET  /api/v1/admin/ledger/accounts/{accountCode}/balance?ownerId=` and `GET /api/v1/admin/ledger/entries?kind=&accountCode=&ownerId=&cursor=&limit=`
  - `GET  /api/v1/admin/payouts/queue` — shows `payoutsFrozen` alongside the payable tutors (`src/Modules/Payouts/Features/Batches/ListPayoutQueue/ListPayoutQueue.cs`)
- Table: `ledger_controls` (columns `id, tenant_id, payouts_frozen, freeze_reason, last_reconciled_at, last_assets_kobo, last_liabilities_kobo, last_verified_at, last_unbalanced_entries, created_at, updated_at, row_version`). It has **no** append-only trigger (only `ledger_journal_entries` and `ledger_postings` do), but do not edit it by SQL: the endpoint is idempotent, policy-checked and leaves an audit trail.

## Symptoms / alerts

- API log at 03:00 UTC: `Client-funds shortfall: assets {Assets} < liabilities {Liabilities} kobo; payouts frozen` (LogError).
- Outbox row in `ledger_outbox` for topic `plastoids.ledger.shortfall-detected.v1` (`ShortfallDetected` in `src/Modules/Ledger/Contracts/LedgerEvents.cs`). Note: **no module consumes this topic** today, so the only automatic signal is the log line. Alert on it.
- Finance sees `409 payouts_frozen` from `POST /api/v1/admin/payouts/batches`, or the Thursday run logs that it skipped.
- `GET /api/v1/admin/payouts/queue` returns `"payoutsFrozen": true`.
- Tutors ask why Thursday payouts did not arrive.

## How to confirm

Staff token (seeded staff user in Development: `staff@plastoids.local`, password and TOTP secret in `src/Plastoids.Api/appsettings.Development.json`; the token must come from an allow-listed IP):

```bash
MFA=$(curl -s -X POST localhost:5080/api/v1/identity/auth/token -H 'Content-Type: application/json' \
  -d '{"grantType":"password","email":"staff@plastoids.local","password":"Staff-Pass-2026!"}' | jq -r .mfaToken)
CODE=$(oathtool --totp -b JBSWY3DPEHPK3PXPJBSWY3DPEHPK3PXP)
STAFF_TOKEN=$(curl -s -X POST localhost:5080/api/v1/identity/auth/mfa/verify -H 'Content-Type: application/json' \
  -d "{\"code\":\"$CODE\",\"mfaToken\":\"$MFA\"}" | jq -r .accessToken)
```

Status:

```bash
curl -s -H "Authorization: Bearer $STAFF_TOKEN" localhost:5080/api/v1/admin/ledger/reconciliation | jq .
# {"payoutsFrozen":true,"freezeReason":"Shortfall of 1,234,567.00 on 2026-11-21","lastReconciledAt":...,
#  "lastAssetsKobo":...,"lastLiabilitiesKobo":...,"lastVerifiedAt":...,"lastUnbalancedEntries":0}
```

Same thing in SQL:

```sql
SELECT payouts_frozen, freeze_reason, last_reconciled_at,
       last_assets_kobo, last_liabilities_kobo,
       (last_liabilities_kobo - last_assets_kobo) AS shortfall_kobo,
       last_verified_at, last_unbalanced_entries
FROM ledger_controls
WHERE id = UUID_TO_BIN('00000000-0000-7000-8000-00000000c0de');
```

Understand *why*: the figures the job compared are the natural balances of these accounts (see `docs/runbooks/reconciliation-shortfall.md` for the full investigation):

```sql
-- assets side: 1020 bank client funds + 1030 gateway in transit; liabilities: 2010 + 2020 + 2030
SELECT a.code, a.name, a.kind,
       SUM(p.debit_kobo) - SUM(p.credit_kobo) AS debit_minus_credit_kobo
FROM ledger_postings p JOIN ledger_accounts a ON a.code = p.account_code
WHERE a.code IN ('1020','1030','2010','2020','2030')
GROUP BY a.code, a.name, a.kind
ORDER BY a.code;
```

(Assets have a natural debit balance, liabilities a natural credit balance; multiply the liability rows by -1 to compare with the API figures.)

Check whether the nightly integrity job also found unbalanced entries (`last_unbalanced_entries > 0` means the shortfall may be a bookkeeping error, not missing money):

```sql
SELECT BIN_TO_UUID(p.journal_entry_id) AS entry, SUM(p.debit_kobo) - SUM(p.credit_kobo) AS net_kobo
FROM ledger_postings p GROUP BY p.journal_entry_id HAVING net_kobo <> 0;
```

## Impact

- Blocked: creating payout batches (manual and weekly). Tutors are not paid until the flag is lifted; existing approved/submitted batches are unaffected (the check is at batch creation).
- Not blocked: learner payments, refunds, sponsor invoicing, enrolments. The freeze is deliberately narrow.
- Every further reconciliation run keeps `payouts_frozen = true` while `assets < liabilities`; the `ShortfallDetected` event is only raised on the transition from unfrozen to frozen, so after an unfreeze a persisting shortfall re-freezes **and** re-emits the event at the next 03:00 UTC run.

## Remediation

1. **Do not unfreeze first.** Work `docs/runbooks/reconciliation-shortfall.md` to find out whether money is missing (bank/gateway) or the ledger is wrong (unbalanced or mis-posted entry).
2. Fix the cause:
   - Missing funds: wait for the gateway settlement / bank credit, or top up the client-funds account from operating funds (1010 → 1020) by a journal entry posted through the owning module's flow. There is no generic "post a manual entry" endpoint; corrections are reversing entries created in code, never SQL (`ledger_journal_entries` and `ledger_postings` reject UPDATE/DELETE with SQLSTATE 45000).
   - Ledger error: deploy the code fix and post the reversing entry.
3. Re-run reconciliation on demand and confirm `shortfall: false`:

   ```bash
   curl -s -X POST -H "Authorization: Bearer $STAFF_TOKEN" localhost:5080/api/v1/admin/ledger/reconciliation/run | jq .
   # {"assetsKobo":...,"liabilitiesKobo":...,"shortfall":false}
   ```

   A run with `shortfall: false` does **not** clear the flag (`RecordReconciliation` only ever sets it). That is intentional: finance must sign off.
4. Lift the freeze with a written reason (this is the Sprint 8 "human decision" endpoint; the reason is stored nowhere else, so make it specific):

   ```bash
   curl -s -X POST localhost:5080/api/v1/admin/ledger/reconciliation/unfreeze \
     -H "Authorization: Bearer $STAFF_TOKEN" -H 'Content-Type: application/json' \
     -H "Idempotency-Key: unfreeze-$(date +%Y%m%d)-1" \
     -d '{"reason":"Shortfall of N1,234,567 explained by Paystack settlement delay; settled on 2026-11-22 ref PSK-123."}' | jq .
   # {"payoutsFrozen":false,"lastReconciledAt":"..."}
   ```

   `404 ledger_control_missing` means reconciliation has never run (no control row); run step 3 first.
5. If a payout run was missed, create the batch by hand (maker) and have a different staff member approve it (checker): `POST /api/v1/admin/payouts/batches` then the approve endpoint under `/api/v1/admin/payouts/batches/{batchId}` (see `src/Modules/Payouts/Features/Batches/ApprovePayoutBatch/ApprovePayoutBatch.cs`). The next scheduled run is Thursday 08:00 UTC.

## Verification

```bash
curl -s -H "Authorization: Bearer $STAFF_TOKEN" localhost:5080/api/v1/admin/ledger/reconciliation | jq '{payoutsFrozen, freezeReason}'
curl -s -H "Authorization: Bearer $STAFF_TOKEN" localhost:5080/api/v1/admin/payouts/queue | jq '{payoutsFrozen, tutors: (.tutors|length)}'
```

Both must show `payoutsFrozen: false`. Then watch the next 03:00 UTC run: the log must **not** contain `Client-funds shortfall`, and `ledger_outbox` must not gain a new `plastoids.ledger.shortfall-detected.v1` row:

```sql
SELECT occurred_at, published_at, JSON_EXTRACT(payload, '$.data.shortfallKobo') AS shortfall_kobo
FROM ledger_outbox WHERE topic = 'plastoids.ledger.shortfall-detected.v1' ORDER BY occurred_at DESC LIMIT 5;
```

## Rollback

If the freeze was lifted in error, re-run reconciliation (`POST .../reconciliation/run`): the job re-freezes automatically whenever `assets < liabilities`. There is no "freeze now" endpoint for a manual hold; if finance wants payouts stopped for another reason, use the per-tutor hold at `/api/v1/admin/payouts/holds` (`src/Modules/Payouts/Features/Holds/PlaceHold/PlaceHold.cs`) or stop the Thursday job by scaling the API to zero before 08:00 UTC.

## Who to notify

- Head of finance (owner of the unfreeze decision) and the finance operations analyst running reconciliation.
- On-call backend engineer if the cause is a ledger defect or an outbox/consumer failure.
- Tutor support, with the expected payout date, whenever a Thursday run is skipped; tutors are paid weekly and will ask.
- Compliance if the shortfall touches segregated client money (account 1020) for more than one business day.
