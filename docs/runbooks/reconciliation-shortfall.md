# Runbook: reconciliation shortfall

The daily reconciliation (`ReconciliationJob`) asserts the client-money invariant from blueprint §18 / build prompt §2.9:

```
bank client funds (1020) + gateway in transit (1030)  >=  tutor payable (2010) + programme funds held (2020) + learner refund credit (2030)
```

A shortfall means the platform appears to hold less money than it owes tutors, sponsors and learners. The job freezes payouts (see `docs/runbooks/payouts-frozen.md`) and raises `plastoids.ledger.shortfall-detected.v1`. This runbook is the investigation.

Code references (verified):

- Job: `ReconciliationJob.RunAsync` in `src/Modules/Ledger/Jobs/LedgerJobs.cs`. Quartz key `ledger-reconciliation`, 03:00 UTC daily (`src/Modules/Ledger/LedgerModule.cs`). Also callable on demand: `POST /api/v1/admin/ledger/reconciliation/run` (`src/Modules/Ledger/Features/Admin/GetReconciliationStatus/GetReconciliationStatusEndpoint.cs`).
- Inputs: `IBankStatement.GetClientFundsBalanceAsync(today)` and `IGatewaySettlementReport.GetInTransitAsync(today)` (`src/Plastoids.Infrastructure/Integrations/IReconciliationSources.cs`). At MVP both are fakes (`FakeBankStatement`, `FakeGatewaySettlementReport`) that return `null`, and `null` means "use the ledger balance of 1020 / 1030". No real adapter exists yet (`InfrastructureServiceCollectionExtensions.cs` registers the fake under key `"none"` for both).
- Balances: `ILedgerService.GetBalanceAsync(code, ownerId)` (`src/Modules/Ledger/Services/LedgerService.cs`); account codes in `src/Modules/Ledger/Contracts/ILedgerService.cs` (`AccountCodes`).
- Nightly integrity check: `BalanceVerificationJob` (`ledger-balance-verification`, 01:00 UTC) — every `ledger_journal_entries` row must have postings that net to zero; failures raise `plastoids.ledger.integrity-alert.v1` and set `last_unbalanced_entries`.
- Schema: `src/Modules/Ledger/Persistence/LedgerDbContext.cs` and `src/Modules/Ledger/Persistence/Migrations/20261121174855_InitialLedger.cs` — tables `ledger_accounts` (chart of accounts seeded: 1010, 1020, 1030, 2010, 2020, 2030, 2040, 4010, 4020, 4030, 4040, 4050, 5010), `ledger_journal_entries`, `ledger_postings` (check constraint `ck_ledger_postings_one_side`; `BEFORE UPDATE`/`BEFORE DELETE` triggers `*_no_update` / `*_no_delete` signal SQLSTATE 45000), `ledger_controls`.

Important consequence of the fakes: while both adapters return `null`, assets are computed **from the ledger itself** (1020 + 1030), so a shortfall at MVP can only come from the ledger's own postings (a refund credit, tutor payable or programme fund recorded without a matching asset debit), never from the bank. Once a real bank statement or settlement adapter is wired, a shortfall can also mean money genuinely has not arrived.

## Symptoms / alerts

- Log: `Client-funds shortfall: assets {Assets} < liabilities {Liabilities} kobo; payouts frozen` (LogError) from `ReconciliationJob`.
- Log: `Journal entry {EntryId} is unbalanced by {Net} kobo` from `BalanceVerificationJob` (01:00 UTC) — often precedes a shortfall by two hours.
- `GET /api/v1/admin/ledger/reconciliation` shows `payoutsFrozen: true` with `freezeReason` "Shortfall of … on YYYY-MM-DD".
- `ledger_outbox` contains `plastoids.ledger.shortfall-detected.v1` or `plastoids.ledger.integrity-alert.v1` rows. Neither topic has a consumer in the codebase, so the alert must come from log monitoring.

## How to confirm

