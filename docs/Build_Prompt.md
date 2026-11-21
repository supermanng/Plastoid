# Plastoids — Build Prompt (C# · Vertical Slice Architecture · MySQL · Kafka)

This file holds the prompts to hand to a coding agent (Claude Code, Cursor, Copilot Workspace, etc.) to build the Plastoids backend described in [Plastoids_Blueprint.md](Plastoids_Blueprint.md).

It has three parts:

1. **Kickoff prompt** — the short message you paste first.
2. **Master specification** — the full constraints the agent must follow. Keep it in the repo so every sprint prompt can point at it.
3. **Sprint prompts** — ten incremental prompts, one per unit of work, in the order they should be built.

---

## Part 1 — Kickoff prompt

Paste this as the first message of the session:

```text
You are the lead backend engineer on Plastoids, a sponsored-learning platform for Nigeria.

Before writing any code:
1. Read docs/Plastoids_Blueprint.md in full. Sections 6, 9, 10, 16, 17, 18, 19 and 26 are the product and technical spec.
2. Read docs/Build_Prompt.md, Part 2 (Master specification). It OVERRIDES the blueprint wherever they differ:
   - Database is MySQL 8.4, not PostgreSQL.
   - Messaging is Apache Kafka, not Azure Service Bus.
   - Code is organised as Vertical Slices inside a modular monolith, not Clean Architecture layers.
3. Summarise back to me, in under 300 words, the MVP scope (the 24 stories in blueprint section 26), the module list, and the three override decisions above. Do not start coding until I confirm.

Then we will work sprint by sprint using the prompts in docs/Build_Prompt.md Part 3. In each sprint: propose the slices you will add, write them with tests, run the full test suite, and report what is done, what is not, and anything you assumed.

Rules of engagement:
- Build only MVP (Phase 1) scope. Anything marked Phase 2+ in the blueprint is out of scope; leave a TODO comment and an extension point, nothing more.
- Every money movement goes through the Ledger slice in the same MySQL transaction as the business change. No exceptions.
- Every cross-module side effect goes through the transactional outbox to Kafka. No module calls another module's handlers directly; only its published Contracts.
- Ask me only when two readings of the spec would produce materially different code. Otherwise choose, state the assumption in your report, and continue.
```

---

## Part 2 — Master specification

### 2.1 Goal

Build the Plastoids backend API (and the supporting infrastructure for local development and CI) that satisfies the MVP definition of done in blueprint section 26:

> A sponsor can fund a 150-seat programme, 150 learners can be selected, taught, assessed and certified, tutors are paid correctly, and the sponsor downloads a report — with no manual spreadsheet in the loop.

The front ends (Next.js web, Flutter Android) are separate repositories and are out of scope here. This repository exposes the REST API they consume, documented in OpenAPI 3.1.

### 2.2 Deviations from the blueprint (these win)

| Blueprint says | Build instead | Consequences you must handle |
|---|---|---|
| PostgreSQL 16, schema per module, row-level security, pgvector | **MySQL 8.4 LTS**, InnoDB, one database, table-name prefix per module | No RLS: tenant and organisation scoping is enforced in code (EF Core global query filters + a SaveChanges interceptor that rejects cross-tenant writes). No pgvector: AI retrieval is Phase 2, leave an `IVectorStore` interface with no implementation. |
| Azure Service Bus + transactional outbox | **Apache Kafka** (KRaft) + transactional outbox table polled by a publisher | Idempotent producer, inbox table per consumer module, dead-letter topics, manual offset commit after the inbox write. |
| Clean Architecture per module (Domain / Application / Infrastructure / API) | **Vertical Slice Architecture**: one folder per use case holding endpoint, request, validator, handler and response | Shared code is limited to the module's Domain entities, its DbContext, and the SharedKernel. Enforced by architecture tests. |
| Azure-specific services (Blob, Key Vault, Front Door, Container Apps) | Cloud-neutral abstractions (`IBlobStorage`, `ISecretProvider`) with local implementations (MinIO, env/user-secrets) | Azure remains the production target; keep the abstractions thin so an Azure adapter is a one-file addition. |
| PostgreSQL full-text search | MySQL `FULLTEXT` indexes (natural-language mode) | Typo tolerance is deferred to OpenSearch at scale; document the seam. |
| UUID v7 primary keys (native) | UUID v7 generated in .NET (`Guid.CreateVersion7()`), stored as `BINARY(16)` | A single EF value converter, applied by convention, handles the mapping. Never store UUIDs as `CHAR(36)`. |

Everything else in the blueprint stands: fee rules, hold periods, maker–checker, four-eyes, idempotency keys, append-only ledger, NDPA data rules, API conventions, role model.

### 2.3 Technology stack

