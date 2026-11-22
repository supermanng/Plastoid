import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { CheckCircle2, Send, XCircle } from 'lucide-react';
import { Alert, Breadcrumb, Button, Card, CardHeader, ConfirmDialog, KeyValue, Loading, PageHeader, Stat, StatusPill, Table } from '../../components/ui';
import { formatDateTime, formatMoney } from '../../lib/format';
import { useAction } from '../../lib/hooks';
import { adminApi, adminKeys, SKIP_REASONS } from '../../api/admin';
import { AdminError, FourEyesNote, Id, PayeeLink, useIsMaker, UserLink } from './_shared';

/** GET /admin/payouts/batches/{id} → BatchView; approve (checker ≠ maker), reject (reason), submit. */
export default function PayoutBatch() {
  const { batchId } = useParams();
  const q = useQuery({ queryKey: adminKeys.payoutBatch(batchId), queryFn: () => adminApi.batch(batchId), refetchInterval: (query) => (query.state.data?.status === 'submitted' ? 30_000 : false) });
  const [confirm, setConfirm] = useState(null); // 'approve' | 'reject' | 'submit'
  const isMaker = useIsMaker(q.data?.createdBy);

  const invalidate = [adminKeys.payoutBatch(batchId), adminKeys.payoutQueue, adminKeys.dashboard];
  const approve = useAction(() => adminApi.approveBatch(batchId), { success: 'Batch approved', invalidate, onSuccess: () => setConfirm(null) });
  const reject = useAction((reason) => adminApi.rejectBatch(batchId, reason), { success: 'Batch rejected; payouts cancelled and funds restored', invalidate, onSuccess: () => setConfirm(null) });
  const submit = useAction(() => adminApi.submitBatch(batchId), { success: (r) => `Submitted ${r.submitted} to ${r.provider}${r.failedAtSubmission ? `, ${r.failedAtSubmission} failed` : ''}`, invalidate, onSuccess: () => setConfirm(null) });

  if (q.isPending) return <Loading />;
  if (q.isError) return <AdminError error={q.error} />;
  const b = q.data;
  const status = String(b.status).toLowerCase();
  const byStatus = b.payouts.reduce((acc, p) => ({ ...acc, [p.status]: (acc[p.status] || 0) + 1 }), {});

  return (
    <div>
      <PageHeader
        breadcrumb={<Breadcrumb items={[{ to: '/admin/payouts', label: 'Payouts' }, { label: b.reference }]} />}
        title={`Batch ${b.reference}`}
        description={`${b.count} payouts · ${formatMoney(b.total)} · created ${formatDateTime(b.createdAt)}`}
        actions={
          <>
            {status === 'pendingapproval' || status === 'pending_approval' ? (
              <>
                <Button variant="danger" icon={XCircle} onClick={() => setConfirm('reject')}>Reject</Button>
                <Button variant="teal" icon={CheckCircle2} disabled={isMaker} title={isMaker ? 'You drafted this batch; a different staff member must approve it.' : undefined} onClick={() => setConfirm('approve')}>Approve</Button>
              </>
            ) : null}
            {status === 'approved' ? <Button icon={Send} onClick={() => setConfirm('submit')}>Submit to provider</Button> : null}
          </>
        }
      />

      <div className="grid gap-4 sm:grid-cols-4">
        <Stat label="Status" value={<StatusPill status={b.status} />} />
        <Stat label="Total" value={formatMoney(b.total)} tone="teal" />
        <Stat label="Payouts" value={b.count} hint={Object.entries(byStatus).map(([s, n]) => `${n} ${s}`).join(' · ')} />
        <Stat label="Provider" value={b.provider || '—'} />
      </div>

      <Card className="mt-6">
        <CardHeader title="Approval trail" />
        <KeyValue cols={3} items={[['Drafted by', <UserLink id={b.createdBy} />], ['Approved by', b.approvedBy ? <UserLink id={b.approvedBy} /> : <span className="text-muted">not yet</span>], ['Batch id', <Id value={b.batchId} />]]} />
        <div className="mt-3"><FourEyesNote makerId={b.createdBy} what="this batch" /></div>
        {status === 'submitted' ? <Alert tone="info" className="mt-4" title="Waiting for the provider">Settlement, failure and reversal events arrive on the HMAC-signed provider webhook and update each payout below (ledger entry 4 on success; funds restored on failure). This page refreshes every 30 seconds.</Alert> : null}
      </Card>

      <Card className="mt-6">
        <CardHeader title="Payouts in this batch" />
        <Table
          rows={b.payouts}
          rowKey={(p) => p.payoutId}
          columns={[
            { key: 'payee', header: 'Payee', render: (p) => <><PayeeLink row={p} /><div className="text-[11px] text-muted">{p.accountName} · {p.accountNumberMasked}</div></> },
            { key: 'amount', header: 'Amount', align: 'right', render: (p) => <span className="font-semibold">{formatMoney(p.amount)}</span> },
            { key: 'status', header: 'Status', render: (p) => <StatusPill status={p.status} /> },
            { key: 'providerReference', header: 'Provider ref', render: (p) => <span className="font-mono text-xs">{p.providerReference || '—'}</span> },
            { key: 'bankReference', header: 'Bank ref', render: (p) => <span className="font-mono text-xs">{p.bankReference || '—'}</span> },
            { key: 'failureReason', header: 'Failure', render: (p) => <span className="text-xs text-danger">{p.failureReason || ''}</span> },
          ]}
          empty={<p className="text-sm text-muted">No payouts in this batch.</p>}
        />
      </Card>

      {b.skipped?.length ? (
        <Card className="mt-6">
          <CardHeader title="Skipped payees" description="Not included in this batch and why." />
          <Table rows={b.skipped} rowKey={(s, i) => s.payeeId || s.tutorUserId || i} columns={[{ key: 'payee', header: 'Payee', render: (s) => <PayeeLink row={s} /> }, { key: 'reason', header: 'Reason', render: (s) => SKIP_REASONS[s.reason] || s.reason || s.skipReason || '—' }, { key: 'payable', header: 'Payable', align: 'right', render: (s) => (s.payable ? formatMoney(s.payable) : '—') }]} />
        </Card>
      ) : null}

      <ConfirmDialog open={confirm === 'approve'} onClose={() => setConfirm(null)} title="Approve this batch?" description={`${b.count} payouts totalling ${formatMoney(b.total)} will be ready to submit.`} confirmLabel="Approve" tone="teal" loading={approve.isPending} error={approve.error} onConfirm={() => approve.mutate()} />
      <ConfirmDialog open={confirm === 'reject'} onClose={() => setConfirm(null)} title="Reject this batch?" description="All payouts are cancelled and the amounts return to the payees' balances." confirmLabel="Reject" tone="danger" reason loading={reject.isPending} error={reject.error} onConfirm={(reason) => reject.mutate(reason)} />
      <ConfirmDialog open={confirm === 'submit'} onClose={() => setConfirm(null)} title="Submit to the payout provider?" description="Transfers are initiated now and cannot be recalled from here." confirmLabel="Submit" loading={submit.isPending} error={submit.error} onConfirm={() => submit.mutate()} />
    </div>
  );
}
