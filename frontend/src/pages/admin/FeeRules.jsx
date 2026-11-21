import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Percent, PlusCircle } from 'lucide-react';
import { Alert, Button, Card, CardHeader, EmptyState, Field, Input, Loading, Modal, PageHeader, Select, StatusPill, Table, Textarea } from '../../components/ui';
import { formatDate, formatDateTime } from '../../lib/format';
import { useAction, useForm } from '../../lib/hooks';
import { adminApi, adminKeys } from '../../api/admin';
import { AdminError, DecisionDialog, FourEyesNote, Id, useIsMaker, UserLink } from './_shared';

/** GET /admin/payments/fee-rules → { effectiveNow: EffectiveRateView[], rules: FeeRuleView[] }; propose; decide (checker ≠ maker). */
export default function FeeRules() {
  const q = useQuery({ queryKey: adminKeys.feeRules(), queryFn: () => adminApi.feeRules() });
  const [proposing, setProposing] = useState(false);
  const [deciding, setDeciding] = useState(null);
  const decide = useAction(({ decision, note }) => adminApi.decideFeeRule(deciding.ruleId, decision, note), { success: 'Fee rule decision recorded', invalidate: [['admin', 'fee-rules']], onSuccess: () => setDeciding(null) });

  if (q.isPending) return <Loading />;
  if (q.isError) return <AdminError error={q.error} />;
  const { effectiveNow = [], rules = [] } = q.data;
  const pending = rules.filter((r) => String(r.status).toLowerCase() === 'proposed' || String(r.status).toLowerCase() === 'pending');

  return (
    <div>
      <PageHeader title="Fee rules" description="Commission per rule code in basis points (100 bps = 1%). A proposal takes effect from its date once a different staff member approves it." actions={<Button icon={PlusCircle} onClick={() => setProposing(true)}>Propose a rule</Button>} />

      <Card className="mb-6">
        <CardHeader title="Effective now" action={<Percent className="h-5 w-5 text-cyan" />} />
        {effectiveNow.length ? (
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            {effectiveNow.map((e) => (
              <div key={e.ruleCode} className="rounded-card border border-line p-4">
                <div className="font-mono text-xs text-muted">{e.ruleCode}</div>
                <div className="mt-1 font-display text-2xl font-extrabold tabular">{(e.commissionBps / 100).toFixed(2)}%</div>
                <div className="text-xs text-muted">{e.commissionBps} bps · {e.source}</div>
              </div>
            ))}
          </div>
        ) : <p className="text-sm text-muted">No rule codes reported.</p>}
      </Card>

      {pending.length ? (
        <Card className="mb-6">
          <CardHeader title={`Awaiting decision (${pending.length})`} />
          <Table rows={pending} rowKey={(r) => r.ruleId} columns={ruleColumns((r) => <DecideButton row={r} onClick={() => setDeciding(r)} />)} />
        </Card>
      ) : null}

      <Card>
        <CardHeader title="All versions" />
        {rules.length ? <Table rows={rules} rowKey={(r) => r.ruleId} columns={ruleColumns()} /> : <EmptyState icon={Percent} title="No rules yet" description="Defaults from configuration apply until a rule is approved." />}
      </Card>

      <ProposeDialog open={proposing} onClose={() => setProposing(false)} codes={effectiveNow.map((e) => e.ruleCode)} />
      <DecisionDialog
        open={Boolean(deciding)}
        onClose={() => setDeciding(null)}
        title="Decide on fee rule"
        description={deciding ? `${deciding.ruleCode} → ${(deciding.commissionBps / 100).toFixed(2)}% from ${formatDate(deciding.effectiveFrom)}` : ''}
        options={[{ value: 'approved', label: 'Approve', tone: 'teal' }, { value: 'rejected', label: 'Reject', tone: 'danger' }]}
        loading={decide.isPending}
        error={decide.error}
        onSubmit={(decision, note) => decide.mutate({ decision, note })}
      >
        {deciding ? <div className="mb-3 space-y-2"><p className="text-sm text-muted">Rationale: {deciding.rationale}</p><FourEyesNote makerId={deciding.proposedBy} what="this rule" /></div> : null}
      </DecisionDialog>
    </div>
  );
}

