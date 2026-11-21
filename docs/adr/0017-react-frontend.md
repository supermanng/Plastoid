# ADR 0017: React web app in `frontend/`

**Status:** Accepted (2026-11-21)

## Context

The blueprint (§15) names Next.js + TypeScript for the web. The user asked for the frontend "in React JS". Server-side
rendering of public pages is an SEO concern that can be added later; what the MVP needs now is a client for every
API area so the flows can be exercised end to end.

## Decision

- **Vite + React 19, plain JavaScript (JSX)**, React Router 7, TanStack Query 5, Tailwind CSS v4 themed with the §14
  design tokens (navy / royal / cyan / teal, surface / line / muted, Nunito + Inter, 8 / 12 / 999 px radii). No
  TypeScript, as asked; no component library, a small in-repo kit (`src/components/ui.jsx`) covers the blueprint's
  rules (icon + label status pills, 44 px targets, loading / error / empty states).
- **One SPA, five areas** behind route guards that mirror the API policies: public (`/`), learner (`/learn`, any signed-in
  account), tutor (`/tutor`, role + MFA), sponsor (`/sponsor`, role + MFA), back office (`/admin`, staff + MFA).
  `RequireAuth` reads the `amr` claim from the access token and sends users to `/auth/mfa` to enrol or step up.
- **Same-origin API**: Vite proxies `/api` in development; the nginx image proxies `/api/` to the API container.
  CORS on the API is opt-in through `Cors:AllowedOrigins` for a separately hosted build.
- **API client** (`src/lib/api.js`): Bearer tokens in `localStorage`, one `refresh_token` retry on 401, a fresh
  `Idempotency-Key` on every command, `ApiError` built from the problem-details body (`code`, `detail`, `errors`,
  `correlationId`).
- **Payment return**: `Payments:CallbackBaseUrl` now points at `/learn/payments/return` on the web origin in Development
  (`appsettings.Development.json`); the API appends `/{paymentIntentId}` and the page calls `verify`.
- Pages read request and response shapes straight from the C# records; nothing is generated from OpenAPI yet.

## Backend gaps the UI works around (candidates for the next API sprint)

| Gap | Workaround in the UI |
|---|---|
| No "my courses" / "my cohorts" reads for tutors; drafts are not readable through `GET /catalogue/courses/{slug}` | ids created in this browser are kept in `localStorage`; "open by id / slug" boxes |
| No "programmes for organisation", "invoices", "organisation members" reads for sponsors | same `localStorage` memory + open-by-id |
| No staff list or single GET for programmes before publication | programmes come from the public opportunity list; open-by-id for the rest |
| No public single GET for an opportunity or a tutor | detail pages walk the paginated list |
| `GradebookRow` has no `submissionId`; rubric criteria keys are not exposed to staff | grading and scoring dialogs take ids / keys typed by hand |
| No selfie upload URL for the identity check | placeholder blob key (the fake KYC adapter ignores it) |
| Application form fields are not published per programme | fixed 8-field form (4 eligibility facts + motivation, prior experience, weekly hours, how heard) |
| Fake gateway checkout URL (`checkout.plastoids.local`) is unreachable locally | pages show the intent id and a link to the payment page; mark intents paid with the gateway webhook |
| Sponsor invoices are settled by bank transfer to a virtual account, not a checkout | invoice page shows transfer instructions and refreshes while outstanding |
| Video SDK / HLS player not embedded | join details and stream URLs are shown; Safari plays HLS natively |

## Tooling notes

- `make web`, `make web-build`, `make web-lint`; CI job `frontend` (lint, build, `npm audit --audit-level=high`).
- Vite 8 and oxlint ship native bindings as optional dependencies. On this Mac npm 10.9 skipped them; the two darwin-arm64
  bindings are listed under `optionalDependencies` so a normal `npm install` restores them, and the oxlint one was placed by
  hand from its tarball. Linux CI is unaffected.
- Port 5173 was busy on the dev machine; the preview ran on 5174. The Vite proxy and `CallbackBaseUrl` assume 5173.

## Not done

- Server-side rendering / SEO for public pages (Next.js would be the switch if needed).
- Automated UI tests. Verified by hand in the browser: OTP sign-up → learner dashboard, staff email + TOTP → back office,
  and every back-office and public list page renders against the Development API.
