import { useState } from 'react';
import { Link, useLocation, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Checkbox, Field, Input, Breadcrumb, Button, Card, CardHeader, ConfirmDialog, KeyValue, Modal, PageHeader, StatusPill, Tabs, Textarea } from '../../../shared/components/ui';
import { formatDate, formatMoney, nairaToKobo } from '../../../shared/lib/format';
import { useAction, useQueryParams } from '../../../shared/lib/hooks';
import { adminApi, adminKeys } from '../api/admin';
import { AdminError, Id } from './_shared';
import ApplicationsReview from './ApplicationsReview';

const DEFAULTS = { tab: 'actions' };

/**
 * Staff actions on one programme. There is no staff GET for a programme, so the header shows what the public
 * opportunities listing knows (when published) and the application queue's seat counts.
 */
export default function ProgrammeDetail() {
  const { programmeId } = useParams();
  const location = useLocation();
  const [params, setParam] = useQueryParams(DEFAULTS);

  const open = useQuery({ queryKey: adminKeys.opportunities, queryFn: () => adminApi.publishedOpportunities(100) });
  const opp = location.state?.opportunity || open.data?.items?.find((o) => o.programmeId === programmeId) || null;
  const queue = useQuery({ queryKey: adminKeys.reviewQueue(programmeId, { head: true }), queryFn: () => adminApi.reviewQueue(programmeId, { limit: 1 }) });

  const [review, setReview] = useState(false);
  const [closing, setClosing] = useState(false);
  const [publishing, setPublishing] = useState(false);
  const [closed, setClosed] = useState(null);

  const invalidate = [adminKeys.opportunities, adminKeys.dashboard, ['admin', 'applications', programmeId]];
  const publish = useAction(() => adminApi.publishProgramme(programmeId), { success: (r) => `Published; ${r.seatsUnlocked} seats unlocked`, invalidate, onSuccess: () => setPublishing(false) });
  const close = useAction(() => adminApi.closeProgramme(programmeId), { success: 'Programme closed', invalidate, onSuccess: (r) => { setClosing(false); setClosed(r); } });

  return (
    <div>
      <PageHeader
        breadcrumb={<Breadcrumb items={[{ to: '/programmes', label: 'Programmes' }, { label: opp?.name || 'Programme' }]} />}
        title={opp?.name || 'Programme'}
        description={opp ? `${opp.sponsorName} · ${opp.courseTitle || 'custom programme'}` : 'Not in the public listing (draft, quoted, funded or closed). Actions below still apply.'}
        actions={<Id value={programmeId} />}
      />

      <Card className="mb-6">
        <KeyValue
          cols={3}
          items={[
            ['Status', opp ? <StatusPill status="published" /> : <StatusPill tone="neutral" status="unknown" label="Not published / unknown" />],
            ['Seats', queue.data ? `${queue.data.seats - queue.data.seatsOpen} filled · ${queue.data.seatsOpen} open of ${queue.data.seats}` : opp ? `${opp.seats - opp.seatsOpen} filled of ${opp.seats}` : '—'],
            ['Shortlist', queue.data ? (queue.data.shortlistApproved ? 'Approved by sponsor' : 'Awaiting sponsor sign-off') : '—'],
            opp ? ['Funding', `${opp.sponsorSharePercent}% sponsored · co-pay ${formatMoney(opp.coPayPerSeat)}`] : null,
            opp ? ['Applications close', formatDate(opp.applicationsCloseAt)] : null,
            opp ? ['Runs', `${formatDate(opp.startsAt)} → ${formatDate(opp.endsAt)}`] : null,
          ]}
        />
        {queue.isError && queue.error?.status !== 404 ? <AdminError error={queue.error} className="mt-3" /> : null}
      </Card>

      <Tabs tabs={[{ value: 'actions', label: 'Programme actions' }, { value: 'applications', label: 'Applications review' }]} value={params.tab} onChange={(v) => setParam('tab', v)} className="mb-6" />

      {params.tab === 'actions' ? (
        <div className="grid gap-6 lg:grid-cols-3">
          <Card>
            <CardHeader title="Review campaign" description="Check the eligibility rules are lawful and non-discriminatory; optionally approve a payment plan." />
            <Button onClick={() => setReview(true)}>Open review</Button>
          </Card>
          <Card>
            <CardHeader title="Publish on the sponsor's behalf" description="Same gates as the sponsor: approved quote, invoice paid (or plan approved), review passed." />
            <Button variant="teal" onClick={() => setPublishing(true)}>Publish</Button>
          </Card>
          <Card>
            <CardHeader title="Close programme" description="Unfilled seats are refunded to the sponsor (ledger entry 10) and the funnel is frozen." />
            <Button variant="danger" onClick={() => setClosing(true)}>Close programme</Button>
            {closed ? (
              <KeyValue className="mt-4" cols={1} items={[['Status', <StatusPill status={closed.status} />], ['Filled / unfilled', `${closed.filledSeats} / ${closed.unfilledSeats}`], ['Seat refund', formatMoney(closed.unfilledSeatRefund)], ['Remaining funds', formatMoney(closed.remainingFunds)], ['Unused funds policy', closed.unusedFundsPolicy]]} />
            ) : null}
          </Card>
          <Card className="lg:col-span-3">
            <CardHeader title="Seats" description="Release a reserved or filled seat (drop-out) from the Applications tab: each offered/enrolled applicant row carries its seat id. The waitlist is backfilled automatically." />
            <Link to={`/programmes/${programmeId}?tab=applications&status=enrolled`} className="text-sm font-semibold text-royal">Go to enrolled applicants →</Link>
          </Card>
        </div>
      ) : (
        <ApplicationsReview programmeId={programmeId} />
      )}

      <ReviewDialog open={review} onClose={() => setReview(false)} programmeId={programmeId} />
      <ConfirmDialog open={publishing} onClose={() => setPublishing(false)} title="Publish this programme?" description="Learners can see and apply to it immediately." confirmLabel="Publish" tone="teal" loading={publish.isPending} error={publish.error} onConfirm={() => publish.mutate()} />
      <ConfirmDialog open={closing} onClose={() => setClosing(false)} title="Close this programme?" description="Unfilled seats are refunded and no further applications are accepted. This cannot be undone." confirmLabel="Close programme" tone="danger" loading={close.isPending} error={close.error} onConfirm={() => close.mutate()} />
    </div>
  );
}

