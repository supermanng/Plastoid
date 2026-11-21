import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { KeyRound, PlusCircle } from 'lucide-react';
import { Button, Card, EmptyState, Field, Input, Loading, Modal, PageHeader, Select, StatusPill, Table, Textarea } from '../../components/ui';
import { formatDateTime } from '../../lib/format';
import { useAction, useForm, useQueryParams } from '../../lib/hooks';
import { adminApi, adminKeys } from '../../api/admin';
import { AdminError, DecisionDialog, FourEyesNote, Id, useIsMaker, UserLink } from './_shared';

const DEFAULTS = { status: '' };
const STATUSES = ['', 'proposed', 'approved', 'rejected'];

/** GET /admin/identity/role-grants?status= → RoleGrantView[]; propose { userId, role, reason }; decide { decision, note } (checker ≠ maker). */
export default function RoleGrants() {
  const [params, setParam] = useQueryParams(DEFAULTS);
  const q = useQuery({ queryKey: adminKeys.roleGrants(params.status), queryFn: () => adminApi.roleGrants(params.status || undefined), placeholderData: (prev) => prev });
  const [proposing, setProposing] = useState(false);
  const [deciding, setDeciding] = useState(null);
  const decide = useAction(({ decision, note }) => adminApi.decideRoleGrant(deciding.proposalId, decision, note), { success: 'Role grant decision recorded', invalidate: [['admin', 'role-grants'], ['admin', 'users']], onSuccess: () => setDeciding(null) });

  return (
    <div>
      <PageHeader title="Role grants" description="Platform roles (staff, tutor, sponsor) are granted maker/checker: one staff member proposes, a different one approves. Every step is audited." actions={<Button icon={PlusCircle} onClick={() => setProposing(true)}>Propose a grant</Button>} />
      <Card className="mb-4">
        <Field label="Status" className="max-w-xs">{(p) => <Select {...p} value={params.status} onChange={(e) => setParam('status', e.target.value)}>{STATUSES.map((s) => <option key={s} value={s}>{s ? s[0].toUpperCase() + s.slice(1) : 'All'}</option>)}</Select>}</Field>
      </Card>
      {q.isPending ? <Loading /> : null}
      {q.isError ? <AdminError error={q.error} /> : null}
      {q.data ? (
        q.data.length ? (
          <Table
            rows={q.data}
            rowKey={(r) => r.proposalId}
            columns={[
              { key: 'userId', header: 'User', render: (r) => <UserLink id={r.userId} /> },
              { key: 'role', header: 'Role', render: (r) => <span className="rounded-chip bg-surface px-2 py-0.5 text-xs font-semibold capitalize">{r.role}</span> },
              { key: 'reason', header: 'Reason', render: (r) => <span className="text-sm">{r.reason}</span> },
              { key: 'status', header: 'Status', render: (r) => <StatusPill status={r.status} /> },
              { key: 'proposedBy', header: 'Proposed', render: (r) => <><UserLink id={r.proposedBy} /><div className="text-[11px] text-muted">{formatDateTime(r.proposedAt)}</div></> },
              { key: 'decidedBy', header: 'Decided', render: (r) => (r.decidedBy ? <><UserLink id={r.decidedBy} /><div className="text-[11px] text-muted">{formatDateTime(r.decidedAt)}{r.decisionNote ? ` · ${r.decisionNote}` : ''}</div></> : <span className="text-xs text-muted">—</span>) },
              { key: 'act', header: '', align: 'right', render: (r) => (String(r.status).toLowerCase() === 'proposed' ? <DecideButton row={r} onClick={() => setDeciding(r)} /> : <Id value={r.proposalId} />) },
            ]}
          />
        ) : (
          <EmptyState icon={KeyRound} title="No role grant proposals" description="Propose one to give a colleague staff access or to grant tutor/sponsor roles manually." />
        )
      ) : null}

      <ProposeDialog open={proposing} onClose={() => setProposing(false)} />
      <DecisionDialog
        open={Boolean(deciding)}
        onClose={() => setDeciding(null)}
        title="Decide on role grant"
        description={deciding ? `Grant ${deciding.role} to ${deciding.userId}` : ''}
        options={[{ value: 'approved', label: 'Approve', tone: 'teal' }, { value: 'rejected', label: 'Reject', tone: 'danger' }]}
        loading={decide.isPending}
        error={decide.error}
        onSubmit={(decision, note) => decide.mutate({ decision, note })}
      >
        {deciding ? <div className="mb-3 space-y-2"><p className="text-sm text-muted">Reason: {deciding.reason}</p><FourEyesNote makerId={deciding.proposedBy} what="this grant" /></div> : null}
      </DecisionDialog>
    </div>
  );
}

function DecideButton({ row, onClick }) {
  const isMaker = useIsMaker(row.proposedBy);
  return (
    <div className="flex flex-col items-end gap-1">
      <Button size="sm" variant="secondary" disabled={isMaker} onClick={onClick} title={isMaker ? 'You proposed this grant; a different staff member must decide.' : undefined}>Decide</Button>
      {isMaker ? <span className="text-[11px] text-warn">You proposed this</span> : null}
    </div>
  );
}

function ProposeDialog({ open, onClose }) {
  const form = useForm({ userId: '', role: 'staff', reason: '' });
  const propose = useAction(() => adminApi.proposeRoleGrant({ userId: form.values.userId.trim(), role: form.values.role, reason: form.values.reason.trim() }), { success: 'Grant proposed; a colleague must approve it', invalidate: [['admin', 'role-grants']], onSuccess: () => { form.reset(); onClose(); } });
  const valid = /^[0-9a-f-]{36}$/i.test(form.values.userId.trim()) && form.values.reason.trim();
  return (
    <Modal open={open} onClose={onClose} title="Propose a role grant" description="Find the user id under Users. You cannot approve your own proposal." size="sm" footer={<><Button variant="secondary" onClick={onClose}>Cancel</Button><Button loading={propose.isPending} disabled={!valid} onClick={() => propose.mutate()}>Propose</Button></>}>
      <AdminError error={propose.error} className="mb-3" />
      <div className="space-y-3">
        <Field label="User id" required>{(p) => <Input {...p} className="font-mono text-xs" {...form.bind('userId')} placeholder="GUID" />}</Field>
        <Field label="Role" required>{(p) => <Select {...p} {...form.bind('role')}><option value="staff">Staff</option><option value="tutor">Tutor</option><option value="sponsor">Sponsor</option></Select>}</Field>
        <Field label="Reason" required>{(p) => <Textarea {...p} rows={3} {...form.bind('reason')} />}</Field>
      </div>
    </Modal>
  );
}
