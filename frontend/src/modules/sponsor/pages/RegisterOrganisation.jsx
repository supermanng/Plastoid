import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Building2, ShieldCheck } from 'lucide-react';
import { Alert, Button, Card, ErrorAlert, Field, FormGrid, Input, PageHeader, Select } from '../../../shared/components/ui';
import { useAuth } from '../../../shared/lib/auth';
import { useForm } from '../../../shared/lib/hooks';
import { sponsorApi } from '../api/sponsor';

const TYPES = [
  ['sponsor', 'Sponsor (foundation, NGO, CSR programme)'],
  ['employer', 'Employer (hiring partner)'],
  ['institution', 'Institution (school, university)'],
  ['hub', 'Hub (training centre)'],
  ['provider', 'Training provider: teaches, does not sponsor'],
];

/** POST /identity/organisations → { organisationId, verificationStatus }. Sponsor, employer and provider types grant the sponsor role (the organisation portal). */
export default function RegisterOrganisation() {
  const auth = useAuth();
  const navigate = useNavigate();
  const { values, bind } = useForm({ name: '', type: 'sponsor', cacNumber: '', signatoryName: auth.me?.displayName || '', signatoryEmail: auth.me?.email || '' });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const [done, setDone] = useState(null);

  async function submit(e) {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      const res = await sponsorApi.registerOrganisation({
        name: values.name.trim(),
        type: values.type,
        cacNumber: values.cacNumber.trim(),
        signatoryName: values.signatoryName.trim(),
        signatoryEmail: values.signatoryEmail.trim(),
      });
      await auth.refreshMe();
      try {
        localStorage.setItem('plastoids.sponsor.org', res.organisationId);
      } catch {
        /* ignore */
      }
      setDone(res);
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  if (done) {
    return (
      <div className="mx-auto max-w-2xl">
        <PageHeader title="Organisation registered" />
        <Card className="space-y-4">
          <Alert tone="success" title={`${values.name} is registered`}>
            Verification status: <span className="font-semibold capitalize">{String(done.verificationStatus).replace(/_/g, ' ')}</span>. Plastoids staff will check the CAC number and the authorised signatory; you will be notified when it is verified.
          </Alert>
          {!auth.mfa ? (
            <Alert tone="warn" title="Two-step verification required">
              Sponsor actions (campaigns, quotes, invoices) need an authenticator app on your account. Set it up now; it takes a minute.
              <div className="mt-3"><Button to="/auth/mfa" size="sm" icon={ShieldCheck} state={{ from: '/sponsor' }}>Set up MFA</Button></div>
            </Alert>
          ) : null}
          <div className="flex flex-wrap gap-2">
            <Button onClick={() => navigate(auth.mfa ? '/' : '/auth/mfa', { state: { from: '/sponsor' } })}>Go to the sponsor area</Button>
            <Button variant="secondary" to="/for-sponsors">How sponsorship works</Button>
          </div>
        </Card>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-2xl">
      <PageHeader
        title="Register your organisation"
        description="Sponsors fund seats on Plastoids programmes; training providers publish courses taught by verified tutors. We verify every organisation (CAC number and authorised signatory) before a campaign or course can be published."
      />
      <Card>
        <div className="mb-4 flex items-center gap-3 text-sm text-muted">
          <span className="grid h-10 w-10 place-items-center rounded-full bg-royal-50 text-royal"><Building2 className="h-5 w-5" /></span>
          Signed in as <span className="font-semibold text-navy">{auth.me?.displayName}</span>. You become the organisation's first owner.
        </div>
        <ErrorAlert error={error} className="mb-4" />
        <form onSubmit={submit} className="space-y-4">
          <Field label="Organisation name" required error={error?.field?.('name')}>
            {(p) => <Input {...p} {...bind('name')} placeholder="Adebayo Foundation" required maxLength={200} />}
          </Field>
          <FormGrid>
            <Field label="Type" required>
              {(p) => (
                <Select {...p} {...bind('type')}>
                  {TYPES.map(([v, l]) => <option key={v} value={v}>{l}</option>)}
                </Select>
              )}
            </Field>
            <Field label="CAC registration number" hint="RC or BN number as on your certificate." required error={error?.field?.('cacNumber')}>
              {(p) => <Input {...p} {...bind('cacNumber')} placeholder="RC1234567" required />}
            </Field>
          </FormGrid>
          <FormGrid>
            <Field label="Authorised signatory" hint="The person who can approve quotes and invoices." required error={error?.field?.('signatoryName')}>
              {(p) => <Input {...p} {...bind('signatoryName')} required />}
            </Field>
            <Field label="Signatory email" required error={error?.field?.('signatoryEmail')}>
              {(p) => <Input {...p} type="email" {...bind('signatoryEmail')} required />}
            </Field>
          </FormGrid>
          <div className="flex flex-wrap items-center justify-between gap-3 pt-2">
            <p className="text-xs text-muted">By registering you accept the <Link to="/terms">terms</Link> on behalf of the organisation.</p>
            <Button type="submit" loading={busy} disabled={!values.name.trim() || !values.cacNumber.trim() || !values.signatoryName.trim() || !values.signatoryEmail.trim()}>Register</Button>
          </div>
        </form>
      </Card>
    </div>
  );
}