| Concern | Choice | Notes |
|---|---|---|
| Runtime | .NET 10 LTS, C# 14, nullable enabled, warnings as errors | `Directory.Build.props` sets analyzers and `TreatWarningsAsErrors`. |
| HTTP | ASP.NET Core Minimal APIs, grouped per module, versioned under `/api/v1` | No MVC controllers. |
| Mediation | MediatR (request/handler) with pipeline behaviours: logging, validation, idempotency, unit-of-work, audit | Or a hand-rolled dispatcher if you prefer; keep the behaviour pipeline. |
| Validation | FluentValidation, one validator per request, run in the pipeline | Validation failures return RFC 9457 problem details. |
| ORM | EF Core 10 with Pomelo.EntityFrameworkCore.MySql | One `DbContext` per module; migrations per module in `Modules/<Module>/Persistence/Migrations`. |
| Messaging | Confluent.Kafka (.NET client), Apache Kafka 3.9+ in KRaft mode | JSON payloads with a versioned envelope. No Schema Registry at MVP. |
| Cache / locks | Redis (StackExchange.Redis) | Booking slot locks, OTP rate limits, short-lived tokens. |
| Jobs | Quartz.NET with MySQL job store (clustered) | Outbox publisher, payout runs, reconciliation, hold-period release, waitlist backfill. |
| Identity | OpenIddict (OIDC) with EF Core MySQL stores; phone OTP, email + password, Google sign-in; TOTP MFA | Tokens are JWT access + opaque refresh. |
| Object storage | S3-compatible via `IBlobStorage`; MinIO locally | Course videos are uploaded to storage and handed to the video provider adapter. |
| Logging / tracing | Serilog (structured, JSON) + OpenTelemetry (traces, metrics) exported via OTLP | Correlation id and causation id flow through HTTP, MediatR and Kafka headers. |
| Tests | xUnit, FluentAssertions, NSubstitute, Testcontainers (MySQL, Kafka, Redis), `WebApplicationFactory`, NetArchTest | Integration tests run against real MySQL and Kafka in containers, never SQLite or in-memory providers. |
| Local dev | Docker Compose: MySQL 8.4, Kafka (KRaft), Redis, MinIO, Mailpit, Kafka UI | `make up`, `make migrate`, `make seed`, `make test`. |
| CI | GitHub Actions: build, analyzers, unit tests, integration tests (service containers), OpenAPI export, Docker image | Fail the build on any warning or architecture-test violation. |

### 2.4 Repository layout

```
Plastoids.sln
Directory.Build.props
docker-compose.yml
Makefile
docs/
  Plastoids_Blueprint.md
  Build_Prompt.md
  adr/                              # one ADR per non-obvious decision
src/
  Plastoids.Api/                    # host only: Program.cs, module registration, middleware, OpenAPI
  Plastoids.SharedKernel/           # Result<T>, Error, Money, EntityId, IClock, domain event base, outbox/inbox contracts, tenancy, auth policies
  Plastoids.Infrastructure/         # Kafka producer/consumer host, outbox publisher, EF conventions (UUID BINARY(16), kobo), Redis, blob, Quartz, OpenTelemetry
  Modules/
    Identity/
      IdentityModule.cs             # DI registration, endpoint group, consumer registration
      Contracts/                    # public: integration events + query interfaces other modules may use
      Domain/                       # User, Organisation, Membership, invariants
      Persistence/                  # IdentityDbContext, EntityTypeConfigurations, Migrations
      Features/
        RequestOtp/
          RequestOtpEndpoint.cs
          RequestOtpCommand.cs
          RequestOtpHandler.cs
          RequestOtpValidator.cs
        VerifyOtp/
        IssueToken/
        EnableMfa/
        ExportMyData/
        DeleteMyAccount/
      Consumers/                    # Kafka consumers this module owns
      Jobs/                         # Quartz jobs this module owns
    Learners/
    Tutors/
    Catalogue/
    Lms/
    Booking/
    Sponsorship/
    Applications/
    Payments/
    Ledger/
    Payouts/
    Certificates/
    Notifications/
    BackOffice/
    Audit/
tests/
  Plastoids.ArchitectureTests/      # slice isolation, naming, no cross-module internals
  Plastoids.SharedKernel.Tests/
  Plastoids.Modules.<Module>.Tests/ # one per module: handler unit tests
  Plastoids.IntegrationTests/       # Testcontainers end-to-end, including the MVP definition-of-done scenario
```

### 2.5 Vertical slice rules

1. **One folder per use case.** A slice contains everything to serve one request: endpoint mapping, request record, validator, handler, response record, and any slice-private helpers. Name the folder after the use case in verb-noun form (`CreateCampaign`, `AcceptOffer`, `SubmitAttempt`).
2. **Slices do not reference each other.** They share only `Domain/`, `Persistence/`, `Contracts/` of their own module and `Plastoids.SharedKernel`. Duplication of a few lines between slices is preferred to a shared "service" class.
3. **Modules talk through `Contracts/` only.** A module may call another module's `Contracts/` interface in-process for synchronous reads (for example, Payments asks Ledger to post an entry; Lms asks Identity whether a user is verified). Writes that cross modules go through integration events on Kafka.
4. **Handlers own the transaction.** A `UnitOfWorkBehaviour` opens one MySQL transaction per command, the handler writes business rows, ledger postings (if any) and outbox rows in that transaction, then commits. Queries do not open transactions.
5. **Endpoints are thin.** Bind, authorise, send to MediatR, map `Result` to HTTP. No logic in endpoints.
6. **Architecture tests enforce 1–3.** Use NetArchTest: `Modules.X.Features.*` may not depend on `Modules.Y.*` except `Modules.Y.Contracts.*`; `Modules.X.Features.A` may not depend on `Modules.X.Features.B`.

