import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Check, Clock, CreditCard, FileQuestion, Scale } from 'lucide-react';
import { Alert, Breadcrumb, Button, Card, CardHeader, ConfirmDialog, ErrorAlert, KeyValue, Loading, StatusPill } from '../../../../shared/components/ui';
import { useAction } from '../../../../shared/lib/hooks';
import { formatDate, formatDateTime, formatMoney, koboOf, relativeTime, titleCase } from '../../../../shared/lib/format';
import { UnitPrice } from '../../../../shared/lib/units.jsx';
import { applications, goToCheckout, isDevCheckout, keys } from '../../api/learn';
import { applicationLabel } from './Dashboard';
import { APPEAL_DAYS, useProgrammeNames } from './Applications';
import { formatSeconds } from './LessonViews';

const TIMELINE = ['draft', 'submitted', 'accepted', 'offered', 'enrolled'];

export default function ApplicationDetail() {
  const { applicationId } = useParams();
  const apps = useQuery({ queryKey: keys.applications, queryFn: applications.mine });
  const { byId } = useProgrammeNames();
  const app = apps.data?.find((a) => a.applicationId === applicationId);
  const prog = app ? byId[app.programmeId] : null;
  const [confirm, setConfirm] = useState(null); // 'decline' | 'appeal' | 'accept'
  const [coPay, setCoPay] = useState(null);

  const accept = useAction(() => applications.acceptOffer(applicationId), {
    invalidate: [keys.applications, keys.enrolments],
    success: (r) => (r.coPay ? 'Seat held. Complete your co-payment to confirm.' : 'Offer accepted. Welcome to the programme!'),
    onSuccess: (r) => {
      setConfirm(null);
      if (r.coPay) {
        setCoPay(r.coPay);
        if (!isDevCheckout(String(r.coPay.checkoutUrl))) goToCheckout(r.coPay.checkoutUrl);
      }
    },
  });
  const decline = useAction(() => applications.declineOffer(applicationId), { invalidate: [keys.applications], success: 'Offer declined. The seat goes to the next person on the waitlist.', onSuccess: () => setConfirm(null) });
  const appeal = useAction((reason) => applications.appeal(applicationId, reason), { invalidate: [keys.applications], success: 'Appeal filed. Staff review it within a few days.', onSuccess: () => setConfirm(null) });

  if (apps.isPending) return <Loading />;
  if (apps.isError) return <ErrorAlert error={apps.error} />;
  if (!app) return <Alert tone="danger" title="Application not found">It may belong to another account. <Link to="/learn/applications">Back to applications</Link></Alert>;

  const offerOpen = app.status === 'offered' && (!app.offerExpiresAt || new Date(app.offerExpiresAt) > new Date());
  const appealDeadline = app.status === 'rejected' && app.submittedAt ? new Date(new Date(app.submittedAt).getTime() + APPEAL_DAYS * 86400000) : null;
  const stageIndex = TIMELINE.indexOf(app.status === 'pendingCoPay' ? 'offered' : ['merged', 'waitlisted'].includes(app.status) ? 'submitted' : app.status);
  const coPayDue = koboOf(app.coPayAmount) > 0;

  return (
    <div className="mx-auto max-w-4xl">
      <Breadcrumb items={[{ to: '/learn/applications', label: 'My applications' }, { label: prog?.name || 'Application' }]} />
      <div className="mt-2 flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl">{prog?.name || 'Sponsored programme'}</h1>
          {prog ? <p className="text-sm text-muted">{prog.courseTitle || 'Custom programme'} · sponsored by {prog.sponsorName}{prog.startsAt ? ` · starts ${formatDate(prog.startsAt)}` : ''}</p> : null}
        </div>
        <StatusPill status={app.status} label={applicationLabel(app.status)} />
      </div>

      {/* Timeline */}
      {!['rejected', 'declined', 'expired', 'withdrawn'].includes(app.status) ? (
        <ol className="mt-5 flex flex-wrap gap-2">
          {TIMELINE.map((s, i) => (
            <li key={s} className={`flex items-center gap-2 rounded-chip border px-3 py-1 text-xs font-semibold ${i < stageIndex ? 'border-teal/40 bg-teal-50 text-teal-600' : i === stageIndex ? 'border-royal bg-royal text-white' : 'border-line text-muted'}`}>
              {i < stageIndex ? <Check className="h-3 w-3" /> : null}{titleCase(s)}
            </li>
          ))}
        </ol>
      ) : null}

      <div className="mt-6 grid gap-6 lg:grid-cols-[1fr_320px]">
        <div className="space-y-6">
          {/* Offer */}
          {app.status === 'offered' ? (
            <Card className="border-teal/50">
              <CardHeader title="You have been offered a seat" description={app.offerExpiresAt ? `Respond before ${formatDateTime(app.offerExpiresAt)} (${relativeTime(app.offerExpiresAt)}).` : 'Respond within five days.'} />
              {!offerOpen ? <Alert tone="danger" title="Offer expired">The seat has gone to the waitlist.</Alert> : (
                <>
                  <p className="text-sm text-navy">By accepting you agree to the programme's code of conduct: attend live classes, submit assignments on time and keep communication on the platform.</p>
                  {coPayDue ? <Alert tone="info" className="mt-3" title={`Co-payment ${formatMoney(app.coPayAmount)}`}>This seat is {prog?.sponsorSharePercent ?? ''}% sponsored. Accepting holds the seat for 7 days while you pay the balance.</Alert> : null}
                  <ErrorAlert error={accept.error || decline.error} className="mt-3" />
                  <div className="mt-4 flex flex-wrap gap-2">
                    <Button variant="teal" loading={accept.isPending} onClick={() => setConfirm('accept')}>Accept offer{coPayDue ? ' & pay' : ''}</Button>
                    <Button variant="secondary" loading={decline.isPending} onClick={() => setConfirm('decline')}>Decline</Button>
                  </div>
                </>
              )}
            </Card>
          ) : null}

          {coPay || app.status === 'pendingCoPay' ? (
            <Card className="border-warn/40">
              <CardHeader title="Co-payment due" description={coPay ? `Seat held until ${formatDateTime(coPay.seatHeldUntil)}.` : 'Your seat is held for 7 days while you pay the balance.'} />
              {coPay ? (
                <>
                  <KeyValue items={[['Amount', <UnitPrice money={coPay.amount} size="sm" inline />], ['Reference', coPay.gatewayReference]]} />
                  <div className="mt-4 flex flex-wrap gap-2">
                    <Button icon={CreditCard} href={String(coPay.checkoutUrl)}>Pay now</Button>
                    <Button variant="secondary" to={`/learn/payments/${coPay.paymentIntentId}`}>Payment details</Button>
                  </div>
                  {isDevCheckout(String(coPay.checkoutUrl)) ? <p className="mt-2 text-xs text-muted">Development: the fake gateway has no checkout page. Open payment details and use “Verify payment” after simulating the webhook.</p> : null}
                </>
              ) : (
                <Button variant="secondary" to="/learn/payments">Find the payment</Button>
              )}
            </Card>
          ) : null}

          {/* Entry assessment */}
          {prog?.hasEntryAssessment && ['submitted', 'merged', 'draft'].includes(app.status) && app.assessmentScore == null ? (
            <EntryAssessment applicationId={applicationId} />
          ) : null}

          {/* Rejection + appeal */}
          {app.status === 'rejected' ? (
            <Card>
              <CardHeader title="Not selected this time" description="Decisions weigh the entry assessment and rubric scores. You can appeal within seven days of the decision." />
              {appealDeadline && appealDeadline > new Date() ? (
                <Button icon={Scale} variant="secondary" onClick={() => setConfirm('appeal')}>Appeal this decision</Button>
              ) : (
                <p className="text-sm text-muted">The appeal window has closed. Other opportunities are open on <Link to="/opportunities">the opportunities page</Link>.</p>
              )}
              <ErrorAlert error={appeal.error} className="mt-3" />
            </Card>
          ) : null}

          {app.status === 'waitlisted' ? <Alert tone="info" title="You are on the waitlist">If a seat is released or an offer expires, the next waitlisted applicant is offered it automatically.</Alert> : null}
          {app.status === 'enrolled' ? <Alert tone="success" title="You are enrolled">Find the course under <Link to="/learn/courses" className="font-semibold">My courses</Link>.</Alert> : null}
          {app.eligible === false ? <Alert tone="danger" title="Eligibility">{(app.eligibilityReasons || []).map(titleCase).join(', ') || 'The stated criteria were not met.'}</Alert> : null}

          {/* Answers */}
          <Card>
            <CardHeader title="Your answers" action={app.status === 'draft' ? <Button size="sm" variant="secondary" to={`/learn/apply/${app.programmeId}`}>Edit & submit</Button> : null} />
            <KeyValue items={Object.entries(app.answers || {}).map(([k, v]) => [titleCase(k), typeof v === 'object' ? JSON.stringify(v) : String(v)])} />
            {!Object.keys(app.answers || {}).length ? <p className="text-sm text-muted">No answers saved.</p> : null}
          </Card>
        </div>

        <aside>
          <Card>
            <KeyValue
              cols={1}
              items={[
                ['Status', <StatusPill status={app.status} label={applicationLabel(app.status)} />],
                ['Submitted', app.submittedAt ? formatDateTime(app.submittedAt) : 'Not yet'],
                app.assessmentScore != null ? ['Entry assessment', `${Number(app.assessmentScore).toFixed(0)}% ${app.assessmentPassed ? '(passed)' : '(not passed)'}`] : null,
                app.totalScore != null ? ['Total score', Number(app.totalScore).toFixed(1)] : null,
                coPayDue ? ['Co-payment', <UnitPrice money={app.coPayAmount} size="sm" inline />] : ['Co-payment', 'None (fully sponsored)'],
                app.offerExpiresAt ? ['Offer expires', formatDateTime(app.offerExpiresAt)] : null,
                app.seatId ? ['Seat', app.seatId.slice(0, 8)] : null,
                ['Application ID', <code className="text-xs">{app.applicationId}</code>],
              ]}
            />
          </Card>
        </aside>
      </div>

      <ConfirmDialog open={confirm === 'accept'} onClose={() => setConfirm(null)} onConfirm={() => accept.mutate()} loading={accept.isPending} error={accept.error} title="Accept this offer?" description="You accept the programme's code of conduct. Any other open offers you hold stay open until you withdraw them." confirmLabel="Accept" tone="teal" />
      <ConfirmDialog open={confirm === 'decline'} onClose={() => setConfirm(null)} onConfirm={() => decline.mutate()} loading={decline.isPending} error={decline.error} title="Decline this offer?" description="The seat goes to the next person on the waitlist. This cannot be undone." confirmLabel="Decline offer" tone="danger" />
      <ConfirmDialog open={confirm === 'appeal'} onClose={() => setConfirm(null)} onConfirm={(reason) => appeal.mutate(reason)} loading={appeal.isPending} error={appeal.error} title="Appeal the decision" description="Explain why the decision should be reviewed. Staff uphold (back to the waitlist) or dismiss appeals." confirmLabel="File appeal" reason reasonLabel="Grounds for appeal" />
    </div>
  );
}

