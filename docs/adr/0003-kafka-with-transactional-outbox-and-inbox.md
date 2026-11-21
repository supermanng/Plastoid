# ADR 0003 — Kafka with a transactional outbox and a consumer inbox

**Status:** accepted · **Date:** 2026-11-21

## Decision
- Handlers never produce to Kafka. They call `IOutbox.Enqueue(event)`; `UnitOfWork.CommitAsync` writes `<module>_outbox` rows in the business transaction.
- `OutboxPublisherJob<TContext>` (Quartz, every 500 ms) selects `FOR UPDATE SKIP LOCKED`, produces with an idempotent producer (`acks=all`), marks `published_at`.
- Topics: `plastoids.<module>.<event>.v<major>`, declared by modules and created by `TopicProvisioner`; never auto-created. Each has a `.dlq`.
- `KafkaConsumerHost` per module (group `plastoids-<module>`): inbox insert → handler → commit → offset commit. Duplicate inbox key = already processed = skip. 5 attempts with exponential backoff, then dead-letter with the error in headers.
- Envelope is JSON (`IntegrationEventEnvelope`); headers carry type, version, correlationId, traceparent.

## Consequences
- At-least-once delivery with effectively-once processing. Handlers must still be written idempotently where they touch external systems.
- No Schema Registry at MVP; breaking changes mean a new `v<major>` topic and a dual-publish window.
