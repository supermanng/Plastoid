import { useEffect, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { Button, Card, ErrorAlert, Field, Input } from '../../components/ui';
import { homeFor, useAuth } from '../../lib/auth';
import { AppLink, navigateTo } from '../../lib/navigation';

/** Email + password sign-in (staff, and any account that added email sign-in). MFA challenge follows on the module's /auth/mfa. */
export default function StaffSignIn() {
  const auth = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const from = location.state?.from;
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (auth.isAuthenticated && auth.mfa) navigateTo(from || homeFor(auth.roles), navigate, { replace: true });
  }, [auth.isAuthenticated, auth.mfa, auth.roles, from, navigate]);

  async function submit(e) {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      const result = await auth.passwordLogin(email.trim(), password);
      if (result.mfaRequired) {
        navigate('/auth/mfa', { replace: true, state: { mfaToken: result.mfaToken, from } });
      } else {
        navigateTo(from || homeFor(result.me?.roles || []), navigate, { replace: true });
      }
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="mx-auto max-w-md px-4 py-10 sm:px-6 lg:py-16">
      <Card className="p-6 sm:p-8">
        <h1 className="text-2xl">Sign in with email</h1>
        <p className="mt-1 text-sm text-muted">For Plastoids staff and accounts with email sign-in. You will confirm with your authenticator app next.</p>
        <ErrorAlert error={error} className="mt-4" />
        <form onSubmit={submit} className="mt-6 space-y-4">
          <Field label="Email" required>
            {(p) => <Input {...p} type="email" autoComplete="username" value={email} onChange={(e) => setEmail(e.target.value)} autoFocus />}
          </Field>
          <Field label="Password" required>
            {(p) => <Input {...p} type="password" autoComplete="current-password" value={password} onChange={(e) => setPassword(e.target.value)} />}
          </Field>
          <Button type="submit" className="w-full" size="lg" loading={busy} disabled={!email || !password}>Continue</Button>
        </form>
        <p className="mt-6 text-center text-sm text-muted">
          Learner or tutor? <AppLink to="/auth/sign-in" className="font-semibold">Sign in with your phone</AppLink>
        </p>
      </Card>
    </div>
  );
}
