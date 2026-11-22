/*
 * Thin fetch wrapper for the Plastoids API.
 *
 * - Base path `/api/v1` (proxied by Vite in development; set VITE_API_BASE for a separately hosted API).
 * - Bearer tokens from the auth store; a 401 triggers one refresh_token grant and a retry.
 * - Every non-GET request carries an `Idempotency-Key` (the API requires it on most commands).
 * - Failures throw `ApiError` built from RFC 9457 problem details: { status, code, detail, errors, correlationId }.
 */

const BASE = import.meta.env.VITE_API_BASE ?? '/api/v1';
const STORAGE_KEY = 'plastoids.tokens';

let tokens = readTokens();
const listeners = new Set();

function readTokens() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? JSON.parse(raw) : null;
  } catch {
    return null;
  }
}

export function getTokens() {
  return tokens;
}

export function setTokens(next) {
  tokens = next && next.accessToken ? { accessToken: next.accessToken, refreshToken: next.refreshToken, expiresAt: next.expiresAt } : null;
  try {
    if (tokens) localStorage.setItem(STORAGE_KEY, JSON.stringify(tokens));
    else localStorage.removeItem(STORAGE_KEY);
  } catch {
    /* storage unavailable (private mode): keep tokens in memory only */
  }
  listeners.forEach((fn) => fn(tokens));
}

export function onTokensChange(fn) {
  listeners.add(fn);
  return () => listeners.delete(fn);
}

/** Decode the JWT payload without verifying (the API verifies; the client only reads claims for routing). */
export function decodeJwt(token) {
  try {
    const [, payload] = token.split('.');
    const json = atob(payload.replace(/-/g, '+').replace(/_/g, '/'));
    return JSON.parse(decodeURIComponent(Array.from(json, (c) => '%' + c.charCodeAt(0).toString(16).padStart(2, '0')).join('')));
  } catch {
    return null;
  }
}

export class ApiError extends Error {
  constructor({ status, code, title, detail, errors, correlationId }) {
    super(detail || title || code || `Request failed (${status})`);
    this.name = 'ApiError';
    this.status = status;
    this.code = code || title || 'error';
    this.detail = detail;
    this.errors = errors || null; // { field: [{ code, message }] }
    this.correlationId = correlationId;
  }

  /** First message for a field, or null. */
  field(name) {
    const list = this.errors?.[name] || this.errors?.[name.charAt(0).toLowerCase() + name.slice(1)];
    return list?.[0]?.message ?? null;
  }
}

function buildUrl(path, query) {
  const url = path.startsWith('http') ? new URL(path) : new URL(BASE + path, window.location.origin);
  if (query) {
    for (const [k, v] of Object.entries(query)) {
      if (v === undefined || v === null || v === '') continue;
      if (Array.isArray(v)) v.forEach((item) => url.searchParams.append(k, item));
      else url.searchParams.set(k, String(v));
    }
  }
  return url.toString();
}

async function parseBody(response) {
  if (response.status === 204) return null;
  const type = response.headers.get('content-type') || '';
  if (type.includes('json')) {
    const text = await response.text();
    return text ? JSON.parse(text) : null;
  }
  return response;
}

let refreshing = null;
async function refreshTokens() {
  if (!tokens?.refreshToken) return false;
  if (!refreshing) {
    refreshing = (async () => {
      try {
        const res = await fetch(buildUrl('/identity/auth/token'), {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', Accept: 'application/json', 'Idempotency-Key': crypto.randomUUID() },
          body: JSON.stringify({ grantType: 'refresh_token', refreshToken: tokens.refreshToken }),
        });
        if (!res.ok) throw new Error('refresh failed');
        const body = await res.json();
        if (!body?.accessToken) throw new Error('refresh failed');
        setTokens(body);
        return true;
      } catch {
        setTokens(null);
        return false;
      } finally {
        refreshing = null;
      }
    })();
  }
  return refreshing;
}

/**
 * request(method, path, options)
 * options: { body, query, headers, auth = true, idempotencyKey, retry = true, signal }
 */
export async function request(method, path, options = {}) {
  const { body, query, headers = {}, auth = true, idempotencyKey, retry = true, signal } = options;
  const init = { method, headers: { Accept: 'application/json', ...headers }, signal };
  if (body !== undefined) {
    if (body instanceof FormData || body instanceof Blob) init.body = body;
    else {
      init.headers['Content-Type'] = 'application/json';
      init.body = JSON.stringify(body);
    }
  }
  if (method !== 'GET' && method !== 'HEAD') {
    init.headers['Idempotency-Key'] = idempotencyKey || crypto.randomUUID();
  }
  if (auth && tokens?.accessToken) {
    init.headers.Authorization = `Bearer ${tokens.accessToken}`;
  }

  const response = await fetch(buildUrl(path, query), init);

  if (response.status === 401 && auth && retry && tokens?.refreshToken && !path.startsWith('/identity/auth/')) {
    const ok = await refreshTokens();
    if (ok) return request(method, path, { ...options, retry: false });
  }

  if (!response.ok) {
    let problem = {};
    try {
      problem = (await response.json()) || {};
    } catch {
      /* non-JSON error body */
    }
    throw new ApiError({
      status: response.status,
      code: problem.code || problem.title,
      title: problem.title,
      detail: problem.detail,
      errors: problem.errors,
      correlationId: problem.correlationId || response.headers.get('X-Correlation-Id'),
    });
  }

  return parseBody(response);
}

export const api = {
  get: (path, query, options) => request('GET', path, { ...options, query }),
  post: (path, body, options) => request('POST', path, { ...options, body }),
  put: (path, body, options) => request('PUT', path, { ...options, body }),
  patch: (path, body, options) => request('PATCH', path, { ...options, body }),
  delete: (path, body, options) => request('DELETE', path, { ...options, body }),

  /** Fetch a CSV/PDF and hand back a Blob plus the suggested file name. */
  async download(path, query, options) {
    const res = await request('GET', path, { ...options, query, headers: { Accept: '*/*', ...(options?.headers || {}) } });
    if (!(res instanceof Response)) return { blob: new Blob([JSON.stringify(res)], { type: 'application/json' }), fileName: 'download.json' };
    const disposition = res.headers.get('content-disposition') || '';
    const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition);
    return { blob: await res.blob(), fileName: match ? decodeURIComponent(match[1]) : path.split('/').pop() };
  },
};

/** Trigger a browser download of a Blob. */
export function saveBlob(blob, fileName) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

/** Turn an unknown error into a user-facing message. */
export function errorMessage(error, fallback = 'Something went wrong. Please try again.') {
  if (!error) return fallback;
  if (error instanceof ApiError) {
    if (error.errors) {
      const first = Object.values(error.errors).flat()[0];
      if (first?.message) return first.message;
    }
    return error.detail || humanise(error.code) || fallback;
  }
  if (error.name === 'TypeError') return 'Cannot reach the server. Check your connection and try again.';
  return error.message || fallback;
}

export function humanise(code) {
  if (!code) return '';
  return String(code).replace(/[_-]+/g, ' ').replace(/^\w/, (c) => c.toUpperCase());
}
