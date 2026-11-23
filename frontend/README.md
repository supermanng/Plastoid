# Plastoids web app

React 19 + Vite web app for the Plastoids API, built as three interface modules (ADR 0020) from one package: the **Learner** module (public site, learner and tutor areas), the **Sponsor** module (organisation portal) and the **Administration** module (back office). Plain JavaScript (JSX), Tailwind CSS v4 themed with the blueprint's design tokens (§14), React Router 7 and TanStack Query 5.

## Run it

```bash
# 1. API on :5080 (from the repo root; needs the Docker stack: make up)
make run

# 2. Web app on :5173 (proxies /api and /health to :5080, so no CORS is needed)
make web
```

Or from this folder: `npm install` then `npm run dev`. Open http://localhost:5173. The dev server serves all three modules on that one port: `/` (learner), `/sponsor/...` and `/admin/...` each fall back to their module's HTML, so deep links work.

Sign-in options locally:

- **Learners / tutors / sponsors**: phone + OTP. SMS is faked in Development; the 6-digit code is printed in the API log as `[fake-sms] to ***1234: …`.
- **Staff**: email + password + authenticator code at `/auth/staff`. Two accounts are seeded so four-eyes flows (payout batches, fee rules, role grants, large refunds) can be exercised with a different maker and checker: `staff@plastoids.local` (`Seed:StaffPassword` / `Seed:StaffMfaSecret`) and the super admin `admin@plastoids.local` (`Seed:SuperAdminPassword` / `Seed:SuperAdminMfaSecret`), both in `appsettings.Development.json`. Add the TOTP secret to an authenticator app to get codes. Both carry the `staff` role; the API has no separate super-admin role.
- Tutor, sponsor and staff areas need an MFA-stepped-up token. The app sends you to `/auth/mfa` to enrol or confirm.

Payments and payouts use fake providers locally: the fake gateway's checkout URL is not reachable, so pages show the payment intent id and a link to the payment detail page instead; mark intents paid by posting the gateway webhook (see `docs/runbooks/` and the integration tests for the HMAC).

## Layout

```
index.html, sponsor/index.html, admin/index.html   one HTML entry per module (scripts src/modules/<module>/main.jsx)
src/shared/                                        everything more than one module uses; must not import from src/modules
  lib/api.js          fetch wrapper: /api/v1 base, Bearer + refresh_token retry, Idempotency-Key on commands, ApiError from problem details
  lib/auth.jsx        AuthProvider, useAuth, RequireAuth(role, mfa) → the current module's sign-in / mfa / no-access pages, homeFor(roles)
  lib/navigation.js   MODULES bases, moduleOf/currentModule, <AppLink> and navigateTo() for links that may leave the module
  lib/hooks.js        useQueryParams, useAction (mutation + toast + invalidate), useForm, useCursor, pageOf
  lib/format.js       formatMoney ({ amount: kobo, currency }), dates, titleCase …
  lib/units.jsx       units wallet helpers (ADR 0018); lib/authoring.js, lib/engagementTerms.js: course builder + engagement helpers
  components/ui.jsx   Button, Card, PageHeader, Stat, StatusPill, Field/Input/Select, Table, Tabs, Stepper, Modal, ConfirmDialog, Toast …
  components/layout/Shell.jsx   the signed-in shell (sidebar, top bar, bottom tabs, user menu); each module passes its nav + area switcher
  components/course-authoring/  AuthoringEditor (lesson builder), CourseForm, builder modals — used by the tutor and sponsor modules
  components/         Logo, WalletPanels, EarningsStatement
  pages/auth          phone OTP sign-in, staff email sign-in, MFA enrol/challenge, no-access (mounted inside every module)
  api/public.js, api/wallet.js, api/course.js   query functions shared across modules
  styles/index.css    Tailwind v4 @theme tokens (navy/royal/cyan/teal, surface/line/muted, fonts, radii)
src/modules/learner/   router basename `/`: public site, auth, /learn/*, /tutor/*
  main.jsx, App.jsx, layout/{PublicLayout,AppShell}.jsx, api/{learn,tutor}.js
  pages/public        home (upcoming cohorts), courses, compare, course detail (upcoming cohorts), tutors, booking, verify, marketing pages
  pages/learn         dashboard, onboarding, my courses + player (video/doc/quiz/assignment; "Sponsored by …" chip), bookings, certificates, payments, wallet, messages, notifications, profile
  pages/tutor         onboarding (5 verification stages), dashboard, profile (availability/pricing/payout account), courses + lesson builder, offers (engagements), gradebook, cohorts, sessions, earnings
src/modules/sponsor/   router basename `/sponsor`: the organisation portal (sponsors and providers)
  main.jsx, App.jsx, layout/AppShell.jsx, api/{sponsor,seats,provider}.js
  pages/              register organisation, dashboard, organisation members, seats (list, buy seats in a cohort, seat block: give/release seats — ADR 0021), wallet, courses + editor + course tutors (engagements), payouts (ADR 0019)
src/modules/admin/     router basename `/admin`: the back office
  main.jsx, App.jsx, layout/AppShell.jsx, api/admin.js
  pages/              dashboard, queues, users, tutors, sponsors, seat blocks (read-only, ADR 0021), course moderation, finance (ledger/reconciliation/settlements/refunds), payouts, fee rules, unit rates, role grants, templates, feature flags, compliance, audit
scripts/check-module-boundaries.mjs   fails when a module imports another module, or shared imports a module
```