### 2.6 MySQL rules

- **Engine and charset:** InnoDB, `utf8mb4`, collation `utf8mb4_0900_ai_ci`. Timestamps as `DATETIME(6)` in UTC.
- **Naming:** `snake_case`, table names prefixed with the module: `identity_users`, `ledger_postings`, `payments_payment_intents`.
- **Every table carries:** `id BINARY(16)` (UUID v7), `tenant_id BINARY(16)`, `created_at`, `updated_at`, `row_version BIGINT UNSIGNED` (EF concurrency token incremented by a `SaveChanges` interceptor).
- **Money:** `amount_kobo BIGINT` plus `currency CHAR(3)`. Never `DECIMAL`, never `DOUBLE`. The SharedKernel `Money` value object is the only type that touches these columns.
- **Tenant scoping:** every `DbContext` applies a global query filter on `tenant_id` from `ICurrentTenant`, and organisation-scoped tables (sponsor data) additionally filter on `organisation_id` from the caller's memberships. A `SaveChanges` interceptor throws if any added or modified entity's `tenant_id` differs from the current tenant. Back-office staff use an explicit `IgnoreQueryFilters()` path that is audited.
- **Personal data:** `nin`, `dob`, `phone` and similar are encrypted at the application layer (AES-GCM, key from `ISecretProvider`, key id stored beside the ciphertext). Lookups use a blind index column (`phone_hash` = HMAC-SHA256, unique).
- **Ledger tables are append-only.** `ledger_journal_entries` and `ledger_postings` get `BEFORE UPDATE` and `BEFORE DELETE` triggers that `SIGNAL SQLSTATE '45000'`. A `CHECK` constraint enforces `(debit_kobo = 0) <> (credit_kobo = 0)`. A nightly Quartz job asserts every entry sums to zero and pages on failure.
- **Idempotency:** table `shared_idempotency_keys (key, actor_id, request_hash, status_code, response_body JSON, created_at, expires_at)`. Required on every `POST` that moves money or records a decision; replay returns the stored response; a different `request_hash` under the same key returns 422.
- **Outbox / inbox:** `<module>_outbox (id, topic, partition_key, event_type, event_version, payload JSON, headers JSON, occurred_at, published_at NULL, attempts)` and `<module>_inbox (message_id PK, consumer, received_at, processed_at)`.
- **Search:** `FULLTEXT` index on `catalogue_courses (title, summary, tags_text)`; query in natural-language mode; filters are ordinary indexed columns.
- **JSON columns** for `outline_json`, `shares_json`, `before_json` / `after_json`, `headers`. Validate shape in code, not in the database.
- **Migrations:** EF Core migrations per module, applied in a fixed order by a `migrate` CLI command in `Plastoids.Api`. Never auto-migrate on startup in production.
- **Soft delete** for user-facing records (`deleted_at`), hard delete of personal-data columns on erasure requests while keeping anonymised learning and ledger rows.

### 2.7 Kafka rules

- **Cluster:** Kafka 3.9+ in KRaft mode. Local: one broker in Docker Compose. Topics are created by a startup job (`TopicProvisioner`) from a single declarative list; never auto-created.
- **Topic naming:** `plastoids.<module>.<event-name>.v<major>` in kebab-case, e.g. `plastoids.payments.payment-succeeded.v1`. Dead letters go to `<topic>.dlq`.
- **Partition key:** the aggregate id (user, enrolment, programme, payment intent) so events for one aggregate are ordered.
- **Envelope (JSON):**

  ```json
  {
    "id": "0192f3a4-…",            // UUID v7, also the Kafka message key header for inbox dedup
    "type": "payments.payment-succeeded",
    "version": 1,
    "occurredAt": "2026-11-21T10:15:30.123456Z",
    "tenantId": "…",
    "aggregateId": "…",
    "correlationId": "…",
    "causationId": "…",
    "actorId": "…",
    "data": { }
  }
  ```

  Headers duplicate `type`, `version`, `correlationId`, `traceparent`.
