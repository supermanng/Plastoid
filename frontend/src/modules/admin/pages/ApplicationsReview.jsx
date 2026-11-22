import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { ClipboardList, Eye, EyeOff, Send } from 'lucide-react';
import { Alert, Button, Card, EmptyState, Field, Input, Loading, Modal, Pagination, Select, StatusPill, Switch, Table, Textarea } from '../../../shared/components/ui';
import { formatDateTime } from '../../../shared/lib/format';
import { useAction, useCursor, useQueryParams } from '../../../shared/lib/hooks';
import { adminApi, adminKeys } from '../api/admin';
import { AdminError, DecisionDialog, Id, IdLookup, JsonView, UserLink } from './_shared';

const DEFAULTS = { status: '', blind: '1', tab: 'applications' };
const STATUSES = ['', 'submitted', 'accepted', 'rejected', 'waitlisted', 'offered', 'pendingcopay', 'enrolled', 'declined', 'expired', 'withdrawn'];

/**
 * GET /admin/applications/programmes/{id}/queue?status=&blind=&cursor=&limit= → ReviewQueueView
 * { programmeId, seats, seatsOpen, shortlistApproved, countsByStatus, page: { items: ReviewQueueItem[], nextCursor } }
 */
export default function ApplicationsReview({ programmeId }) {
  const [params, setParam] = useQueryParams(DEFAULTS);
  const blind = params.blind !== '0';
  const cursor = useCursor();
  const filters = { status: params.status || undefined, blind, cursor: cursor.cursor || undefined, limit: 50 };
  const q = useQuery({ queryKey: adminKeys.reviewQueue(programmeId, filters), queryFn: () => adminApi.reviewQueue(programmeId, filters), placeholderData: (prev) => prev });

  const [selected, setSelected] = useState({});
  const [scoring, setScoring] = useState(null);
  const [deciding, setDeciding] = useState(false);
  const [viewing, setViewing] = useState(null);
  const [releasing, setReleasing] = useState(null);
  const [appealId, setAppealId] = useState(null);

  const invalidate = [['admin', 'applications', programmeId], adminKeys.opportunities];
  const sendOffers = useAction(() => adminApi.sendOffers(programmeId), { success: (r) => `${r.sent} offers sent, ${r.waitlisted} waitlisted, ${r.seatsOpen} seats still open`, invalidate });
  const release = useAction(({ note }) => adminApi.releaseSeat(programmeId, releasing.seatId, note), { success: (r) => `Seat released; ${r.openSeats} open`, invalidate, onSuccess: () => setReleasing(null) });
  const appeal = useAction(({ decision, note }) => adminApi.resolveAppeal(appealId, decision, note), { success: (r) => `Appeal ${r.appealStatus}; application now ${r.applicationStatus}`, invalidate, onSuccess: () => setAppealId(null) });

  if (q.isPending) return <Loading />;
  if (q.isError) return <AdminError error={q.error} />;
  const view = q.data;
  const items = view.page?.items || [];
  const selectedIds = Object.keys(selected).filter((k) => selected[k]);

  return (
    <div className="space-y-4">
      <Card>
        <div className="flex flex-wrap items-center gap-3">
          <div className="text-sm">
            <span className="font-semibold">{view.seatsOpen}</span> of {view.seats} seats open ·{' '}
            {view.shortlistApproved ? <StatusPill tone="success" status="approved" label="Shortlist approved" /> : <StatusPill tone="warn" status="pending" label="Shortlist not yet approved by sponsor" />}
          </div>
          <div className="ml-auto flex flex-wrap items-center gap-2">
            <Switch label={blind ? 'Blind review on' : 'Blind review off'} checked={blind} onChange={(v) => setParam('blind', v ? '1' : '0')} className="min-h-9 gap-2 text-xs" />
            <Button size="sm" icon={Send} variant="teal" loading={sendOffers.isPending} onClick={() => sendOffers.mutate()}>Send offers</Button>
          </div>
        </div>
        <div className="mt-3 flex flex-wrap gap-2">
          {Object.entries(view.countsByStatus || {}).map(([s, n]) => (
            <button key={s} type="button" onClick={() => { setParam('status', params.status === s ? '' : s); cursor.reset(); }} className={`rounded-chip border px-3 py-1 text-xs font-semibold capitalize ${params.status === s ? 'border-royal bg-royal text-white' : 'border-line text-navy hover:bg-surface'}`}>
              {s.replace(/_/g, ' ')} <span className="opacity-70">{n}</span>
            </button>
          ))}
        </div>
        <AdminError error={sendOffers.error} className="mt-3" />
      </Card>

      <div className="flex flex-wrap items-end gap-3">
        <Field label="Status" className="w-48">
          {(p) => <Select {...p} value={params.status} onChange={(e) => { setParam('status', e.target.value); cursor.reset(); }}>{STATUSES.map((s) => <option key={s} value={s}>{s || 'All'}</option>)}</Select>}
        </Field>
        <Button variant="secondary" size="sm" disabled={!selectedIds.length} onClick={() => setDeciding(true)}>Decide on {selectedIds.length || ''} selected</Button>
        <div className="ml-auto w-full sm:w-auto">
          <IdLookup label="Resolve an appeal by id" placeholder="appeal GUID" buttonLabel="Resolve" onOpen={setAppealId} />
        </div>
      </div>

      {items.length ? (
        <Table
          rows={items}
          rowKey={(r) => r.applicationId}
          columns={[
            { key: 'sel', header: '', render: (r) => <input type="checkbox" aria-label="Select application" className="h-5 w-5 accent-royal" checked={Boolean(selected[r.applicationId])} onChange={(e) => setSelected((s) => ({ ...s, [r.applicationId]: e.target.checked }))} /> },
            { key: 'applicationId', header: 'Application', render: (r) => <><Id value={r.applicationId} />{r.flagged ? <div className="text-[11px] text-danger">⚑ {r.flagReason || 'flagged'}</div> : null}</> },
            { key: 'learnerId', header: 'Learner', render: (r) => (blind || !r.learnerId ? <span className="inline-flex items-center gap-1 text-xs text-muted"><EyeOff className="h-3.5 w-3.5" /> hidden</span> : <UserLink id={r.learnerId} />) },
            { key: 'status', header: 'Status', render: (r) => <StatusPill status={r.status} /> },
            { key: 'assessment', header: 'Assessment', align: 'right', render: (r) => (r.assessmentScore == null ? '—' : <span className={r.assessmentPassed ? 'text-teal-600' : 'text-danger'}>{Number(r.assessmentScore).toFixed(0)}%</span>) },
            { key: 'rubricScore', header: 'Rubric', align: 'right', render: (r) => (r.rubricScore == null ? '—' : Number(r.rubricScore).toFixed(0)) },
            { key: 'totalScore', header: 'Total', align: 'right', render: (r) => <span className="font-semibold">{r.totalScore == null ? '—' : Number(r.totalScore).toFixed(1)}</span> },
            { key: 'submittedAt', header: 'Submitted', render: (r) => <span className="text-xs text-muted">{formatDateTime(r.submittedAt)}</span> },
            { key: 'offer', header: 'Offer', render: (r) => (r.offerExpiresAt ? <span className="text-xs text-muted">expires {formatDateTime(r.offerExpiresAt)}</span> : '—') },
            { key: 'act', header: '', align: 'right', render: (r) => (
              <div className="flex justify-end gap-1">
                <Button size="sm" variant="ghost" icon={Eye} onClick={() => setViewing(r)}>Answers</Button>
                <Button size="sm" variant="secondary" onClick={() => setScoring(r)}>Score</Button>
                {r.seatId ? <Button size="sm" variant="ghost" className="text-danger" onClick={() => setReleasing(r)}>Release seat</Button> : null}
              </div>
            ) },
          ]}
        />
      ) : (
        <EmptyState icon={ClipboardList} title="No applications" description={params.status ? 'None in this status.' : 'Nobody has applied yet.'} />
      )}
      <Pagination hasMore={Boolean(view.page?.nextCursor)} canBack={cursor.canBack} onMore={() => cursor.next(view.page?.nextCursor)} onBack={cursor.back} loading={q.isFetching} />

      <ScoreDialog row={scoring} onClose={() => setScoring(null)} programmeId={programmeId} />
      <BulkDecideDialog open={deciding} onClose={() => setDeciding(false)} programmeId={programmeId} ids={selectedIds} onDone={() => { setSelected({}); setDeciding(false); }} />
      <Modal open={Boolean(viewing)} onClose={() => setViewing(null)} title="Application answers" description={viewing ? `Application ${viewing.applicationId}` : ''}>
        <JsonView value={viewing?.answers} />
      </Modal>
      <DecisionDialog open={Boolean(releasing)} onClose={() => setReleasing(null)} title="Release seat" description={releasing ? `Seat ${releasing.seatId} · application ${releasing.applicationId}` : ''} options={[{ value: 'release', label: 'Release seat', tone: 'danger' }]} noteLabel="Reason" noteRequiredFor={['release']} loading={release.isPending} error={release.error} onSubmit={(_, note) => release.mutate({ note })} />
      <DecisionDialog open={Boolean(appealId)} onClose={() => setAppealId(null)} title="Resolve appeal" description={appealId ? `Appeal ${appealId}` : ''} options={[{ value: 'upheld', label: 'Uphold (back to waitlist)', tone: 'teal' }, { value: 'dismissed', label: 'Dismiss', tone: 'danger' }]} noteLabel="Reason" noteRequiredFor={['upheld', 'dismissed']} loading={appeal.isPending} error={appeal.error} onSubmit={(decision, note) => appeal.mutate({ decision, note })} />
    </div>
  );
}

