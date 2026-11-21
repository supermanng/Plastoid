# ADR 0002 — MySQL 8.4, one scoped connection, one transaction per command

**Status:** accepted · **Date:** 2026-11-21

## Context
MySQL replaces PostgreSQL (build prompt §2.2). MySQL has no row-level security, no `rowversion`, no `pgvector`, and schemas are databases.

## Decision
- One database; every table is prefixed with its module name (`identity_users`, `ledger_postings`).
- `ScopedDbConnection`: one `MySqlConnection` per DI scope, shared by all module `DbContext`s, so `IUnitOfWork` can begin one transaction and enlist any context (`UseTransaction`). Business rows, ledger postings and outbox rows commit together.
- UUID v7 keys stored as `BINARY(16)` via the MySqlConnector `GuidFormat=Binary16` connection-string option (big-endian, ordering preserved). Enforced by `EnsureGuidFormat`.
- `row_version BIGINT UNSIGNED` incremented by `AuditableEntityInterceptor` and used as the EF concurrency token.
- Tenant isolation: global query filter on `tenant_id` in `ModuleDbContext` + `TenantWriteGuardInterceptor` that throws on cross-tenant writes. System contexts bypass and are audited.
- Money as `*_kobo BIGINT` + `*_currency CHAR(3)` complex columns, mapped by convention from the `Money` type.

## Consequences
- Migrations are per context, applied in order by `Migrator` (shared first, then modules).
- Vector search is deferred (`IVectorStore` arrives with the AI gateway in Phase 2).
