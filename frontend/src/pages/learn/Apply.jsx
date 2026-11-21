import { useEffect, useMemo, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { CheckCircle2, Save, Send, XCircle } from 'lucide-react';
import { Alert, Breadcrumb, Button, Card, CardHeader, Checkbox, ErrorAlert, Field, Input, Loading, Select, Textarea } from '../../components/ui';
import { useAction } from '../../lib/hooks';
import { formatDate, formatMoney, koboOf, titleCase } from '../../lib/format';
import { applications, keys } from '../../api/learn';

/*
 * Sponsored application (blueprint: ≤ 8 fields, save-and-resume, eligibility first).
 * The API accepts any JSON object keyed by field name (max 8, no protected characteristics); the four "facts" below feed
 * the eligibility engine (countryOfResidence, stateOfResidence, educationLevel, employmentStatus).
 */
const STATES = ['Abia', 'Adamawa', 'Akwa Ibom', 'Anambra', 'Bauchi', 'Bayelsa', 'Benue', 'Borno', 'Cross River', 'Delta', 'Ebonyi', 'Edo', 'Ekiti', 'Enugu', 'FCT', 'Gombe', 'Imo', 'Jigawa', 'Kaduna', 'Kano', 'Katsina', 'Kebbi', 'Kogi', 'Kwara', 'Lagos', 'Nasarawa', 'Niger', 'Ogun', 'Ondo', 'Osun', 'Oyo', 'Plateau', 'Rivers', 'Sokoto', 'Taraba', 'Yobe', 'Zamfara'];
const EDUCATION = ['secondary', 'nce_ond', 'hnd_bsc', 'postgraduate', 'other'];
const EMPLOYMENT = ['student', 'unemployed', 'employed', 'self_employed'];

const FIELDS = [
  { key: 'countryOfResidence', label: 'Country of residence', type: 'select', options: ['Nigeria', 'Other'], required: true },
  { key: 'stateOfResidence', label: 'State of residence', type: 'select', options: STATES, required: true },
  { key: 'educationLevel', label: 'Highest education', type: 'select', options: EDUCATION, required: true },
  { key: 'employmentStatus', label: 'Employment status', type: 'select', options: EMPLOYMENT, required: true },
  { key: 'motivation', label: 'Why do you want this seat?', type: 'textarea', required: true, hint: 'Two or three sentences. Reviewers read this blind (no name attached).', max: 1000 },
  { key: 'priorExperience', label: 'Relevant experience so far', type: 'textarea', hint: 'Projects, jobs, courses. “None yet” is a fine answer.', max: 1000 },
  { key: 'weeklyHours', label: 'Hours you can commit per week', type: 'select', options: ['under 5', '5-10', '10-20', '20+'], required: true },
  { key: 'howHeard', label: 'How did you hear about this?', type: 'select', options: ['Social media', 'Campus ambassador', 'Friend or family', 'Sponsor', 'Search', 'Other'] },
];

export default function Apply() {
  const { programmeId } = useParams();
  const navigate = useNavigate();
  const opps = useQuery({ queryKey: [...keys.opportunities, 'all'], queryFn: () => applications.opportunities({ limit: 100 }) });
  const mine = useQuery({ queryKey: keys.applications, queryFn: applications.mine });
  const prog = opps.data?.items?.find((o) => o.programmeId === programmeId);
  const existing = mine.data?.find((a) => a.programmeId === programmeId);

  const [answers, setAnswers] = useState({});
  const [consent, setConsent] = useState(false);
  const [eligibility, setEligibility] = useState(null);
  const [fieldErrors, setFieldErrors] = useState({});

  useEffect(() => {
    if (existing?.answers && typeof existing.answers === 'object') setAnswers((a) => ({ ...existing.answers, ...a }));
  }, [existing]);

  const facts = useMemo(() => ({ countryOfResidence: answers.countryOfResidence || null, stateOfResidence: answers.stateOfResidence || null, educationLevel: answers.educationLevel || null, employmentStatus: answers.employmentStatus || null }), [answers]);
  const check = useAction(() => applications.eligibility(programmeId, facts), { silent: true, onSuccess: setEligibility });

  const save = useAction((submit) => applications.submit(programmeId, answers, submit), {
    invalidate: [keys.applications],
    success: (r, submit) => (submit ? 'Application submitted' : 'Draft saved'),
    onSuccess: (r, submit) => {
      setFieldErrors({});
      if (submit) navigate(`/learn/applications/${r.applicationId}`);
    },
    onError: (e) => setFieldErrors(e?.errors || {}),
  });

  if (opps.isPending || mine.isPending) return <Loading />;
  if (!prog) return <Alert tone="danger" title="Programme not open">This programme is not accepting applications. <Link to="/opportunities">See open opportunities</Link></Alert>;
  if (existing && existing.status !== 'draft') return <Alert tone="info" title="You have already applied">Track it from <Link to={`/learn/applications/${existing.applicationId}`} className="font-semibold">your applications</Link>.</Alert>;

  const missing = FIELDS.filter((f) => f.required && !String(answers[f.key] || '').trim());
  const canSubmit = !missing.length && consent && eligibility?.eligible !== false;
  const factsReady = FIELDS.slice(0, 4).every((f) => answers[f.key]);
  const closed = prog.applicationsCloseAt && new Date(prog.applicationsCloseAt) < new Date();

  return (
    <div className="mx-auto max-w-3xl pb-24">
      <Breadcrumb items={[{ to: '/opportunities', label: 'Opportunities' }, { to: `/opportunities/${programmeId}`, label: prog.name }, { label: 'Apply' }]} />
      <h1 className="mt-2 text-2xl">Apply: {prog.name}</h1>
      <p className="text-sm text-muted">{prog.courseTitle || 'Custom programme'} · {prog.sponsorName} · {prog.seatsOpen} of {prog.seats} seats open{prog.applicationsCloseAt ? ` · closes ${formatDate(prog.applicationsCloseAt)}` : ''}{koboOf(prog.coPayPerSeat) ? ` · co-payment ${formatMoney(prog.coPayPerSeat)}` : ' · fully sponsored'}</p>
      {closed ? <Alert tone="danger" className="mt-4" title="Applications closed">The deadline was {formatDate(prog.applicationsCloseAt)}.</Alert> : null}

      <Card className="mt-6">
        <CardHeader title="1. Check your eligibility" description="Age comes from your verified profile; these four facts are self-declared and checked instantly." />
        <div className="grid gap-4 sm:grid-cols-2">
          {FIELDS.slice(0, 4).map((f) => <FieldInput key={f.key} f={f} value={answers[f.key]} error={fieldErrors[f.key]?.[0]?.message} onChange={(v) => { setAnswers((a) => ({ ...a, [f.key]: v })); setEligibility(null); }} />)}
        </div>
        <div className="mt-4 flex flex-wrap items-center gap-3">
          <Button variant="secondary" disabled={!factsReady} loading={check.isPending} onClick={() => check.mutate()}>Check eligibility</Button>
          {eligibility ? (
            eligibility.eligible ? <span className="inline-flex items-center gap-1 text-sm font-semibold text-teal-600"><CheckCircle2 className="h-4 w-4" /> You qualify</span> : <span className="inline-flex items-center gap-1 text-sm font-semibold text-danger"><XCircle className="h-4 w-4" /> Not eligible</span>
          ) : null}
        </div>
        <ErrorAlert error={check.error} className="mt-3" />
        {eligibility && !eligibility.eligible ? (
          <Alert tone="danger" className="mt-3" title="Why not">
            <ul className="list-disc pl-5">{eligibility.reasons.map((r) => <li key={r}>{titleCase(r)}</li>)}</ul>
            {eligibility.alternatives?.length ? (
              <div className="mt-2">Alternatives you may qualify for: {eligibility.alternatives.map((a) => <Link key={a.programmeId} to={`/opportunities/${a.programmeId}`} className="mr-2 font-semibold">{a.name}</Link>)}</div>
            ) : <div className="mt-2"><Link to="/courses" className="font-semibold">Browse paid courses</Link> instead.</div>}
          </Alert>
        ) : null}
      </Card>

      <Card className="mt-6">
        <CardHeader title="2. About you" description="Four short answers. At most eight fields in total; we never ask about gender, religion, ethnicity or disability." />
        <div className="grid gap-4">
          {FIELDS.slice(4).map((f) => <FieldInput key={f.key} f={f} value={answers[f.key]} error={fieldErrors[f.key]?.[0]?.message} onChange={(v) => setAnswers((a) => ({ ...a, [f.key]: v }))} />)}
        </div>
      </Card>

      <Card className="mt-6">
        <CardHeader title="3. Consent" />
        <Checkbox checked={consent} onChange={(e) => setConsent(e.target.checked)} label={<span>I confirm my answers are true, and I agree to the programme's code of conduct and to Plastoids sharing my application with {prog.sponsorName} for selection. <Link to="/privacy" className="font-semibold">Privacy notice</Link></span>} />
        {prog.hasEntryAssessment ? <Alert tone="info" className="mt-3">This programme has a timed entry assessment (about 30 minutes). You take it from your application page after submitting.</Alert> : null}
        <ErrorAlert error={save.error} className="mt-3" />
      </Card>

      <div className="fixed inset-x-0 bottom-14 z-20 border-t border-line bg-white/95 p-3 backdrop-blur md:bottom-0 md:left-64">
        <div className="mx-auto flex max-w-3xl items-center justify-between gap-3">
          <span className="hidden text-xs text-muted sm:block">{missing.length ? `${missing.length} required field${missing.length === 1 ? '' : 's'} left` : 'Ready to submit'}</span>
          <div className="ml-auto flex gap-2">
            <Button variant="secondary" icon={Save} loading={save.isPending && save.variables === false} disabled={closed} onClick={() => save.mutate(false)}>Save draft</Button>
            <Button icon={Send} loading={save.isPending && save.variables === true} disabled={!canSubmit || closed} onClick={() => save.mutate(true)}>Submit application</Button>
          </div>
        </div>
      </div>
    </div>
  );
}

function FieldInput({ f, value, error, onChange }) {
  return (
    <Field label={f.label} hint={f.hint} error={error} required={f.required}>
      {(p) =>
        f.type === 'select' ? (
          <Select {...p} value={value || ''} onChange={(e) => onChange(e.target.value)}>
            <option value="">Select…</option>
            {f.options.map((o) => <option key={o} value={o}>{titleCase(o)}</option>)}
          </Select>
        ) : f.type === 'textarea' ? (
          <Textarea {...p} value={value || ''} maxLength={f.max} onChange={(e) => onChange(e.target.value)} rows={4} />
        ) : (
          <Input {...p} value={value || ''} onChange={(e) => onChange(e.target.value)} />
        )
      }
    </Field>
  );
}