- **Producer:** `enable.idempotence=true`, `acks=all`, `max.in.flight=5`, retries unlimited with backoff. Only the outbox publisher produces; handlers never call Kafka directly.
- **Outbox publisher:** Quartz job every 500 ms per module: `SELECT … FROM <module>_outbox WHERE published_at IS NULL ORDER BY occurred_at LIMIT 200 FOR UPDATE SKIP LOCKED`, produce, await delivery reports, set `published_at`. Rows older than 7 days and published are purged nightly.
- **Consumers:** one consumer group per module per topic (`plastoids-<module>`). `enable.auto.commit=false`. Processing order: begin MySQL transaction → insert inbox row (duplicate key → skip and commit offset) → handle → commit transaction → commit offset. Retries with exponential backoff up to 5 attempts, then produce to `.dlq` with the error in headers.
- **Consumer handlers are plain MediatR commands** living in the owning module's `Consumers/` folder, so the same validation and audit pipeline applies.
- **Integration events are versioned records in `Contracts/`.** Adding a field is backward compatible; removing or renaming one requires a new `v<major>` topic and a dual-publish window.

### 2.8 Integration events for MVP

| Topic (`plastoids.` prefix omitted) | Producer | Consumers | Payload (data) |
|---|---|---|---|
| `identity.user-registered.v1` | Identity | Learners, Notifications, Audit | userId, phoneHash, role |
| `identity.organisation-verified.v1` | Identity | Sponsorship, Notifications | organisationId, type |
| `tutors.tutor-verified.v1` | Tutors | Catalogue, Notifications | tutorId, level |
| `catalogue.course-published.v1` | Catalogue | Lms, Notifications | courseId, versionNo, offeringIds |
| `payments.payment-succeeded.v1` | Payments | Lms, Booking, Sponsorship, Notifications | paymentIntentId, payerId, purpose, referenceId, amount |
| `payments.payment-failed.v1` | Payments | Notifications | paymentIntentId, reason |
| `payments.invoice-paid.v1` | Payments | Sponsorship, Notifications | invoiceId, programmeId, amount |
| `payments.refund-issued.v1` | Payments | Lms, Booking, Payouts, Notifications | refundId, originalPaymentIntentId, amount |
| `booking.booking-confirmed.v1` | Booking | Lms, Notifications | bookingId, tutorId, learnerId, slotStart, slotEnd |
| `booking.booking-cancelled.v1` | Booking | Payments, Notifications | bookingId, cancelledBy, refundPercent |
| `lms.enrolment-created.v1` | Lms | Sponsorship, Notifications | enrolmentId, learnerId, offeringId, fundingSourceId |
| `lms.enrolment-completed.v1` | Lms | Certificates, Sponsorship, Payouts, Notifications | enrolmentId, learnerId, finalScore |
| `lms.attendance-recorded.v1` | Lms | Sponsorship, Payouts | liveSessionId, enrolmentId, joinedAt, leftAt |
| `lms.attempt-graded.v1` | Lms | Sponsorship | attemptId, enrolmentId, score, passed |
| `sponsorship.programme-funded.v1` | Sponsorship | Applications, Notifications | programmeId, seatsFunded |
| `sponsorship.seat-filled.v1` | Sponsorship | Ledger (via Contracts, in-process), Notifications | programmeId, seatId, enrolmentId |
| `sponsorship.seat-released.v1` | Sponsorship | Applications (waitlist backfill) | programmeId, seatId, reason |
| `applications.decision-recorded.v1` | Applications | Sponsorship, Notifications | applicationId, programmeId, decision, decidedBy |
| `applications.offer-accepted.v1` | Applications | Lms, Sponsorship | applicationId, programmeId, learnerId |
| `payouts.batch-approved.v1` | Payouts | Notifications | batchId, count, totalAmount |
| `payouts.payout-settled.v1` | Payouts | Notifications | payoutId, tutorId, amount, bankRef |
| `certificates.certificate-issued.v1` | Certificates | Notifications, Sponsorship | certificateId, enrolmentId, publicCode |
| `notifications.send-requested.v1` | any module | Notifications | templateKey, recipientId, channelPreference, data |
| `audit.event.v1` | every module (pipeline behaviour) | Audit | actorId, action, entity, entityId, before, after, ip |

### 2.9 Ledger invariants (copy from blueprint section 18, enforce in code and tests)

- Chart of accounts seeded from a migration: 1010, 1020, 1030, 2010, 2020, 2030, 2040, 4010, 4020, 4030, 4040, 4050, 5010.
- `ILedgerService.Post(JournalEntryDraft)` is the only write path; it is called by Payments, Booking, Lms, Sponsorship and Payouts handlers inside their own transaction (same `DbConnection` via the unit of work).
- A draft with postings that do not sum to zero is rejected before it reaches the database.
- Commission rate is resolved by `IFeeRules.Resolve(saleContext)` at sale time and stored on the transaction row.
- The daily reconciliation job computes `client bank balance + gateway in transit ≥ tutor payable + programme funds held + refund credits + hub payable` from ledger balances versus a gateway settlement report adapter and a bank statement adapter (fakes at MVP), and sets a `payouts_frozen` flag on any shortfall.
- Illustrative entries 1–10 in blueprint section 18 must each exist as an integration test.

