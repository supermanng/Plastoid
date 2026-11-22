import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { ClipboardList } from 'lucide-react';
import { Alert, Button, Card, EmptyState, Field, Input, Loading, PageHeader, StatusPill, Table, Tabs } from '../../../shared/components/ui';
import { formatDateTime, formatMoney, koboOf, relativeTime } from '../../../shared/lib/format';
import { useAction, useQueryParams } from '../../../shared/lib/hooks';
import { hrefFor } from '../../../shared/lib/navigation';
import { adminApi, adminKeys, FOUR_EYES_REFUND_KOBO } from '../api/admin';
import { AdminError, DecisionDialog, FourEyesNote, Id, useIsMaker, UserLink, PayeeLink } from './_shared';

const DEFAULTS = { tab: 'verification' };
const TABS = [
  { value: 'verification', label: 'Verification' },
  { value: 'moderation', label: 'Course moderation' },
  { value: 'refunds', label: 'Refunds' },
  { value: 'disputes', label: 'Disputes' },
];

export default function Queues() {
  const [params, setParam] = useQueryParams(DEFAULTS);
  return (
    <div>
      <PageHeader title="Work queues" description="Everything waiting on a staff decision. Each decision is audited and needs an idempotency key, which the app adds." />
      <Tabs tabs={TABS} value={params.tab} onChange={(v) => setParam('tab', v)} className="mb-6" />
      {params.tab === 'verification' ? <VerificationQueue /> : null}
      {params.tab === 'moderation' ? <ModerationQueue /> : null}
      {params.tab === 'refunds' ? <RefundQueue /> : null}
      {params.tab === 'disputes' ? <DisputeQueue /> : null}
    </div>
  );
}

/* ----------------------------------------------------------------- Verification */

export function VerificationQueue() {
  const q = useQuery({ queryKey: adminKeys.verificationQueue, queryFn: () => adminApi.verificationQueue() });
  const [item, setItem] = useState(null); // { type: 'credential' | 'subject' | 'organisation', row }
  const [score, setScore] = useState('');

  const decide = useAction(
    ({ decision, note }) => {
      if (item.type === 'credential') return adminApi.reviewCredential(item.row.referenceId, decision, note);
      if (item.type === 'subject') return adminApi.reviewSubject(item.row.tutorId, decision, score === '' ? null : Number(score), note);
      return adminApi.verifyOrganisation(item.row.organisationId, decision, note);
    },
    { success: 'Decision recorded', invalidate: [adminKeys.verificationQueue, ['admin', 'tutors'], ['admin', 'sponsors']], onSuccess: () => setItem(null) },
  );

  if (q.isPending) return <Loading />;
  if (q.isError) return <AdminError error={q.error} />;
  const { tutors = [], organisations = [] } = q.data;

  return (
    <div className="space-y-6">
      <Card>
        <h3 className="mb-3 text-base font-bold">Tutor checks <span className="text-sm font-normal text-muted">({tutors.length}) · SLA shown as due time</span></h3>
        {tutors.length ? (
          <Table
            rows={tutors}
            rowKey={(r) => r.queueItemId}
            columns={[
              { key: 'kind', header: 'Stage', render: (r) => <StatusPill tone="info" status={r.kind} label={r.kind === 'credential' ? 'Credential' : 'Demo lesson / subject'} /> },
              { key: 'tutorUserId', header: 'Tutor', render: (r) => <><UserLink id={r.tutorUserId} /> <span className="block text-[11px] text-muted">profile <Id value={r.tutorId} /></span></> },
              { key: 'tutorStatus', header: 'Status', render: (r) => <StatusPill status={r.tutorStatus} /> },
              { key: 'enqueuedAt', header: 'Queued', render: (r) => <span className="text-xs text-muted">{formatDateTime(r.enqueuedAt)}</span> },
              { key: 'dueAt', header: 'Due', render: (r) => <span className={`text-xs ${r.overdue ? 'font-semibold text-danger' : 'text-muted'}`}>{relativeTime(r.dueAt)}{r.overdue ? ' · overdue' : ''}</span> },
              { key: 'act', header: '', align: 'right', render: (r) => <Button size="sm" variant="secondary" onClick={() => { setScore(''); setItem({ type: r.kind === 'credential' ? 'credential' : 'subject', row: r }); }}>Review</Button> },
            ]}
          />
        ) : (
          <EmptyState icon={ClipboardList} title="No tutor checks pending" description="Credential uploads and demo lessons land here with a review SLA." />
        )}
      </Card>

      <Card>
        <h3 className="mb-3 text-base font-bold">Organisations awaiting verification <span className="text-sm font-normal text-muted">({organisations.length})</span></h3>
        {organisations.length ? (
          <OrganisationTable rows={organisations} onDecide={(row) => setItem({ type: 'organisation', row })} />
        ) : (
          <EmptyState icon={ClipboardList} title="No organisations pending" description="Sponsors submit a CAC number and an authorised signatory; verify them here." />
        )}
      </Card>

      <DecisionDialog
        open={Boolean(item)}
        onClose={() => setItem(null)}
        title={item?.type === 'credential' ? 'Review credential' : item?.type === 'subject' ? 'Subject review (demo lesson)' : 'Verify organisation'}
        description={item?.type === 'organisation' ? `${item.row.name} · CAC ${item.row.cacNumber} · signatory ${item.row.signatoryName}` : item ? `Tutor ${item.row.tutorUserId}` : ''}
        options={item?.type === 'organisation' ? [{ value: 'verified', label: 'Verified', tone: 'teal' }, { value: 'rejected', label: 'Reject', tone: 'danger' }] : [{ value: 'approved', label: 'Approve', tone: 'teal' }, { value: 'rejected', label: 'Reject', tone: 'danger' }]}
        noteLabel={item?.type === 'organisation' ? 'Reason' : 'Note'}
        loading={decide.isPending}
        error={decide.error}
        onSubmit={(decision, note) => decide.mutate({ decision, note })}
        extra={item?.type === 'subject' ? () => (
          <Field label="Score (0–100)" hint="Optional quality score for the demo lesson.">
            {(p) => <Input {...p} type="number" min={0} max={100} value={score} onChange={(e) => setScore(e.target.value)} />}
          </Field>
        ) : undefined}
      />
    </div>
  );
}

