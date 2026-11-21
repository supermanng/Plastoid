# Runbook: outbox backlog

Integration events are written to a per-module outbox table inside the business transaction and published to Kafka by a Quartz job. A backlog means rows are accumulating with `published_at IS NULL`, so downstream modules (enrolment creation, programme funding, payouts, audit trail) fall behind or stop.

Code references (verified):

- Publisher: `src/Plastoids.Infrastructure/Jobs/OutboxPublisherJob.cs` — `OutboxPublisherJob<TContext>`, Quartz job key `outbox-publish-<module>`, runs every 500 ms per module, `SELECT * FROM <module>_outbox WHERE published_at IS NULL AND attempts < 10 ORDER BY occurred_at LIMIT <Kafka:OutboxBatchSize=200> FOR UPDATE SKIP LOCKED`.
- Purge: same file, `OutboxPurgeJob<TContext>`, job key `outbox-purge-<module>`, 02:30 UTC daily, deletes published rows and processed inbox rows older than 7 days.
- Job registration: `src/Plastoids.Infrastructure/InfrastructureServiceCollectionExtensions.cs` (`AddModuleDbContext`). Quartz uses the in-memory store (ADR 0004), so jobs live inside the API process.
- Producer: `src/Plastoids.Infrastructure/Messaging/Kafka/KafkaProducer.cs` — idempotent, `acks=all`, `MessageSendMaxRetries=int.MaxValue`, `RetryBackoffMs=200`.
- Table shape: `src/Plastoids.Infrastructure/Messaging/Outbox/OutboxMessage.cs` — `id BINARY(16), topic, partition_key, event_type, event_version, payload JSON, headers JSON, occurred_at, published_at NULL, attempts, last_error`.
- Row flush: `src/Plastoids.Infrastructure/Persistence/UnitOfWork.cs` writes pending events to the *primary* module context's outbox on commit.

Outbox tables (one per `ModuleDbContext.ModulePrefix`):
`identity_outbox`, `audit_outbox`, `tutors_outbox`, `catalogue_outbox`, `ledger_outbox`, `payments_outbox`, `booking_outbox`, `lms_outbox`, `certificates_outbox`, `sponsorship_outbox`, `applications_outbox`, `payouts_outbox`. There is also `shared_outbox` (`SharedDbContext`, prefix `shared`), which has **no publisher job** because `SharedDbContext` is registered with `AddDbContext` directly rather than `AddModuleDbContext`; nothing should ever write to it, so a non-zero count there is a bug, not a backlog.

## Symptoms / alerts

- Learners pay but no enrolment appears (`GET /api/v1/lms/enrolments/me` stays empty); sponsor invoice paid but programme never `isFunded`; tutor earnings never released; audit trail stops growing.
- API log: `Outbox publish failed for {Id} to {Topic} (attempt {Attempts})` (LogError) repeating; or `Kafka producer error {Code}: {Reason}`.
- `GET /health/ready` reports `kafka` unhealthy (probe topic `plastoids.healthcheck`, see `src/Plastoids.Infrastructure/Observability/HealthChecks.cs`).
- Metric to alert on (add to the dashboard if missing): `COUNT(*) WHERE published_at IS NULL` per table > 1,000, or oldest unpublished row older than 2 minutes.

## How to confirm

All SQL below runs against the local stack with:

```bash
docker compose exec mysql mysql -uplastoids -pplastoids plastoids
```

Backlog per module (oldest row and highest attempt count):

```sql
SELECT 'identity'     AS module, COUNT(*) AS pending, MIN(occurred_at) AS oldest, MAX(attempts) AS max_attempts FROM identity_outbox     WHERE published_at IS NULL
UNION ALL SELECT 'audit',        COUNT(*), MIN(occurred_at), MAX(attempts) FROM audit_outbox        WHERE published_at IS NULL
UNION ALL SELECT 'tutors',       COUNT(*), MIN(occurred_at), MAX(attempts) FROM tutors_outbox       WHERE published_at IS NULL
UNION ALL SELECT 'catalogue',    COUNT(*), MIN(occurred_at), MAX(attempts) FROM catalogue_outbox    WHERE published_at IS NULL
UNION ALL SELECT 'ledger',       COUNT(*), MIN(occurred_at), MAX(attempts) FROM ledger_outbox       WHERE published_at IS NULL
UNION ALL SELECT 'payments',     COUNT(*), MIN(occurred_at), MAX(attempts) FROM payments_outbox     WHERE published_at IS NULL
UNION ALL SELECT 'booking',      COUNT(*), MIN(occurred_at), MAX(attempts) FROM booking_outbox      WHERE published_at IS NULL
UNION ALL SELECT 'lms',          COUNT(*), MIN(occurred_at), MAX(attempts) FROM lms_outbox          WHERE published_at IS NULL
UNION ALL SELECT 'certificates', COUNT(*), MIN(occurred_at), MAX(attempts) FROM certificates_outbox WHERE published_at IS NULL
UNION ALL SELECT 'sponsorship',  COUNT(*), MIN(occurred_at), MAX(attempts) FROM sponsorship_outbox  WHERE published_at IS NULL
UNION ALL SELECT 'applications', COUNT(*), MIN(occurred_at), MAX(attempts) FROM applications_outbox WHERE published_at IS NULL
UNION ALL SELECT 'payouts',      COUNT(*), MIN(occurred_at), MAX(attempts) FROM payouts_outbox      WHERE published_at IS NULL
UNION ALL SELECT 'shared(bug)',  COUNT(*), MIN(occurred_at), MAX(attempts) FROM shared_outbox       WHERE published_at IS NULL;
```

