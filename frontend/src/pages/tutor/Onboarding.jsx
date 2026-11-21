import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { ShieldCheck } from 'lucide-react';
import { Alert, Button, Card, Checkbox, ErrorAlert, Field, FormGrid, Input, Loading, PageHeader, Select, StatusPill, Textarea, useToast } from '../../components/ui';
import { useAuth } from '../../lib/auth';
import { useAction, useForm } from '../../lib/hooks';
import { formatDateTime, nairaToKobo } from '../../lib/format';
import { tutorApi, tutorKeys } from '../../api/tutor';
import { STAGES, TEACHING_FORMATS, UploadButton, localTimeZone, splitList, stageLabel, stageTone, uploadToPresignedUrl } from './shared';

/*
 * /tutor/onboarding
 *  - No tutor role yet → "Apply to teach" (POST /tutors/applications) grants the role; MFA is required for the rest.
 *  - Tutor role → verification tracker from GET /tutors/me with the four stages the API reports
 *    (identity, qualifications, subject, payout_account) plus the demo-lesson upload that feeds the subject review.
 */
export default function TutorOnboarding() {
  const auth = useAuth();
  if (!auth.hasRole('tutor')) return <ApplyToTeach />;
  return <VerificationTracker />;
}

/* ---------------------------------------------------------------- Apply */