export function OrganisationTable({ rows, onDecide }) {
  return (
    <Table
      rows={rows}
      rowKey={(r) => r.organisationId}
      columns={[
        { key: 'name', header: 'Organisation', render: (r) => <><div className="font-semibold">{r.name}</div><div className="text-[11px] text-muted capitalize">{r.type} · <Id value={r.organisationId} /></div></> },
        { key: 'cacNumber', header: 'CAC', render: (r) => <span className="font-mono text-xs">{r.cacNumber}</span> },
        { key: 'signatoryName', header: 'Signatory' },
        { key: 'verificationStatus', header: 'Status', render: (r) => <StatusPill status={r.verificationStatus} /> },
        { key: 'submittedAt', header: 'Submitted', render: (r) => <span className="text-xs text-muted">{formatDateTime(r.submittedAt)}</span> },
        ...(onDecide ? [{ key: 'act', header: '', align: 'right', render: (r) => <Button size="sm" variant="secondary" onClick={() => onDecide(r)}>Decide</Button> }] : []),
      ]}
    />
  );
}

/* ----------------------------------------------------------------- Moderation */

export function ModerationQueue({ compact = false }) {
  const q = useQuery({ queryKey: adminKeys.moderationQueue(), queryFn: () => adminApi.moderationQueue() });
  const [row, setRow] = useState(null);
  const decide = useAction(({ decision, note }) => adminApi.reviewCourse(row.courseId, decision, note), {
    success: 'Course review recorded',
    invalidate: [['admin', 'queues', 'course-moderation'], ['catalogue']],
    onSuccess: () => setRow(null),
  });
  if (q.isPending) return <Loading />;
  if (q.isError) return <AdminError error={q.error} />;
  const courses = q.data?.courses || [];
  return (
    <Card>
      {!compact ? <h3 className="mb-3 text-base font-bold">Courses in review <span className="text-sm font-normal text-muted">({courses.length})</span></h3> : null}
      {courses.length ? (
        <Table
          rows={courses}
          rowKey={(r) => r.courseId}
          columns={[
            { key: 'title', header: 'Course', render: (r) => <><div className="font-semibold">{r.title}</div><div className="text-[11px] text-muted">/{r.slug} · owner <UserLink id={r.ownerUserId} /></div></> },
            { key: 'status', header: 'Status', render: (r) => <StatusPill status={r.status} /> },
            { key: 'submittedAt', header: 'Submitted', render: (r) => <span className="text-xs text-muted">{formatDateTime(r.submittedAt)}</span> },
            { key: 'rating', header: 'Rating', render: (r) => (r.ratingCount ? `${Number(r.ratingAverage).toFixed(1)} (${r.ratingCount})` : <span className="text-muted">new</span>) },
            { key: 'reviewNote', header: 'Last note', render: (r) => <span className="text-xs text-muted">{r.reviewNote || '—'}</span> },
            { key: 'act', header: '', align: 'right', render: (r) => <div className="flex justify-end gap-2"><Button size="sm" variant="ghost" href={hrefFor(`/courses/${r.slug}`)} target="_blank" rel="noreferrer">Preview</Button><Button size="sm" variant="secondary" onClick={() => setRow(r)}>Decide</Button></div> },
          ]}
        />
      ) : (
        <EmptyState icon={ClipboardList} title="Nothing to moderate" description="Tutors submit a course after the automated checks pass; it waits here for content review." />
      )}
      <DecisionDialog
        open={Boolean(row)}
        onClose={() => setRow(null)}
        title="Review course"
        description={row?.title}
        options={[{ value: 'approved', label: 'Approve', tone: 'teal' }, { value: 'rejected', label: 'Reject', tone: 'danger' }]}
        noteLabel="Note to the tutor"
        loading={decide.isPending}
        error={decide.error}
        onSubmit={(decision, note) => decide.mutate({ decision, note })}
      />
    </Card>
  );
}