/** POST /admin/applications/{applicationId}/score { scores: { criterionKey: number } }. Rubric keys come from the programme (not exposed to staff), so they are typed in. */
function ScoreDialog({ row, onClose, programmeId }) {
  const [rows, setRows] = useState([{ key: '', value: '' }]);
  const score = useAction(
    () => adminApi.scoreApplication(row.applicationId, Object.fromEntries(rows.filter((r) => r.key.trim() && r.value !== '').map((r) => [r.key.trim(), Number(r.value)]))),
    { success: (r) => `Scored: total ${Number(r.totalScore).toFixed(1)}`, invalidate: [['admin', 'applications', programmeId]], onSuccess: onClose },
  );
  return (
    <Modal
      open={Boolean(row)}
      onClose={onClose}
      title="Score rubric criteria"
      description="Enter the programme's rubric keys with a score each; the total combines the entry assessment and the rubric by the programme's weights."
      size="sm"
      footer={<><Button variant="secondary" onClick={onClose}>Cancel</Button><Button loading={score.isPending} disabled={!rows.some((r) => r.key.trim() && r.value !== '')} onClick={() => score.mutate()}>Save scores</Button></>}
    >
      <AdminError error={score.error} className="mb-3" />
      <div className="space-y-2">
        {rows.map((r, i) => (
          <div key={i} className="grid grid-cols-[1fr_6rem_auto] gap-2">
            <Input aria-label="Criterion key" placeholder="e.g. motivation" value={r.key} onChange={(e) => setRows((rs) => rs.map((x, j) => (j === i ? { ...x, key: e.target.value } : x)))} />
            <Input aria-label="Score" type="number" step="0.5" placeholder="0–max" value={r.value} onChange={(e) => setRows((rs) => rs.map((x, j) => (j === i ? { ...x, value: e.target.value } : x)))} />
            <Button variant="ghost" size="sm" disabled={rows.length === 1} onClick={() => setRows((rs) => rs.filter((_, j) => j !== i))}>×</Button>
          </div>
        ))}
        <Button variant="secondary" size="sm" onClick={() => setRows((rs) => [...rs, { key: '', value: '' }])}>Add criterion</Button>
      </div>
    </Modal>
  );
}