### Modules

| Module | Entry | URLs | Who |
| --- | --- | --- | --- |
| Learner | `index.html` → `src/modules/learner` | `/`, `/auth/*`, `/learn/*`, `/tutor/*` | everyone; learners and tutors (a tutor is a person account that also learns) |
| Sponsor | `sponsor/index.html` → `src/modules/sponsor` | `/sponsor/*` (sign-in at `/sponsor/auth/sign-in`) | organisation members: sponsors and course providers |
| Administration | `admin/index.html` → `src/modules/admin` | `/admin/*` (sign-in at `/admin/auth/staff`) | Plastoids staff |

URLs did not change with the split: each module's router has a `basename`, and the shared auth pages are mounted inside every module so `RequireAuth` can redirect without leaving it. Tokens live in localStorage on the one origin, so a sign-in in one module is valid in all. Inside a module, pages link with ordinary react-router `Link`/`navigate` and module-relative paths (in the sponsor module `/sponsor/courses` is `to="/courses"`). A link that may leave the module (role home, profile and notifications from the sponsor/admin shells, the public course page from the course builder, the area switcher) uses `<AppLink to="/learn/profile">` or `navigateTo(path, navigate)` from `shared/lib/navigation.js`, which stay client-side in-module and become a full-page navigation otherwise. The sponsor and admin shells link to the learner module for profile, notifications and wallet rather than carrying copies of those pages.

### Running or building one module

```bash
VITE_MODULES=admin npm run build            # only dist/admin/index.html + the assets it needs
VITE_MODULES=learner,sponsor npm run build  # a subset; default is all three
```

The dev server always serves all three (open `/`, `/sponsor` or `/admin`). A module can be deployed alone by shipping its HTML and the `assets/` folder; nginx has one `try_files` fallback per module (`nginx.conf`).

### Module boundaries

`npm run boundaries` (also part of `npm run lint`, `make web-lint` and CI) runs `scripts/check-module-boundaries.mjs`: a file under `src/modules/<a>/` may import `src/shared/` and its own module only, and `src/shared/` may not import from `src/modules/`. Code two modules need moves to `src/shared/`.

Conventions the pages follow:

- Read shapes from the C# records in `src/Modules/*/Features/**` (camelCase JSON, camelCase enum strings, `Money` as `{ amount, currency }` in kobo, paging as `{ items, nextCursor }`).
- Every command goes through `api.post/put/...`, which attaches a fresh `Idempotency-Key`; pass `idempotencyKey` explicitly when a retry must reuse one.
- Status is never colour-only: `StatusPill` renders icon + label. Touch targets are 44 px. Lists have loading, error and empty states.
- Role gates mirror the API policies: `RequireAuth role="tutor" mfa` and so on; the API is the source of truth and 401/403 are surfaced.

## Build and deploy

```bash
npm run build          # → dist/ (index.html, sponsor/index.html, admin/index.html + assets/)
npm run lint           # oxlint + module boundary check
npm run boundaries     # the boundary check alone
docker build -t plastoids-web -f frontend/Dockerfile frontend
```

The image serves `dist/` with nginx (one history fallback per module: `/sponsor/`, `/admin/`, everything else → learner) and proxies `/api/` and `/health/` to `API_UPSTREAM` (default `http://api:8080`). To host the SPA on a different origin instead, set `VITE_API_BASE` at build time and `Cors:AllowedOrigins` on the API.

## Known gaps (backend reads the UI works around)

Recorded in `docs/adr/0017-react-frontend.md`: no "my cohorts" list endpoint for tutors (the page remembers ids created in this browser and offers open-by-id), no single-tutor public read (pages search the list), the video SDK and HLS player are not embedded (join details and stream URLs are shown). Seat sponsorship (ADR 0021) lists seat blocks from the API, so nothing is remembered in the browser for it. Courses are listed from `GET /catalogue/courses/mine` and `GET /catalogue/organisations/{id}/courses` and drafts load from the authoring view since ADR 0019, so the browser no longer remembers course ids.