Rows the publisher has given up on (the job filters `attempts < 10`, so these are never retried automatically):

```sql
SELECT BIN_TO_UUID(id) AS id, topic, attempts, last_error, occurred_at
FROM payments_outbox
WHERE published_at IS NULL AND attempts >= 10
ORDER BY occurred_at
LIMIT 50;
```

Repeat for the module named in the first query. `last_error` is the truncated exception message from Confluent.Kafka.

Is the broker reachable from the API host?

```bash
curl -s http://localhost:5080/health/ready | jq .
docker compose ps kafka
docker compose exec kafka /opt/kafka/bin/kafka-broker-api-versions.sh --bootstrap-server localhost:19092 | head -3
```

Do the topics exist? (They are created by `TopicProvisioner` at API start-up; auto-create is off in `docker-compose.yml`.)

```bash
docker compose exec kafka /opt/kafka/bin/kafka-topics.sh --bootstrap-server localhost:19092 --list | sort
```

A row whose `last_error` says `Unknown topic or partition` means the topic is missing: the API started before Kafka was ready, or the topic was deleted.

Is the publisher job running at all? It logs at Debug (`Outbox [{Prefix}] published {Published}/{Total}`); temporarily run with `Serilog__MinimumLevel__Default=Debug` or check that the API process is up and not stuck in a long Kafka call (see "Impact").

## Impact

- Events are **not lost**: rows stay in the table until published; the producer is idempotent and consumers dedup through `<module>_inbox`, so a late publish is safe.
- Ordering within an aggregate is preserved (`ORDER BY occurred_at`, partition key = aggregate id).
- With unlimited producer retries, a dead broker makes `ProduceAsync` block until librdkafka's `message.timeout.ms` (default 300 s). `[DisallowConcurrentExecution]` means one stuck batch pauses that module's publisher for up to 5 minutes per message, which is the usual cause of a backlog that "looks frozen" even after Kafka returns.
- Rows with `attempts >= 10` are silently parked. Nothing alerts on them today.

## Remediation

1. **Broker down or unreachable**

   ```bash
   docker compose up -d --wait kafka
   docker compose logs --tail=200 kafka
   ```

   Production: check the broker's disk and the controller quorum, then restart the API once `/health/ready` shows `kafka: Healthy` so the publisher jobs reconnect quickly instead of waiting out the message timeout.

2. **Topic missing** (`Unknown topic or partition`)

   Restart the API: `TopicProvisioner` (`src/Plastoids.Infrastructure/Messaging/Kafka/TopicProvisioner.cs`) creates every declared topic and its `.dlq`. To create one by hand:

   ```bash
   docker compose exec kafka /opt/kafka/bin/kafka-topics.sh --bootstrap-server localhost:19092 \
     --create --topic plastoids.payments.payment-succeeded.v1 --partitions 6 --replication-factor 1
   ```

   Use the same partition count as `Kafka:DefaultPartitions` (6 in `appsettings.json`).

3. **Parked rows (`attempts >= 10`)**

   Once the cause is fixed, reset the attempt counter so the job picks them up on its next 500 ms tick. Outbox tables have no append-only triggers (only `ledger_journal_entries`, `ledger_postings` and `audit_events` do), so this UPDATE is allowed:

   ```sql
   UPDATE payments_outbox
   SET attempts = 0, last_error = NULL
   WHERE published_at IS NULL AND attempts >= 10;
   ```

   Never set `published_at` by hand to "clear" a backlog; that drops the event.

4. **Large backlog, broker healthy, publishing slowly**

   The job publishes 200 rows per tick per module and awaits each delivery report serially. Raise `Kafka__OutboxBatchSize` (environment variable) to 1000 and restart the API; the LIMIT is read from options at each run. Running a second API instance also helps: `FOR UPDATE SKIP LOCKED` lets instances share the table without double-publishing.

5. **Payload/serialisation failure** (`last_error` mentions JSON or the envelope)

   This is a code defect in the event record or `EnvelopeSerializer`. Do not delete the row. Copy it aside, fix the code, redeploy, then reset `attempts` as in step 3:

   ```sql
   CREATE TABLE IF NOT EXISTS ops_outbox_quarantine LIKE payments_outbox;
   INSERT INTO ops_outbox_quarantine SELECT * FROM payments_outbox WHERE published_at IS NULL AND attempts >= 10;
   ```

## Verification

```sql
SELECT COUNT(*) AS still_pending, MIN(occurred_at) AS oldest FROM payments_outbox WHERE published_at IS NULL;
```

`still_pending` should drop to zero (or to the rows written in the last second) within a minute. Then confirm the consumer side caught up:

```bash
docker compose exec kafka /opt/kafka/bin/kafka-consumer-groups.sh --bootstrap-server localhost:19092 --describe --all-groups
```

`LAG` should trend to 0 for every `plastoids-<module>` group. If lag stays high while the outbox is clear, switch to `docs/runbooks/consumer-dlq-replay.md`.

Finally spot-check one business effect: pay for a course on the fake gateway (recipe in `perf/README.md`) and confirm the enrolment appears.

## Rollback

There is nothing to roll back; the remediation steps are additive. If a batch-size increase causes memory pressure or long transactions, set `Kafka__OutboxBatchSize` back to 200 and restart.

## Who to notify

- On-call backend engineer (owner of the API process and Quartz jobs).
- Finance operations if `payments_outbox`, `ledger_outbox` or `payouts_outbox` are affected: payouts and settlement sweeps depend on these events.
- Sponsor success if `sponsorship_outbox` or `applications_outbox` back up during a live programme window (funding, seat allocation and offers stall).
- Record the incident and the row counts in the incident log; include the `last_error` text.