Status via API (staff token recipe in `docs/runbooks/payouts-frozen.md`):

```bash
curl -s -H "Authorization: Bearer $STAFF_TOKEN" localhost:5080/api/v1/admin/ledger/reconciliation | jq .
for c in 1020 1030 2010 2020 2030; do
  echo -n "$c: "; curl -s -H "Authorization: Bearer $STAFF_TOKEN" localhost:5080/api/v1/admin/ledger/accounts/$c/balance | jq -c .
done
```

The same five balances in SQL, signed the way the job sees them (natural balance = debit−credit for assets, credit−debit for liabilities):

```sql
SELECT a.code, a.name,
       CASE WHEN a.kind IN ('Asset','Expense')
            THEN SUM(p.debit_kobo) - SUM(p.credit_kobo)
            ELSE SUM(p.credit_kobo) - SUM(p.debit_kobo) END AS natural_balance_kobo
FROM ledger_accounts a
LEFT JOIN ledger_postings p ON p.account_code = a.code
WHERE a.code IN ('1020','1030','2010','2020','2030')
GROUP BY a.code, a.name, a.kind
ORDER BY a.code;
```

Shortfall = (2010 + 2020 + 2030) − (1020 + 1030). Compare with `last_assets_kobo` / `last_liabilities_kobo` in `ledger_controls`; if they differ, postings landed after the run (or an adapter override was in play).

### Step through the usual causes

1. **Unbalanced entry** (bookkeeping bug — should be impossible because `LedgerService.Post` rejects drafts that do not sum to zero, but check):

   ```sql
   SELECT BIN_TO_UUID(p.journal_entry_id) AS entry, e.reference, e.kind, e.occurred_at,
          SUM(p.debit_kobo) - SUM(p.credit_kobo) AS net_kobo
   FROM ledger_postings p JOIN ledger_journal_entries e ON e.id = p.journal_entry_id
   GROUP BY p.journal_entry_id, e.reference, e.kind, e.occurred_at
   HAVING net_kobo <> 0;
   ```

2. **Liability booked without the asset** — most likely at MVP. Find the liability postings since the last clean reconciliation and check each has a matching 1020/1030 debit in the same entry:

   ```sql
   SELECT e.reference, e.kind, e.occurred_at,
          SUM(CASE WHEN p.account_code IN ('2010','2020','2030') THEN p.credit_kobo - p.debit_kobo ELSE 0 END) AS liability_delta_kobo,
          SUM(CASE WHEN p.account_code IN ('1020','1030') THEN p.debit_kobo - p.credit_kobo ELSE 0 END) AS client_asset_delta_kobo
   FROM ledger_journal_entries e JOIN ledger_postings p ON p.journal_entry_id = e.id
   WHERE e.occurred_at >= (SELECT COALESCE(last_reconciled_at, '2000-01-01') FROM ledger_controls
                           WHERE id = UUID_TO_BIN('00000000-0000-7000-8000-00000000c0de')) - INTERVAL 1 DAY
   GROUP BY e.id, e.reference, e.kind, e.occurred_at
   HAVING liability_delta_kobo > client_asset_delta_kobo
   ORDER BY e.occurred_at;
   ```

   Typical offenders: a refund issued as learner credit (2030) for a payment whose gateway settlement (entry 2: 1030 → 1020) was never imported; a payout failure that restores 2010 without reversing the 1020 credit; a programme funded (2020) from an invoice credit that was matched twice.

3. **Settlement not imported** — 1030 (in transit) stays high and 1020 low; not a shortfall by itself because both are assets, but a *fee* sweep or refund can tip it. Check the last settlement import under `/api/v1/admin/payments/*` (`src/Modules/Payments/PaymentsModule.cs`) and the entries of kind settlement:

   ```sql
   SELECT kind, COUNT(*), MAX(occurred_at) FROM ledger_journal_entries GROUP BY kind ORDER BY kind;
   ```