/** Timed entry assessment: POST /applications/{id}/assessment → questions; PUT with answers { "0": 1, ... }. */
function EntryAssessment({ applicationId }) {
  const [view, setView] = useState(null);
  const [answers, setAnswers] = useState({});
  const [remaining, setRemaining] = useState(null);
  const [result, setResult] = useState(null);
  const start = useAction(() => applications.startAssessment(applicationId), { silent: true, onSuccess: (v) => { setView(v); setRemaining(v.secondsRemaining); } });
  const submit = useAction(() => applications.submitAssessment(applicationId, answers), { invalidate: [keys.applications], success: (r) => (r.passed ? 'Assessment passed' : 'Assessment submitted'), onSuccess: (r) => { setResult(r); setView(null); } });

  useEffect(() => {
    if (!view) return undefined;
    const t = setInterval(() => setRemaining((s) => (s > 0 ? s - 1 : 0)), 1000);
    return () => clearInterval(t);
  }, [view]);
  useEffect(() => {
    if (view && remaining === 0 && !submit.isPending && !result) submit.mutate();
  }, [remaining, view, submit, result]);

  const questions = view?.questions?.questions || (Array.isArray(view?.questions) ? view.questions : []);

  return (
    <Card>
      <CardHeader title="Entry assessment" description="A short multiple-choice test, about 30 minutes. The timer starts when you begin and the deadline is enforced." />
      {result ? <Alert tone={result.passed ? 'success' : 'warn'} title={result.passed ? 'Passed' : 'Submitted'}>{result.correct} of {result.total} correct ({Number(result.score).toFixed(0)}/{Number(result.maxScore).toFixed(0)}).</Alert> : null}
      {!view && !result ? (
        <>
          <ErrorAlert error={start.error} className="mb-3" />
          <Button icon={FileQuestion} loading={start.isPending} onClick={() => start.mutate()}>Start assessment</Button>
          <p className="mt-2 text-xs text-muted">Use a quiet place and a stable connection. You cannot pause once started.</p>
        </>
      ) : null}
      {view ? (
        <form className="space-y-4" onSubmit={(e) => { e.preventDefault(); submit.mutate(); }}>
          <div className="sticky top-16 z-10 -mx-5 flex items-center justify-between border-y border-line bg-white/95 px-5 py-2 text-sm backdrop-blur">
            <span className="text-muted">Answered {Object.keys(answers).length}/{questions.length}</span>
            <span className={`inline-flex items-center gap-1 font-semibold tabular ${remaining < 120 ? 'text-danger' : 'text-navy'}`}><Clock className="h-4 w-4" /> {formatSeconds(remaining)}</span>
          </div>
          {questions.map((q, qi) => {
            const idx = q.index ?? qi;
            const prompt = q.prompt || q.question || q.text;
            const options = q.options || q.choices || [];
            return (
              <fieldset key={idx} className="rounded-card border border-line p-4">
                <legend className="px-1 text-sm font-semibold text-navy">{idx + 1}. {prompt}</legend>
                {options.length ? (
                  <div className="mt-2 grid gap-2">
                    {options.map((opt, oi) => (
                      <label key={oi} className={`flex min-h-11 cursor-pointer items-center gap-3 rounded-input border px-3 text-sm ${answers[String(idx)] === oi ? 'border-royal bg-royal-50' : 'border-line hover:bg-surface'}`}>
                        <input type="radio" name={`ea-${idx}`} className="h-4 w-4 accent-royal" checked={answers[String(idx)] === oi} onChange={() => setAnswers((a) => ({ ...a, [String(idx)]: oi }))} />
                        {opt}
                      </label>
                    ))}
                  </div>
                ) : (
                  <input className="input mt-2" value={answers[String(idx)] ?? ''} onChange={(e) => setAnswers((a) => ({ ...a, [String(idx)]: e.target.value }))} placeholder="Your answer" />
                )}
              </fieldset>
            );
          })}
          <ErrorAlert error={submit.error} />
          <div className="flex justify-end"><Button type="submit" loading={submit.isPending}>Submit assessment</Button></div>
        </form>
      ) : null}
    </Card>
  );
}
