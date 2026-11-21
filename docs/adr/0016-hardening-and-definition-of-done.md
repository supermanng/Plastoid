# ADR 0016: Sprint 9 — hardening and the MVP definition of done

**Status:** Accepted (2026-11-21)

## What was done

1. **Definition-of-done scenario** (`tests/Plastoids.IntegrationTests/DefinitionOfDoneTests.cs`): sponsor verified →
   campaign quoted, invoiced and paid → review → published → two learners apply, sit the entry assessment, are scored,
   accepted, shortlisted and offered seats → enrolments in a cohort → live attendance from the provider webhook →
   quiz and completion → certificates verified → programme months posted (entries 8 and 9) → weekly payout drafted by
   the system, approved by staff, submitted and settled (entry 4) → funnel, CSV and PDF report → every journal entry
   nets to zero and the reconciliation shows assets ≥ liabilities with payouts not frozen. Two seats stand in for the
   blueprint's 150; the counts scale linearly and the worked example is covered by the Sponsorship unit scenario.
2. **Security pass.**
   - Dependency audit: `dotnet list package --vulnerable --include-transitive` was clean after moving OpenTelemetry to
     1.15.x (GHSA-g94r-2vxg-569j in OpenTelemetry.Api < 1.15.3). The audit now runs in CI.
   - Secrets scan: a grep-based scan runs in CI (gitleaks is not installed on the dev machine; the CI step uses the
     gitleaks action). The repository holds only the documented development placeholders.
   - PII never logged: `PiiDestructuringPolicy` masks phone, email, NIN, BVN, DOB, names, account numbers, tokens and
     secrets on every `@`-destructured object or dictionary (test: `PiiLoggingTests`); the fake SMS sender masks the
     phone number in its dev log line.
   - Problem details never leak stack traces: `GET /api/v1/diagnostics/throw` exists in Development and Testing only and
     `HardeningTests` asserts the 500 body carries just `code`, `status` and `correlationId`.
   - Webhooks: all four (payments, live video, lesson video, payouts) verify an HMAC with constant-time comparison and are
     exempt from the per-IP rate limiter so provider retry bursts are never throttled.
   - Rate limits on `/auth/*` (10 per minute per IP) and the global token bucket are unchanged and documented in
     `docs/security-checklist.md`.
   - Forwarded headers: `Hosting:KnownProxies` / `Hosting:KnownNetworks` make the rate limiter and the admin IP allow-list
     see the real client behind a load balancer (nothing is trusted by default).
   - Production guard: the host refuses to start in Production with any fake integration (`Integrations:*`,
     `Sms:Provider`).
3. **Audit coverage**: the audit behaviour now snapshots `[Audited]` changes from every context enlisted in the unit of
   work, so cross-module admin mutations (back office suspending a user through Identity.Contracts) are audited too.
4. **Operations**: runbooks for outbox backlog, consumer DLQ replay, payouts frozen, reconciliation shortfall and
   migration rollback (`docs/runbooks/`); `POST /admin/ledger/reconciliation/unfreeze` is the human path out of a freeze.
5. **Traceability**: `docs/mvp-traceability.md` maps every story A1–I1 to endpoints and tests (11 covered, 13 partial, no
   story without an endpoint or test) and the ten ledger entries to their unit and integration tests.
6. **Performance**: `perf/load-test.js` (k6, 5,000 VUs, p95 < 300 ms thresholds) and `docs/perf.md`. The run itself is
   **not done**: k6 is not installed on this machine and the figure must come from the Docker stack on representative
   hardware. `docs/perf.md` records the exact command.
7. **OpenAPI**: exported to `artifacts/openapi.json` from the Development host; the diff against the Sprint 4 export is
   summarised in `docs/openapi-changes.md`.

## Not done / assumptions

- Staff SSO (story A2) is replaced by email + password + TOTP (ADR 0007); OIDC remains a Phase 2 switch.
- Audio-only live fallback, USSD/transfer learner payments and emailed receipts are client or provider concerns
  recorded as partial in the traceability matrix.
- Coverage gate (80 % on Modules/*) is collected in CI but not yet enforced as a failing threshold.
- The load test and the external penetration test are the two definition-of-done items that need an environment
  rather than code.
