# Runbook: consumer dead-letter queue (DLQ) replay

Each module runs one Kafka consumer (`KafkaConsumerHost`) in consumer group `plastoids-<module>`. A message that fails `Kafka:ConsumerMaxAttempts` (5) times, or cannot be parsed, is produced to `<topic>.dlq` and its offset is committed, so the partition keeps flowing. Replay is a manual operation: nothing in the codebase re-reads a `.dlq` topic.

Code references (verified):

- Consumer loop and dead-lettering: `src/Plastoids.Infrastructure/Messaging/Kafka/KafkaConsumerHost.cs`. Order per message: begin MySQL transaction → insert `<module>_inbox` row (duplicate key → skip, commit offset) → run handler(s) → set `processed_at` → commit transaction → commit offset. Retries use exponential backoff from `Kafka:ConsumerBaseBackoffMs` (500 ms): 0.5 s, 1 s, 2 s, 4 s, 8 s.
- DLQ headers written: `dlq.error` (`ExceptionType: message`), `dlq.sourceTopic`, `dlq.sourceOffset`, plus the original headers (`type`, `version`, `correlationId`, `traceparent`).
- Consumer registrations (which module consumes which topic, and the handler type): each module's `Consumers()` in `src/Modules/<Module>/<Module>Module.cs`, e.g. `src/Modules/Payouts/PayoutsModule.cs`. Identity, Audit, Booking, Lms, Certificates, Sponsorship, Applications, Catalogue and Payouts have consumers; Payments, Ledger and Tutors have none.
- Inbox table: `<module>_inbox (message_id BINARY(16), consumer VARCHAR(200), received_at, processed_at)`, primary key `(message_id, consumer)`. `consumer` is `"<module>:<HandlerTypeName>"`, e.g. `payouts:PaymentSucceededConsumer`.
- Topics and `.dlq` twins are created at start-up by `src/Plastoids.Infrastructure/Messaging/Kafka/TopicProvisioner.cs`.
- Options: `src/Plastoids.Infrastructure/Messaging/Kafka/KafkaOptions.cs`.

Because the handler runs inside the unit of work and the scope (and therefore the uncommitted transaction) is disposed on failure, a dead-lettered message leaves **no** inbox row. Replaying the identical envelope is therefore processed as a first delivery. If a message was processed successfully once, a replay is swallowed by the inbox (`Duplicate delivery of {Type} {Id} on [{Module}] ignored`).

## Symptoms / alerts

- API log: `Dead-lettered message from {Topic} offset {Offset} to {Dlq}` (LogError), preceded by five `Attempt {Attempt}/{Max} failed for {Type} {Id} on [{Module}]; retrying in {Delay}` warnings.
- `Poison message on {Topic} offset {Offset}; dead-lettering` for an unparseable envelope (goes straight to the DLQ, no retries).
- A `.dlq` topic has a non-zero end offset (alert on this; see "How to confirm").
- Business symptom matches the topic: e.g. `plastoids.payments.payment-succeeded.v1.dlq` growing means paid learners are not enrolled (Lms consumer) or tutor earnings are not accrued (Payouts consumer).

## How to confirm

List DLQ topics and their sizes (an end offset above 0 means at least one message was ever dead-lettered; compare with the earliest offset to see what is retained):

```bash
docker compose exec kafka bash -c '
for t in $(/opt/kafka/bin/kafka-topics.sh --bootstrap-server localhost:19092 --list | grep "\.dlq$"); do
  /opt/kafka/bin/kafka-get-offsets.sh --bootstrap-server localhost:19092 --topic "$t" --time -1
done' | awk -F: '$3 > 0'
```

Read the dead letters with headers (the `dlq.error` header tells you why):

```bash
docker compose exec kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server localhost:19092 \
  --topic plastoids.payments.payment-succeeded.v1.dlq --from-beginning --timeout-ms 10000 \
  --property print.headers=true --property print.key=true --property print.offset=true --property print.partition=true
```

Check the consumer group is alive and not lagging on the source topic (replay is pointless if the consumer is down):

```bash
docker compose exec kafka /opt/kafka/bin/kafka-consumer-groups.sh --bootstrap-server localhost:19092 --describe --group plastoids-payouts
```

Confirm no inbox row exists for the envelope id (`id` in the JSON value); if one exists with `processed_at` set, the message already succeeded on another attempt and must not be replayed:

```bash
docker compose exec mysql mysql -uplastoids -pplastoids plastoids -e "
SELECT BIN_TO_UUID(message_id) AS message_id, consumer, received_at, processed_at
FROM payouts_inbox
WHERE message_id = UUID_TO_BIN('<envelope id from the DLQ message>');"
```

Find the originating outbox row (useful for the business context and to confirm the payload was published as intended):

```bash
docker compose exec mysql mysql -uplastoids -pplastoids plastoids -e "
SELECT topic, event_type, occurred_at, published_at, attempts, JSON_PRETTY(payload)
FROM payments_outbox
WHERE id = UUID_TO_BIN('<envelope id>');"
```

## Impact

- The source partition is **not** blocked: the failed offset was committed after dead-lettering, later events for the same aggregate are processed in order of arrival. A replay therefore arrives *after* newer events for that aggregate; check that the handler tolerates that (most do, because they key off the aggregate state rather than assume ordering).
- The side effect of the handler did not happen (enrolment, earning accrual, programme funding, audit row, certificate). Money already moved in the producing module (e.g. the payment is `succeeded` and ledger entry 1 is posted); the replay only completes the downstream effect.
- Payouts and Sponsorship handlers post ledger entries. Replaying them twice is safe only because of the inbox; never bypass the consumer by calling handlers directly.

## Remediation

