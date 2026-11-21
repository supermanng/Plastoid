# Plastoids backend

Sponsored-learning platform for Nigeria. Blueprint: [docs/Plastoids_Blueprint.md](docs/Plastoids_Blueprint.md). Build spec and sprint prompts: [docs/Build_Prompt.md](docs/Build_Prompt.md). Decisions: [docs/adr](docs/adr).

**Stack:** .NET 9 · ASP.NET Core Minimal APIs · MediatR · EF Core + MySQL 8.4 (Pomelo) · Apache Kafka (outbox/inbox) · Redis · Quartz · S3-compatible storage · Serilog + OpenTelemetry.

## Quick start

```bash
make up          # MySQL, Kafka, Redis, S3Mock, Mailpit, Kafka UI (http://localhost:8080)
make migrate     # apply EF migrations
make run         # API on http://localhost:5080 (Development: migrates on startup, diagnostics enabled)
make test-unit   # unit + architecture tests
make test-integration   # Testcontainers: real MySQL, Kafka, Redis, S3Mock
```

Integration tests start MySQL in a container by default. To use an existing MySQL server instead (the tests create and drop a
throw-away `plastoids_test_<id>` database, so the account needs CREATE/DROP DATABASE):

```bash
PLASTOIDS_TEST_MYSQL="Server=host;Port=3306;User=user;Password=***" make test-integration
```

Connection details come from `appsettings.json`; override locally with `dotnet user-secrets` or environment variables
(`ConnectionStrings__Plastoids`, `ConnectionStrings__Redis`, `Kafka__BootstrapServers`). Never commit credentials.
For a MySQL 8.0 server also set `MySql:ServerVersion` (e.g. `8.0.45-mysql`) so Pomelo generates matching SQL.

MySQL containers (compose and Testcontainers) run as the `mysql` user because the image's `gosu` step hangs on some
Docker Desktop builds; see `docs/adr/0006`. If a MySQL container ever sticks at "Switching to dedicated user", also check
`docker image inspect mysql:8.4 --format '{{.Architecture}}'` is your host's architecture.

## Signing in (Sprint 1)

Phone OTP is the primary flow; locally the SMS is written to the API log (`[fake-sms] …`):

```bash
curl -s -X POST localhost:5080/api/v1/identity/auth/otp/request -H 'Content-Type: application/json' -d '{"phone":"08031234567"}'
```

```bash
curl -s -X POST localhost:5080/api/v1/identity/auth/otp/verify -H 'Content-Type: application/json' -d '{"phone":"08031234567","code":"123456","dateOfBirth":"1999-01-01","displayName":"Ada"}'
```

Email + password (`POST /auth/register-email` once signed in, then `POST /auth/token` with `grantType: password`),
`refresh_token` and `google` grants are on the same token endpoint. MFA is TOTP: `POST /me/mfa/enable`, then
`POST /auth/mfa/verify`. Tutor, sponsor and staff policies require a session that completed MFA.

Development seeds tenant zero, three learners and a staff user (`Seed` section of `appsettings.Development.json`;
the staff password and TOTP secret there are development-only values). Staff endpoints live under `/api/v1/admin/*`
and also need the caller's IP in `Admin:IpAllowList`. Secrets (`Secrets:JwtSigningKey`, `FieldEncryptionKey`,
`BlindIndexKey`) must be set per environment; the Development ones are placeholders.

## Tutors and catalogue (Sprint 2)

Tutor onboarding follows blueprint §13: `POST /tutors/applications` (grants the `tutor` role; refresh the token and
enable MFA), `POST /tutors/me/identity-check` (fake KYC passes unless the NIN ends in 0), `POST /tutors/me/credentials`
and `POST /tutors/me/demo-lesson` (presigned uploads, reviewer queue with 3/5 working-day SLAs), staff decisions under
`/admin/tutors/*`, then availability, pricing, calendar and payout account under `/tutors/me/*`. Verified tutors appear
in `GET /tutors` and `GET /tutors/{id}/availability`.