### 2.10 Security rules

- OIDC bearer tokens; policies are `role + resource ownership + tenant` evaluated in an `IAuthorizationHandler` per resource type, not in handlers.
- MFA (TOTP) mandatory for tutor, sponsor, institution admin and staff roles before any privileged endpoint.
- Staff endpoints under `/api/v1/admin/*` require the `staff` scope, MFA, and an IP allow-list middleware.
- Maker–checker: `PayoutBatch`, refunds above ₦100,000, fee-rule changes and role grants require `approved_by != created_by`, enforced in the domain and covered by tests.
- No card data is ever received; checkout is gateway-hosted. Webhook endpoints verify HMAC signatures and are idempotent on the gateway reference.
- Rate limits: ASP.NET rate limiter, stricter buckets on `/auth/*`.
- Every command passes through an `AuditBehaviour` that writes an `audit.event.v1` outbox row with before/after snapshots for entities marked `[Audited]`.

### 2.11 API conventions

Follow blueprint section 17 exactly: `/api/v1`, camelCase JSON, ISO 8601 UTC, money as `{ "amount": 4500000, "currency": "NGN" }`, cursor pagination, ETag / `If-Match`, RFC 9457 problem details with stable `code`, `Idempotency-Key` on money and decision writes. Export OpenAPI 3.1 at build time to `artifacts/openapi.json` and diff it in CI.

### 2.12 Testing requirements

- **Unit:** every handler has tests for the happy path, each validation rule, and each domain invariant.
- **Integration (Testcontainers):** every slice has at least one end-to-end HTTP test against real MySQL, Kafka and Redis; every consumer has a test that publishes the event and asserts the side effect and inbox dedup on redelivery.
- **Architecture:** slice and module isolation, naming conventions, no `DateTime.Now`, no direct Kafka producer use outside Infrastructure.
- **Ledger:** entries 1–10 from the blueprint, the append-only triggers, and the balance invariant.
- **Definition-of-done scenario:** one integration test that runs the whole MVP flow — sponsor verified → campaign funded → applications → selection → enrolment → live attendance → assessment → certificate → tutor payout → sponsor funnel report — and asserts the ledger reconciles to zero breaks.
- Coverage gate: 80% line coverage on `Modules/*`, no gate on `Infrastructure`.

### 2.13 What NOT to build (Phase 2+)

AI tutor and AI gateway, recommendations, sponsor analytics dashboards beyond the funnel endpoint, proctoring, Plastoids Plus and tutor Pro subscriptions, corporate training workspace, institution tenants and LMS-as-a-Service, facilities, white-label, SCORM/xAPI/LTI, multi-currency, multilingual, iOS. Leave interfaces and TODOs only where the MVP code would otherwise need a rewrite later (`IVectorStore`, `IProctoringProvider`, `ITenantResolver` already multi-tenant-aware).

### 2.14 Working agreement for the agent

- Start each sprint by listing the slices, tables, topics and jobs you will add. Then build.
- Write an ADR in `docs/adr/` for any decision not already made by this document.
- Keep `docker-compose.yml` and `make up` working at the end of every sprint.
- Report at the end of each sprint: done, not done, assumptions, test results (paste the summary line), open questions.
- Never mark a story done if its acceptance criteria in blueprint section 26 are not covered by a test.

---

## Part 3 — Sprint prompts

Paste one at a time, after the kickoff. Each assumes the previous ones are merged.

### Sprint 0 — Scaffold and platform

```text
Sprint 0: scaffold the solution per docs/Build_Prompt.md §2.3–2.7.

Deliver:
- Plastoids.sln, Directory.Build.props (net10.0, nullable, warnings as errors, analyzers), src/ and tests/ layout from §2.4 with empty module folders and an IdentityModule placeholder.
- docker-compose.yml: MySQL 8.4, Kafka 3.9 KRaft single broker, Redis, MinIO, Mailpit, Kafka UI. Makefile targets: up, down, migrate, seed, test, openapi.
- SharedKernel: Result<T>/Error, Money (kobo + currency, arithmetic, no floats), EntityId base with Guid.CreateVersion7(), IClock, ICurrentTenant, ICurrentUser, domain event base, IOutbox, integration event envelope record from §2.7.
- Infrastructure: EF Core conventions (UUID→BINARY(16) converter, kobo Money owned type, snake_case naming, DATETIME(6), row_version interceptor, tenant filter + tenant write guard interceptor), Kafka producer wrapper, generic outbox publisher Quartz job, generic inbox-backed consumer host with retry and DLQ, TopicProvisioner, Redis registration, MinIO IBlobStorage, Serilog + OpenTelemetry, ASP.NET rate limiter, problem-details mapping for Result errors.
- MediatR pipeline behaviours: Logging, Validation (FluentValidation), Idempotency (shared_idempotency_keys), UnitOfWork (one MySQL transaction per command), Audit (outbox row for [Audited] entities).
- Plastoids.Api: Program.cs that discovers modules via an IModule interface (RegisterServices, MapEndpoints, RegisterConsumers, RegisterJobs), health endpoints, OpenAPI 3.1 export command, `migrate` CLI command.
- Tests: ArchitectureTests enforcing §2.5 rules; SharedKernel tests for Money; an integration test that writes an outbox row, sees it on Kafka, and sees a consumer process it once even when redelivered.
- GitHub Actions workflow from §2.3.

Report with `make test` output.
```

