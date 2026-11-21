# Security pass checklist — Sprint 9

Scope (docs/Build_Prompt.md, Sprint 9): dependency audit, secrets scan, rate limits on `/auth/*`, HMAC verification on every webhook, problem details never leak stack traces, PII never logged. Every item names the file that implements the control, the command that proves it, and the finding at the time of writing. Tick the box when the command has been run on the release commit and the result recorded.

Run everything from the repository root. The commands do not need Docker unless stated.

---

## 1. Dependency audit

Package versions are pinned centrally in `Directory.Packages.props`; `Directory.Build.props` turns warnings into errors.

- [ ] Vulnerable packages (direct and transitive):

  ```bash
  dotnet restore Plastoids.sln
  dotnet list Plastoids.sln package --vulnerable --include-transitive
  ```

  Expected: "has no vulnerable packages" for every project. Any `High`/`Critical` row blocks the release; `Moderate`/`Low` needs a ticket with an owner.

- [ ] Deprecated and outdated packages (informational, record in the release notes):

  ```bash
  dotnet list Plastoids.sln package --deprecated
  dotnet list Plastoids.sln package --outdated --include-transitive
  ```

- [ ] NuGet audit at restore time is on (default in the .NET 9 SDK, `global.json` pins 9.0.203). Make it fail the build rather than warn by adding to `Directory.Build.props`: `<NuGetAuditMode>all</NuGetAuditMode>` and `<NuGetAuditLevel>low</NuGetAuditLevel>` (NU1901–NU1904 are then errors because `TreatWarningsAsErrors` is set).

- [ ] Add the `--vulnerable --include-transitive` command as a CI step in `.github/workflows/ci.yml` (there is none today) and fail on a non-empty result.

Findings at time of writing:

- `src/Plastoids.Infrastructure/Observability/Observability.cs` states that `OpenTelemetry.Exporter.OpenTelemetryProtocol` 1.13 has known CVEs and is therefore not referenced; `OpenTelemetry.Extensions.Hosting` / `Instrumentation.*` are at 1.13.x (`Directory.Packages.props`). Confirm the audit output for those packages and record the decision.
- Docker base images (`src/Plastoids.Api/Dockerfile`, `mysql:8.4`, `apache/kafka:3.9.0`, `redis:7-alpine`, `adobe/s3mock:4.9.0`, `provectuslabs/kafka-ui:latest`, `axllent/mailpit:latest`) are outside `dotnet list`; scan the built image with `docker scout cves plastoids-api:<sha>` or `trivy image plastoids-api:<sha>`. Pin the two `:latest` images in `docker-compose.yml`.

## 2. Secrets scan