/** POST /admin/applications/programmes/{id}/decisions { decisions: [{ applicationId, decision, reason }] } (≤ 500). */
function BulkDecideDialog({ open, onClose, programmeId, ids, onDone }) {
  const [decision, setDecision] = useState('accepted');
  const [reason, setReason] = useState('');
  const [result, setResult] = useState(null);
  const decide = useAction(
    () => adminApi.bulkDecide(programmeId, ids.map((applicationId) => ({ applicationId, decision, reason: reason.trim() }))),
    { success: (r) => `${r.applied} decision(s) applied${r.failures?.length ? `, ${r.failures.length} failed` : ''}`, invalidate: [['admin', 'applications', programmeId]], onSuccess: (r) => { setResult(r); if (!r.failures?.length) onDone(); } },
  );
  return (
    <Modal
      open={open}
      onClose={() => { setResult(null); onClose(); }}
      title={`Decide on ${ids.length} application${ids.length === 1 ? '' : 's'}`}
      description="Each decision is recorded with the reason (append-only). Accepted applicants get an offer when you press Send offers; the sponsor signs off the shortlist unless selection is delegated."
      size="sm"
      footer={<><Button variant="secondary" onClick={() => { setResult(null); onClose(); }}>Close</Button><Button loading={decide.isPending} disabled={!reason.trim() || !ids.length} onClick={() => decide.mutate()}>Apply</Button></>}
    >
      <AdminError error={decide.error} className="mb-3" />
      <div className="flex flex-wrap gap-2">
        {[['accepted', 'Accept', 'teal'], ['waitlisted', 'Waitlist', 'secondary'], ['rejected', 'Reject', 'danger']].map(([v, l]) => (
          <button key={v} type="button" onClick={() => setDecision(v)} aria-pressed={decision === v} className={`min-h-10 rounded-chip border px-4 text-sm font-semibold ${decision === v ? 'border-royal bg-royal text-white' : 'border-line text-navy hover:bg-surface'}`}>{l}</button>
        ))}
      </div>
      <Field label="Reason (sent to the applicant)" required className="mt-3">
        {(p) => <Textarea {...p} rows={3} value={reason} onChange={(e) => setReason(e.target.value)} />}
      </Field>
      {result?.failures?.length ? (
        <Alert tone="warn" title={`${result.failures.length} could not be decided`} className="mt-3">
          <ul className="mt-1 space-y-1 text-xs">{result.failures.map((f) => <li key={f.applicationId}><Id value={f.applicationId} /> {f.code}: {f.message}</li>)}</ul>
        </Alert>
      ) : null}
    </Modal>
  );
}
