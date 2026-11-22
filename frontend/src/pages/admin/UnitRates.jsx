import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Coins, PlusCircle, Search } from 'lucide-react';
import { Alert, Button, Card, CardHeader, EmptyState, ErrorAlert, Field, FormGrid, Input, Loading, Modal, PageHeader, Select, StatusPill, Table, Textarea } from '../../components/ui';
import { formatDateTime, formatMoney, nairaToKobo, titleCase } from '../../lib/format';
import { useAction, useForm } from '../../lib/hooks';
import { formatUnits } from '../../lib/units.jsx';
import { walletApi, walletKeys } from '../../api/wallet';
import { AdminError, DecisionDialog, FourEyesNote, Id, useIsMaker, UserLink } from './_shared';

/** /admin/unit-rates — GET/POST /admin/wallet/rates, decision (checker ≠ maker), wallet lookup and adjustments. */
export default function UnitRates() {
  const q = useQuery({ queryKey: walletKeys.adminRates, queryFn: () => walletApi.rates() });
  const [proposing, setProposing] = useState(false);
  const [deciding, setDeciding] = useState(null);
  const decide = useAction(({ decision, note }) => walletApi.decideRate(deciding.rateId, decision, note), {
    success: 'Unit rate decision recorded',
    invalidate: [walletKeys.adminRates, walletKeys.rate],
    onSuccess: () => setDeciding(null),
  });

  if (q.isPending) return <Loading />;
  if (q.isError) return <AdminError error={q.error} />;
  const { effectiveRateKobo, effectiveRateValue, effectiveSource, rates = [] } = q.data;
  const pending = rates.filter((r) => String(r.status).toLowerCase() === 'proposed');

  return (
    <div className="space-y-6">
      <PageHeader title="Unit rate" description="The naira value of one unit. Every purchase keeps the rate it was bought at; a new rate only changes future prices and purchases." actions={<Button icon={PlusCircle} onClick={() => setProposing(true)}>Propose a rate</Button>} />

      <Card>
        <CardHeader title="Effective now" action={<Coins className="h-5 w-5 text-cyan" aria-hidden />} />
        <div className="flex flex-wrap items-end gap-6">
          <div>
            <div className="font-display text-3xl font-extrabold tabular">{formatMoney(effectiveRateValue)}</div>
            <div className="text-xs text-muted">per unit · {effectiveRateKobo} kobo · {effectiveSource === 'default' ? 'configured default (no approved rate yet)' : effectiveSource}</div>
          </div>
          <div className="text-sm text-muted">Example: a ₦45,000 course costs {formatUnits(Math.ceil(4_500_000 / effectiveRateKobo))}.</div>
        </div>
      </Card>

      {pending.length ? (
        <Card>
          <CardHeader title={`Awaiting decision (${pending.length})`} />
          <Table rows={pending} rowKey={(r) => r.rateId} columns={rateColumns((r) => <DecideButton row={r} onClick={() => setDeciding(r)} />)} />
        </Card>
      ) : null}

      <Card>
        <CardHeader title="All versions" />
        {rates.length ? <Table rows={rates} rowKey={(r) => r.rateId} columns={rateColumns()} /> : <EmptyState icon={Coins} title="No rate proposed yet" description="The configured default applies until a proposal is approved." />}
      </Card>

      <WalletLookup />

      <ProposeDialog open={proposing} onClose={() => setProposing(false)} />
      <DecisionDialog
        open={Boolean(deciding)}
        onClose={() => setDeciding(null)}
        title="Decide on unit rate"
        description={deciding ? `${formatMoney(deciding.rateValue)} per unit from ${formatDateTime(deciding.effectiveFrom)}` : ''}
        options={[{ value: 'approved', label: 'Approve', tone: 'teal' }, { value: 'rejected', label: 'Reject', tone: 'danger' }]}
        loading={decide.isPending}
        error={decide.error}
        onSubmit={(decision, note) => decide.mutate({ decision, note })}
      >
        {deciding ? <div className="mb-3 space-y-2"><p className="text-sm text-muted">Rationale: {deciding.rationale}</p><FourEyesNote makerId={deciding.proposedBy} what="this rate" /></div> : null}
      </DecisionDialog>
    </div>
  );
}