- [ ] Preferred (gitleaks, https://github.com/gitleaks/gitleaks):

  ```bash
  gitleaks git --redact --verbose --report-format sarif --report-path artifacts/gitleaks.sarif .
  # or, for the working tree only (no history):
  gitleaks dir --redact --verbose .
  ```

- [ ] Grep fallback when gitleaks is not installed (expect only the documented development placeholders listed below):

  ```bash
  grep -rniE '(password|passwd|secret|signingkey|apikey|api_key|token|private[_-]?key)\s*["'"'"']?\s*[:=]\s*["'"'"']?[A-Za-z0-9+/=_\-]{8,}' \
    --include='*.cs' --include='*.json' --include='*.yml' --include='*.yaml' --include='*.props' --include='Makefile' . \
    | grep -v -e '/bin/' -e '/obj/' -e 'Migrations/'
  grep -rnE 'AKIA[0-9A-Z]{16}|sk_(live|test)_[0-9A-Za-z]{10,}|FLWSECK-|-----BEGIN (RSA |EC )?PRIVATE KEY' . --exclude-dir=bin --exclude-dir=obj
  ```

- [ ] Confirm `.gitignore` keeps `appsettings.*.local.json`, `.env` and `*.user` out of the repository (it does today).

Known, accepted development-only values (must never be reused outside local/test):

| Location | Value | Why it is acceptable |
|---|---|---|
| `src/Plastoids.Api/appsettings.Development.json` | `Secrets:JwtSigningKey`, `Secrets:FieldEncryptionKey`, `Secrets:BlindIndexKey`, `Seed:StaffPassword`, `Seed:StaffMfaSecret` | Development only; `appsettings.json` ships them empty and README says they must be set per environment |
| `src/Plastoids.Api/appsettings.json` | `ConnectionStrings:Plastoids` (plastoids/plastoids), `BlobStorage:AccessKey/SecretKey` | Local compose credentials; override with `ConnectionStrings__Plastoids`, `BlobStorage__*` |
| `docker-compose.yml` | `MYSQL_ROOT_PASSWORD: root`, `MYSQL_PASSWORD: plastoids` | Local only |
| `tests/Plastoids.IntegrationTests/PlastoidsApiFactory.cs` | test signing/encryption keys, staff password, TOTP secret | Testcontainers only |
| `src/Plastoids.Infrastructure/Integrations/IPaymentGateway.cs`, `IPayoutProvider.cs`, `IVideoProvider.cs`, `ILiveVideoProvider.cs` | fallback strings `fake-payment-webhook-secret`, `fake-payout-webhook-secret`, `fake-video-webhook-secret`, `fake-live-webhook-secret` used when `Secrets:*WebhookSecret` is unset | Only the **fake** providers use them |

- [ ] Production guard to add: nothing in `InfrastructureServiceCollectionExtensions.AddPlastoidsInfrastructure` stops a Production deployment from running with `Integrations:PaymentGateway=fake` (or `Sms:Provider=fake`, `Integrations:Video/LiveVideo/PayoutProvider` unset), in which case the public fallback secrets would verify real webhooks and OTP codes would be written to the log. Add a start-up check: when `IHostEnvironment.IsProduction()`, fail if any integration resolves to a `Fake*` implementation or if any `Secrets:*` value is empty (`ConfigurationSecretProvider`, `src/Plastoids.Infrastructure/Secrets/ISecretProvider.cs`).

## 3. Rate limits on `/auth/*`

Implementation: `src/Plastoids.Api/Program.cs` lines 57–69 (`services.AddRateLimiter`) and line 87 (`app.UseRateLimiter()`, before authentication).

- Policy `auth`: fixed window, `RateLimiting:AuthPermitsPerMinute` (default **10 per minute per client IP**), `QueueLimit = 0`, rejection `429`.
- Global limiter: token bucket per client IP, `RateLimiting:GlobalTokensPerMinute` (default **300 per minute**), applies to every endpoint including webhooks and `/health/*`.
- Applied with `.RequireRateLimiting("auth")` on all five `/api/v1/identity/auth/*` endpoints (`src/Modules/Identity/Features/Auth/{RequestOtp,VerifyOtp,RegisterWithEmail,IssueToken,VerifyMfa}/*Endpoint.cs`) and on `POST /api/v1/tutors/me/identity-check` (`src/Modules/Tutors/Features/Onboarding/SubmitIdentityCheck/SubmitIdentityCheckEndpoint.cs`). `POST /api/v1/identity/me/mfa/enable` is under `/me`, authenticated, and relies on the global bucket.
- A second, per-phone limit lives in Redis: `OtpService` (`src/Modules/Identity/Services/OtpService.cs`) returns `otp_rate_limited` ("Too many codes requested for this number. Try again in an hour.").

- [ ] Prove it (API running locally with default settings; the 11th request in a minute must be 429):

  ```bash
  for i in $(seq 1 11); do
    curl -s -o /dev/null -w "%{http_code}\n" -X POST localhost:5080/api/v1/identity/auth/otp/request \
      -H 'Content-Type: application/json' -d "{\"phone\":\"0803123450$((i % 10))\"}"
  done
  ```

- [ ] Existing test: `tests/Plastoids.IntegrationTests/IdentityAuthTests.cs` asserts `TooManyRequests` (line ~93) for the per-phone OTP limit. Add one for the per-IP `auth` policy by lowering `RateLimiting:AuthPermitsPerMinute` in a dedicated factory, and one asserting the 429 body is a problem-details document.

- [ ] Finding — proxy awareness: both limiters and `AdminIpAllowListMiddleware` (`src/Plastoids.Infrastructure/Http/AdminIpAllowListMiddleware.cs`) key on `HttpContext.Connection.RemoteIpAddress`. There is no `UseForwardedHeaders` / `ForwardedHeadersOptions` anywhere in the code, so behind a load balancer every client shares one bucket (and the allow-list sees the balancer's IP). Before production, add forwarded-headers handling restricted to the known proxy addresses.

- [ ] Finding — webhooks share the global 300/min bucket. A gateway retry storm (Paystack retries failed webhooks) could be throttled. Either raise the global bucket for the webhook routes with `.DisableRateLimiting()` plus their own policy, or document the expected volume.

## 4. HMAC verification on every webhook

Every inbound webhook route found by `grep -rn 'webhook' --include='*Endpoint.cs' --include='*.cs' src/Modules`:

| Route | Header | Verifier | Where verified | Real provider status |
|---|---|---|---|---|
| `POST /api/v1/payments/webhooks/{gateway}` | `x-paystack-signature` or `X-Gateway-Signature` | `IPaymentGateway.VerifyWebhookSignature` — HMAC-SHA512 over the raw body, constant-time compare | Endpoint, before dispatch (`src/Modules/Payments/Features/Checkout/HandleGatewayWebhook/HandleGatewayWebhookEndpoint.cs`); also checks `{gateway}` equals the configured provider name | `PaystackGateway` verifies with the Paystack secret; `FlutterwaveGateway.VerifyWebhookSignature` returns `false` (fails closed) |
| `POST /api/v1/lms/webhooks/live/{provider}` | `X-Live-Signature` | `ILiveVideoProvider.VerifyWebhookSignature` — HMAC-SHA256 | Endpoint (`src/Modules/Lms/Features/Live/HandleProviderAttendanceWebhook/HandleProviderAttendanceWebhookCommand.cs`) | `DailyLiveVideoProvider` returns `false` (stub, fails closed) |
| `POST /api/v1/catalogue/webhooks/video/{provider}` | `X-Video-Signature` | `IVideoProvider.VerifyWebhookSignature` — HMAC-SHA256 | Endpoint (`src/Modules/Catalogue/Features/Authoring/UploadLessonVideo/UploadLessonVideoEndpoint.cs`) | `MuxVideoProvider` returns `false` (stub, fails closed) |
| `POST /api/v1/payouts/webhooks/{provider}` | `x-payout-signature` or `x-paystack-signature` | `IPayoutProvider.VerifySignature` — HMAC-SHA512 | **Handler**, after FluentValidation (`src/Modules/Payouts/Features/Batches/HandlePayoutStatusWebhook/HandlePayoutStatusWebhook.cs` line ~42) | `PaystackTransferProvider` verifies with `PaystackSecretKey`; `BankBulkFilePayoutProvider` returns `false` |

All four use `CryptographicOperations.FixedTimeEquals`, reject with `401 webhook_signature_invalid`, are `AllowAnonymous`, and are idempotent on the provider event id / upload id.

- [ ] Prove it: a body signed with the wrong key is rejected on each route. Tests already exist for payments (`tests/Plastoids.IntegrationTests/PaymentsAndLedgerTests.cs` ~line 66), payouts (`PayoutsTests.cs` ~line 116), video (`CatalogueTests.cs` ~line 115) and live attendance (`BookingAndLiveSessionTests.cs` ~line 122). Keep them; add a test that a **missing** header is rejected on each route (today's tests use a wrong value).

  ```bash
  curl -s -o /dev/null -w "%{http_code}\n" -X POST localhost:5080/api/v1/payments/webhooks/fake \
    -H 'Content-Type: application/json' -H 'x-paystack-signature: deadbeef' -d '{"eventType":"charge.success"}'   # expect 401
  ```

- [ ] Finding — the payouts route verifies inside the MediatR pipeline, so an unsigned request first passes `ValidationBehaviour` (payload ≤ 64 KB) and opens a unit-of-work transaction before the signature check. Harmless but inconsistent; move the check into the endpoint like the other three.

- [ ] Finding — the Paystack verifiers (`PaystackGateway`, `PaystackTransferProvider`) do not check that the webhook originates from Paystack's published IP ranges; Paystack recommends it in addition to the signature. Optional hardening once the real provider is enabled.

## 5. Problem details never leak stack traces

Implementation:

- `GlobalExceptionHandler` (`src/Plastoids.Api/Program.cs`, class at the bottom) maps every unhandled exception to a fixed `{ type, title, status, detail, code, correlationId }` body: `CrossTenantWriteException → 403 cross_tenant_write`, `DbUpdateConcurrencyException → 409 concurrency_conflict`, anything else `→ 500 internal_error` with the constant detail "An unexpected error occurred." The exception goes only to the log (`LogError`). It always returns `true`, so the Development developer-exception page (added automatically by `WebApplication`) never renders.
- `services.AddProblemDetails(...)` adds `correlationId`; `app.UseExceptionHandler()` + `app.UseStatusCodePages()` are registered before authentication.
- Expected failures never throw: `Result`/`Error` → `ResultHttp.ToHttpResult()` / `ResultHttp.Problem()` (`src/Plastoids.Infrastructure/Http/ResultHttp.cs`); validation failures → 422 via `ValidationBehaviour` (`src/Plastoids.Infrastructure/Behaviours/ValidationBehaviour.cs`).

- [ ] Prove it: trigger a 500 and inspect the body for `at ` / `Exception` / file paths. The diagnostics smoke endpoint is the easiest hook (`src/Modules/Identity/Features/Diagnostics/EmitSmokeEvent/EmitSmokeEventEndpoint.cs`, only mapped when `Diagnostics:Enabled=true`); add a `POST /api/v1/identity/diagnostics/throw` next to it and assert in an integration test that the 500 body equals the fixed shape and contains no `stackTrace`, `exception` or `   at ` substring. Run with `Diagnostics:Enabled=false` in Production (it is false in `appsettings.json`).

  ```bash
  curl -s -i localhost:5080/api/v1/catalogue/courses/does-not-exist | head -20      # 404 problem, stable code
  curl -s -X POST localhost:5080/api/v1/identity/auth/otp/request -H 'Content-Type: application/json' -d '{}' | jq .  # 422, field errors only
  ```

- [ ] Existing test: `tests/Plastoids.IntegrationTests/OutboxPipelineTests.cs` `Validation_failure_is_a_422_problem_with_field_errors`. There is no test for the 500 path yet.

- [ ] Finding — `ASPNETCORE_ENVIRONMENT=Development` also enables Swagger UI at `/swagger` and `Database:MigrateOnStartup`; confirm production runs with `Production` (CI's OpenAPI export step runs Development on purpose).

## 6. PII never logged (Serilog destructuring policy)

Implementation found:

- `src/Plastoids.Infrastructure/Observability/Observability.cs`: Serilog is configured with `Enrich.FromLogContext()`, `Enrich.WithProperty("service", …)`, `Destructure.ToMaximumDepth(2)` and the compact JSON console sink. The comment says "PII never reaches logs: destructure only to depth 2 and never dump entity graphs", but **there is no `IDestructuringPolicy`, no `Destructure.ByTransforming<…>`, no masking enricher and no denylist of property names.** Protection today relies on callers never passing a request or entity object as a structured property.
- `LoggingBehaviour` (`src/Plastoids.Infrastructure/Behaviours/LoggingBehaviour.cs`) logs only `typeof(TRequest).Name`, the error code and elapsed time — no request payload.
- `AuditBehaviour` (`src/Plastoids.Infrastructure/Behaviours/AuditBehaviour.cs`) redacts `PasswordHash, Nin, NinCiphertext, Dob, DobCiphertext, Phone, PhoneCiphertext, Bvn, BvnCiphertext` in audit snapshots — this protects the audit trail, not the log.
- `CorrelationIdMiddleware` pushes `correlationId` only.
- `app.UseSerilogRequestLogging()` logs method, path, status and elapsed; no query strings containing PII are used by the API (lookups are by id or slug).

**Conclusion: a Serilog PII destructuring policy does not exist. The Sprint 9 item "PII never logged (Serilog destructuring policy + test)" is open.**

Known PII/secret leaks into the log today:

- `src/Plastoids.Infrastructure/Sms/ISmsSender.cs` line ~39, `FakeSmsSender`: `logger.LogInformation("[fake-sms] to {Phone}: {Message}", phoneE164, message)` — phone number **and the OTP code** are logged. README documents this as the local sign-in aid. Acceptable only while the fake is restricted to non-production (see the production guard in section 2).
- Review every `Log*` call with a user-supplied argument: `grep -rn 'Log\(Information\|Warning\|Error\|Debug\)(' --include='*.cs' src | grep -iE 'phone|email|nin|bvn|dob|name|address'`.

- [ ] Implement a `PiiDestructuringPolicy : IDestructuringPolicy` in `src/Plastoids.Infrastructure/Observability/` that, for any object passed with `@`, replaces properties named (case-insensitive) `phone`, `phoneE164`, `email`, `nin`, `bvn`, `dob`, `dateOfBirth`, `password`, `passwordHash`, `accountNumber`, `otp`, `code`, `token`, `accessToken`, `refreshToken`, `secret` with `"[redacted]"`, and register it with `.Destructure.With<PiiDestructuringPolicy>()` next to `ToMaximumDepth(2)`.
- [ ] Add a `MaskingEnricher` (or a `ILogEventFilter`) that scrubs Nigerian phone patterns (`\+?234\d{10}|0[789]\d{9}`), 11-digit NIN/BVN, and e-mail addresses from rendered message text, as defence in depth for scalar arguments like the `[fake-sms]` line.
- [ ] Test: in `tests/Plastoids.IntegrationTests` (or a new `Plastoids.Infrastructure.Tests`), configure the same `LoggerConfiguration` with an in-memory sink, log `{@Request}` for `VerifyOtpCommand`-like objects and a scalar phone, and assert the rendered event contains neither the phone nor the code. Name it after the Sprint 9 item (`Security_pii_never_logged`).
- [ ] Change `FakeSmsSender` to log the phone through the policy (or only its blind index) and the OTP at `Debug` level only when `Diagnostics:Enabled` is true.

## 7. Other controls reviewed while verifying the above

- MFA: `Policies.Tutor`, `Policies.Sponsor`, `Policies.Staff`, `Policies.StaffAdmin` require `amr=mfa` (`src/Plastoids.Infrastructure/Security/Authentication.cs`). Staff endpoints additionally pass `AdminIpAllowListMiddleware` on `/api/v1/admin/*` (empty allow-list in `appsettings.json` → everything denied by default; Development allows loopback).
- JWT: self-issued HS256 (`SecurityAlgorithms.HmacSha256`, ADR 0007); `Secrets:JwtSigningKey` must be ≥ 32 bytes of real entropy in production. Access tokens 15 min, refresh 30 days.
- Field encryption: AES-GCM with key id beside the ciphertext; blind index HMAC-SHA256 (`src/Plastoids.Infrastructure/Security/FieldEncryptor.cs`).
- Idempotency: `IdempotencyBehaviour` + `shared_idempotency_keys` (`src/Plastoids.Infrastructure/Behaviours/IdempotencyBehaviour.cs`).
- Append-only ledger and audit tables enforced by MySQL triggers (see `docs/runbooks/migration-rollback.md`).
- Not configured (confirm the edge handles them, or add): `UseHttpsRedirection`/`UseHsts`, CORS policy, security response headers (`X-Content-Type-Options`, `Referrer-Policy`, CSP for Swagger UI), request body size limits beyond Kestrel defaults, `AllowedHosts` is `*`.
- Stray duplicate code at the repository root (`CatalogueModule.cs` and `Features/Discovery/**`) differs from `src/Modules/Catalogue`; it is not part of the solution but confuses scans and reviewers. Delete it.