### Step 1 — fix the cause

Read `dlq.error`. Typical causes and fixes:

| `dlq.error` starts with | Cause | Fix before replay |
|---|---|---|
| `MySqlException` / `DbUpdateException` | Database outage, deadlock, FK violation | Restore the DB, or correct the data the handler expects (e.g. a missing course projection in `catalogue_verified_tutors`) |
| `InvalidOperationException: Sequence contains no elements` | Handler expects a row that does not exist yet (ordering) | Check the aggregate exists now; if so, replay |
| `JsonException` / `Poison message` | Envelope shape or event version mismatch | Deploy the code fix first (a new `v<major>` topic if the contract changed, per §2.7) |
| `KafkaException` | Handler produced a nested event and the broker rejected it | Fix Kafka (see `outbox-backlog.md`) |

If the code needed a change, deploy it and confirm the consumer group re-subscribes (`Consumer [{Module}] subscribed to {Topics}` in the log).

### Step 2 — replay

Replay = re-produce the **same value** (the envelope JSON) with the **same key** (partition key = aggregate id) to the **source topic**. The consumer parses everything it needs from the value, so headers are optional; the original `correlationId` is inside the envelope.

Single message, by hand:

```bash
# 1. Dump key<TAB>value lines from the DLQ (no headers), newest run only.
docker compose exec kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server localhost:19092 \
  --topic plastoids.payments.payment-succeeded.v1.dlq --from-beginning --timeout-ms 10000 \
  --property print.key=true --property key.separator=$'\t' > /tmp/dlq.tsv

# 2. Inspect; keep only the lines you intend to replay (grep by envelope id or aggregate id).
grep '"id":"<envelope id>"' /tmp/dlq.tsv > /tmp/replay.tsv

# 3. Produce them back to the SOURCE topic, keyed.
docker compose exec -T kafka /opt/kafka/bin/kafka-console-producer.sh --bootstrap-server localhost:19092 \
  --topic plastoids.payments.payment-succeeded.v1 \
  --property parse.key=true --property key.separator=$'\t' < /tmp/replay.tsv
```

Note the `.dlq` topic keeps the replayed message; it is an audit trail, not a queue. Record the DLQ offsets you replayed in the incident log so nobody replays them again (the inbox would reject them, but the log noise is confusing).

Replay from the application side is also possible without the Kafka CLI when the original outbox row still exists (rows are purged 7 days after publishing): the integration test `tests/Plastoids.IntegrationTests/OutboxPipelineTests.cs` shows the exact call (`IKafkaProducer.ProduceRawAsync(topic, partitionKey, payload, headers)`), which is what a future `POST /admin/messaging/replay` endpoint should wrap. There is no such endpoint today.

### Step 3 — poison messages that must be discarded

If the message is genuinely invalid and the business effect was handled manually, do nothing in Kafka; just record the DLQ offset and the manual correction in the incident log. Do not delete the `.dlq` topic (the provisioner would recreate it, and you lose the trail).

### Resetting a consumer group (last resort)

Only when a handler bug caused *successful but wrong* processing and you need to re-run from an offset. This replays **every** message after that offset, and the inbox will skip those already processed, so it only helps after you delete the relevant inbox rows for the affected consumer:

```bash
# Stop the API first: a reset is refused while the group has active members.
docker compose exec kafka /opt/kafka/bin/kafka-consumer-groups.sh --bootstrap-server localhost:19092 \
  --group plastoids-lms --topic plastoids.payments.payment-succeeded.v1:0 \
  --reset-offsets --to-offset 1234 --execute
```

```sql
DELETE FROM lms_inbox
WHERE consumer = 'lms:PaymentSucceededConsumer'
  AND received_at >= '2026-11-21 10:00:00';
```

Inbox tables have no append-only triggers; this DELETE is allowed. Think carefully about ledger-posting handlers (Payouts, Sponsorship): re-running them posts entries again, and the ledger is append-only, so the correct path there is a reversing entry, not a replay.

## Verification

```bash
# Consumer processed it: inbox row with processed_at set.
docker compose exec mysql mysql -uplastoids -pplastoids plastoids -e "
SELECT consumer, received_at, processed_at FROM payouts_inbox
WHERE message_id = UUID_TO_BIN('<envelope id>');"

# No new dead letters.
docker compose exec kafka /opt/kafka/bin/kafka-get-offsets.sh --bootstrap-server localhost:19092 \
  --topic plastoids.payments.payment-succeeded.v1.dlq --time -1

# Business effect, e.g. tutor earning accrued (staff token, allow-listed IP):
curl -s -H "Authorization: Bearer $STAFF_TOKEN" http://localhost:5080/api/v1/admin/payouts/queue | jq .
```

Also tail the API log for `Duplicate delivery ... ignored` (fine, already processed) versus a fresh `Dead-lettered message` (cause not fixed).

## Rollback

A replay cannot be undone at the messaging layer. If the replay produced a wrong side effect, correct it through the owning module's own endpoints (refund, reversing ledger entry via a new posting, certificate revoke at `POST /api/v1/admin/certificates/{id}/revoke`), never by editing rows.

## Who to notify

- On-call backend engineer; the engineer owning the consuming module (see the `Consumers()` list) reviews the `dlq.error` before replay.
- Finance operations for `plastoids.payments.*`, `plastoids.payouts.*`, `plastoids.ledger.*` and any replay into Payouts or Sponsorship (ledger postings).
- Learner support if `plastoids.lms.*` or `plastoids.certificates.*` dead letters delayed enrolments or certificates; they will field the tickets.
- Compliance if `plastoids.audit.event.v1.dlq` is non-empty: the audit trail has a gap until replayed.
