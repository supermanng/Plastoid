import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowLeft, ArrowRight, Save } from 'lucide-react';
import { Alert, Breadcrumb, Button, Card, ErrorAlert, KeyValue, Loading, PageHeader, StatusPill, Stepper, useToast } from '../../../shared/components/ui';
import { formatDate, formatMoney, koboOf } from '../../../shared/lib/format';
import { money, programmeMemory, sponsorApi, sponsorKeys } from '../api/sponsor';
import { useOrganisation } from './useOrganisation';
import { NoOrganisation, RoleHint, VerificationBanner } from './OrganisationSwitcher';
import { CourseStep, DatesStep, EligibilityStep, ObjectivesStep, SeatsBudgetStep, SelectionStep, STEPS } from './CampaignSteps';
import { QuoteLines } from './QuotePanel';
import { statusLabel } from './ProgrammeCard';

/*
 * /sponsor/programmes/new and /sponsor/programmes/:programmeId/edit
 * Step 1 → POST /sponsorship/organisations/{orgId}/programmes (needs name); later steps → PUT .../{programmeId}.
 * The API merges each step into the draft (null fields keep their value) and resets the status to draft, so the
 * final step requests the quote (POST .../quote) and, for owner/finance, approves it (POST .../quote/approve → invoice).
 */
