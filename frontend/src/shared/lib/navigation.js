import { createElement } from 'react';
import { Link } from 'react-router-dom';

/*
 * Interface modules (ADR 0020). Each module is its own HTML entry with a router mounted at its base path.
 * Paths handed to the helpers here are always absolute site paths (`/sponsor/courses`, `/learn/profile`, `/courses`).
 * Inside one module, pages use ordinary react-router links with module-relative paths; only a link that may leave
 * the current module goes through <AppLink> / navigateTo(), which fall back to a full-page navigation.
 */
export const MODULES = { learner: '/', sponsor: '/sponsor', admin: '/admin' };

/** Sign-in page of each module (RequireAuth sends unauthenticated users here). */
export const SIGN_IN = { learner: '/auth/sign-in', sponsor: '/sponsor/auth/sign-in', admin: '/admin/auth/staff' };

/** Which module serves an absolute path. The longest base wins; `/sponsorship-x` is the learner module. */
export function moduleOf(path) {
  const pathname = String(path || '/').split(/[?#]/)[0];
  let best = 'learner';
  let bestLen = 0;
  for (const [name, base] of Object.entries(MODULES)) {
    if (base === '/') continue;
    if ((pathname === base || pathname.startsWith(base + '/')) && base.length > bestLen) {
      best = name;
      bestLen = base.length;
    }
  }
  return best;
}

/** The module the browser is currently in, from window.location. */
export function currentModule() {
  if (typeof window === 'undefined') return 'learner';
  return moduleOf(window.location.pathname);
}

/** Absolute href for an absolute path: modules share one origin, so the path is the href. */
export function hrefFor(path) {
  return path;
}

export function isInCurrentModule(path) {
  return moduleOf(path) === currentModule();
}

/** Strip the current module's base so the module's router (which has that basename) sees the path it expects. */
export function toModulePath(path) {
  const base = MODULES[moduleOf(path)];
  if (base === '/') return path;
  const rest = path.slice(base.length);
  return rest === '' || rest.startsWith('?') || rest.startsWith('#') ? `/${rest}` : rest;
}

/** The absolute site path for a module-relative router location (the inverse of toModulePath for the current module). */
export function toSitePath(modulePath, module = currentModule()) {
  const base = MODULES[module];
  if (base === '/') return modulePath;
  return modulePath === '/' ? base : base + modulePath;
}

/** Sign-in page of the current module. */
export function signInPath(module = currentModule()) {
  return SIGN_IN[module] || SIGN_IN.learner;
}

/**
 * Link that stays a client-side <Link> inside the current module and becomes a plain anchor to another module.
 * `to` is an absolute site path. Router-only props (state, replace, end) are dropped on the anchor.
 */
export function AppLink({ to, state, replace, end: _end, ...rest }) {
  if (isInCurrentModule(to)) return createElement(Link, { to: toModulePath(to), state, replace, ...rest });
  return createElement('a', { href: hrefFor(to), ...rest });
}

/** Programmatic counterpart of <AppLink>: router navigation in-module, full page load across modules. */
export function navigateTo(path, navigate, options) {
  if (navigate && isInCurrentModule(path)) return navigate(toModulePath(path), options);
  if (options?.replace) return window.location.replace(hrefFor(path));
  return window.location.assign(hrefFor(path));
}
