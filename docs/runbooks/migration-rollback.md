# Runbook: migration rollback

Schema changes are EF Core migrations, one set per module, applied in a fixed order by the `migrate` CLI command. Rolling back means moving one module's context to an earlier migration **and** deploying the matching code; the two must move together.

Code references (verified):

- Orchestrator: `src/Plastoids.Infrastructure/Persistence/Migrator.cs` — `Migrator.MigrateAllAsync` applies `SharedDbContext` first, then each module's `DbContext` in registration order. Registration order (`src/Plastoids.Api/Program.cs`): Identity, Audit, Tutors, Catalogue, Ledger, Payments, Booking, Lms, Certificates, Sponsorship, Applications, Payouts.
- CLI entry: `dotnet run --project src/Plastoids.Api -- migrate` (`make migrate`). `Program.cs` runs the migrator and exits before any hosted service starts.
- Auto-migrate: `Database:MigrateOnStartup` (false in `appsettings.json`, **true** in `appsettings.Development.json`, true in integration tests). In Development the API re-applies pending migrations on every `make run`, which silently undoes a rollback; set `Database__MigrateOnStartup=false` while you work.
- History table: no context sets `MigrationsHistoryTable`, so all thirteen contexts share the default `__EFMigrationsHistory` table in the single `plastoids` database. Migration ids are unique across contexts because each carries the module name.
- Tooling: `dotnet-ef` 9.0.23 is pinned in `.config/dotnet-tools.json` (`dotnet tool restore`).
- Migration sources: `src/Modules/<Module>/Persistence/Migrations/` and `src/Plastoids.Infrastructure/Persistence/Shared/Migrations/`.

Current migrations (as of this runbook):

| Context | Migrations (oldest → newest) |
|---|---|
| SharedDbContext | `20261121111630_InitialShared` |
| IdentityDbContext | `20261121112114_InitialIdentity`, `20261121161010_Sprint1Identity` |
| AuditDbContext | `20261121161011_InitialAudit` |
| TutorsDbContext | `20261121170041_InitialTutors` |
| CatalogueDbContext | `20261121170042_InitialCatalogue` |
| LedgerDbContext | `20261121174855_InitialLedger` |
| PaymentsDbContext | `20261121174946_InitialPayments`, `20261121180742_RefundDestination` |
| BookingDbContext | `20261121180739_InitialBooking` |
| LmsDbContext | `20261121180741_InitialLms`, `20261121182458_Sprint5Learning` |
| CertificatesDbContext | `20261121182500_InitialCertificates` |
| SponsorshipDbContext | `20261121190044_InitialSponsorship` |
| ApplicationsDbContext | `20261121190045_InitialApplications` |
| PayoutsDbContext | `20261121192933_InitialPayouts` |

Hard rules:

- **Never roll `LedgerDbContext` or `AuditDbContext` back to `0`.** Their `Down()` drops the append-only triggers and tables (`20261121174855_InitialLedger.cs` lines ~251–271; `20261121161011_InitialAudit.cs`). Financial and audit history is destroyed. Any future ledger migration must be additive; its `Down()` must not touch `ledger_journal_entries` / `ledger_postings` rows (the `*_no_update` / `*_no_delete` triggers would make a data migration fail with SQLSTATE 45000 anyway).
- MySQL DDL is not transactional (each `CREATE`/`ALTER` implicitly commits). A migration that fails halfway leaves a partial schema and no history row. See "Partial failure".
- Prefer a forward fix (a new migration) over a rollback whenever data was written against the new schema.

## Symptoms / alerts

- `make migrate` / the deploy's migrate step failed: log shows `{Context}: N pending migration(s)` followed by a `MySqlException`.
- API returns `500 internal_error` on routes of one module right after a deploy, and the log shows `Unknown column` / `Table 'plastoids.<module>_…' doesn't exist` (code and schema out of step).
- Startup in Development hangs or crashes in `MigrateOnStartupService` (runs before Quartz, Kafka consumers and the server).
- On a shared managed MySQL cluster: `ERROR 1142` / `Access denied` for DDL — the account may lack privileges or DDL may be blocked by the provider; confirm with `SHOW GRANTS;` before assuming the migration is wrong.

## How to confirm

What the database thinks is applied:

```bash
docker compose exec mysql mysql -uplastoids -pplastoids plastoids -e "
SELECT MigrationId, ProductVersion FROM __EFMigrationsHistory ORDER BY MigrationId;"
```

What the deployed code expects (run from a checkout of the deployed commit; `--no-build` after `dotnet build`):

```bash
dotnet tool restore
export ConnectionStrings__Plastoids='Server=localhost;Port=3306;Database=plastoids;User=plastoids;Password=plastoids;'
dotnet ef migrations list --project src/Modules/Payments --startup-project src/Plastoids.Api --context PaymentsDbContext
# entries marked "(Pending)" are in code but not in the database
```

Repeat per module as needed (`--project src/Modules/<Module> --context <Module>DbContext`; for the shared tables `--project src/Plastoids.Infrastructure --context SharedDbContext`).

Partial-failure check for the module in question (tables present but no history row):

```sql
SELECT table_name FROM information_schema.tables WHERE table_schema = 'plastoids' AND table_name LIKE 'payments\_%' ORDER BY 1;
SELECT trigger_name, event_object_table FROM information_schema.triggers WHERE trigger_schema = 'plastoids';
```

## Impact