function ApplyToTeach() {
  const auth = useAuth();
  const [done, setDone] = useState(null);
  const { values, bind, set } = useForm({ headline: '', bio: '', subjects: '', languages: 'English', formats: ['one_to_one', 'course'], timeZone: localTimeZone(), hourlyRateNaira: '', accept: false });

  const apply = useAction(
    () => tutorApi.apply({
      headline: values.headline.trim(),
      bio: values.bio.trim(),
      subjects: splitList(values.subjects),
      languages: splitList(values.languages),
      formats: values.formats,
      timeZone: values.timeZone,
      hourlyRate: { amount: nairaToKobo(values.hourlyRateNaira), currency: 'NGN' },
      acceptCodeOfConduct: values.accept,
    }),
    { onSuccess: async (res) => { setDone(res); await auth.refreshMe(); }, silent: true },
  );

  const toggleFormat = (f) => set('formats', values.formats.includes(f) ? values.formats.filter((x) => x !== f) : [...values.formats, f]);
  const valid = values.headline.trim().length >= 10 && values.bio.trim().length >= 50 && splitList(values.subjects).length > 0 && splitList(values.languages).length > 0 && values.formats.length > 0 && values.accept;

  if (done) {
    return (
      <div className="mx-auto max-w-xl">
        <Card className="text-center">
          <span className="mx-auto grid h-12 w-12 place-items-center rounded-full bg-teal-50 text-teal-600"><ShieldCheck className="h-6 w-6" /></span>
          <h1 className="mt-3 text-2xl">Application received</h1>
          <p className="mt-2 text-sm text-muted">Your tutor profile is <StatusPill status={done.status} className="align-middle" />. Next, set up two-step verification: every tutor action (availability, courses, earnings) requires it.</p>
          <div className="mt-5 flex flex-wrap justify-center gap-2">
            <Button to="/auth/mfa" state={{ from: '/tutor/onboarding' }}>Set up two-step verification</Button>
            <Button variant="secondary" to="/tutor/onboarding">Continue to verification</Button>
          </div>
        </Card>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-2xl">
      <PageHeader title="Apply to teach" description="Tell learners who you are and what you teach. Identity, credentials and a short demo lesson follow after this step." />
      <ErrorAlert error={apply.error} className="mb-4" />
      <Card as="form" onSubmit={(e) => { e.preventDefault(); apply.mutate(); }} className="space-y-4">
        <Field label="Headline" hint="10–160 characters, e.g. “Data analyst teaching Excel and SQL to beginners”." required error={apply.error?.field?.('headline')}>
          {(p) => <Input {...p} {...bind('headline')} maxLength={160} />}
        </Field>
        <Field label="Bio" hint={`${values.bio.trim().length}/4000 characters, at least 50.`} required error={apply.error?.field?.('bio')}>
          {(p) => <Textarea {...p} {...bind('bio')} rows={5} maxLength={4000} />}
        </Field>
        <FormGrid>
          <Field label="Subjects" hint="Comma-separated, up to 20." required error={apply.error?.field?.('subjects')}>
            {(p) => <Input {...p} {...bind('subjects')} placeholder="Excel, SQL, Power BI" />}
          </Field>
          <Field label="Languages" hint="Comma-separated." required>
            {(p) => <Input {...p} {...bind('languages')} placeholder="English, Yoruba" />}
          </Field>
          <Field label="Time zone" required>
            {(p) => <Input {...p} {...bind('timeZone')} placeholder="Africa/Lagos" />}
          </Field>
          <Field label="Hourly rate (₦)" hint="You can refine per-format prices later." required error={apply.error?.field?.('hourlyRate')}>
            {(p) => <Input {...p} {...bind('hourlyRateNaira')} inputMode="numeric" placeholder="5000" />}
          </Field>
        </FormGrid>
        <Field label="Formats you teach" required>
          <div className="flex flex-wrap gap-2">
            {TEACHING_FORMATS.map(([value, label]) => (
              <button key={value} type="button" onClick={() => toggleFormat(value)} aria-pressed={values.formats.includes(value)} className={`min-h-10 rounded-chip border px-3 text-sm font-semibold ${values.formats.includes(value) ? 'border-royal bg-royal text-white' : 'border-line bg-white text-navy'}`}>
                {label}
              </button>
            ))}
          </div>
        </Field>
        <Checkbox label="I accept the tutor code of conduct: sessions stay on the platform, no contact details or payments off-platform, and I turn up on time." checked={values.accept} onChange={(e) => set('accept', e.target.checked)} />
        <Button type="submit" size="lg" loading={apply.isPending} disabled={!valid}>Submit application</Button>
      </Card>
    </div>
  );
}

/* ---------------------------------------------------------------- Tracker */

function VerificationTracker() {
  const auth = useAuth();
  const me = useQuery({ queryKey: tutorKeys.me, queryFn: tutorApi.me, enabled: auth.mfa });

  if (!auth.mfa) {
    return (
      <div className="mx-auto max-w-xl">
        <PageHeader title="Verification" />
        <Alert tone="warn" title="Two-step verification required">
          Tutor endpoints need an authenticator-confirmed session. <Link to="/auth/mfa" state={{ from: '/tutor/onboarding' }} className="font-semibold">Set it up now</Link>.
        </Alert>
      </div>
    );
  }
  if (me.isPending) return <Loading />;
  if (me.isError) return <ErrorAlert error={me.error} />;

  const d = me.data;
  const stage = (name) => d.verification?.find((s) => s.name === name) || { status: 'not_started' };
  const done = (d.verification || []).filter((s) => s.status === 'passed').length;

  return (
    <div className="mx-auto max-w-3xl">
      <PageHeader title="Become a verified tutor" description={`${done} of ${STAGES.length} stages complete. Your profile is ${d.status}; learners can book you once every stage passes.`} />
      <ol className="space-y-4">
        <StageCard meta={STAGES[0]} stage={stage('identity')} index={1}>
          {stage('identity').status !== 'passed' ? <IdentityForm /> : null}
        </StageCard>
        <StageCard meta={STAGES[1]} stage={stage('qualifications')} index={2}>
          <CredentialsPanel credentials={d.credentials || []} />
        </StageCard>
        <StageCard meta={STAGES[2]} stage={stage('subject')} index={3}>
          <DemoLessonPanel stage={stage('subject')} />
        </StageCard>
        <StageCard meta={STAGES[3]} stage={stage('payout_account')} index={4}>
          <PayoutAccountForm current={d.payoutAccountMasked} />
        </StageCard>
      </ol>
      {done === STAGES.length ? (
        <Alert tone="success" className="mt-6" title="You are verified">
          Set your <Link to="/tutor/profile" className="font-semibold">availability and pricing</Link>, then <Link to="/tutor/courses" className="font-semibold">publish a course</Link>.
        </Alert>
      ) : null}
    </div>
  );
}

function StageCard({ meta, stage, index, children }) {
  return (
    <li>
      <Card>
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="flex items-start gap-3">
            <span className="grid h-8 w-8 shrink-0 place-items-center rounded-full bg-surface text-sm font-bold text-navy">{index}</span>
            <div>
              <h2 className="text-lg">{meta.label}</h2>
              <p className="text-sm text-muted">{meta.next}</p>
              {stage.completedAt ? <p className="mt-1 text-xs text-muted">Completed {formatDateTime(stage.completedAt)}</p> : null}
              {stage.reviewDueAt && stage.status !== 'passed' ? <p className="mt-1 text-xs text-muted">Review due {formatDateTime(stage.reviewDueAt)}</p> : null}
            </div>
          </div>
          <StatusPill status={stage.status} tone={stageTone(stage.status)} label={stageLabel(stage.status)} />
        </div>
        {children ? <div className="mt-4 border-t border-line pt-4">{children}</div> : null}
      </Card>
    </li>
  );
}

function IdentityForm() {
  const auth = useAuth();
  const { values, bind } = useForm({ fullName: auth.me?.displayName || '', nin: '', bvn: '', selfieBlobKey: '' });
  const [result, setResult] = useState(null);
  const check = useAction(() => tutorApi.identityCheck({ ...values, nin: values.nin.trim(), bvn: values.bvn.trim(), selfieBlobKey: values.selfieBlobKey.trim() || `selfies/${crypto.randomUUID()}.jpg` }), {
    invalidate: [tutorKeys.me],
    onSuccess: setResult,
    silent: true,
  });
  const valid = /^\d{11}$/.test(values.nin) && /^\d{11}$/.test(values.bvn) && values.fullName.trim().length >= 3;
  return (
    <form onSubmit={(e) => { e.preventDefault(); check.mutate(); }} className="space-y-4">
      <ErrorAlert error={check.error} />
      {result ? (
        <Alert tone={result.passed ? 'success' : 'danger'} title={result.passed ? 'Identity verified' : 'Identity check failed'}>
          {result.passed ? `Verified by ${result.provider} (ref ${result.reference}).` : result.failureReason || 'The provider could not match your details.'}
        </Alert>
      ) : null}
      <FormGrid>
        <Field label="Full name (as on your ID)" required>{(p) => <Input {...p} {...bind('fullName')} autoComplete="name" />}</Field>
        <Field label="Selfie reference" hint="Blob key of your liveness selfie. Leave blank to let the app generate one; the KYC adapter is a stub locally.">{(p) => <Input {...p} {...bind('selfieBlobKey')} />}</Field>
        <Field label="NIN" hint="11 digits. Encrypted at rest and never logged." required>{(p) => <Input {...p} {...bind('nin')} inputMode="numeric" maxLength={11} autoComplete="off" />}</Field>
        <Field label="BVN" hint="11 digits." required>{(p) => <Input {...p} {...bind('bvn')} inputMode="numeric" maxLength={11} autoComplete="off" />}</Field>
      </FormGrid>
      <Button type="submit" loading={check.isPending} disabled={!valid}>Verify identity</Button>
    </form>
  );
}

function CredentialsPanel({ credentials }) {
  const qc = useQueryClient();
  const toast = useToast();
  const { values, bind, reset } = useForm({ type: 'degree', issuer: '', title: '' });
  const [file, setFile] = useState(null);
  const [progress, setProgress] = useState(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  async function submit(e) {
    e.preventDefault();
    if (!file) return;
    setBusy(true);
    setError(null);
    try {
      const res = await tutorApi.addCredential({ type: values.type, issuer: values.issuer.trim(), title: values.title.trim(), contentType: file.type || 'application/pdf' });
      await uploadToPresignedUrl(res.uploadUrl, file, file.type, setProgress);
      toast.success('Credential uploaded', `Review due ${formatDateTime(res.reviewDueAt)}.`);
      reset();
      setFile(null);
      qc.invalidateQueries({ queryKey: tutorKeys.me });
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
      setProgress(null);
    }
  }

  return (
    <div className="space-y-4">
      {credentials.length ? (
        <ul className="divide-y divide-line rounded-card border border-line">
          {credentials.map((c) => (
            <li key={c.id} className="flex flex-wrap items-center justify-between gap-2 px-4 py-3 text-sm">
              <div>
                <div className="font-semibold text-navy">{c.title}</div>
                <div className="text-muted">{c.issuer} · {c.type.replace('_', ' ')}{c.reviewNote ? ` · “${c.reviewNote}”` : ''}</div>
              </div>
              <StatusPill status={c.status} />
            </li>
          ))}
        </ul>
      ) : <p className="text-sm text-muted">No credentials yet. Add at least one degree, certificate or work-history document.</p>}
      <form onSubmit={submit} className="space-y-3">
        <ErrorAlert error={error} />
        <FormGrid cols={3}>
          <Field label="Type">{(p) => (
            <Select {...p} {...bind('type')}>
              <option value="degree">Degree</option>
              <option value="certificate">Certificate</option>
              <option value="work_history">Work history</option>
              <option value="linkedin">LinkedIn export</option>
            </Select>
          )}</Field>
          <Field label="Issuer" required>{(p) => <Input {...p} {...bind('issuer')} placeholder="University of Lagos" />}</Field>
          <Field label="Title" required>{(p) => <Input {...p} {...bind('title')} placeholder="B.Sc. Computer Science" />}</Field>
        </FormGrid>
        <div className="flex flex-wrap items-center gap-3">
          <UploadButton accept=".pdf,image/*" label={file ? file.name : 'Choose document'} onFile={setFile} progress={progress} busy={busy} />
          <Button type="submit" loading={busy} disabled={!file || !values.issuer.trim() || !values.title.trim()}>Upload credential</Button>
        </div>
      </form>
    </div>
  );
}

function DemoLessonPanel({ stage }) {
  const qc = useQueryClient();
  const toast = useToast();
  const [progress, setProgress] = useState(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  async function onFile(file) {
    const contentType = file.type === 'video/webm' ? 'video/webm' : 'video/mp4';
    setBusy(true);
    setError(null);
    try {
      const res = await tutorApi.demoLesson(contentType);
      await uploadToPresignedUrl(res.uploadUrl, file, contentType, setProgress);
      toast.success('Demo lesson submitted', `A subject reviewer will respond by ${formatDateTime(res.reviewDueAt)}.`);
      qc.invalidateQueries({ queryKey: tutorKeys.me });
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
      setProgress(null);
    }
  }

  if (stage.status === 'passed') return <p className="text-sm text-muted">Approved. You can teach the subjects on your profile.</p>;
  return (
    <div className="space-y-3">
      <ErrorAlert error={error} />
      <p className="text-sm text-muted">Record a 5-minute lesson on one of your subjects (MP4 or WebM). {stage.status === 'in_review' ? 'Your current upload is with a reviewer; uploading again replaces it.' : ''}{stage.status === 'failed' ? 'The previous demo was not approved. Upload a new one to be reviewed again.' : ''}</p>
      <UploadButton accept="video/mp4,video/webm" label="Upload demo lesson" onFile={onFile} progress={progress} busy={busy} variant="primary" />
    </div>
  );
}

export function PayoutAccountForm({ current, onSaved }) {
  const { values, bind, reset } = useForm({ bankCode: '', accountNumber: '' });
  const [result, setResult] = useState(null);
  const save = useAction(() => tutorApi.setPayoutAccount({ bankCode: values.bankCode.trim(), accountNumber: values.accountNumber.trim() }), {
    invalidate: [tutorKeys.me],
    onSuccess: (res) => { setResult(res); reset(); onSaved?.(res); },
    silent: true,
  });
  return (
    <form onSubmit={(e) => { e.preventDefault(); save.mutate(); }} className="space-y-3">
      <ErrorAlert error={save.error} />
      {current ? <p className="text-sm text-muted">Current account: <span className="font-semibold text-navy">{current}</span>. Saving a new one replaces it.</p> : null}
      {result ? (
        <Alert tone={result.nameMatched ? 'success' : 'warn'} title={result.nameMatched ? `Resolved: ${result.accountName}` : `Name mismatch: ${result.accountName}`}>
          {result.nameMatched ? `Payouts will go to ${result.accountNumberMasked}.` : 'The account name does not match your verified identity, so payouts to it will be skipped until it does.'}
        </Alert>
      ) : null}
      <FormGrid>
        <Field label="Bank code" hint="CBN/NIP bank code, e.g. 058 for GTBank." required>{(p) => <Input {...p} {...bind('bankCode')} inputMode="numeric" />}</Field>
        <Field label="Account number" hint="10-digit NUBAN." required>{(p) => <Input {...p} {...bind('accountNumber')} inputMode="numeric" maxLength={10} />}</Field>
      </FormGrid>
      <Button type="submit" loading={save.isPending} disabled={!values.bankCode.trim() || !/^\d{10}$/.test(values.accountNumber.trim())}>Resolve and save</Button>
    </form>
  );
}
