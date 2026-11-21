# ADR 0001 — Vertical slices inside a modular monolith

**Status:** accepted · **Date:** 2026-11-21

## Context
The blueprint (§15) chose a modular monolith with Clean Architecture layers per module. The build prompt replaces the layering with vertical slices.

## Decision
- One ASP.NET Core host, one deployable.
- Each module (`src/Modules/<Name>`) owns `Domain/`, `Persistence/`, `Contracts/`, `Features/<UseCase>/`, `Consumers/`, `Jobs/`.
- A slice folder holds endpoint, request, validator, handler, response. Slices never reference sibling slices.
- Modules reference other modules only through `Contracts/` (events and read interfaces). Writes across modules go through Kafka.
- `tests/Plastoids.ArchitectureTests` enforces all of the above with NetArchTest.

## Consequences
- Small duplication between slices is accepted in exchange for independent change.
- Cross-module atomic writes (e.g. Payments + Ledger) use the in-process Contracts call inside the same unit of work (ADR 0002).