function DecideButton({ row, onClick }) {
  const isMaker = useIsMaker(row.proposedBy);
  return (
    <div className="flex flex-col items-end gap-1">
      <Button size="sm" variant="secondary" disabled={isMaker} onClick={onClick} title={isMaker ? 'You proposed this rate; a different staff member must decide.' : undefined}>Decide</Button>
      {isMaker ? <span className="text-[11px] text-warn">You proposed this</span> : null}
    </div>
  );
}

function rateColumns(action) {
  const cols = [
    { key: 'version', header: 'v', render: (r) => `v${r.version}` },
    { key: 'rateValue', header: 'Per unit', align: 'right', render: (r) => formatMoney(r.rateValue) },
    { key: 'effectiveFrom', header: 'Effective from', render: (r) => formatDateTime(r.effectiveFrom) },
    { key: 'status', header: 'Status', render: (r) => <StatusPill status={r.status} /> },
    { key: 'proposedBy', header: 'Proposed', render: (r) => <><UserLink id={r.proposedBy} /><div className="text-xs text-muted">{formatDateTime(r.proposedAt)}</div></> },
    { key: 'decidedBy', header: 'Decided', render: (r) => (r.decidedBy ? <><UserLink id={r.decidedBy} /><div className="text-xs text-muted">{formatDateTime(r.decidedAt)}{r.decisionNote ? ` · ${r.decisionNote}` : ''}</div></> : '—') },
    { key: 'rationale', header: 'Rationale', cellClassName: 'max-w-xs', render: (r) => <span className="line-clamp-2 text-xs text-muted">{r.rationale}</span> },
  ];
  if (action) cols.push({ key: 'action', header: '', align: 'right', render: action });
  return cols;
}

function ProposeDialog({ open, onClose }) {
  const { values, bind, reset } = useForm({ naira: '', effectiveFrom: '', rationale: '' });
  const propose = useAction(() => walletApi.proposeRate({ rateKobo: nairaToKobo(values.naira), effectiveFrom: new Date(values.effectiveFrom).toISOString(), rationale: values.rationale.trim() }), {
    success: 'Rate proposed; a colleague must approve it',
    invalidate: [walletKeys.adminRates],
    onSuccess: () => { reset(); onClose(); },
  });
  const rateKobo = nairaToKobo(values.naira);
  return (
    <Modal
      open={open}
      onClose={onClose}
      title="Propose a unit rate"
      description="Takes effect from the date you set once another staff member approves it. Existing purchases keep their rate."
      footer={<><Button variant="secondary" onClick={onClose}>Cancel</Button><Button loading={propose.isPending} disabled={rateKobo < 100 || !values.effectiveFrom || values.rationale.trim().length < 5} onClick={() => propose.mutate()}>Propose</Button></>}
    >
      <ErrorAlert error={propose.error} className="mb-3" />
      <FormGrid>
        <Field label="Naira per unit" hint={rateKobo ? `${rateKobo} kobo · ₦45,000 would cost ${formatUnits(Math.ceil(4_500_000 / rateKobo))}` : 'Between ₦1 and ₦1,000,000'} required>
          {(p) => <Input {...p} type="number" inputMode="decimal" min={1} step={1} placeholder="1000" {...bind('naira')} />}
        </Field>
        <Field label="Effective from" hint="Cannot be in the past." required>
          {(p) => <Input {...p} type="datetime-local" {...bind('effectiveFrom')} />}
        </Field>
      </FormGrid>
      <Field label="Rationale" className="mt-3" required>
        {(p) => <Textarea {...p} rows={3} maxLength={1000} placeholder="Why the unit price changes" {...bind('rationale')} />}
      </Field>
    </Modal>
  );
}

