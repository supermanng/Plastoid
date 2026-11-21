# ADR 0004 — Quartz in-memory store for Sprint 0

**Status:** accepted (temporary) · **Date:** 2026-11-21

Quartz runs with the RAM job store. Multiple API replicas would each run the outbox publisher, which is safe (`FOR UPDATE SKIP LOCKED`), but durable, clustered scheduling (payout runs, reconciliation) needs the MySQL AdoJobStore.

**Follow-up (Sprint 3, before payouts):** add the Quartz MySQL tables as a migration in `SharedDbContext`, switch to `UsePersistentStore` with `UseMySqlConnector` and `UseClustering`.
