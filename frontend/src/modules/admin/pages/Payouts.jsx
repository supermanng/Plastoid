import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Hand, PlusCircle, Wallet } from 'lucide-react';
import { Alert, Button, Card, CardHeader, EmptyState, Field, Input, Loading, Modal, PageHeader, Stat, StatusPill, Table, Textarea } from '../../../shared/components/ui';
import { formatDateTime, formatMoney } from '../../../shared/lib/format';
import { useAction } from '../../../shared/lib/hooks';
import { adminApi, adminKeys, SKIP_REASONS } from '../api/admin';
import { AdminError, Id, PayeeLink } from './_shared';

/** GET /admin/payouts/queue → PayoutQueueView { payoutsFrozen, minimumPayout, tutors: QueueTutor[], openBatches: QueueBatch[] }. */
export default function Payouts() {
  const navigate = useNavigate();
  const q = useQuery({ queryKey: adminKeys.payoutQueue, queryFn: adminApi.payoutQueue });
  const disputes = useQuery({ queryKey: adminKeys.disputeQueue, queryFn: () => adminApi.disputeQueue() });
  const [holdFor, setHoldFor] = useState(null); // { payeeType, payeeId } or '' for manual entry
  const [reason, setReason] = useState('');
  const [tutorInput, setTutorInput] = useState('');

  const invalidate = [adminKeys.payoutQueue, adminKeys.disputeQueue, adminKeys.dashboard];
  const create = useAction(() => adminApi.createBatch(), {
    success: (b) => `Batch ${b.reference} drafted: ${b.count} payouts, ${formatMoney(b.total)}`,
    invalidate,
    onSuccess: (b) => navigate(`/payouts/${b.batchId}`),
  });
  const place = useAction(() => adminApi.placeHold(holdFor || { payeeType: 'tutor', payeeId: tutorInput.trim() }, reason.trim()), { success: 'Dispute hold placed', invalidate, onSuccess: () => { setHoldFor(null); setReason(''); setTutorInput(''); } });

  if (q.isPending) return <Loading />;
  if (q.isError) return <AdminError error={q.error} />;
  const v = q.data;
  const eligible = v.tutors.filter((t) => t.eligible);
  const payable = eligible.reduce((s, t) => s + (t.payable?.amount || 0), 0);

  return (
    <div>
      <PageHeader
        title="Payouts"
        description="Weekly tutor payouts. Draft a batch (maker), have a colleague approve it (checker), submit it to the provider; settlement arrives by webhook (ledger entry 4)."
        actions={
          <>
            <Button variant="secondary" icon={Hand} onClick={() => setHoldFor('')}>Place hold</Button>
            <Button icon={PlusCircle} loading={create.isPending} disabled={v.payoutsFrozen || !eligible.length} onClick={() => create.mutate()}>Draft this week's batch</Button>
          </>
        }
      />

      {v.payoutsFrozen ? <Alert tone="danger" title="Payouts are frozen by the reconciliation" className="mb-4">No batch can be drafted until the freeze is lifted under <Link to="/finance?tab=reconciliation" className="font-semibold">Finance → Reconciliation</Link>.</Alert> : null}
      {create.error ? <CreateError error={create.error} /> : null}

      <div className="grid gap-4 sm:grid-cols-3">
        <Stat label="Payable now" value={formatMoney(payable)} hint={`${eligible.length} tutor${eligible.length === 1 ? '' : 's'} eligible`} tone="teal" icon={Wallet} />
        <Stat label="Minimum payout" value={formatMoney(v.minimumPayout)} hint="Below this, earnings roll to next week" />
        <Stat label="Open batches" value={v.openBatches.length} hint="Pending approval, approved or submitted" />
      </div>

      <Card className="mt-6">
        <CardHeader title="Open batches" />
        {v.openBatches.length ? (
          <Table
            rows={v.openBatches}
            rowKey={(b) => b.batchId}
            onRowClick={(b) => navigate(`/payouts/${b.batchId}`)}
            columns={[
              { key: 'reference', header: 'Reference', render: (b) => <span className="font-mono text-xs">{b.reference}</span> },
              { key: 'status', header: 'Status', render: (b) => <StatusPill status={b.status} /> },
              { key: 'count', header: 'Payouts', align: 'right' },
              { key: 'total', header: 'Total', align: 'right', render: (b) => <span className="font-semibold">{formatMoney(b.total)}</span> },
              { key: 'createdAt', header: 'Created', render: (b) => <span className="text-xs text-muted">{formatDateTime(b.createdAt)}</span> },
            ]}
          />
        ) : (
          <EmptyState icon={Wallet} title="No open batches" description="Draft this week's batch when tutors are payable." className="py-8" />
        )}
      </Card>

      <Card className="mt-6">
        <CardHeader title="Payees in the next run" description="Tutors (2010) and organisations (2060). Payable = released net earnings − paid − pending, capped by the ledger balance. Skipped payees show why." />
        {v.tutors.length ? (
          <Table
            rows={v.tutors}
            rowKey={(t) => `${t.payeeType || 'tutor'}:${t.payeeId || t.tutorUserId}`}
            columns={[
              { key: 'payee', header: 'Payee', render: (t) => <><PayeeLink row={t} />{t.accountNumberMasked ? <div className="text-[11px] text-muted">acct {t.accountNumberMasked}</div> : null}</> },
              { key: 'payable', header: 'Payable', align: 'right', render: (t) => <span className="font-semibold">{formatMoney(t.payable)}</span> },
              { key: 'held', header: 'Held', align: 'right', render: (t) => formatMoney(t.held) },
              { key: 'pending', header: 'Pending', align: 'right', render: (t) => formatMoney(t.pending) },
              { key: 'eligible', header: 'This run', render: (t) => (t.eligible ? <StatusPill tone="success" status="eligible" /> : <StatusPill tone="warn" status={t.skipReason || 'skipped'} label={SKIP_REASONS[t.skipReason] || t.skipReason || 'Skipped'} />) },
              { key: 'act', header: '', align: 'right', render: (t) => <Button size="sm" variant="ghost" onClick={() => setHoldFor({ payeeType: t.payeeType || 'tutor', payeeId: t.payeeId || t.tutorUserId })}>Hold</Button> },
            ]}
          />
        ) : (
          <EmptyState icon={Wallet} title="Nobody is payable" description="Earnings clear after the hold period (7 days for course sales) and must reach the minimum." className="py-8" />
        )}
      </Card>

      <Card className="mt-6">
        <CardHeader title="Open dispute holds" description="Holds auto-release on their date; release early with a resolution from Queues → Disputes." action={<Link to="/queues?tab=disputes" className="text-sm font-semibold text-royal">Manage</Link>} />
        {disputes.data?.holds?.length ? (
          <Table rows={disputes.data.holds} rowKey={(h) => h.holdId} columns={[{ key: 'payee', header: 'Payee', render: (h) => <PayeeLink row={h} /> }, { key: 'reason', header: 'Reason' }, { key: 'placedAt', header: 'Placed', render: (h) => <span className="text-xs text-muted">{formatDateTime(h.placedAt)}</span> }, { key: 'releasesAt', header: 'Releases', render: (h) => <span className="text-xs text-muted">{formatDateTime(h.releasesAt)}</span> }]} />
        ) : (
          <p className="text-sm text-muted">No open holds.</p>
        )}
      </Card>

      <Modal
        open={holdFor !== null}
        onClose={() => setHoldFor(null)}
        title="Place a dispute hold"
        description="Freezes this payee's payouts until released or the auto-release date. Audited."
        size="sm"
        footer={<><Button variant="secondary" onClick={() => setHoldFor(null)}>Cancel</Button><Button variant="danger" loading={place.isPending} disabled={!(holdFor || tutorInput.trim()) || !reason.trim()} onClick={() => place.mutate()}>Place hold</Button></>}
      >
        <AdminError error={place.error} className="mb-3" />
        {holdFor ? <p className="mb-3 text-sm">{holdFor.payeeType === 'organisation' ? 'Organisation' : 'Tutor'} <Id value={holdFor.payeeId} /></p> : (
          <Field label="Tutor user id" required>{(p) => <Input {...p} className="font-mono text-xs" value={tutorInput} onChange={(e) => setTutorInput(e.target.value)} placeholder="GUID" />}</Field>
        )}
        <Field label="Reason" required className="mt-3">{(p) => <Textarea {...p} rows={3} value={reason} onChange={(e) => setReason(e.target.value)} />}</Field>
      </Modal>
    </div>
  );
}

function CreateError({ error }) {
  if (error?.code === 'payouts_frozen') return <Alert tone="danger" title="Cannot draft: payouts are frozen" className="mb-4">{error.detail}</Alert>;
  if (error?.code === 'nothing_to_pay') return <Alert tone="info" title="Nothing to pay this week" className="mb-4">{error.detail || 'No tutor reaches the minimum payout with a verified account.'}</Alert>;
  return <AdminError error={error} className="mb-4" />;
}
