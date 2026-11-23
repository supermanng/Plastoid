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
make web         # React web app on http://localhost:5173 (proxies /api to :5080); see frontend/README.md
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

Development seed (`Seed:Enabled`) creates two staff accounts with MFA already enrolled: `staff@plastoids.local` and the
super admin `admin@plastoids.local` (passwords and TOTP secrets under `Seed:*` in `appsettings.Development.json`). Two
staff members are needed locally because the four-eyes commands refuse the same maker and checker.

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

## Sponsorship and applications (Sprint 6)

Verified sponsors build a campaign in steps (`POST`/`PUT /sponsorship/organisations/{id}/programmes[/{programmeId}]`),
request the quote (`…/quote`: tuition, management fee 12 % min ₦3m, seat fee ₦12,000, VAT on fees) and approve it
(`…/quote/approve`, issues the invoice). Staff review under `/admin/sponsorship/programmes/{id}/review`; paying the
invoice funds the programme; `…/publish` opens it. Learners browse `GET /sponsorship/opportunities`, check
`POST …/{id}/eligibility`, apply with `POST /applications/programmes/{id}/applications` (≤ 8 fields, `submit=false`
saves a draft), sit the timed entry assessment (`POST`/`PUT /applications/{id}/assessment`) and accept offers
(`POST /applications/{id}/offer/accept`, co-pay checkout for partial sponsorship). Staff screen under
`/admin/applications/*` (blind queue, scoring, bulk decisions, offers, appeals); the sponsor approves the shortlist.
Funnel (`…/funnel`, `?format=csv`), budget and the PDF impact report sit on the programme. See `docs/adr/0012`.

## Payouts (Sprint 7)

Earnings arrive from events (course sales hold 7 days, sessions wait for Booking's 72-hour release, sponsored months are
payable at once); `GET /payouts/me` shows gross, commission, net, held, available, pending and paid and
`GET /payouts/me/statement` exports CSV. Staff see the queue at `GET /admin/payouts/queue`, draft a batch with
`POST /admin/payouts/batches` (or the Thursday job does), a different staff member approves it (`…/approve`,
maker–checker enforced in the domain), `…/submit` sends it to the payout provider (`Integrations:PayoutProvider`:
fake, paystack, bankfile) and `POST /payouts/webhooks/{provider}` settles (entry 4), fails or reverses it. Dispute holds:
`POST /admin/payouts/holds` and `…/release`. A reconciliation shortfall freezes payouts until
`POST /admin/ledger/reconciliation/unfreeze`. See `docs/adr/0013`.

## Notifications (Sprint 8)