/* ----------------------------------------------------------------- Refunds */

function RefundQueue() {
  const q = useQuery({ queryKey: adminKeys.refundQueue, queryFn: () => adminApi.refundQueue() });
  const approve = useAction((refundId) => adminApi.approveRefund(refundId), { success: 'Refund approved and issued (ledger entry 5)', invalidate: [adminKeys.refundQueue, adminKeys.dashboard] });
  if (q.isPending) return <Loading />;
  if (q.isError) return <AdminError error={q.error} />;
  const refunds = q.data?.refunds || [];
  return (
    <Card>
      <h3 className="mb-1 text-base font-bold">Refunds awaiting a second approver <span className="text-sm font-normal text-muted">({refunds.length})</span></h3>
      <p className="mb-3 text-xs text-muted">Refunds above {formatMoney(FOUR_EYES_REFUND_KOBO)} need a different staff member than the one who requested them. Issue a new refund from Finance → Refunds.</p>
      {refunds.length ? (
        <Table
          rows={refunds}
          rowKey={(r) => r.refundId}
          columns={[
            { key: 'amount', header: 'Amount', render: (r) => <span className="font-semibold tabular">{formatMoney(r.amount)}</span> },
            { key: 'reason', header: 'Reason' },
            { key: 'paymentIntentId', header: 'Payment', render: (r) => <Id value={r.paymentIntentId} /> },
            { key: 'requestedBy', header: 'Requested by', render: (r) => <><UserLink id={r.requestedBy} /><div className="text-[11px] text-muted">{formatDateTime(r.requestedAt)}</div></> },
            { key: 'needs', header: 'Rule', render: (r) => (r.needsSecondApprover || koboOf(r.amount) > FOUR_EYES_REFUND_KOBO ? <StatusPill tone="warn" status="four_eyes" label="Four-eyes" /> : <StatusPill tone="info" status="single" label="Single approver" />) },
            { key: 'act', header: '', align: 'right', render: (r) => <ApproveRefundButton row={r} onApprove={() => approve.mutate(r.refundId)} loading={approve.isPending && approve.variables === r.refundId} /> },
          ]}
        />
      ) : (
        <EmptyState icon={ClipboardList} title="No refunds waiting" description="Large refunds park here until a second staff member approves them." />
      )}
      <AdminError error={approve.error} className="mt-3" />
    </Card>
  );
}

