import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { api, decodeJwt, getTokens, onTokensChange, setTokens } from './api';

/*
 * Auth state for the whole app.
 *   status: 'loading' | 'anonymous' | 'authenticated'
 *   me:     GET /identity/me → { id, phoneMasked, email, displayName, mfaEnabled, roles[], organisations[], careerGoal, interests }
 *   mfa:    whether the current access token carries amr=mfa (tutor/sponsor/staff endpoints require it)
 */

export const ROLES = { learner: 'learner', tutor: 'tutor', sponsor: 'sponsor', staff: 'staff' };

/** Where each role lands after sign-in; the first match in this order wins for multi-role accounts. */
export const ROLE_HOME = [
  [ROLES.staff, '/admin'],
  [ROLES.sponsor, '/sponsor'],
  [ROLES.tutor, '/tutor'],
  [ROLES.learner, '/learn'],
];

const AuthContext = createContext(null);

function tokenHasMfa(tokens) {
  const claims = tokens?.accessToken ? decodeJwt(tokens.accessToken) : null;
  const amr = claims?.amr;
  if (!amr) return false;
  return Array.isArray(amr) ? amr.includes('mfa') : String(amr).split(/[ ,]/).includes('mfa');
}

export function AuthProvider({ children }) {
  const [tokens, setTokenState] = useState(getTokens);
  const [me, setMe] = useState(null);
  const [status, setStatus] = useState(tokens ? 'loading' : 'anonymous');

  useEffect(() => onTokensChange((next) => {
    setTokenState(next);
    if (!next) {
      setMe(null);
      setStatus('anonymous');
    }
  }), []);

  const loadMe = useCallback(async () => {
    try {
      const profile = await api.get('/identity/me');
      setMe(profile);
      setStatus('authenticated');
      return profile;
    } catch (error) {
      if (error?.status === 401) {
        setTokens(null);
      } else {
        // The token may still be valid (network blip); keep the session but surface nothing.
        setStatus(getTokens() ? 'authenticated' : 'anonymous');
      }
      return null;
    }
  }, []);

  useEffect(() => {
    if (tokens?.accessToken && !me) loadMe();
  }, [tokens, me, loadMe]);

  const applyTokens = useCallback(async (tokenResponse) => {
    if (tokenResponse?.mfaRequired) return { mfaRequired: true, mfaToken: tokenResponse.mfaToken };
    setTokens(tokenResponse);
    const profile = await loadMe();
    return { mfaRequired: false, me: profile };
  }, [loadMe]);

  const value = useMemo(() => ({
    status,
    me,
    tokens,
    isAuthenticated: status === 'authenticated',
    roles: me?.roles ?? [],
    hasRole: (role) => (me?.roles ?? []).includes(role),
    mfa: tokenHasMfa(tokens),
    organisations: me?.organisations ?? [],
    refreshMe: loadMe,

    /** Phone OTP: step 1. */
    requestOtp: (phone) => api.post('/identity/auth/otp/request', { phone }, { auth: false }),
    /** Phone OTP: step 2. New accounts must send dateOfBirth (YYYY-MM-DD) and displayName. */
    verifyOtp: async ({ phone, code, dateOfBirth, displayName }) => {
      const res = await api.post('/identity/auth/otp/verify', { phone, code, dateOfBirth: dateOfBirth || null, displayName: displayName || null }, { auth: false });
      const applied = await applyTokens(res.tokens);
      return { ...applied, isNewAccount: res.isNewAccount, userId: res.userId };
    },
    /** Email + password (staff, and any account that added email sign-in). May return { mfaRequired, mfaToken }. */
    passwordLogin: async (email, password) => {
      const res = await api.post('/identity/auth/token', { grantType: 'password', email, password }, { auth: false });
      return applyTokens(res);
    },
    /** Complete a login MFA challenge (mfaToken from passwordLogin) or confirm enrolment (signed in, mfaToken null). */
    verifyMfa: async (code, mfaToken = null) => {
      const res = await api.post('/identity/auth/mfa/verify', { code, mfaToken }, { auth: !mfaToken });
      return applyTokens(res);
    },
    /** Start TOTP enrolment → { secret, otpAuthUri, recoveryCodes[] }. */
    enableMfa: () => api.post('/identity/me/mfa/enable', {}),
    /** Add email + password to the signed-in phone account. */
    registerEmail: (email, password) => api.post('/identity/auth/register-email', { email, password }),
    logout: () => setTokens(null),
  }), [status, me, tokens, loadMe, applyTokens]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used inside <AuthProvider>');
  return ctx;
}

/** Pick the dashboard for the signed-in user. */
export function homeFor(roles = []) {
  for (const [role, path] of ROLE_HOME) if (roles.includes(role)) return path;
  return '/learn';
}

/**
 * Route guard. `role` (optional) restricts to a platform role; `mfa` requires an MFA-stepped-up token
 * (the API enforces this for tutor/sponsor/staff endpoints, so the UI sends the user to enrol first).
 */
export function RequireAuth({ role, mfa = false, children }) {
  const auth = useAuth();
  const location = useLocation();

  if (auth.status === 'loading') return <FullPageSpinner />;
  if (auth.status !== 'authenticated') {
    return <Navigate to="/auth/sign-in" replace state={{ from: location.pathname + location.search }} />;
  }
  if (role && !auth.hasRole(role)) {
    return <Navigate to="/auth/no-access" replace state={{ role, from: location.pathname }} />;
  }
  if (mfa && !auth.mfa) {
    return <Navigate to="/auth/mfa" replace state={{ from: location.pathname + location.search }} />;
  }
  return children;
}

function FullPageSpinner() {
  return (
    <div className="min-h-dvh grid place-items-center text-muted" role="status" aria-live="polite">
      <div className="flex items-center gap-3">
        <span className="h-5 w-5 animate-spin rounded-full border-2 border-line border-t-royal" />
        Loading…
      </div>
    </div>
  );
}