/** POST /admin/sponsorship/programmes/{id}/review { decision, rulesLawful, notes, paymentPlan? { tranches: [{ dueAt, amount }] } } */
function ReviewDialog({ open, onClose, programmeId }) {
  const [decision, setDecision] = useState('approved');
  const [lawful, setLawful] = useState(true);
  const [notes, setNotes] = useState('');
  const [withPlan, setWithPlan] = useState(false);
  const [tranches, setTranches] = useState([{ dueAt: '', naira: '' }]);

  const submit = useAction(
    () => adminApi.reviewProgramme(programmeId, {
      decision,
      rulesLawful: lawful,
      notes: notes.trim() || null,
      paymentPlan: withPlan ? { tranches: tranches.filter((t) => t.dueAt && t.naira).map((t) => ({ dueAt: new Date(t.dueAt).toISOString(), amount: { amount: nairaToKobo(t.naira), currency: 'NGN' } })) } : null,
    }),
    { success: (r) => `Review recorded: ${r.reviewStatus}${r.paymentPlanApproved ? ' (payment plan approved)' : ''}`, invalidate: [adminKeys.opportunities, adminKeys.dashboard], onSuccess: onClose },
  );

  const needsNotes = decision !== 'approved';
  return (
    <Modal
      open={open}
      onClose={onClose}
      title="Programme review"
      description="Decision: approved, changes requested or rejected. Notes are required unless approving."
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>Cancel</Button>
          <Button loading={submit.isPending} disabled={needsNotes && !notes.trim()} onClick={() => submit.mutate()}>Record decision</Button>
        </>
      }
    >
      <AdminError error={submit.error} className="mb-3" />
      <div className="flex flex-wrap gap-2">
        {[['approved', 'Approve'], ['changes_requested', 'Request changes'], ['rejected', 'Reject']].map(([v, l]) => (
          <button key={v} type="button" onClick={() => setDecision(v)} aria-pressed={decision === v} className={`min-h-10 rounded-chip border px-4 text-sm font-semibold ${decision === v ? 'border-royal bg-royal text-white' : 'border-line text-navy hover:bg-surface'}`}>{l}</button>
        ))}
      </div>
      <Checkbox className="mt-3" label="Eligibility rules are lawful and non-discriminatory (no protected characteristics)" checked={lawful} onChange={(e) => setLawful(e.target.checked)} />
      <Field label="Notes" required={needsNotes} className="mt-2" hint={needsNotes ? 'Say what must change.' : 'Optional.'}>
        {(p) => <Textarea {...p} rows={3} value={notes} onChange={(e) => setNotes(e.target.value)} />}
      </Field>
      <Checkbox className="mt-2" label="Approve a payment plan (tranches instead of one invoice)" checked={withPlan} onChange={(e) => setWithPlan(e.target.checked)} />
      {withPlan ? (
        <div className="mt-2 space-y-2">
          {tranches.map((t, i) => (
            <div key={i} className="grid grid-cols-[1fr_1fr_auto] gap-2">
              <Input type="date" aria-label={`Tranche ${i + 1} due`} value={t.dueAt} onChange={(e) => setTranches((ts) => ts.map((x, j) => (j === i ? { ...x, dueAt: e.target.value } : x)))} />
              <Input type="number" min={0} aria-label={`Tranche ${i + 1} amount (₦)`} placeholder="₦ amount" value={t.naira} onChange={(e) => setTranches((ts) => ts.map((x, j) => (j === i ? { ...x, naira: e.target.value } : x)))} />
              <Button variant="ghost" size="sm" disabled={tranches.length === 1} onClick={() => setTranches((ts) => ts.filter((_, j) => j !== i))}>Remove</Button>
            </div>
          ))}
          <Button variant="secondary" size="sm" disabled={tranches.length >= 12} onClick={() => setTranches((ts) => [...ts, { dueAt: '', naira: '' }])}>Add tranche</Button>
        </div>
      ) : null}
    </Modal>
  );
}