function ApproveRefundButton({ row, onApprove, loading }) {
  const isMaker = useIsMaker(row.requestedBy);
  return (
    <div className="flex flex-col items-end gap-1">
      <Button size="sm" disabled={isMaker} loading={loading} onClick={onApprove} title={isMaker ? 'You requested this refund; another staff member must approve it.' : undefined}>Approve</Button>
      {isMaker ? <span className="text-[11px] text-warn">You are the requester</span> : null}
    </div>
  );
}

/* ----------------------------------------------------------------- Disputes */

function DisputeQueue() {
  const q = useQuery({ queryKey: adminKeys.disputeQueue, queryFn: () => adminApi.disputeQueue() });
  const [hold, setHold] = useState(null);
  const release = useAction(({ note }) => adminApi.releaseHold(hold.holdId, note), {
    success: 'Hold released; the tutor is payable again',
    invalidate: [adminKeys.disputeQueue, adminKeys.payoutQueue],
    onSuccess: () => setHold(null),
  });
  if (q.isPending) return <Loading />;
  if (q.isError) return <AdminError error={q.error} />;
  const { holds = [], flaggedTutors = [] } = q.data;
  return (
    <div className="space-y-6">
      <Card>
        <h3 className="mb-3 text-base font-bold">Open payout holds <span className="text-sm font-normal text-muted">({holds.length})</span></h3>
        {holds.length ? (
          <Table
            rows={holds}
            rowKey={(r) => r.holdId}
            columns={[
              { key: 'payee', header: 'Payee', render: (r) => <PayeeLink row={r} /> },
              { key: 'reason', header: 'Reason' },
              { key: 'placedBy', header: 'Placed by', render: (r) => <><UserLink id={r.placedBy} /><div className="text-[11px] text-muted">{formatDateTime(r.placedAt)}</div></> },
              { key: 'releasesAt', header: 'Auto-release', render: (r) => <span className={`text-xs ${r.overdue ? 'font-semibold text-danger' : 'text-muted'}`}>{formatDateTime(r.releasesAt)}{r.overdue ? ' · overdue' : ''}</span> },
              { key: 'act', header: '', align: 'right', render: (r) => <Button size="sm" variant="secondary" onClick={() => setHold(r)}>Release</Button> },
            ]}
          />
        ) : (
          <EmptyState icon={ClipboardList} title="No open holds" description="Place a hold from Payouts when a learner disputes a session or course." />
        )}
      </Card>
      <Card>
        <h3 className="mb-3 text-base font-bold">Tutors flagged by strikes <span className="text-sm font-normal text-muted">({flaggedTutors.length})</span></h3>
        {flaggedTutors.length ? (
          <Table
            rows={flaggedTutors}
            rowKey={(r) => r.alertId}
            columns={[
              { key: 'tutorId', header: 'Tutor', render: (r) => <Id value={r.tutorId} /> },
              { key: 'message', header: 'Alert' },
              { key: 'raisedAt', header: 'Raised', render: (r) => <span className="text-xs text-muted">{formatDateTime(r.raisedAt)}</span> },
            ]}
          />
        ) : (
          <Alert tone="success" title="No strike alerts">Tutors accrue strikes for late cancellations and no-shows; three strikes raise an alert here.</Alert>
        )}
      </Card>
      <DecisionDialog
        open={Boolean(hold)}
        onClose={() => setHold(null)}
        title="Release payout hold"
        description={hold ? `${hold.payeeType === 'organisation' ? 'Organisation' : 'Tutor'} ${hold.payeeId || hold.tutorUserId} · ${hold.reason}` : ''}
        options={[{ value: 'released', label: 'Release hold', tone: 'teal' }]}
        noteLabel="Resolution"
        noteRequiredFor={['released']}
        loading={release.isPending}
        error={release.error}
        onSubmit={(_, note) => release.mutate({ note })}
      >
        {hold ? <div className="mb-3"><FourEyesNote makerId={hold.placedBy} what="this hold" /></div> : null}
      </DecisionDialog>
    </div>
  );
}
