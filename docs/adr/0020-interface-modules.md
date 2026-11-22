# ADR 0020: Three interface modules — Learner, Sponsor, Administration

**Status:** Accepted (2026-11-23)

## Context

ADR 0017 shipped one single-page app with five route areas (public, learner, tutor, sponsor, back office) behind one
router and one shell. The user asked for the interface to be broken into a Student/Learner module, a Sponsor module and
an Administration module, so each can be worked on, built and deployed on its own.

## Decision

1. **One package, three entry points.** `frontend/` stays one npm package (one dependency set, one design system) but
   Vite builds three pages: `index.html` (Learner module), `sponsor/index.html` (Sponsor module) and `admin/index.html`
   (Administration module). Each has its own `main.jsx`, `App.jsx`, router, shell and navigation. Pages are lazy, so a
   module only downloads its own code plus the shared chunks.
2. **Code layout.**
   - `src/shared/` — everything more than one module uses: `lib/` (api client, auth provider and guards, hooks, format,
     units, navigation), `components/` (ui kit, Logo, WalletPanels, EarningsStatement), `pages/auth/` (phone sign-in,
     staff sign-in, MFA, no-access), `api/public.js`, `api/wallet.js`, `styles/index.css`.
   - `src/modules/learner/` — the people module: public marketplace pages, learner area (`/learn/*`), tutor area
     (`/tutor/*`), `api/learn.js`, `api/tutor.js`, `PublicLayout`, its `AppShell` with the learn and tutor navigations.
     The tutor area lives here because a tutor is a person account that also learns; it can be split out later the same way.
   - `src/modules/sponsor/` — the organisation portal (sponsors and providers): `/sponsor/*`, `api/sponsor.js`,
     `api/provider.js`, its own shell.
   - `src/modules/admin/` — the back office: `/admin/*`, `api/admin.js`, its own shell.
3. **URLs do not change.** Each module's router uses a `basename` (`/`, `/sponsor`, `/admin`), so every existing deep
   link keeps working. Sign-in pages are shared and mounted inside each module (`/auth/*`, `/sponsor/auth/*`,
   `/admin/auth/*`); tokens live in localStorage on the one origin, so a sign-in in one module is valid in all.
4. **Cross-module links are full-page navigations.** `src/shared/lib/navigation.js` names the module bases and exposes
   `hrefFor(path)` and `<AppLink>`, which renders a router `Link` inside the current module and a plain anchor to another
   module. `homeFor(roles)` returns module URLs; `RequireAuth` redirects to the current module's sign-in page.
5. **Boundaries are enforced.** `scripts/check-module-boundaries.mjs` fails the build if a module imports from another
   module or shared imports from a module. It runs in `npm run lint`, `make web-lint` and CI.
6. **Serving.** The Vite dev server serves all three on one port with a history fallback per module. nginx does the
   same with one `try_files` location per module. `VITE_MODULES=learner,sponsor` builds a subset, so a module can be
   deployed alone by shipping its HTML and the `assets/` folder.

## Consequences

- `frontend/src/App.jsx`, `main.jsx`, `pages/` and `components/layout` move; imports change throughout. No page logic
  changes.
- The sponsor and admin shells link to the learner module for profile, notifications and wallet rather than carrying
  copies of those pages.
- A future split into separate packages (or repositories) is a move of `src/modules/<name>` plus a copy of `shared`.