function WalletLookup() {
  const { values, bind } = useForm({ ownerType: 'user', ownerId: '' });
  const [target, setTarget] = useState(null);
  const q = useQuery({ queryKey: walletKeys.adminLookup(target?.ownerType, target?.ownerId), queryFn: () => walletApi.lookup(target.ownerType, target.ownerId), enabled: Boolean(target) });
  const adjust = useForm({ units: '', reason: '' });
  const apply = useAction(() => walletApi.adjust(target.ownerType, target.ownerId, adjust.values.units, adjust.values.reason.trim()), {
    success: (r) => `Adjusted: balance now ${formatUnits(r.balanceAfter)}`,
    invalidate: [['admin', 'wallet']],
    onSuccess: () => adjust.reset(),
  });
  const w = q.data;

  return (
    <Card>
      <CardHeader title="Wallet lookup" description="Any wallet by owner. Adjustments add or remove units with a reason; they move no cash and are audited." action={<Search className="h-5 w-5 text-cyan" aria-hidden />} />
      <form className="grid gap-3 sm:grid-cols-[160px_1fr_auto] items-end" onSubmit={(e) => { e.preventDefault(); if (values.ownerId.trim()) setTarget({ ownerType: values.ownerType, ownerId: values.ownerId.trim() }); }}>
        <Field label="Owner type">{(p) => <Select {...p} {...bind('ownerType')}><option value="user">User</option><option value="organisation">Organisation</option></Select>}</Field>
        <Field label="Owner id">{(p) => <Input {...p} placeholder="Paste a user or organisation id" {...bind('ownerId')} />}</Field>
        <Button type="submit" icon={Search} disabled={!values.ownerId.trim()}>Look up</Button>
      </form>

      {target ? (
        q.isPending ? <Loading /> : q.isError ? <AdminError error={q.error} className="mt-4" /> : (
          <div className="mt-5 space-y-4">
            <div className="flex flex-wrap items-center gap-4 rounded-card bg-surface p-4">
              <div><div className="text-xs uppercase tracking-wide text-muted">Balance</div><div className="font-display text-2xl font-extrabold tabular">{formatUnits(w.balance)}</div><div className="text-xs text-muted">{formatMoney(w.balanceValue)}</div></div>
              <div className="text-sm text-muted"><div>Bought {formatUnits(w.lifetimePurchased)} · spent {formatUnits(w.lifetimeRedeemed)}</div><div>Wallet <Id value={w.walletId} /></div></div>
            </div>
            {w.walletId && w.walletId !== '00000000-0000-0000-0000-000000000000' ? null : <Alert tone="info">No wallet has been opened for this owner yet; the first credit or purchase creates it.</Alert>}
            <Table
              dense
              rows={w.openLots || []}
              rowKey={(l) => l.lotId}
              empty={<p className="text-sm text-muted">No open lots.</p>}
              columns={[
                { key: 'acquiredAt', header: 'Lot acquired', render: (l) => formatDateTime(l.acquiredAt) },
                { key: 'kind', header: 'Source', render: (l) => titleCase(l.kind) },
                { key: 'unitsRemaining', header: 'Remaining', align: 'right', render: (l) => `${l.unitsRemaining} / ${l.unitsTotal}` },
                { key: 'valueRemaining', header: 'Value', align: 'right', render: (l) => formatMoney(l.valueRemaining) },
              ]}
            />
            <Table
              dense
              rows={w.recentTransactions || []}
              rowKey={(t) => t.transactionId}
              empty={<p className="text-sm text-muted">No movements.</p>}
              columns={[
                { key: 'occurredAt', header: 'When', render: (t) => formatDateTime(t.occurredAt) },
                { key: 'kind', header: 'Kind', render: (t) => <StatusPill tone={t.units < 0 ? 'warn' : 'success'} status={t.kind} label={titleCase(t.kind)} /> },
                { key: 'description', header: 'Description' },
                { key: 'units', header: 'Units', align: 'right', render: (t) => `${t.units > 0 ? '+' : ''}${t.units}` },
                { key: 'value', header: 'Value', align: 'right', render: (t) => formatMoney(t.value) },
                { key: 'balanceAfter', header: 'Balance', align: 'right' },
              ]}
            />
            <form className="grid gap-3 border-t border-line pt-4 sm:grid-cols-[160px_1fr_auto] items-end" onSubmit={(e) => { e.preventDefault(); apply.mutate(); }}>
              <Field label="Adjust by (units)" hint="Negative removes units">{(p) => <Input {...p} type="number" step={1} placeholder="e.g. 10 or -5" {...adjust.bind('units')} />}</Field>
              <Field label="Reason" required>{(p) => <Input {...p} maxLength={500} placeholder="Goodwill for the outage on 21 Nov" {...adjust.bind('reason')} />}</Field>
              <Button type="submit" variant="secondary" loading={apply.isPending} disabled={!Number(adjust.values.units) || adjust.values.reason.trim().length < 3}>Apply</Button>
            </form>
          </div>
        )
      ) : null}
    </Card>
  );
}