### Sprint 1 — Identity and Audit

```text
Sprint 1: Identity module (stories A1, A2, A3) and Audit module.

Identity slices: RequestOtp, VerifyOtp, RegisterWithEmail, IssueToken (OpenIddict password + refresh + Google), EnableMfa, VerifyMfa, GetMe, UpdateMe, UpdateGoals, RequestDataExport, DeleteMyAccount, VerifyOrganisation (CAC + signatory, staff-approved), AddMember, ChangeMemberRole.
Domain: User (phone encrypted + phone_hash blind index, email, password_hash, mfa_enabled, status, dob encrypted, nin_verified_at), Organisation (type, cac_number, verification_status), Membership (user, organisation|tenant, role).
Rules: OTP by SMS through an ISmsSender adapter (fake locally, Termii adapter stub), 5 attempts/hour per phone enforced in Redis, account exists only after verification, one account per phone, under-18 blocked by dob. MFA mandatory for tutor/sponsor/staff roles before privileged endpoints. Data export runs as a Quartz job producing JSON + PDF to blob storage within 24 h; deletion anonymises personal columns and keeps ledger rows.
Events: identity.user-registered.v1, identity.organisation-verified.v1.
Audit module: consumer of audit.event.v1 writing audit_events (append-only trigger), plus GET /admin/audit-events with cursor pagination.
Tests per §2.12. Seed: a staff user, a tenant-zero row, and three test learners.
```

### Sprint 2 — Tutors and Catalogue

```text
Sprint 2: Tutors module (C1, C3) and Catalogue module (C2, B1, B2).

Tutors slices: ApplyAsTutor, SubmitIdentityCheck (IKycProvider adapter: fake locally, Smile ID stub), UploadCredential, ReviewCredential (staff), SubmitDemoLesson, ReviewSubject (reviewer), SetAvailability (weekly rules + exceptions, time zone), SetPricing, ConnectGoogleCalendar (adapter stub), SetPayoutAccount (name-match via gateway account-resolve adapter), GetTutorDashboard, ListTutorsForDiscovery (filters: skill, maxRate, lang, format; FULLTEXT on headline/bio), GetTutorAvailability.
Domain: TutorProfile (level new/rising/top, quality_score, hourly_rate Money), Credential, VerificationCheck, Availability, PriceRule, PayoutAccount. Reviewer queue with SLA timers (Quartz job raises an alert outbox event when a queue item exceeds 3 or 5 working days).
Catalogue slices: CreateCourse, UpdateCourseOutline, AddLesson, UploadLessonVideo (IVideoProvider adapter: direct-upload URL, transcode webhook, signed HLS URL; fake locally), AddQuiz, CreateOffering (self-paced | cohort | one-to-one, price Money, funding_types, capacity, start_at), SubmitForReview, ReviewCourse (staff: automated checks + human), PublishCourse (new CourseVersion), SearchCourses (FULLTEXT + filters incl. funding=sponsored, p95 < 500 ms, zero-result suggestions), GetCourse, CompareCourses (up to 3), PostReview (verified enrolment, ≥30% progress — the progress check calls Lms Contracts; stub until Sprint 5).
Rules: tutors cannot publish until identity + subject review pass; first 3 courses need human review, then sampled; price honoured 30 minutes after being seen (Redis-held quote); unpublished course keeps access for enrolled learners.
Events: tutors.tutor-verified.v1, catalogue.course-published.v1.
```

### Sprint 3 — Payments and Ledger

```text
Sprint 3: Ledger module and Payments module (G1, G2).

Ledger: chart of accounts migration (§2.9), JournalEntry + Posting (append-only triggers, CHECK constraint), ILedgerService in Contracts with Post(draft) and GetBalance(accountCode, ownerId), sub-ledgers per tutor (2010), per programme (2020), per learner refund credit (2030). Nightly BalanceVerificationJob. Daily ReconciliationJob with IGatewaySettlementReport and IBankStatement adapters (fakes) that sets payouts_frozen and emits an alert on shortfall.
Payments: CreatePaymentIntent (purpose: course | booking | co-pay | subscription-later; hosted checkout URL from IPaymentGateway — Paystack adapter real, Flutterwave stub, fake locally), HandleGatewayWebhook (HMAC verify, idempotent on gateway_ref, handles arrival before redirect and duplicates), ConfirmPayment (posts ledger entry 1), RecordSettlement (entry 2, from settlement report), SweepEarnedRevenue (entry 3, job), IssueRefund (entry 5, four-eyes above ₦100,000), CreateInvoice (sponsor: tuition, management fee, seat fee, VAT lines; dedicated virtual account per invoice via gateway adapter), MatchIncomingTransfer (webhook on virtual-account credit; auto-match within 5 min; partial payments flagged; wrong amount held), GetReceipt, ListMyPayments.
IFeeRules: resolves commission (20% platform-sourced, 8% tutor-referred, 15% live tutoring, Pro tiers later) at sale time; stored on the transaction row.
Events: payments.payment-succeeded.v1, payment-failed.v1, invoice-paid.v1, refund-issued.v1.
Tests: blueprint section 18 entries 1–6 as integration tests; webhook replay; idempotency-key replay and conflict.
```