function DecideButton({ row, onClick }) {
  const isMaker = useIsMaker(row.proposedBy);
  return (
    <div className="flex flex-col items-end gap-1">
      <Button size="sm" variant="secondary" disabled={isMaker} onClick={onClick} title={isMaker ? 'You proposed this rule; a different staff member must decide.' : undefined}>Decide</Button>
      {isMaker ? <span className="text-[11px] text-warn">You proposed this</span> : null}
    </div>
  );
}

function ruleColumns(action) {
  return [
    { key: 'ruleCode', header: 'Code', render: (r) => <><span className="font-mono text-xs">{r.ruleCode}</span><div className="text-[11px] text-muted">v{r.version} · <Id value={r.ruleId} /></div></> },
    { key: 'commissionBps', header: 'Commission', align: 'right', render: (r) => <span className="font-semibold">{(r.commissionBps / 100).toFixed(2)}%</span> },
    { key: 'effectiveFrom', header: 'Effective from', render: (r) => formatDate(r.effectiveFrom) },
    { key: 'status', header: 'Status', render: (r) => <StatusPill status={r.status} /> },
    { key: 'proposedBy', header: 'Proposed', render: (r) => <><UserLink id={r.proposedBy} /><div className="text-[11px] text-muted">{formatDateTime(r.proposedAt)}</div></> },
    { key: 'decidedBy', header: 'Decided', render: (r) => (r.decidedBy ? <><UserLink id={r.decidedBy} /><div className="text-[11px] text-muted">{formatDateTime(r.decidedAt)}{r.decisionNote ? ` · ${r.decisionNote}` : ''}</div></> : <span className="text-xs text-muted">—</span>) },
    ...(action ? [{ key: 'act', header: '', align: 'right', render: action }] : []),
  ];
}

/** POST /admin/payments/fee-rules { ruleCode, commissionBps, effectiveFrom, rationale }. */
function ProposeDialog({ open, onClose, codes }) {
  const form = useForm({ ruleCode: codes[0] || '', custom: '', percent: '', effectiveFrom: '', rationale: '' });
  const code = form.values.ruleCode === '__custom' ? form.values.custom.trim() : form.values.ruleCode;
  const bps = Math.round(Number(form.values.percent || 0) * 100);
  const propose = useAction(
    () => adminApi.proposeFeeRule({ ruleCode: code, commissionBps: bps, effectiveFrom: new Date(form.values.effectiveFrom).toISOString(), rationale: form.values.rationale.trim() }),
    { success: 'Fee rule proposed; a colleague must approve it', invalidate: [['admin', 'fee-rules']], onSuccess: () => { form.reset(); onClose(); } },
  );
  const valid = code && bps >= 0 && bps <= 10_000 && form.values.effectiveFrom && form.values.rationale.trim();
  return (
    <Modal open={open} onClose={onClose} title="Propose a fee rule" description="Versioned and effective from a date. You cannot approve your own proposal." size="sm" footer={<><Button variant="secondary" onClick={onClose}>Cancel</Button><Button loading={propose.isPending} disabled={!valid} onClick={() => propose.mutate()}>Propose</Button></>}>
      <AdminError error={propose.error} className="mb-3" />
      <div className="space-y-3">
        <Field label="Rule code" required>
          {(p) => <Select {...p} {...form.bind('ruleCode')}>{codes.map((c) => <option key={c} value={c}>{c}</option>)}<option value="__custom">Other…</option></Select>}
        </Field>
        {form.values.ruleCode === '__custom' ? <Field label="Custom code" required>{(p) => <Input {...p} {...form.bind('custom')} placeholder="e.g. course_sale" className="font-mono text-xs" />}</Field> : null}
        <Field label="Commission (%)" required hint={`${bps} bps`}>{(p) => <Input {...p} type="number" min={0} max={100} step="0.01" {...form.bind('percent')} />}</Field>
        <Field label="Effective from" required>{(p) => <Input {...p} type="date" {...form.bind('effectiveFrom')} />}</Field>
        <Field label="Rationale" required>{(p) => <Textarea {...p} rows={3} {...form.bind('rationale')} />}</Field>
      </div>
      <Alert tone="info" className="mt-3">Existing payments keep the rate that applied when they were made.</Alert>
    </Modal>
  );
}
