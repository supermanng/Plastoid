# Load test — Sprint 9 read endpoints

`perf/load-test.js` is a [k6](https://k6.io) script that ramps to 5,000 virtual learners against the four read endpoints named in the Sprint 9 prompt and fails if any endpoint's p95 is ≥ 300 ms or more than 1 % of requests fail. Results go to `perf/results/` and into `docs/perf.md`.

| Endpoint | Route | Auth | Env vars |
|---|---|---|---|
| SearchCourses | `GET /api/v1/catalogue/courses?q=&limit=` | anonymous | `SEARCH_TERMS` (optional) |
| ListOpportunities | `GET /api/v1/sponsorship/opportunities?limit=` | anonymous | – |
| GetLessonContent | `GET /api/v1/lms/enrolments/{enrolmentId}/lessons/{lessonId}` | learner bearer, must own the enrolment | `LEARNER_TOKEN`, `ENROLMENT_ID`, `LESSON_ID` |
| GetProgrammeFunnel | `GET /api/v1/sponsorship/organisations/{orgId}/programmes/{programmeId}/funnel` | sponsor bearer (organisation member, MFA) | `SPONSOR_TOKEN`, `ORG_ID`, `PROGRAMME_ID` |

Other variables: `BASE_URL` (default `http://localhost:5080`), `MAX_VUS` (5000), `SPONSOR_VUS` (50), `THINK_SECONDS` (1), `RAMP1`/`RAMP2`/`HOLD`/`RAMP_DOWN` (2m/3m/5m/1m). Endpoints whose variables are missing are skipped with a warning rather than failing the run.

## 1. Prerequisites

- k6 ≥ 0.50 (`brew install k6`). 5,000 VUs need roughly 4–6 GB free RAM on the load generator and `ulimit -n 65536`.
- The Docker stack and the API, started as below. Run the load generator and the API on different machines if you want numbers that reflect the API rather than CPU contention on one laptop; for a first local run, one machine is fine.

## 2. Start the stack with the rate limiter lifted

The API enforces a **per-client-IP** token bucket of 300 requests/min and 10/min on `/auth/*` (`src/Plastoids.Api/Program.cs`). All k6 traffic comes from one IP, so lift both for the run (the integration tests do the same through `RateLimiting:*`):

```bash
make up            # MySQL, Kafka, Redis, S3Mock, Mailpit, Kafka UI
RateLimiting__GlobalTokensPerMinute=100000000 \
RateLimiting__AuthPermitsPerMinute=100000 \
Serilog__MinimumLevel__Default=Warning \
ASPNETCORE_ENVIRONMENT=Development \
dotnet run --project src/Plastoids.Api/Plastoids.Api.csproj -c Release
```

Development applies migrations on start-up and seeds tenant zero, three learners and the staff user (`Seed` section in `src/Plastoids.Api/appsettings.Development.json`, `src/Modules/Identity/Seed/DevelopmentSeeder.cs`). Keep the log at `Warning` so the `[fake-sms]` lines do not dominate the console — raise it to `Information` only while you need OTP codes (step 3).

Sanity check: `curl -s localhost:5080/health/ready | jq .` must report `mysql`, `kafka` and `redis` healthy.

## 3. Seed data

The seeded database has users but no courses, programmes or enrolments. The integration tests contain the exact request sequences; use them as the recipe (every call is plain HTTP against the running API; replace the C# with `curl`/`httpie`, or script them with a small k6 `setup()` or a one-off .NET console app that reuses the helper bodies):

| Need | Helper to copy | File |
|---|---|---|
| Learner sign-in (phone OTP) | `SignInWithPhoneAsync`, `LastOtpCodeFor` | `tests/Plastoids.IntegrationTests/ApiClientExtensions.cs` |
| Staff sign-in (password + TOTP) | `SignInAsStaffAsync` | same file |
| Verified tutor | `TutorOnboardingTests.ApplyAsTutorAsync`, `CatalogueTests.VerifiedTutorAsync` | `tests/Plastoids.IntegrationTests/TutorOnboardingTests.cs`, `CatalogueTests.cs` |
| Published, sponsorable course (₦45,000) | `CatalogueTests.PublishedCourseAsync` | `tests/Plastoids.IntegrationTests/CatalogueTests.cs` |
| Course with a doc lesson + quiz | `LearningAndCertificatesTests.CourseWithQuizAsync` | `tests/Plastoids.IntegrationTests/LearningAndCertificatesTests.cs` |
| Enrolment by paying through the fake gateway | `LearningAndCertificatesTests.EnrolByPaymentAsync` | same file |
| Verified sponsor organisation | `SponsorshipAndApplicationsTests.VerifiedSponsorAsync` | `tests/Plastoids.IntegrationTests/SponsorshipAndApplicationsTests.cs` |
| Funded, published programme (appears in ListOpportunities, has a funnel) | first half of `Sponsored_programme_runs_from_campaign_to_closeout` (create → update → quote → approve → staff review → pay invoice by `virtual_account.credit` webhook → publish) | same file |

Aim for a realistic catalogue before measuring SearchCourses: at least 200 published courses across several categories/skills (the FULLTEXT index and the zero-result suggestion path behave differently on a tiny table), 20+ open opportunities, and one enrolment per load-test learner token you intend to use (the script reuses one `LEARNER_TOKEN`/`ENROLMENT_ID`, which is fine for latency, but 5,000 VUs hitting one enrolment row keeps it in the buffer pool; vary ids if you want a cold-cache number).

### 3a. Learner token (curl)

```bash
PHONE=+2348000000011   # a seeded learner; any new number also works (auto-registers)
curl -s -X POST localhost:5080/api/v1/identity/auth/otp/request -H 'Content-Type: application/json' -d "{\"phone\":\"$PHONE\"}"
# Read the 6-digit code from the API log line:  [fake-sms] to +2348000000011: ...
CODE=123456
LEARNER_TOKEN=$(curl -s -X POST localhost:5080/api/v1/identity/auth/otp/verify -H 'Content-Type: application/json' \
  -d "{\"phone\":\"$PHONE\",\"code\":\"$CODE\",\"dateOfBirth\":\"2003-04-12\",\"displayName\":\"Chiamaka Okafor\"}" | jq -r .accessToken)
```

Tokens expire after 15 minutes (`Auth:AccessTokenMinutes`); mint them right before `k6 run`. A `refreshToken` is returned alongside if you want to automate renewal.

### 3b. Enrolment via the fake gateway (curl)

After publishing a course (recipe above), buy it and confirm with a signed webhook. The fake gateway (`FakePaymentGateway` in `src/Plastoids.Infrastructure/Integrations/IPaymentGateway.cs`) signs with HMAC-SHA512 over the raw body using `Secrets:PaymentWebhookSecret` (default `fake-payment-webhook-secret`); the body is the `GatewayWebhook` record serialised camelCase, with `Money` as `{ "amount": kobo, "currency": "NGN" }`:

```bash
SLUG=<course slug>
QUOTE_ID=$(curl -s localhost:5080/api/v1/catalogue/courses/$SLUG | jq -r '.offerings[0].quoteId')
INTENT=$(curl -s -X POST localhost:5080/api/v1/payments/intents -H "Authorization: Bearer $LEARNER_TOKEN" \
  -H 'Content-Type: application/json' -H "Idempotency-Key: $(uuidgen)" -d "{\"purpose\":\"course\",\"quoteId\":\"$QUOTE_ID\"}")
REF=$(echo "$INTENT" | jq -r .gatewayReference)

# Fee as the fake computes it for a card charge: 1.5 % + N100 (waived under N2,500), capped at N2,000. For N45,000 → N775.
BODY=$(printf '{"eventId":"evt_%s_charge.success","eventType":"charge.success","reference":"%s","amount":{"amount":4500000,"currency":"NGN"},"fee":{"amount":77500,"currency":"NGN"},"occurredAt":"%s","virtualAccountNumber":null,"payerEmail":null}' \
  "$REF" "$REF" "$(date -u +%Y-%m-%dT%H:%M:%SZ)")
SIG=$(printf '%s' "$BODY" | openssl dgst -sha512 -hmac 'fake-payment-webhook-secret' | awk '{print $NF}')
curl -s -X POST localhost:5080/api/v1/payments/webhooks/fake -H 'Content-Type: application/json' -H "x-paystack-signature: $SIG" --data-binary "$BODY"
# {"outcome":"confirmed",...}

# The Lms consumer creates the enrolment asynchronously (outbox → Kafka → consumer); poll:
curl -s -H "Authorization: Bearer $LEARNER_TOKEN" localhost:5080/api/v1/lms/enrolments/me | jq '.[] | {enrolmentId, courseSlug}'
ENROLMENT_ID=<from above>
LESSON_ID=<lessonId returned when the lesson was created, or from GET /api/v1/catalogue/courses/$SLUG outline>
```

If the webhook returns `401 webhook_signature_invalid`, the body was altered after signing (whitespace, quoting); sign the exact bytes you send.

### 3c. Sponsor token, organisation and programme

Follow `VerifiedSponsorAsync` (sign in a new phone, `POST /api/v1/identity/organisations` with `type: "sponsor"`, staff verifies at `POST /api/v1/admin/identity/organisations/{id}/verification`) and then the programme steps in `Sponsored_programme_runs_from_campaign_to_closeout`. The sponsor policy requires MFA, so after the organisation is verified: `POST /api/v1/identity/me/mfa/enable` → `POST /api/v1/identity/auth/mfa/verify` (compute the TOTP from the returned secret with `oathtool --totp -b <secret>`), and use that access token as `SPONSOR_TOKEN`. `ORG_ID` is the `organisationId`, `PROGRAMME_ID` the `programmeId` of a published programme. The funnel endpoint needs the programme to exist; it does not need applicants, but numbers are more realistic with the five applications the test creates.

## 4. Run

```bash
ulimit -n 65536
mkdir -p perf/results
BASE_URL=http://localhost:5080 MAX_VUS=5000 \
LEARNER_TOKEN=$LEARNER_TOKEN ENROLMENT_ID=$ENROLMENT_ID LESSON_ID=$LESSON_ID \
SPONSOR_TOKEN=$SPONSOR_TOKEN ORG_ID=$ORG_ID PROGRAMME_ID=$PROGRAMME_ID \
k6 run perf/load-test.js
```

Smoke first (30 s, 50 VUs) to catch bad ids or expired tokens before the 11-minute run:

```bash
MAX_VUS=50 RAMP1=10s RAMP2=10s HOLD=10s RAMP_DOWN=5s k6 run perf/load-test.js
```

The run ends with k6's summary plus a Markdown table; both are also written to `perf/results/<timestamp>.json` and `perf/results/<timestamp>.md`. Exit code is non-zero when a threshold fails.

## 5. Record

Paste the Markdown rows into `docs/perf.md`, fill in the environment block (commit, host, k6 version, dataset sizes) and note anything observed in `docker stats` / the API log during the run. Commit `docs/perf.md`; `perf/results/` can be committed for the first baseline and ignored afterwards.

## Notes and caveats

- Everything runs as a single API process with Quartz in-memory and the Kafka consumers inside it; the outbox publisher fires every 500 ms per module even when idle, which is a small constant background load.
- MySQL in `docker-compose.yml` has `max-connections=300` and a 512 MB buffer pool; the API opens one connection per request scope. Connection waits show as latency, not errors.
- `GetLessonContent` signs an HLS URL per call for video lessons (`FakeVideoProvider.SignedPlaybackUrl`); use a document lesson id if you want the pure database path, a video lesson for the realistic one.
- `GetProgrammeFunnel` is sponsor-facing and event-driven (refreshed by consumers), so a handful of sponsor VUs is the realistic shape; the script runs `SPONSOR_VUS` (50) alongside the learner ramp.
- Do not run this against a shared or production database: the seed steps post real ledger entries.