export default function CampaignBuilder() {
  const org = useOrganisation();
  const { programmeId: routeId } = useParams();
  const navigate = useNavigate();
  const qc = useQueryClient();
  const toast = useToast();
  const orgId = org.organisationId;

  const [programmeId, setProgrammeId] = useState(routeId || null);
  const [step, setStep] = useState(0);
  const [draft, setDraft] = useState({});
  const [dirty, setDirty] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const [quote, setQuote] = useState(null);
  const [approval, setApproval] = useState(null);

  const existing = useQuery({
    queryKey: sponsorKeys.programme(orgId, programmeId),
    queryFn: () => sponsorApi.getProgramme(orgId, programmeId),
    enabled: Boolean(orgId && programmeId),
  });

  // Seed the local draft from the saved programme once.
  useEffect(() => {
    if (existing.data && !dirty) setDraft(fromProgramme(existing.data));
  }, [existing.data, dirty]);

  const set = (key, value) => {
    setDraft((d) => ({ ...d, [key]: value }));
    setDirty(true);
  };

  if (!org.organisation) return <div><PageHeader title="New campaign" /><NoOrganisation /></div>;
  if (programmeId && existing.isPending) return <Loading />;
  if (programmeId && existing.isError) return <ErrorAlert error={existing.error} />;

  const programme = existing.data;
  const locked = programme && !['draft', 'quoted'].includes(programme.status);

  async function save({ advance = true } = {}) {
    setError(null);
    const payload = toStep(draft, programme);
    if (!programmeId && !payload.name) {
      setError({ message: 'Give the campaign a name to save the draft.' });
      return false;
    }
    setBusy(true);
    try {
      const saved = programmeId ? await sponsorApi.saveStep(orgId, programmeId, payload) : await sponsorApi.createCampaign(orgId, payload);
      programmeMemory.remember(orgId, saved.programmeId);
      qc.setQueryData(sponsorKeys.programme(orgId, saved.programmeId), saved);
      if (!programmeId) {
        setProgrammeId(saved.programmeId);
        navigate(`/programmes/${saved.programmeId}/edit`, { replace: true });
      }
      setDirty(false);
      setDraft(fromProgramme(saved));
      setQuote(null);
      if (advance) setStep((s) => Math.min(s + 1, STEPS.length - 1));
      else toast.success('Draft saved');
      return true;
    } catch (err) {
      setError(err);
      return false;
    } finally {
      setBusy(false);
    }
  }

  async function requestQuote() {
    setError(null);
    setBusy(true);
    try {
      if (dirty) {
        const ok = await save({ advance: false });
        if (!ok) return;
      }
      const res = await sponsorApi.requestQuote(orgId, programmeId);
      setQuote(res);
      qc.invalidateQueries({ queryKey: sponsorKeys.programme(orgId, programmeId) });
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  async function approveQuote() {
    setError(null);
    setBusy(true);
    try {
      const res = await sponsorApi.approveQuote(orgId, programmeId, 14);
      setApproval(res);
      qc.invalidateQueries({ queryKey: sponsorKeys.programme(orgId, programmeId) });
      toast.success(`Invoice ${res.invoiceNumber} issued`);
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  const stepProps = { draft, set, programme, error };
  const canNext = step === 0 ? Boolean((draft.name || '').trim()) : true;

  return (
    <div className="space-y-6">
      <PageHeader
        breadcrumb={<Breadcrumb items={[{ to: '/programmes', label: 'Programmes' }, { label: programme ? programme.name : 'New campaign' }]} />}
        title={programme ? `Edit: ${programme.name}` : 'Create a campaign'}
        description="Save at every step and come back any time. Requesting a quote locks nothing; approving it does."
        actions={programme ? <StatusPill status={programme.status} label={statusLabel(programme.status)} /> : null}
      />
      <VerificationBanner org={org} />
      {locked ? (
        <Alert tone="info" title="This programme can no longer be edited">
          Its quote is approved. <Button size="sm" variant="secondary" to={`/programmes/${programmeId}`} className="mt-2">Open the programme</Button>
        </Alert>
      ) : null}

      <Stepper steps={STEPS} current={step} onSelect={programmeId ? setStep : undefined} />

      <Card>
        <h2 className="mb-4 text-lg">{STEPS[step]}</h2>
        <ErrorAlert error={error} className="mb-4" />
        <fieldset disabled={locked || busy} className="min-w-0">
          {step === 0 ? <ObjectivesStep {...stepProps} /> : null}
          {step === 1 ? <CourseStep {...stepProps} /> : null}
          {step === 2 ? <SeatsBudgetStep {...stepProps} /> : null}
          {step === 3 ? <EligibilityStep {...stepProps} /> : null}
          {step === 4 ? <DatesStep {...stepProps} /> : null}
          {step === 5 ? <SelectionStep {...stepProps} /> : null}
          {step === 6 ? (
            <ReviewStep programme={programme} draft={draft} dirty={dirty} quote={quote} approval={approval} org={org} busy={busy} onRequestQuote={requestQuote} onApprove={approveQuote} />
          ) : null}
        </fieldset>

        <div className="mt-6 flex flex-wrap items-center justify-between gap-2 border-t border-line pt-4">
          <Button variant="secondary" icon={ArrowLeft} disabled={step === 0 || busy} onClick={() => setStep((s) => s - 1)}>Back</Button>
          <div className="flex flex-wrap gap-2">
            {step < 6 ? (
              <>
                <Button variant="ghost" icon={Save} loading={busy} disabled={locked || !canNext} onClick={() => save({ advance: false })}>Save draft</Button>
                <Button loading={busy} disabled={locked || !canNext} onClick={() => (dirty || !programmeId ? save() : setStep((s) => s + 1))}>
                  Next <ArrowRight className="h-4 w-4" />
                </Button>
              </>
            ) : (
              <Button variant="secondary" to={programmeId ? `/programmes/${programmeId}` : '/programmes'}>Done</Button>
            )}
          </div>
        </div>
      </Card>
    </div>
  );
}

function ReviewStep({ programme, draft, dirty, quote, approval, org, busy, onRequestQuote, onApprove }) {
  const q = quote;
  const saved = programme?.quote;
  const hasQuote = Boolean(q || (saved && programme.status !== 'draft'));
  return (
    <div className="space-y-5">
      <KeyValue
        cols={3}
        items={[
          ['Campaign', draft.name],
          ['Course', draft.courseTitle || programme?.courseTitle || 'Not chosen'],
          ['Seats', draft.seats],
          ['Sponsor share', draft.sponsorSharePercent ? `${draft.sponsorSharePercent}%` : '—'],
          ['Tuition per seat', formatMoney(draft.tuitionPerSeatKobo)],
          ['Budget (ex VAT)', formatMoney(draft.budgetKobo)],
          ['Applications close', formatDate(draft.applicationsCloseAt)],
          ['Programme', draft.startsAt ? `${formatDate(draft.startsAt)} → ${formatDate(draft.endsAt)}` : '—'],
          ['Selection', draft.selectionMode || 'plastoids'],
          ['Eligibility rules', Object.keys(draft.eligibilityRules || {}).length ? Object.keys(draft.eligibilityRules).join(', ') : 'None'],
          ['Entry assessment', draft.entryAssessment ? `${draft.entryAssessment.questions?.length || 0} questions` : 'None'],
          ['Unused funds', draft.unusedFundsPolicy || 'refund'],
        ]}
      />
      {dirty ? <Alert tone="warn">You have unsaved changes; requesting a quote saves them first.</Alert> : null}

      {!approval && !programme?.quote?.approvedAt ? (
        <div className="flex flex-wrap items-center gap-3">
          <Button loading={busy} onClick={onRequestQuote}>{hasQuote ? 'Recalculate quote' : 'Request quote'}</Button>
          <span className="text-xs text-muted">Checks course, seats, dates and budget; nothing is committed.</span>
        </div>
      ) : null}

      {q ? (
        <Card className="bg-surface">
          <h3 className="mb-3 text-base">Quote v{q.version} · {q.durationMonths} month{q.durationMonths === 1 ? '' : 's'}</h3>
          <QuoteLines lines={q.lines.map((l) => [l.kind.replace(/_/g, ' '), `${l.description} (${l.quantity} × ${formatMoney(l.unitAmount)})`, l.amount])} subtotal={q.subtotal} vat={q.vat} total={q.total} />
          <p className="mt-2 text-xs text-muted">Learner co-pay per seat: {koboOf(q.coPayPerSeat) ? formatMoney(q.coPayPerSeat) : 'none'}.</p>
        </Card>
      ) : saved && programme.status !== 'draft' ? (
        <Card className="bg-surface">
          <h3 className="mb-3 text-base">Quote v{saved.version} · requested {formatDate(saved.quotedAt)}</h3>
          <QuoteLines
            lines={[['Tuition', `${saved.seats} × ${formatMoney(saved.tuitionPerSeatSponsorShare)}`, saved.tuition], ['Management fee', null, saved.managementFee], ['Seat fee', null, saved.seatFee]]}
            subtotal={(koboOf(saved.tuition) || 0) + (koboOf(saved.managementFee) || 0) + (koboOf(saved.seatFee) || 0)}
            vat={saved.vat}
            total={saved.total}
          />
        </Card>
      ) : null}

      {hasQuote && !approval && !programme?.quote?.approvedAt ? (
        <div className="space-y-2 border-t border-line pt-4">
          <Button variant="teal" loading={busy} disabled={!org.isFinance} onClick={onApprove}>Approve quote and issue invoice</Button>
          <RoleHint org={org} needs="finance" />
          <p className="text-xs text-muted">Approving locks the campaign and issues an invoice due in 14 days with a dedicated virtual account. Plastoids reviews the eligibility rules in parallel; the campaign publishes once funds arrive and review passes.</p>
        </div>
      ) : null}

      {approval ? (
        <Alert tone="success" title={`Invoice ${approval.invoiceNumber} issued · ${formatMoney(approval.total)} due ${formatDate(approval.dueAt)}`}>
          {approval.virtualAccountNumber ? (
            <>Transfer to <span className="font-semibold text-navy">{approval.virtualAccountBank} {approval.virtualAccountNumber}</span> ({approval.virtualAccountName}). </>
          ) : null}
          <div className="mt-2 flex gap-2">
            <Button size="sm" to={`/invoices/${approval.invoiceId}`}>View invoice</Button>
            <Button size="sm" variant="secondary" to={`/programmes/${approval.programmeId}`}>Open programme</Button>
          </div>
        </Alert>
      ) : programme?.quote?.approvedAt ? (
        <Alert tone="success" title="Quote approved">Invoice issued {formatDate(programme.quote.approvedAt)}. {programme.invoiceId ? <Button size="sm" variant="secondary" to={`/invoices/${programme.invoiceId}`} className="ml-2">View invoice</Button> : null}</Alert>
      ) : null}
    </div>
  );
}

/** Local draft shape ⇄ API CampaignStep. */
function fromProgramme(p) {
  return {
    name: p.name,
    objectives: p.objectives || '',
    courseId: p.courseId || null,
    offeringId: p.offeringId || null,
    courseTitle: p.courseTitle || null,
    seats: p.seats || '',
    tuitionPerSeatKobo: koboOf(p.tuitionPerSeat) || 0,
    sponsorSharePercent: p.sponsorSharePercent || '',
    budgetKobo: koboOf(p.budget) || 0,
    eligibilityRules: p.eligibilityRules && typeof p.eligibilityRules === 'object' ? p.eligibilityRules : {},
    entryAssessment: null, // the API only tells us whether one exists (hasEntryAssessment); editing replaces it
    scoringRubric: null,
    hasEntryAssessment: p.hasEntryAssessment,
    hasScoringRubric: p.hasScoringRubric,
    applicationsOpenAt: p.applicationsOpenAt,
    applicationsCloseAt: p.applicationsCloseAt,
    startsAt: p.startsAt,
    endsAt: p.endsAt,
    selectionMode: p.selectionMode || 'plastoids',
    unusedFundsPolicy: p.unusedFundsPolicy || 'refund',
  };
}

function toStep(d) {
  const num = (v) => (v === '' || v === null || v === undefined ? null : Number(v));
  const assessment = d.entryAssessment
    ? { ...d.entryAssessment, questions: d.entryAssessment.questions.filter((q) => q.prompt?.trim()).map((q) => ({ prompt: q.prompt.trim(), options: q.options.filter((o) => o.trim()), correctIndex: q.correctIndex })) }
    : null;
  return {
    name: d.name?.trim() || null,
    objectives: d.objectives?.trim() || null,
    courseId: d.courseId || null,
    offeringId: d.offeringId || null,
    seats: num(d.seats),
    tuitionPerSeat: d.tuitionPerSeatKobo ? money(d.tuitionPerSeatKobo) : null,
    sponsorSharePercent: num(d.sponsorSharePercent),
    budget: d.budgetKobo ? money(d.budgetKobo) : null,
    eligibilityRules: d.eligibilityRules && Object.keys(d.eligibilityRules).length ? d.eligibilityRules : null,
    entryAssessment: assessment && assessment.questions.length ? assessment : null,
    scoringRubric: d.scoringRubric && d.scoringRubric.criteria?.some((c) => c.key) ? { ...d.scoringRubric, criteria: d.scoringRubric.criteria.filter((c) => c.key) } : null,
    applicationsOpenAt: d.applicationsOpenAt || null,
    applicationsCloseAt: d.applicationsCloseAt || null,
    startsAt: d.startsAt || null,
    endsAt: d.endsAt || null,
    selectionMode: d.selectionMode || null,
    unusedFundsPolicy: d.unusedFundsPolicy || null,
  };
}