Every module raises `notifications.send-requested.v1` with a template key; Notifications also consumes the domain
events that imply a message and dedupes the two. Each message is an in-app inbox item (`GET /notifications/me`,
`POST /notifications/{id}/read`) plus one delivery on the user's first available channel (push → WhatsApp → SMS →
email; `PUT /notifications/preferences` sets order, opt-outs and quiet hours in local time, inside which deliveries
are deferred). Staff manage approved template versions under `/admin/notifications/templates`; built-in copy is the
fallback. Tutors announce to cohorts (`POST /notifications/cohorts/{id}/announcements`); learner–tutor threads
(`/notifications/threads`) stay on platform and are flagged when they share phone numbers or solicit off-platform
payment. Locally email goes to Mailpit (http://localhost:8025) when `Integrations:Email=smtp`. See `docs/adr/0014`.

## Back office (Sprint 8)

Staff work from `/admin/backoffice/*` (staff role + MFA + IP allow-list): `GET …/dashboard` reads GMV, net revenue,
participants, programme health, the payout queue and risk alerts from the ledger and event-derived counters (never
recomputed on screen); `…/users` (search, suspend, reinstate), `…/tutors`, `…/sponsors`; queues for verification, course
moderation, refunds, disputes and NDPA data requests (views that point at the deciding endpoint); `…/feature-flags`;
`…/audit-log` (cursor-paged, filtered); `…/risk-alerts/{id}/resolve`. Four-eyes actions: fee rules
(`/admin/payments/fee-rules`, versioned with effective-from), role grants (`/admin/identity/role-grants`), refunds above
₦100,000. See `docs/adr/0015`.

## Hardening and definition of done (Sprint 9)

`tests/Plastoids.IntegrationTests/DefinitionOfDoneTests.cs` runs the whole MVP flow (sponsor → campaign → applications →
selection → enrolment → live attendance → assessment → certificate → payout → sponsor report) and checks the ledger
reconciles with zero breaks. Security: dependency audit and secrets scan in CI, PII masked in logs
(`PiiDestructuringPolicy`), problem details without stack traces, HMAC on every webhook, `/auth/*` rate limits,
forwarded-headers support (`Hosting:KnownProxies`), and a production guard against fake integrations. Operations:
`docs/runbooks/`. Traceability: `docs/mvp-traceability.md`. Load test: `perf/` (k6) with results to be recorded in
`docs/perf.md`. See `docs/adr/0016`.

## Units wallet (ADR 0018)

Every payment buys **units**: learners and organisations hold a prepaid wallet, the naira value of a unit is set by
staff (`POST /admin/wallet/rates`, four-eyes; `Wallet:DefaultRateKobo` = ₦1,000 until one is approved) and prices are shown
as `ceil(naira / rate)` units from the public `GET /wallet/rate`. `POST /wallet/purchases` starts a checkout for units
(entry 11: cash held in 2050 per wallet); `POST /payments/intents { payWithUnits: true }` settles a sale from the wallet at
once (entry 12: lots released first-in first-out, tutor payable and commission as usual, rate gaps to 4060); refunds of a
units sale go back to the wallet; sponsors can settle a programme invoice from the organisation wallet with
`POST …/programmes/{id}/fund-from-wallet`. Units are never paid out as cash. Tests: `Plastoids.Modules.Wallet.Tests`,
`UnitPostingsTests` and `IntegrationTests/WalletTests.cs`.

## Seat sponsorship (ADR 0021)

Learners do not search for sponsors. A sponsor picks a **cohort** (a dated run of a course), buys a block of seats from
the organisation wallet, and gives each seat to a learner by phone or email.

- `GET /lms/cohorts?courseId=` and `GET /lms/cohorts/{id}` list upcoming cohorts with dates, seats left and the price
  per seat; a cohort tracks `reserved` seats held by sponsors.
- `POST /sponsorship/organisations/{organisationId}/seat-blocks` `{cohortId, seats}` redeems units for
  `seats × price` and posts entry 14 (Dr 2050 wallet / Cr 2020 held for the block). `POST …/{blockId}/allocations`
  `{phone | email, displayName}` enrols the learner at once (entry 15 splits the seat price into commission, tutor share
  and provider or tutor net, then `sponsorship.seat-allocated.v1` enrols them in the cohort, records earnings and
  notifies them) or parks an **invite** that converts when that phone or email signs up. `POST …/{blockId}/release`
  returns unallocated seats to the wallet (entry 16); a daily job does the same when the cohort ends.
- The campaign, quote, invoice and application endpoints of Sprint 6 remain in the API but are no longer in the web
  app; the Sponsor module has Seats pages, the Learner module shows upcoming cohorts and "Sponsored by" on enrolled
  courses, and the Administration module lists seat blocks.

## Organisation-owned courses (ADR 0019)

Any verified organisation (a sponsor, employer, institution, hub or the new `provider` type) can own courses and engage
verified tutors to teach them; learners buy seats as usual and the money splits three ways.

- Organisation roles gain `author` (create and edit courses, engage tutors); owners can do the same. Policy
  `organisation-author` guards `POST /catalogue/organisations/{organisationId}/courses` and the engagement endpoints.
- Authoring endpoints (`/catalogue/courses/{courseId}/…`) are resource-authorised: the owning tutor, an owner/author of the
  owning organisation, or staff. `GET /catalogue/courses/{courseId}/authoring` returns the draft for the builder;
  `GET /catalogue/courses/mine` and `GET /catalogue/organisations/{organisationId}/courses` list courses.
- Engagements: `POST …/courses/{courseId}/engagements` offers a verified tutor `revenue_share` (basis points of the sale net
  of commission) or `per_enrolment` (fixed fee per paid enrolment) terms; the tutor accepts or declines at
  `POST /catalogue/engagements/{id}/respond`; `GET /catalogue/engagements/mine` lists a tutor's offers. Offerings of an
  organisation course name the teaching tutor (`tutorUserId`), and cohorts default to that tutor.
- Money: fee code `course_provider` (`Fees:CourseProviderBps`, default 15%). Entry 1 / entry 12 credit 4010 commission,
  2010 the tutor share and **2060 Provider payable** (per organisation) the rest; entry 5 reverses all three pro rata.
- Payouts: earnings and payouts carry a payee type. `PUT /payouts/organisations/{organisationId}/payout-account` (owner or
  finance; the bank account name must match the registered organisation name) and
  `GET /payouts/organisations/{organisationId}/earnings`; the weekly four-eyes batch pays organisations from 2060.
- Web app: organisation Courses, course Tutors and Payouts pages in the sponsor area; a tutor Offers page; public course
  pages show the provider.

## Web app (React)

`frontend/` is a Vite + React 19 web app against this API, built as three interface modules from one package (ADR 0020):
the Learner module (`/`: public site, learner and tutor areas), the Sponsor module (`/sponsor`: organisation portal) and the
Administration module (`/admin`: back office), each with its own HTML entry, router and shell over a shared `src/shared/`
layer; `scripts/check-module-boundaries.mjs` (in `npm run lint`) keeps modules from importing each other. `make web` runs
all three on :5173 with `/api` proxied to the API on :5080; `make web-build` produces `frontend/dist` (`VITE_MODULES=admin`
builds one module), and `frontend/Dockerfile` serves it with nginx (one history fallback per module) proxying `/api/` to
the API container. Role gates mirror the API policies (tutor, sponsor and staff areas need an MFA-stepped-up token).
Details, sign-in notes for development and the backend gaps the UI works around are in
[frontend/README.md](frontend/README.md), `docs/adr/0017` and `docs/adr/0020`.

## Layout

```
src/Plastoids.Api             host, CLI (`migrate`), health, OpenAPI
src/Plastoids.SharedKernel    Result, Money, Entity, IClock, tenancy, CQRS markers, event contracts
src/Plastoids.Infrastructure  MySQL conventions, unit of work, outbox/inbox, Kafka, Quartz, behaviours
src/Modules/<Module>          Domain · Persistence · Contracts · Features/<UseCase> · Consumers · Jobs
tests/                        SharedKernel.Tests · ArchitectureTests · IntegrationTests
frontend/                     React 19 + Vite web app: learner, sponsor and admin modules over src/shared (ADR 0020); nginx image
```

## Adding a slice

Create `Features/<UseCase>/` with `<UseCase>Command.cs` (or `Query`), `<UseCase>Validator.cs`, `<UseCase>Handler.cs`, `<UseCase>Endpoint.cs`. Commands run in one MySQL transaction; raise integration events with `IOutbox.Enqueue`. Map the endpoint in the module's `MapEndpoints`.

## Adding a migration

```bash
dotnet ef migrations add <Name> --project src/Modules/<Module> --startup-project src/Plastoids.Api --context <Module>DbContext --output-dir Persistence/Migrations
```