4. **Real-adapter discrepancy** (only once `Integrations:BankStatement` / `Integrations:SettlementReport` point at real providers): compare the bank/gateway figure with the ledger 1020/1030 balance. If the ledger is right and the bank is short, this is a treasury/operations issue (delayed settlement, bank fees charged to the client account, fraud), not a code issue.

5. **Sub-ledger drift** — the totals may balance while one owner is negative (a tutor paid more than earned, a programme overdrawn). Negative owners are a leading indicator:

   ```sql
   SELECT p.account_code, p.owner_type, BIN_TO_UUID(p.owner_id) AS owner,
          SUM(p.credit_kobo) - SUM(p.debit_kobo) AS balance_kobo
   FROM ledger_postings p
   WHERE p.account_code IN ('2010','2020','2030') AND p.owner_id IS NOT NULL
   GROUP BY p.account_code, p.owner_type, p.owner_id
   HAVING balance_kobo < 0;
   ```

6. **Fake override left set** — in a test or staging environment someone may have set `FakeBankStatement.BalanceOverride` / `FakeGatewaySettlementReport.InTransitOverride` (the integration tests do this to simulate a shortfall). These are in-process singletons; a restart clears them.

## Impact

- Payouts frozen (see `payouts-frozen.md`). Everything else continues.
- Reputational/regulatory: client money (1020) is segregated by design; an unexplained shortfall older than one business day must go to compliance.
- The job runs with `AmbientContext.Enter(Tenants.Marketplace, isSystem: true)` and reads with `AsNoTracking`; it never modifies ledger rows, so re-running it is always safe.

## Remediation

- **Ledger error:** fix the code path that produced the one-sided effect, deploy, then post the correcting entry through the owning module (Payments refund/settlement flows, Payouts failure handling, Sponsorship funding). Corrections are *reversing entries*: the triggers make UPDATE/DELETE on `ledger_journal_entries` / `ledger_postings` fail with `… is append-only`. Never disable the triggers.
- **Missing money:** finance moves operating funds into the client account and the transfer is booked 1010 → 1020 (an entry of the `transfer`/top-up kind in the Payments admin flow; if no such flow exists yet, raise it as a Payments slice — do not insert SQL).
- **Settlement backlog:** run the settlement import and revenue sweep under `/api/v1/admin/payments/*` (entries 2 and 3) and re-run reconciliation.
- After the fix: `POST /api/v1/admin/ledger/reconciliation/run` must report `shortfall: false`, then lift the freeze per `payouts-frozen.md`.

Simulating a shortfall locally (to rehearse): the integration tests set the fake adapters' overrides; the same can be done with a scoped diagnostics endpoint, or by posting a refund credit against a course that was never settled. Do this only on a disposable database.

## Verification

```bash
curl -s -X POST -H "Authorization: Bearer $STAFF_TOKEN" localhost:5080/api/v1/admin/ledger/reconciliation/run | jq .
# expect "shortfall": false and assetsKobo >= liabilitiesKobo
```

```sql
SELECT last_reconciled_at, last_assets_kobo, last_liabilities_kobo, last_unbalanced_entries, payouts_frozen
FROM ledger_controls WHERE id = UUID_TO_BIN('00000000-0000-7000-8000-00000000c0de');
```

Then confirm the ledger still balances globally (every entry nets to zero) with the "unbalanced entry" query above returning no rows, and that the nightly 01:00 UTC job logs no `is unbalanced` line.

## Rollback

Reconciliation itself changes only `ledger_controls`. If a correcting entry was posted wrongly, post its reversal; nothing is ever deleted. If payouts were unfrozen prematurely, re-run reconciliation: it re-freezes on any remaining shortfall.

## Who to notify

- Head of finance immediately (the freeze blocks tutor payouts and the figures are client money).
- Compliance when the shortfall is unexplained after one business day or touches sponsor programme funds (2020).
- On-call backend engineer and the Payments/Payouts module owner for ledger-side causes.
- Sponsor success if programme funds (2020) are affected: funded programmes may need to pause seat allocation.
