import { useState } from 'react';
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { CalendarDays, CheckCircle2, ClipboardList, Sparkles, Users, XCircle } from 'lucide-react';
import { Alert, Button, Card, CardHeader, EmptyState, ErrorAlert, Field, FormGrid, KeyValue, Loading, Select, StatusPill } from '../../../../shared/components/ui';
import { useAuth } from '../../../../shared/lib/auth';
import { useAction } from '../../../../shared/lib/hooks';
import { formatDate, formatMoney, koboOf } from '../../../../shared/lib/format';
import { UnitPrice } from '../../../../shared/lib/units.jsx';
import { checkEligibility, describeReason, describeRule, EDUCATION_LEVELS, EMPLOYMENT_STATUSES, findOpportunity, NIGERIAN_STATES, queryKeys } from '../../../../shared/api/public';

/** /opportunities/:programmeId — details, eligibility checklist and instant check, apply CTA. */
export default function OpportunityDetail() {
  const { programmeId } = useParams();
  const query = useQuery({ queryKey: queryKeys.opportunity(programmeId), queryFn: () => findOpportunity(programmeId), staleTime: 5 * 60_000 });

  if (query.isPending) return <Loading className="min-h-[50vh]" />;
  if (query.isError) return <div className="mx-auto max-w-3xl px-4 py-10 sm:px-6"><ErrorAlert error={query.error} /></div>;
  const o = query.data;
  if (!o) {
    return (
      <div className="mx-auto max-w-3xl px-4 py-10 sm:px-6">
        <EmptyState icon={Sparkles} title="This opportunity is no longer open" description="It may have closed or filled all its seats." action={<Button to="/opportunities">See open opportunities</Button>} />
      </div>
    );
  }

  const rules = o.eligibilityRules && typeof o.eligibilityRules === 'object' ? Object.entries(o.eligibilityRules).map(([k, v]) => describeRule(k, v)).filter(Boolean) : [];
  const free = !koboOf(o.coPayPerSeat);
  const closed = o.applicationsCloseAt && new Date(o.applicationsCloseAt) < new Date();

  return (
    <div className="mx-auto max-w-[1280px] px-4 py-8 sm:px-6">
      <nav className="text-sm text-muted" aria-label="Breadcrumb"><Link to="/opportunities" className="hover:text-navy">Sponsored opportunities</Link> / <span className="text-navy">{o.sponsorName}</span></nav>
      <div className="mt-3 grid gap-8 lg:grid-cols-[1fr_380px]">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <StatusPill tone="success" status="sponsored" label={free ? 'Fully sponsored' : `${o.sponsorSharePercent}% sponsored`} />
            {closed ? <StatusPill status="closed" label="Applications closed" /> : o.seatsOpen === 0 ? <StatusPill status="waitlist" label="Waitlist only" /> : <StatusPill tone="info" status="open" label={`${o.seatsOpen} of ${o.seats} seats open`} />}
          </div>
          <h1 className="mt-3 text-3xl sm:text-4xl">{o.name}</h1>
          <p className="mt-2 text-base text-muted">Sponsored by <strong className="text-navy">{o.sponsorName}</strong>{o.courseTitle ? <> · course: <Link to={`/courses?q=${encodeURIComponent(o.courseTitle)}`} className="font-semibold">{o.courseTitle}</Link></> : null}</p>

          <KeyValue
            className="mt-6"
            cols={3}
            items={[
              ['Seats', `${o.seats} total · ${o.seatsOpen} open`],
              ['Your cost', free ? 'Nothing. Fully sponsored.' : <><UnitPrice money={o.coPayPerSeat} size="sm" inline /> co-payment per seat</>],
              ['Entry assessment', o.hasEntryAssessment ? 'Yes, timed, online' : 'None'],
              ['Applications close', o.applicationsCloseAt ? formatDate(o.applicationsCloseAt) : 'Until seats fill'],
              ['Programme runs', o.startsAt ? `${formatDate(o.startsAt)}${o.endsAt ? ` → ${formatDate(o.endsAt)}` : ''}` : 'Dates to be announced'],
            ]}
          />

          <section className="mt-10">
            <h2 className="text-xl">Who can apply</h2>
            {rules.length ? (
              <ul className="mt-3 space-y-2">
                {rules.map((r) => <li key={r} className="flex items-start gap-2 text-sm text-navy"><CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0 text-teal" aria-hidden /> {r}</li>)}
              </ul>
            ) : <p className="mt-2 text-sm text-muted">Open to everyone with a Plastoids account (18+).</p>}
            <p className="mt-3 text-xs text-muted">Rules may only use age, residency, state, education and employment status. Plastoids refuses rules on protected characteristics.</p>
          </section>

          <section className="mt-10">
            <h2 className="text-xl">How it works</h2>
            <ol className="mt-3 grid gap-3 sm:grid-cols-2">
              {[
                [ClipboardList, 'Apply', 'A short form of at most eight fields. Save a draft and finish later.'],
                [Sparkles, o.hasEntryAssessment ? 'Entry assessment' : 'Scoring', o.hasEntryAssessment ? 'A timed online test taken from your phone; one attempt.' : 'Reviewers score your answers against the sponsor\'s rubric.'],
                [Users, 'Decision', 'Accepted, waitlisted or not this time, with a reason. You can appeal within seven days.'],
                [CalendarDays, 'Offer', 'Accept within five days (and pay any co-payment within seven) or the seat moves to the waitlist.'],
              ].map(([Icon, t, d]) => (
                <li key={t} className="flex gap-3 rounded-card border border-line bg-white p-4"><Icon className="h-5 w-5 shrink-0 text-royal" aria-hidden /><div><div className="font-semibold text-navy">{t}</div><div className="mt-0.5 text-sm text-muted">{d}</div></div></li>
              ))}
            </ol>
          </section>
        </div>

        <aside><div className="lg:sticky lg:top-20"><EligibilityPanel o={o} closed={closed} /></div></aside>
      </div>
    </div>
  );
}

function EligibilityPanel({ o, closed }) {
  const auth = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [facts, setFacts] = useState({ countryOfResidence: 'NG', stateOfResidence: '', educationLevel: '', employmentStatus: '' });
  const [outcome, setOutcome] = useState(null);

  const check = useAction(() => checkEligibility(o.programmeId, { ...facts, stateOfResidence: facts.stateOfResidence || null, educationLevel: facts.educationLevel || null, employmentStatus: facts.employmentStatus || null }), {
    silent: true,
    onSuccess: setOutcome,
  });

  const applyPath = `/learn/apply/${o.programmeId}`;
  function apply() {
    if (!auth.isAuthenticated) return navigate('/auth/sign-in', { state: { from: applyPath } });
    return navigate(applyPath);
  }

  return (
    <Card>
      <CardHeader title="Check your eligibility" description="Instant, based on what you tell us and your profile. Nothing is submitted." />
      {!auth.isAuthenticated ? (
        <Alert tone="info" className="mb-3">Sign in to run the check; your date of birth from your profile is used for age rules.</Alert>
      ) : null}
      <form className="space-y-3" onSubmit={(e) => { e.preventDefault(); if (!auth.isAuthenticated) return navigate('/auth/sign-in', { state: { from: location.pathname } }); return check.mutate(); }}>
        <FormGrid cols={1}>
          <Field label="Country of residence">{(p) => <Select {...p} value={facts.countryOfResidence} onChange={(e) => setFacts({ ...facts, countryOfResidence: e.target.value })}><option value="NG">Nigeria</option><option value="GH">Ghana</option><option value="other">Other</option></Select>}</Field>
          <Field label="State of residence">{(p) => <Select {...p} value={facts.stateOfResidence} onChange={(e) => setFacts({ ...facts, stateOfResidence: e.target.value })}><option value="">Select…</option>{NIGERIAN_STATES.map((s) => <option key={s} value={s}>{s}</option>)}</Select>}</Field>
          <Field label="Highest education">{(p) => <Select {...p} value={facts.educationLevel} onChange={(e) => setFacts({ ...facts, educationLevel: e.target.value })}><option value="">Select…</option>{EDUCATION_LEVELS.map(([v, l]) => <option key={v} value={v}>{l}</option>)}</Select>}</Field>
          <Field label="Employment status">{(p) => <Select {...p} value={facts.employmentStatus} onChange={(e) => setFacts({ ...facts, employmentStatus: e.target.value })}><option value="">Select…</option>{EMPLOYMENT_STATUSES.map(([v, l]) => <option key={v} value={v}>{l}</option>)}</Select>}</Field>
        </FormGrid>
        <ErrorAlert error={check.error} />
        <Button type="submit" variant="secondary" className="w-full" loading={check.isPending}>{auth.isAuthenticated ? 'Check eligibility' : 'Sign in to check'}</Button>
      </form>

      {outcome ? (
        outcome.eligible ? (
          <Alert tone="success" className="mt-4" title="You qualify">You meet every rule for this programme{outcome.evaluationMs !== undefined ? ` (checked in ${outcome.evaluationMs} ms)` : ''}.</Alert>
        ) : (
          <div className="mt-4 space-y-3">
            <Alert tone="warn" title="Not eligible for this programme">
              <ul className="mt-1 space-y-1">{(outcome.reasons || []).map((r) => <li key={r} className="flex gap-2"><XCircle className="mt-0.5 h-4 w-4 shrink-0 text-danger" aria-hidden /> {describeReason(r)}</li>)}</ul>
            </Alert>
            {outcome.alternatives?.length ? (
              <div>
                <div className="text-sm font-semibold text-navy">Alternatives you may qualify for</div>
                <ul className="mt-2 space-y-2">
                  {outcome.alternatives.map((a) => (
                    <li key={a.programmeId}>
                      <Link to={`/opportunities/${a.programmeId}`} className="block rounded-card border border-line p-3 text-sm hover:border-royal">
                        <div className="font-semibold text-navy">{a.name}</div>
                        <div className="text-xs text-muted">{a.sponsorName}{a.applicationsCloseAt ? ` · closes ${formatDate(a.applicationsCloseAt)}` : ''}</div>
                      </Link>
                    </li>
                  ))}
                </ul>
              </div>
            ) : null}
            <p className="text-sm text-muted">Paid options: <Link to={o.courseTitle ? `/courses?q=${encodeURIComponent(o.courseTitle)}` : '/courses'}>browse courses</Link>.</p>
          </div>
        )
      ) : null}

      <div className="mt-5 border-t border-line pt-4">
        <Button className="w-full" size="lg" disabled={closed} onClick={apply}>{closed ? 'Applications closed' : o.seatsOpen === 0 ? 'Join the waitlist' : 'Apply now'}</Button>
        <p className="mt-2 text-center text-xs text-muted">{free ? 'No payment needed.' : `Co-payment of ${formatMoney(o.coPayPerSeat)} is due only after you accept an offer.`}</p>
      </div>
    </Card>
  );
}