- During a rollback the affected module's endpoints fail; other modules keep working (separate tables, shared connection).
- Dropping a column or table loses the data in it. Check `Down()` of the migration you are reverting before running it; EF-generated `Down()` for a `CreateTable` is `DropTable`.
- Outbox/inbox rows for the module live in `<module>_outbox` / `<module>_inbox`; a rollback that drops them loses unpublished events. Check `SELECT COUNT(*) FROM <module>_outbox WHERE published_at IS NULL` first (see `outbox-backlog.md`).

## Remediation

### 0. Freeze and back up

```bash
# Stop the API (no writes, no auto-migrate, no Quartz).
docker compose stop api 2>/dev/null || pkill -f 'Plastoids.Api' || true

# Full logical backup including triggers (needed for ledger_* and audit_events).
docker compose exec mysql mysqldump -uroot -proot --single-transaction --triggers --routines plastoids > artifacts/backup-$(date +%Y%m%dT%H%M%S).sql
```

Production: take the provider's snapshot as well as the dump; confirm the dump restores into a scratch schema before proceeding.

### 1. Generate the rollback SQL and read it

Always script first; never run `database update` blind on a shared server.

```bash
dotnet ef migrations script 20261121180742_RefundDestination 20261121174946_InitialPayments \
  --project src/Modules/Payments --startup-project src/Plastoids.Api --context PaymentsDbContext \
  --idempotent -o artifacts/rollback-payments.sql
less artifacts/rollback-payments.sql
```

(The first argument is the *current* migration, the second the *target*; EF emits the `Down()` of everything in between plus the `DELETE FROM __EFMigrationsHistory` rows.)

### 2. Apply

Option A, through EF (local / staging):

```bash
dotnet ef database update 20261121174946_InitialPayments \
  --project src/Modules/Payments --startup-project src/Plastoids.Api --context PaymentsDbContext
```

Option B, the reviewed script (production, or when the database account for deploys is separate):

```bash
docker compose exec -T mysql mysql -uplastoids -pplastoids plastoids < artifacts/rollback-payments.sql
```

Roll back modules in **reverse registration order** when a release touched several (Payouts → Applications → Sponsorship → Certificates → Lms → Booking → Payments → Ledger → Catalogue → Tutors → Audit → Identity → Shared), because later modules may reference ids written by earlier ones even though there are no cross-module foreign keys.

### 3. Deploy the matching code

Redeploy the image built from the commit whose migrations match the database (`src/Plastoids.Api/Dockerfile`; CI tags `plastoids-api:<git sha>`). With `Database:MigrateOnStartup=false` the API does **not** verify the schema at start-up; a mismatch surfaces as 500s at query time, so step 4 matters.

### 4. Partial failure (migration died halfway)

MySQL has already committed the statements that ran. Either:

- finish it by hand: take the remaining statements from `dotnet ef migrations script <previous> <failed> --idempotent`, run them, then insert the history row the script ends with; or
- unwind it by hand: run the `Down()` statements for the objects that exist (use the `information_schema` queries above to see what was created), leaving no history row, then fix the migration in code and re-run `make migrate`.

Pomelo's `--idempotent` script wraps each migration in a `IF NOT EXISTS (SELECT … __EFMigrationsHistory …)` procedure, which is what makes "finish it by hand" safe to re-run.

### 5. Shared managed cluster note

If `plastoids` lives on a MySQL cluster shared with other applications, confirm the deploy account still has `ALTER, CREATE, DROP, TRIGGER, CREATE ROUTINE` and that the provider is not blocking DDL during a maintenance window: `SHOW GRANTS FOR CURRENT_USER();`. Triggers additionally need `log_bin_trust_function_creators=1` when binary logging is on (set in `docker-compose.yml` for local; on a managed cluster it is a provider setting). Do not bump `max_connections` or other global settings from this runbook; they affect the other tenants.

## Verification

```bash
dotnet ef migrations list --project src/Modules/Payments --startup-project src/Plastoids.Api --context PaymentsDbContext
# nothing "(Pending)" that the deployed code does not have; nothing applied that the code lacks

docker compose exec mysql mysql -uplastoids -pplastoids plastoids -e "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;"
curl -s http://localhost:5080/health/ready | jq .
```

Then exercise one write and one read of the rolled-back module (for Payments: `POST /api/v1/payments/intents` with an `Idempotency-Key`, then the fake webhook; recipe in `perf/README.md`) and watch the log for `Unhandled exception`.

Ledger/audit integrity after any migration work:

```sql
SELECT trigger_name FROM information_schema.triggers WHERE trigger_schema = 'plastoids'
  AND trigger_name IN ('ledger_journal_entries_no_update','ledger_journal_entries_no_delete',
                       'ledger_postings_no_update','ledger_postings_no_delete',
                       'audit_events_no_update','audit_events_no_delete');
-- expect 6 rows
SELECT COUNT(*) FROM ledger_accounts;   -- expect 13
```

## Rollback of the rollback

Re-run `make migrate` (or `dotnet ef database update` with no target) from the newer commit; the migration re-applies forward. Data dropped by `Down()` comes back only from the backup in step 0: restore into a scratch schema and copy the rows across with `INSERT … SELECT`, excluding `ledger_*` and `audit_events`, which must be regenerated from their source events rather than copied.

## Who to notify

- On-call backend engineer runs it; a second engineer reviews the scripted SQL before it touches staging or production (treat it like a maker–checker step).
- Finance before anything touches `ledger_*`, `payments_*`, `payouts_*`.
- Product/support if a module's endpoints are down during the window; post the window in the incident channel.
- The database platform owner when the cluster is shared with other applications.
