import { useEffect, useState } from 'react';
import { Link, useLocation, useNavigate, useSearchParams } from 'react-router-dom';
import { Phone, ShieldCheck } from 'lucide-react';
import { Alert, Button, Card, ErrorAlert, Field, Input } from '../../components/ui';
import { homeFor, useAuth } from '../../lib/auth';

/*
 * Phone + OTP sign-in / sign-up (blueprint learner journey step 2).
 * 1. Enter phone → POST /identity/auth/otp/request (5 per phone per hour).
 * 2. Enter the 6-digit code. New accounts also give a display name and date of birth (under-18 is refused by the API).
 * In development the SMS is a fake: the code is printed in the API log as "[fake-sms] to ***1234: ...".
 */
export default function SignIn() {
  const auth = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [params] = useSearchParams();
  const signup = params.get('mode') === 'signup';
  const from = location.state?.from;

  const [step, setStep] = useState('phone');
  const [phone, setPhone] = useState('+234');
  const [code, setCode] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [dob, setDob] = useState('');
  const [newAccount, setNewAccount] = useState(signup);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const [expiresIn, setExpiresIn] = useState(null);

  useEffect(() => {
    // Already signed in (e.g. opened the page by hand): go to the dashboard. While a verify call is in flight the
    // submit handler decides where to go (new accounts land on onboarding), so the effect stays out of the way.
    if (auth.isAuthenticated && !busy) navigate(from || homeFor(auth.roles), { replace: true });
  }, [auth.isAuthenticated, auth.roles, from, navigate, busy]);

  const normalised = normalisePhone(phone);

  async function requestCode(e) {
    e?.preventDefault();
    setError(null);
    if (!/^\+\d{10,15}$/.test(normalised)) {
      setError({ message: 'Enter a valid phone number, for example +2348012345678.' });
      return;
    }
    setBusy(true);
    try {
      const res = await auth.requestOtp(normalised);
      setExpiresIn(res?.expiresInSeconds ?? null);
      setStep('code');
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  async function verify(e) {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      const result = await auth.verifyOtp({ phone: normalised, code: code.trim(), dateOfBirth: newAccount ? dob : undefined, displayName: newAccount ? displayName.trim() : undefined });
      navigate(result.isNewAccount ? '/learn/onboarding' : from || homeFor(result.me?.roles || []), { replace: true });
    } catch (err) {
      // The API asks for profile details when the phone is new.
      if (!newAccount && (err?.code === 'registration_details_required' || err?.field?.('dateOfBirth') || err?.field?.('displayName'))) {
        setNewAccount(true);
        setError({ message: 'Looks like you are new here. Add your name and date of birth to create your account.' });
      } else {
        setError(err);
      }
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="mx-auto grid max-w-5xl gap-10 px-4 py-10 sm:px-6 lg:grid-cols-2 lg:py-16">
      <div className="hidden lg:block">
        <h1 className="text-4xl leading-tight">
          Learn a skill. <span className="brand-text">Get sponsored.</span>
        </h1>
        <p className="mt-4 max-w-md text-muted">
          One account for sponsored seats, paid courses and one-to-one tutoring. Sign in with your phone number; no password needed.
        </p>
        <ul className="mt-8 space-y-3 text-sm text-navy">
          <li className="flex gap-3"><ShieldCheck className="h-5 w-5 text-teal" /> Your phone, NIN and date of birth are encrypted and never shared.</li>
          <li className="flex gap-3"><Phone className="h-5 w-5 text-teal" /> Codes arrive by SMS. Five requests an hour per number.</li>
        </ul>
      </div>

      <Card className="w-full max-w-md justify-self-center p-6 sm:p-8 lg:justify-self-end">
        <h2 className="text-2xl">{newAccount ? 'Create your account' : 'Sign in'}</h2>
        <p className="mt-1 text-sm text-muted">{step === 'phone' ? 'We will text you a 6-digit code.' : `Enter the code sent to ${normalised}.`}</p>

        <ErrorAlert error={error} className="mt-4" />

        {step === 'phone' ? (
          <form onSubmit={requestCode} className="mt-6 space-y-4">
            <Field label="Phone number" hint="Nigerian numbers: +234 then your 10 digits." required>
              {(p) => <Input {...p} type="tel" inputMode="tel" autoComplete="tel" value={phone} onChange={(e) => setPhone(e.target.value)} placeholder="+2348012345678" autoFocus />}
            </Field>
            <Button type="submit" className="w-full" size="lg" loading={busy}>Send code</Button>
          </form>
        ) : (
          <form onSubmit={verify} className="mt-6 space-y-4">
            <Field label="6-digit code" hint={expiresIn ? `Expires in ${Math.round(expiresIn / 60)} minutes.` : undefined} required>
              {(p) => <Input {...p} inputMode="numeric" autoComplete="one-time-code" pattern="\d{6}" maxLength={6} value={code} onChange={(e) => setCode(e.target.value.replace(/\D/g, ''))} placeholder="123456" autoFocus className="text-center text-2xl tracking-[0.5em]" />}
            </Field>
            {newAccount ? (
              <>
                <Field label="Your name" required>
                  {(p) => <Input {...p} autoComplete="name" value={displayName} onChange={(e) => setDisplayName(e.target.value)} placeholder="Chiamaka Okafor" />}
                </Field>
                <Field label="Date of birth" hint="You must be 18 or older." required>
                  {(p) => <Input {...p} type="date" autoComplete="bday" value={dob} onChange={(e) => setDob(e.target.value)} max={new Date().toISOString().slice(0, 10)} />}
                </Field>
              </>
            ) : null}
            <Button type="submit" className="w-full" size="lg" loading={busy} disabled={code.length !== 6 || (newAccount && (!displayName.trim() || !dob))}>
              {newAccount ? 'Create account' : 'Sign in'}
            </Button>
            <div className="flex justify-between text-sm">
              <button type="button" className="text-royal hover:underline" onClick={() => { setStep('phone'); setCode(''); setError(null); }}>Change number</button>
              <button type="button" className="text-royal hover:underline" onClick={() => requestCode()} disabled={busy}>Resend code</button>
            </div>
            {!newAccount ? (
              <button type="button" className="w-full text-center text-sm text-muted hover:text-navy" onClick={() => setNewAccount(true)}>New to Plastoids? Create an account</button>
            ) : null}
          </form>
        )}

        {import.meta.env.DEV ? (
          <Alert tone="info" className="mt-6" title="Development">
            SMS is faked locally. Find the code in the API log line starting with <code>[fake-sms]</code>.
          </Alert>
        ) : null}

        <p className="mt-6 text-center text-sm text-muted">
          Plastoids staff? <Link to="/auth/staff" className="font-semibold">Sign in with email</Link>
        </p>
      </Card>
    </div>
  );
}

function normalisePhone(value) {
  let v = String(value || '').replace(/[\s()-]/g, '');
  if (/^0\d{10}$/.test(v)) v = '+234' + v.slice(1);
  if (/^234\d{10}$/.test(v)) v = '+' + v;
  return v;
}
