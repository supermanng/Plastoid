import { useEffect, useState } from 'react';
import QRCode from 'qrcode';
import { useLocation, useNavigate } from 'react-router-dom';
import { Copy, ShieldCheck } from 'lucide-react';
import { Alert, Button, Card, ErrorAlert, Field, Input } from '../../components/ui';
import { homeFor, useAuth } from '../../lib/auth';

/*
 * Two jobs on one page:
 *  - Login challenge: arrived from the password form with state.mfaToken → code + mfaToken → full tokens.
 *  - Enrolment / step-up: signed in without amr=mfa → POST /identity/me/mfa/enable → show secret + recovery codes → confirm code.
 * Tutor, sponsor and staff endpoints all require an MFA-stepped-up token, so RequireAuth(mfa) lands here.
 */
export default function Mfa() {
  const auth = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const mfaToken = location.state?.mfaToken || null;
  const from = location.state?.from;

  const [enrolment, setEnrolment] = useState(null);
  const [code, setCode] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const [copied, setCopied] = useState(false);

  const challenge = Boolean(mfaToken);
  const needsEnrolment = !challenge && auth.isAuthenticated && !auth.me?.mfaEnabled;

  useEffect(() => {
    if (!challenge && auth.status === 'anonymous') navigate('/auth/sign-in', { replace: true, state: { from } });
  }, [challenge, auth.status, navigate, from]);

  useEffect(() => {
    if (!challenge && auth.isAuthenticated && auth.mfa) navigate(from || homeFor(auth.roles), { replace: true });
  }, [challenge, auth.isAuthenticated, auth.mfa, auth.roles, from, navigate]);

  async function startEnrolment() {
    setError(null);
    setBusy(true);
    try {
      setEnrolment(await auth.enableMfa());
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  async function submit(e) {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      const result = await auth.verifyMfa(code.trim(), mfaToken);
      navigate(from || homeFor(result.me?.roles || auth.roles), { replace: true });
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  function copySecret() {
    navigator.clipboard?.writeText(enrolment.secret).then(() => {
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    });
  }

  return (
    <div className="mx-auto max-w-lg px-4 py-10 sm:px-6 lg:py-16">
      <Card className="p-6 sm:p-8">
        <div className="flex items-center gap-3">
          <span className="grid h-11 w-11 place-items-center rounded-full bg-teal-50 text-teal-600"><ShieldCheck className="h-6 w-6" /></span>
          <div>
            <h1 className="text-2xl">{challenge ? 'Confirm it is you' : needsEnrolment && !enrolment ? 'Set up two-step verification' : 'Two-step verification'}</h1>
            <p className="text-sm text-muted">
              {challenge
                ? 'Enter the 6-digit code from your authenticator app, or a recovery code.'
                : 'Tutors, sponsors and staff confirm sensitive actions with an authenticator app (Google Authenticator, Authy, 1Password…).'}
            </p>
          </div>
        </div>

        <ErrorAlert error={error} className="mt-4" />

        {needsEnrolment && !enrolment ? (
          <div className="mt-6">
            <Button size="lg" className="w-full" loading={busy} onClick={startEnrolment}>Start setup</Button>
          </div>
        ) : null}

        {enrolment ? (
          <div className="mt-6 space-y-4">
            <Alert tone="info" title="1. Add Plastoids to your authenticator app">
              Scan the QR code or paste the secret key.
            </Alert>
            <div className="flex flex-col items-center gap-3 rounded-card border border-line p-4">
              <QrImage value={enrolment.otpAuthUri} />
              <code className="break-all rounded bg-surface px-3 py-2 text-center text-sm tracking-widest">{enrolment.secret}</code>
              <Button variant="secondary" size="sm" icon={Copy} onClick={copySecret}>{copied ? 'Copied' : 'Copy secret'}</Button>
            </div>
            <Alert tone="warn" title="2. Save your recovery codes">
              Each works once if you lose your phone. Store them somewhere safe; they are not shown again.
              <div className="mt-2 grid grid-cols-2 gap-1 font-mono text-xs text-navy">
                {enrolment.recoveryCodes.map((c) => <span key={c} className="rounded bg-white px-2 py-1">{c}</span>)}
              </div>
            </Alert>
          </div>
        ) : null}

        {challenge || enrolment || (auth.isAuthenticated && auth.me?.mfaEnabled && !auth.mfa) ? (
          <form onSubmit={submit} className="mt-6 space-y-4">
            <Field label={enrolment ? '3. Enter the code from the app' : 'Authenticator code'} required>
              {(p) => <Input {...p} inputMode="numeric" autoComplete="one-time-code" value={code} onChange={(e) => setCode(e.target.value.trim())} placeholder="123456" autoFocus className="text-center text-2xl tracking-[0.4em]" />}
            </Field>
            <Button type="submit" size="lg" className="w-full" loading={busy} disabled={code.length < 6}>{enrolment ? 'Finish setup' : 'Verify'}</Button>
          </form>
        ) : null}

        {auth.isAuthenticated && auth.me?.mfaEnabled && !auth.mfa && !challenge && !enrolment ? (
          <p className="mt-4 text-xs text-muted">Your session was started without two-step verification. Entering a code upgrades it; signing out and in again with email also works.</p>
        ) : null}
      </Card>
    </div>
  );
}

/** Renders the otpauth URI as a QR code locally (the secret never leaves the browser). */
function QrImage({ value }) {
  const [src, setSrc] = useState(null);
  useEffect(() => {
    let cancelled = false;
    QRCode.toDataURL(value, { width: 180, margin: 1, color: { dark: '#003B7A' } }).then((url) => !cancelled && setSrc(url)).catch(() => setSrc(null));
    return () => {
      cancelled = true;
    };
  }, [value]);
  return src ? <img alt="QR code for your authenticator app" width={180} height={180} className="rounded" src={src} /> : <div className="h-[180px] w-[180px] animate-pulse rounded bg-surface" />;
}
