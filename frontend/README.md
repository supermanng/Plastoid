# Plastoids web app

React 19 + Vite single-page app for the Plastoids API: public site, learner, tutor, sponsor and back-office areas. Plain JavaScript (JSX), Tailwind CSS v4 themed with the blueprint's design tokens (§14), React Router 7 and TanStack Query 5.

## Run it

```bash
# 1. API on :5080 (from the repo root; needs the Docker stack: make up)
make run

# 2. Web app on :5173 (proxies /api and /health to :5080, so no CORS is needed)
make web
```

Or from this folder: `npm install` then `npm run dev`. Open http://localhost:5173.

Sign-in options locally:

- **Learners / tutors / sponsors**: phone + OTP. SMS is faked in Development; the 6-digit code is printed in the API log as `[fake-sms] to ***1234: …`.
- **Staff**: email + password + authenticator code at `/auth/staff`. Two accounts are seeded so four-eyes flows (payout batches, fee rules, role grants, large refunds) can be exercised with a different maker and checker: `staff@plastoids.local` (`Seed:StaffPassword` / `Seed:StaffMfaSecret`) and the super admin `admin@plastoids.local` (`Seed:SuperAdminPassword` / `Seed:SuperAdminMfaSecret`), both in `appsettings.Development.json`. Add the TOTP secret to an authenticator app to get codes. Both carry the `staff` role; the API has no separate super-admin role.
- Tutor, sponsor and staff areas need an MFA-stepped-up token. The app sends you to `/auth/mfa` to enrol or confirm.

Payments and payouts use fake providers locally: the fake gateway's checkout URL is not reachable, so pages show the payment intent id and a link to the payment detail page instead; mark intents paid by posting the gateway webhook (see `docs/runbooks/` and the integration tests for the HMAC).

## Layout

```
src/
  main.jsx            providers (Query, Router, Toast, Auth)
  App.jsx             route map; every page is lazy-loaded
  index.css           Tailwind v4 @theme tokens (navy/royal/cyan/teal, surface/line/muted, fonts, radii)
  lib/api.js          fetch wrapper: /api/v1 base, Bearer + refresh_token retry, Idempotency-Key on commands, ApiError from problem details
  lib/auth.jsx        AuthProvider, useAuth, RequireAuth(role, mfa), homeFor(roles)
  lib/hooks.js        useQueryParams, useAction (mutation + toast + invalidate), useForm, useCursor, pageOf
  lib/format.js       formatMoney ({ amount: kobo, currency }), dates, titleCase …
  components/ui.jsx   Button, Card, PageHeader, Stat, StatusPill, Field/Input/Select, Table, Tabs, Stepper, Modal, ConfirmDialog, Toast …
  components/layout/  PublicLayout (header/footer), AppShell (sidebar on desktop, bottom tabs on mobile)
  api/<area>.js       query functions and keys per area
  pages/public        home, courses, compare, course detail, opportunities, tutors, booking, verify, marketing pages
  pages/auth          phone OTP sign-in, staff email sign-in, MFA enrol/challenge, no-access
  pages/learn         dashboard, onboarding, my courses + player (video/doc/quiz/assignment), applications, bookings, certificates, payments, messages, notifications, profile
  pages/tutor         onboarding (5 verification stages), dashboard, profile (availability/pricing/payout account), courses + lesson builder, gradebook, cohorts, sessions, earnings
  pages/sponsor       register organisation, dashboard, organisation members, programmes, campaign stepper, programme detail (budget/funnel/report), invoices
  pages/admin         dashboard, queues, users, tutors, sponsors, programmes + applications review, course moderation, finance (ledger/reconciliation/settlements/refunds), payouts, fee rules, role grants, templates, feature flags, compliance, audit
```

Conventions the pages follow:

- Read shapes from the C# records in `src/Modules/*/Features/**` (camelCase JSON, camelCase enum strings, `Money` as `{ amount, currency }` in kobo, paging as `{ items, nextCursor }`).
- Every command goes through `api.post/put/...`, which attaches a fresh `Idempotency-Key`; pass `idempotencyKey` explicitly when a retry must reuse one.
- Status is never colour-only: `StatusPill` renders icon + label. Touch targets are 44 px. Lists have loading, error and empty states.
- Role gates mirror the API policies: `RequireAuth role="tutor" mfa` and so on; the API is the source of truth and 401/403 are surfaced.

## Build and deploy

```bash
npm run build          # → dist/
npm run lint           # oxlint
docker build -t plastoids-web -f frontend/Dockerfile frontend
```

The image serves `dist/` with nginx and proxies `/api/` and `/health/` to `API_UPSTREAM` (default `http://api:8080`). To host the SPA on a different origin instead, set `VITE_API_BASE` at build time and `Cors:AllowedOrigins` on the API.

## Known gaps (backend reads the UI works around)

Recorded in `docs/adr/0017-react-frontend.md`: no "my courses / my cohorts / my programmes / my invoices" list endpoints (pages remember ids created in this browser and offer open-by-id), no single-tutor or single-opportunity public read (pages search the list), the video SDK and HLS player are not embedded (join details and stream URLs are shown).