### Sprint 4 — Booking and Live sessions

```text
Sprint 4: Booking module (D1, D2) and the live-session part of Lms (E2).

Booking slices: QuoteBooking, LockSlot (Redis lock 10 minutes), CreateBooking (creates PaymentIntent via Payments Contracts; confirmed on payments.payment-succeeded.v1), CancelBooking (>24 h full refund to refund credit or card, <24 h 50%, tutor no-show full refund + strike; three strikes in 90 days → review flag), RescheduleBooking, CreateSessionPackage, ListMyBookings, GetBooking.
Lms live slices: CreateLiveSession (from booking or cohort schedule), IssueJoinToken (ILiveVideoProvider adapter: fake locally, Daily/100ms stub; short-lived token), HandleProviderAttendanceWebhook (join/leave → AttendanceEvent), ListUpcomingSessions.
Reminders: Quartz job emits notifications.send-requested.v1 at T-24h and T-1h.
Hold rules: session earnings become payable 72 h after the session; implemented as a Hold row released by a job.
Events: booking.booking-confirmed.v1, booking.booking-cancelled.v1, lms.attendance-recorded.v1.
Tests: slot lock contention, calendar-sync double-booking guard, each refund rule reversing the ledger correctly (entry 4/5 variants).
```

### Sprint 5 — LMS core and Certificates

```text
Sprint 5: Lms module (E1, E3, E4) and Certificates module.

Lms slices: Enrol (self-paid via payments.payment-succeeded.v1, sponsored via applications.offer-accepted.v1, both idempotent on the source id; FundingSource recorded), ListMyEnrolments, GetLessonContent (signed HLS URL, documents, page tracking), UpdateProgress (batched for offline sync, per-lesson, computes progress_pct), StartAttempt / SaveAnswers / SubmitAttempt (auto-grade MCQ, multi-select, short answer exact-match; server-side timing), SubmitAssignment (file to blob), GradeAssignment (tutor, rubric), GetGradebook, CompleteEnrolment (rule: all required lessons + pass mark → status completed, emits lms.enrolment-completed.v1), RequestOfflineDownload (encrypted package manifest tied to the account, expiring at enrolment end), CreateCohort, ScheduleCohortSessions, CohortForum (threads, posts, moderation flag).
Certificates slices: IssueCertificate (consumer of lms.enrolment-completed.v1; template per course or sponsor; immutable; public_code + hash), VerifyCertificate (GET /verify/{publicCode}, anonymous, QR payload), RevokeCertificate (staff; record kept, page shows revoked), ListMyCertificates.
Events: lms.enrolment-created.v1, lms.enrolment-completed.v1, lms.attempt-graded.v1, certificates.certificate-issued.v1.
Tests: offline progress batch merge, exam timing enforced server-side, certificate immutability, Catalogue PostReview now wired to real progress.
```

### Sprint 6 — Sponsorship and Applications

```text
Sprint 6: Sponsorship module (F1, F2, F5) and Applications module (F3, F4).

Sponsorship slices: CreateCampaign (stepper state saved as draft: objectives, course/offering, seats, budget, eligibility rules, dates, selection mode), RequestQuote (lines: tuition, management fee 12% min ₦3m, seat fee ₦12,000, VAT 7.5% on fees only), ApproveQuote (sponsor), ReviewCampaign (Plastoids programme review, eligibility rules lawful/non-discriminatory flag), PublishCampaign (only after payments.invoice-paid.v1 or an approved payment plan), ListOpportunities, CheckEligibility (deterministic rule engine, < 1 s, returns reasons + alternatives), FillSeat (consumer of applications.offer-accepted.v1: seat → filled, enrolment created via Lms Contracts, ledger entry 7 seat fee earned), ReleaseSeat, RecordMonthlyFees (job: entries 8 and 9 per programme month from attendance data), CloseProgramme (entry 10 refund of unfilled seats; unused funds per contract), GetProgrammeFunnel (applied → offered → enrolled → active → completed → certified, refreshed at least hourly from events, CSV export), GetProgrammeBudget, DownloadImpactReport (MVP: PDF with funnel, attendance, completion, scores; narrative is Phase 2).
Applications slices: SubmitApplication (≤ 8 fields, save-and-resume, one per NIN/phone per programme, duplicates merged and flagged), StartEntryAssessment / SubmitEntryAssessment (reuses Lms assessment engine via Contracts), ScoreApplication (rubric), ListReviewQueue (blind-review option), BulkDecide (accept / reject / waitlist; every decision append-only with reviewer + reason), ApproveShortlist (sponsor or delegated), SendOffers, AcceptOffer (5-day expiry; co-pay checkout for partial sponsorship via Payments Contracts; overlapping programmes → must choose), BackfillWaitlist (job on decline/expiry/seat-released), FileAppeal, ResolveAppeal.
Events: sponsorship.programme-funded.v1, seat-filled.v1, seat-released.v1, applications.decision-recorded.v1, offer-accepted.v1.
Tests: section 10 worked example as a scenario test (500 seats, 480 filled, 20 refunded); partial-sponsorship co-pay path; protected characteristics never reach the scoring input (architecture + unit test).
```