Courses: create → outline → lessons/quizzes → video upload (fake provider; sign the webhook with
`Secrets:VideoWebhookSecret`) → offering → submit (automated checks, human review for the first three courses) →
staff review under `/admin/catalogue/*` → publish. Learners use `GET /catalogue/courses` (full-text, filters,
`funding=sponsored`, zero-result suggestions), `GET /catalogue/courses/{slug}` (30-minute price quotes) and
`GET /catalogue/courses/compare?ids=`. See `docs/adr/0008`.

## Payments and ledger (Sprint 3)

`POST /payments/intents` (Idempotency-Key) starts a hosted checkout from a course quote; the gateway webhook at
`POST /payments/webhooks/{gateway}` (HMAC-verified) or `POST /payments/intents/{id}/verify` confirms it and posts
ledger entry 1. Staff run settlement import and the revenue sweep under `/admin/payments/*` (entries 2 and 3), issue
refunds (entry 5, four-eyes above ₦100,000) and read balances, journal entries and the reconciliation status under
`/admin/ledger/*`. Sponsors invoice through `POST /payments/organisations/{id}/invoices`; the virtual-account credit
webhook matches the invoice (entry 6). Locally the fake gateway signs webhooks with `Secrets:PaymentWebhookSecret`
(default `fake-payment-webhook-secret`). See `docs/adr/0009`.

## Bookings and live sessions (Sprint 4)

`GET /bookings/quote` prices a slot, `POST /bookings/locks` holds it for 10 minutes, `POST /bookings` (Idempotency-Key)
creates the booking with a checkout URL (or confirms at once from a `POST /bookings/packages` package). The payment
webhook confirms it; Lms then creates the live session, and `POST /lms/live-sessions/{id}/join-token` opens the room
from 15 minutes before the start. `POST /bookings/{id}/cancel` applies the §9 refund rules (credit by default) and
`/reschedule` moves the session. Provider attendance arrives at `POST /lms/webhooks/live/{provider}` (signed with
`Secrets:LiveVideoWebhookSecret`). Reminders, completion and 72-hour earning holds run from Quartz. See `docs/adr/0010`.

## Learning and certificates (Sprint 5)

Paying for a course (or accepting a sponsored offer) enrols the learner automatically; `GET /lms/enrolments/me` lists
courses, `GET /lms/enrolments/{id}/lessons/{lessonId}` serves content, `POST /lms/enrolments/{id}/progress` syncs an
offline batch, quizzes run through `POST …/lessons/{lessonId}/attempts` → `PUT /lms/attempts/{id}/answers` →
`POST /lms/attempts/{id}/submit` with server-side timing, assignments through `…/assignment` and
`POST /lms/submissions/{id}/grade`. Completion issues a certificate; `GET /verify/{code}` is public and
`POST /admin/certificates/{id}/revoke` keeps the record. Cohorts: `POST /lms/cohorts`, `…/sessions`, `…/threads`.
See `docs/adr/0011`.

## Layout

```
src/Plastoids.Api             host, CLI (`migrate`), health, OpenAPI
src/Plastoids.SharedKernel    Result, Money, Entity, IClock, tenancy, CQRS markers, event contracts
src/Plastoids.Infrastructure  MySQL conventions, unit of work, outbox/inbox, Kafka, Quartz, behaviours
src/Modules/<Module>          Domain · Persistence · Contracts · Features/<UseCase> · Consumers · Jobs
tests/                        SharedKernel.Tests · ArchitectureTests · IntegrationTests
```

## Adding a slice

Create `Features/<UseCase>/` with `<UseCase>Command.cs` (or `Query`), `<UseCase>Validator.cs`, `<UseCase>Handler.cs`, `<UseCase>Endpoint.cs`. Commands run in one MySQL transaction; raise integration events with `IOutbox.Enqueue`. Map the endpoint in the module's `MapEndpoints`.

## Adding a migration

```bash
dotnet ef migrations add <Name> --project src/Modules/<Module> --startup-project src/Plastoids.Api --context <Module>DbContext --output-dir Persistence/Migrations
```