### Sprint 7 — Payouts

```text
Sprint 7: Payouts module (G3).

Slices: ComputePayableEarnings (job: releases Holds per §18 settlement rules — 72 h sessions, 7 d course sales, per-delivered-session for sponsored teaching, disputes held), CreatePayoutBatch (maker; weekly Thursday job; minimum ₦5,000; name-match on payout account required; skips tutors with open disputes; blocked when payouts_frozen), ApprovePayoutBatch (checker ≠ maker, enforced in domain), SubmitPayoutBatch (IPayoutProvider adapter: gateway transfers, bank bulk fallback stub; fake locally), HandlePayoutStatusWebhook (settled → ledger entry 4; failed/reversed → funds back to tutor balance + notice), GetMyEarnings (gross, commission, net, pending, paid), DownloadStatement, PlaceHold / ReleaseHold (disputes, max 5 working days), ListPayoutQueue (staff).
Events: payouts.batch-approved.v1, payouts.payout-settled.v1.
Tests: maker–checker violation rejected; zero reconciliation breaks after a full weekly run in the integration suite; reversed payout restores balance.
```

### Sprint 8 — Notifications and Back office

```text
Sprint 8: Notifications module (H1) and BackOffice module (I1).

Notifications: consumer of notifications.send-requested.v1 and of the domain events listed in §2.8 that imply a message (user-registered, booking-confirmed, offer sent/accepted, certificate-issued, payout-settled, reminders). Channels: WhatsApp (BSP adapter stub), SMS (Termii stub), email (SMTP → Mailpit locally), push (FCM stub). Per-user channel preferences, quiet hours, approved templates table with versioning, in-app inbox (ListMyNotifications, MarkRead), cohort announcements, learner–tutor threads (on-platform, retained, scanned for off-platform payment solicitation → flag).
BackOffice: executive dashboard query (GMV, net revenue, active learners/tutors/sponsors, programme health, payout queue, risk alerts — all read from ledger and event-derived tables, never recomputed on screen), user/tutor/sponsor management, verification queues, course moderation, refunds (four-eyes > ₦100,000), fee-rule configuration (four-eyes, versioned, effective-from), feature flags, role grants (four-eyes), audit log viewer, disputes queue, data-request queue.
All /admin/* endpoints: staff scope + MFA + IP allow-list. Every admin mutation audited with before/after.
Tests: preference and quiet-hour routing; four-eyes on each protected action; template rendering snapshot tests.
```

### Sprint 9 — Hardening and definition of done

```text
Sprint 9: hardening and the MVP definition-of-done.

- Write the end-to-end integration scenario from §2.12 and make it pass.
- Load test (k6 or NBomber) the read endpoints: SearchCourses, ListOpportunities, GetLessonContent, GetProgrammeFunnel at 5,000 concurrent simulated learners; API p95 < 300 ms for reads against the Docker stack; record results in docs/perf.md.
- Verify every story A1–I1 in blueprint section 26 has a test named after its ID; produce docs/mvp-traceability.md mapping story → tests → endpoints.
- Security pass: dependency audit, secrets scan, rate limits on /auth/*, HMAC verification on every webhook, problem-details never leak stack traces, PII never logged (Serilog destructuring policy + test).
- Operational: runbooks in docs/runbooks/ for outbox backlog, consumer DLQ replay, payouts_frozen, reconciliation shortfall, migration rollback.
- Export final OpenAPI to artifacts/openapi.json and diff against the previous sprint.
Report done / not done / assumptions.
```

### Sprint prompt template (for anything else)

```text
Sprint N: <module or feature>.

Scope: <stories from blueprint §26 or sections referenced>.
Slices to add: <list>.
Tables: <list, with module prefix>.
Topics produced / consumed: <list>.
Jobs: <list>.
Rules to enforce: <bullet list copied from the blueprint>.
Tests required: <per §2.12, plus any scenario>.
Out of scope: <explicit>.
Report done / not done / assumptions / test summary.
```
