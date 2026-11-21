import { Fragment, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { ChevronDown, ChevronRight, Landmark, Play, Snowflake, Sun } from 'lucide-react';
import { Alert, Button, Card, CardHeader, ConfirmDialog, EmptyState, Field, Input, KeyValue, Loading, PageHeader, Pagination, Select, StatusPill, Tabs } from '../../components/ui';
import { formatDateTime, formatMoney, nairaToKobo } from '../../lib/format';
import { useAction, useCursor, useForm, useQueryParams } from '../../lib/hooks';
import { adminApi, adminKeys, LEDGER_ACCOUNTS, FOUR_EYES_REFUND_KOBO } from '../../api/admin';
import { AdminError, Id, UserLink } from './_shared';

const DEFAULTS = { tab: 'ledger', kind: '', accountCode: '', ownerId: '' };
const TABS = [
  { value: 'ledger', label: 'Ledger' },
  { value: 'balances', label: 'Balances' },
  { value: 'reconciliation', label: 'Reconciliation' },
  { value: 'settlement', label: 'Settlements & sweeps' },
  { value: 'refunds', label: 'Refunds' },
];

export default function Finance() {
  const [params, setParam] = useQueryParams(DEFAULTS);
  return (
    <div>
      <PageHeader title="Finance" description="Double-entry ledger, account balances, the daily reconciliation, gateway settlement imports and refunds." />
      <Tabs tabs={TABS} value={params.tab} onChange={(v) => setParam('tab', v)} className="mb-6" />
      {params.tab === 'ledger' ? <Ledger params={params} setParam={setParam} /> : null}
      {params.tab === 'balances' ? <Balances /> : null}
      {params.tab === 'reconciliation' ? <Reconciliation /> : null}
      {params.tab === 'settlement' ? <Settlement /> : null}
      {params.tab === 'refunds' ? <Refunds /> : null}
    </div>
  );
}

/* ----------------------------------------------------------------- Ledger */

/** GET /admin/ledger/entries?cursor&limit&kind&accountCode&ownerId → Page<JournalEntryItem>. */
function Ledger({ params, setParam }) {
  const cursor = useCursor();
  const filters = { cursor: cursor.cursor || undefined, limit: 50, kind: params.kind || undefined, accountCode: params.accountCode || undefined, ownerId: params.ownerId || undefined };
  const q = useQuery({ queryKey: adminKeys.ledgerEntries(filters), queryFn: () => adminApi.ledgerEntries(filters), placeholderData: (prev) => prev });
  const [open, setOpen] = useState(null);
  const kinds = Array.from(new Set((q.data?.items || []).map((e) => e.kind))).sort();

  return (
    <div>
      <Card className="mb-4">
        <div className="grid gap-3 md:grid-cols-3">
          <Field label="Kind" hint="e.g. learner_payment, settlement, payout">
            {(p) => <Input {...p} list="ledger-kinds" value={params.kind} onChange={(e) => { setParam('kind', e.target.value); cursor.reset(); }} placeholder="Any kind" />}
          </Field>
          <datalist id="ledger-kinds">{kinds.map((k) => <option key={k} value={k} />)}</datalist>
          <Field label="Account">
            {(p) => <Select {...p} value={params.accountCode} onChange={(e) => { setParam('accountCode', e.target.value); cursor.reset(); }}><option value="">Any account</option>{LEDGER_ACCOUNTS.map(([code, name]) => <option key={code} value={code}>{code} · {name}</option>)}</Select>}
          </Field>
          <Field label="Owner id (sub-ledger)">
            {(p) => <Input {...p} className="font-mono text-xs" value={params.ownerId} onChange={(e) => { setParam('ownerId', e.target.value); cursor.reset(); }} placeholder="tutor, programme or learner GUID" />}
          </Field>
        </div>
      </Card>
      {q.isPending ? <Loading /> : null}
      {q.isError ? <AdminError error={q.error} /> : null}
      {q.data ? (
        q.data.items?.length ? (
          <div className="overflow-x-auto rounded-card border border-line">
            <table className="w-full min-w-[720px] text-sm">
              <thead className="bg-surface text-left text-xs uppercase tracking-wide text-muted">
                <tr>
                  <th className="w-8 px-2 py-2" />
                  <th className="px-3 py-2 font-semibold">When</th>
                  <th className="px-3 py-2 font-semibold">Kind</th>
                  <th className="px-3 py-2 font-semibold">Reference / description</th>
                  <th className="px-3 py-2 text-right font-semibold">Total debit</th>
                  <th className="px-3 py-2 font-semibold">Balanced</th>
                  <th className="px-3 py-2 font-semibold">Posted by</th>
                </tr>
              </thead>
              <tbody>
                {q.data.items.map((e) => {
                  const debit = e.postings.reduce((s, p) => s + p.debitKobo, 0);
                  const credit = e.postings.reduce((s, p) => s + p.creditKobo, 0);
                  const balanced = debit === credit;
                  const expanded = open === e.id;
                  return (
                    <Fragment key={e.id}>
                      <tr className="border-t border-line hover:bg-surface/60">
                        <td className="px-2 py-2"><Button variant="ghost" size="sm" className="h-8 w-8 p-0" aria-label={expanded ? 'Collapse' : 'Expand'} onClick={() => setOpen(expanded ? null : e.id)}>{expanded ? <ChevronDown className="h-4 w-4" /> : <ChevronRight className="h-4 w-4" />}</Button></td>
                        <td className="whitespace-nowrap px-3 py-2 text-xs text-muted">{formatDateTime(e.occurredAt)}</td>
                        <td className="px-3 py-2 font-mono text-xs">{e.kind}</td>
                        <td className="px-3 py-2"><div className="font-mono text-xs">{e.reference}</div><div className="text-xs text-muted">{e.description}</div></td>
                        <td className="px-3 py-2 text-right tabular font-semibold">{formatMoney(e.totalDebitKobo)}</td>
                        <td className="px-3 py-2">{balanced ? <StatusPill tone="success" status="balanced" label="Nets to zero" /> : <StatusPill tone="danger" status="unbalanced" label={`Off by ${formatMoney(debit - credit)}`} />}</td>
                        <td className="px-3 py-2">{e.postedBy ? <UserLink id={e.postedBy} /> : <span className="text-xs text-muted">system</span>}</td>
                      </tr>
                      {expanded ? (
                        <tr className="border-t border-line bg-surface/40">
                          <td colSpan={7} className="px-4 py-3">
                            <table className="w-full text-xs">
                              <thead className="text-left uppercase text-muted"><tr><th className="py-1 pr-3">#</th><th className="py-1 pr-3">Account</th><th className="py-1 pr-3">Owner</th><th className="py-1 pr-3 text-right">Debit</th><th className="py-1 text-right">Credit</th></tr></thead>
                              <tbody>
                                {e.postings.map((p) => (
                                  <tr key={p.line} className="border-t border-line/60">
                                    <td className="py-1 pr-3">{p.line}</td>
                                    <td className="py-1 pr-3 font-mono">{p.accountCode} <span className="text-muted">{LEDGER_ACCOUNTS.find(([c]) => c === p.accountCode)?.[1] || ''}</span></td>
                                    <td className="py-1 pr-3">{p.ownerId ? <>{p.ownerType} <Id value={p.ownerId} /></> : '—'}</td>
                                    <td className="py-1 pr-3 text-right tabular">{p.debitKobo ? formatMoney(p.debitKobo) : ''}</td>
                                    <td className="py-1 text-right tabular">{p.creditKobo ? formatMoney(p.creditKobo) : ''}</td>
                                  </tr>
                                ))}
                              </tbody>
                            </table>
                            <div className="mt-2 text-[11px] text-muted">Entry <Id value={e.id} /> · {e.currency}</div>
                          </td>
                        </tr>
                      ) : null}
                    </Fragment>
                  );
                })}
              </tbody>
            </table>
          </div>
        ) : (
          <EmptyState icon={Landmark} title="No journal entries" description="Nothing matches these filters." />
        )
      ) : null}
      <Pagination hasMore={Boolean(q.data?.nextCursor)} canBack={cursor.canBack} onMore={() => cursor.next(q.data?.nextCursor)} onBack={cursor.back} loading={q.isFetching} />
    </div>
  );
}

/* ----------------------------------------------------------------- Balances */

/** GET /admin/ledger/accounts/{accountCode}/balance?ownerId → { accountCode, ownerId, balance }. */
function Balances() {
  const [code, setCode] = useState('1020');
  const [ownerId, setOwnerId] = useState('');
  const [query, setQuery] = useState({ code: '1020', ownerId: '' });
  const q = useQuery({ queryKey: adminKeys.balance(query.code, query.ownerId), queryFn: () => adminApi.balance(query.code, query.ownerId || undefined), enabled: Boolean(query.code) });
  const all = useQuery({
    queryKey: ['admin', 'ledger', 'balance', 'all'],
    queryFn: () => Promise.all(LEDGER_ACCOUNTS.map(([c]) => adminApi.balance(c).catch((e) => ({ accountCode: c, error: e })))),
  });

  return (
    <div className="grid gap-6 lg:grid-cols-[1fr_1.4fr]">
      <Card>
        <CardHeader title="Look up a balance" description="Natural balance (debit for assets/expenses, credit for liabilities/revenue). Add an owner id for a sub-ledger (tutor payable, programme funds, learner credit)." />
        <form className="space-y-3" onSubmit={(e) => { e.preventDefault(); setQuery({ code, ownerId: ownerId.trim() }); }}>
          <Field label="Account">{(p) => <Select {...p} value={code} onChange={(e) => setCode(e.target.value)}>{LEDGER_ACCOUNTS.map(([c, n]) => <option key={c} value={c}>{c} · {n}</option>)}</Select>}</Field>
          <Field label="Owner id (optional)">{(p) => <Input {...p} className="font-mono text-xs" value={ownerId} onChange={(e) => setOwnerId(e.target.value)} placeholder="GUID" />}</Field>
          <Button type="submit">Get balance</Button>
        </form>
        {q.isFetching ? <Loading className="py-6" /> : null}
        {q.isError ? <AdminError error={q.error} className="mt-4" /> : null}
        {q.data && !q.isFetching ? (
          <div className="mt-4 rounded-card bg-surface p-4">
            <div className="text-xs uppercase tracking-wide text-muted">{q.data.accountCode} {LEDGER_ACCOUNTS.find(([c]) => c === q.data.accountCode)?.[1]}{q.data.ownerId ? ` · owner ${q.data.ownerId.slice(0, 8)}` : ''}</div>
            <div className="mt-1 font-display text-3xl font-extrabold tabular">{formatMoney(q.data.balance, { precise: true })}</div>
          </div>
        ) : null}
      </Card>
      <Card>
        <CardHeader title="Chart of accounts" description="Control balances right now." action={<Button size="sm" variant="secondary" loading={all.isFetching} onClick={() => all.refetch()}>Refresh</Button>} />
        {all.isPending ? <Loading /> : null}
        {all.data ? (
          <ul className="divide-y divide-line text-sm">
            {all.data.map((b) => (
              <li key={b.accountCode} className="flex items-center justify-between py-2">
                <span><span className="font-mono text-xs">{b.accountCode}</span> <span className="text-muted">{LEDGER_ACCOUNTS.find(([c]) => c === b.accountCode)?.[1]}</span></span>
                <span className="tabular font-semibold">{b.error ? <span className="text-xs text-danger">unavailable</span> : formatMoney(b.balance, { precise: true })}</span>
              </li>
            ))}
          </ul>
        ) : null}
      </Card>
    </div>
  );
}

/* ----------------------------------------------------------------- Reconciliation */

/** GET /admin/ledger/reconciliation → ReconciliationStatus; POST .../run; POST .../unfreeze { reason }. */
function Reconciliation() {
  const q = useQuery({ queryKey: adminKeys.reconciliation, queryFn: adminApi.reconciliation });
  const [unfreezing, setUnfreezing] = useState(false);
  const run = useAction(() => adminApi.runReconciliation(), { success: 'Reconciliation ran', invalidate: [adminKeys.reconciliation, adminKeys.dashboard, adminKeys.payoutQueue] });
  const unfreeze = useAction((reason) => adminApi.unfreezePayouts(reason), { success: 'Payouts unfrozen', invalidate: [adminKeys.reconciliation, adminKeys.dashboard, adminKeys.payoutQueue], onSuccess: () => setUnfreezing(false) });
  if (q.isPending) return <Loading />;
  if (q.isError) return <AdminError error={q.error} />;
  const r = q.data;
  const assets = r.lastAssetsKobo ?? null;
  const liabilities = r.lastLiabilitiesKobo ?? null;
  return (
    <div className="grid gap-6 lg:grid-cols-[1.4fr_1fr]">
      <Card>
        <CardHeader
          title="Daily reconciliation"
          description="Assets (bank + gateway in transit) must cover liabilities (client funds, tutor payable, programme funds, refund credit, VAT). A shortfall freezes payouts."
          action={r.payoutsFrozen ? <StatusPill tone="danger" status="frozen" label="Payouts frozen" /> : <StatusPill tone="success" status="ok" label="Payouts running" />}
        />
        <KeyValue
          cols={2}
          items={[
            ['Last reconciled', r.lastReconciledAt ? formatDateTime(r.lastReconciledAt) : 'never'],
            ['Last verified', r.lastVerifiedAt ? formatDateTime(r.lastVerifiedAt) : 'never'],
            ['Assets', assets === null ? '—' : formatMoney(assets, { precise: true })],
            ['Liabilities', liabilities === null ? '—' : formatMoney(liabilities, { precise: true })],
            ['Headroom', assets === null || liabilities === null ? '—' : <span className={assets >= liabilities ? 'text-teal-600' : 'text-danger'}>{formatMoney(assets - liabilities, { precise: true })}</span>],
            ['Unbalanced entries', <span className={r.lastUnbalancedEntries ? 'font-semibold text-danger' : ''}>{r.lastUnbalancedEntries}</span>],
            r.freezeReason ? ['Freeze reason', r.freezeReason] : null,
          ]}
        />
        <div className="mt-5 flex flex-wrap gap-2">
          <Button icon={Play} variant="secondary" loading={run.isPending} onClick={() => run.mutate()}>Run now</Button>
          {r.payoutsFrozen ? <Button icon={Sun} variant="teal" onClick={() => setUnfreezing(true)}>Lift freeze…</Button> : null}
        </div>
        <AdminError error={run.error} className="mt-3" />
      </Card>
      <Card>
        <CardHeader title="Runbook" action={<Snowflake className="h-5 w-5 text-cyan" />} />
        <ol className="list-decimal space-y-2 pl-5 text-sm text-navy">
          <li>Import the latest gateway settlement (Settlements tab) so in-transit money is recognised.</li>
          <li>Run the reconciliation again. If assets ≥ liabilities it clears itself.</li>
          <li>Still short? Check the Ledger tab for unbalanced entries and the gateway dashboard for missing settlements.</li>
          <li>Only lift the freeze with a reason once the shortfall is explained; the action is audited (docs/runbooks/payouts-frozen.md).</li>
        </ol>
      </Card>
      <ConfirmDialog open={unfreezing} onClose={() => setUnfreezing(false)} title="Lift the payouts freeze?" description="Weekly payout runs resume. Say why the shortfall is resolved." confirmLabel="Unfreeze" tone="teal" reason reasonLabel="Reason" loading={unfreeze.isPending} error={unfreeze.error} onConfirm={(reason) => unfreeze.mutate(reason)} />
    </div>
  );
}

/* ----------------------------------------------------------------- Settlement & sweeps */

/** POST /admin/payments/settlements/import?upTo=YYYY-MM-DD (entry 2, then reconciliation); POST /admin/payments/sweeps/run (entry 3). */
function Settlement() {
  const [upTo, setUpTo] = useState('');
  const [lastImport, setLastImport] = useState(null);
  const [lastSweep, setLastSweep] = useState(null);
  const imp = useAction(() => adminApi.importSettlements(upTo || undefined), { success: (r) => `${r.settled} payment(s) settled`, invalidate: [adminKeys.reconciliation, adminKeys.dashboard, ['admin', 'ledger']], onSuccess: setLastImport });
  const sweep = useAction(() => adminApi.runSweep(), { success: (r) => `Swept ${formatMoney(r.amount)} from ${r.payments} payment(s)`, invalidate: [adminKeys.dashboard, ['admin', 'ledger']], onSuccess: setLastSweep });
  return (
    <div className="grid gap-6 lg:grid-cols-2">
      <Card>
        <CardHeader title="Import gateway settlement" description="Reads the gateway's settlement report and posts entry 2 (gateway receivable → bank client funds, processing fees). Runs the reconciliation afterwards." />
        <Field label="Settled up to (optional)" hint="Leave empty for everything settled so far.">{(p) => <Input {...p} type="date" value={upTo} onChange={(e) => setUpTo(e.target.value)} />}</Field>
        <Button className="mt-3" loading={imp.isPending} onClick={() => imp.mutate()}>Import settlements</Button>
        <AdminError error={imp.error} className="mt-3" />
        {lastImport ? <KeyValue className="mt-4" cols={3} items={[['Settled', lastImport.settled], ['Gross', formatMoney(lastImport.grossKobo)], ['Fees', formatMoney(lastImport.feesKobo)]]} /> : null}
      </Card>
      <Card>
        <CardHeader title="Sweep earned commission" description="Moves commission on settled, hold-cleared payments from client funds to the operating account (entry 3)." />
        <Button variant="secondary" loading={sweep.isPending} onClick={() => sweep.mutate()}>Run sweep</Button>
        <AdminError error={sweep.error} className="mt-3" />
        {lastSweep ? <KeyValue className="mt-4" cols={3} items={[['Payments', lastSweep.payments], ['Amount', formatMoney(lastSweep.amount)], ['Ledger entry', <Id value={lastSweep.ledgerEntryId} />]]} /> : null}
      </Card>
    </div>
  );
}

/* ----------------------------------------------------------------- Refunds */

/** POST /admin/payments/intents/{paymentIntentId}/refunds { amount: Money, reason } (entry 5). Above ₦100,000 a second approver is required. */
function Refunds() {
  const form = useForm({ paymentIntentId: '', naira: '', reason: '' });
  const [result, setResult] = useState(null);
  const issue = useAction(
    () => adminApi.issueRefund(form.values.paymentIntentId.trim(), nairaToKobo(form.values.naira), form.values.reason.trim()),
    { success: (r) => (r.needsSecondApprover ? 'Refund recorded; awaiting a second approver' : 'Refund issued'), invalidate: [adminKeys.refundQueue, adminKeys.dashboard, ['admin', 'ledger']], onSuccess: (r) => { setResult(r); form.reset(); } },
  );
  const kobo = nairaToKobo(form.values.naira || 0);
  return (
    <div className="grid gap-6 lg:grid-cols-[1fr_1fr]">
      <Card>
        <CardHeader title="Issue a refund" description="Refunds the learner (full or partial) and reverses commission proportionally." />
        <form className="space-y-3" onSubmit={(e) => { e.preventDefault(); issue.mutate(); }}>
          <Field label="Payment intent id" required>{(p) => <Input {...p} className="font-mono text-xs" {...form.bind('paymentIntentId')} placeholder="GUID from the receipt or the ledger reference" />}</Field>
          <Field label="Amount (₦)" required hint={kobo > FOUR_EYES_REFUND_KOBO ? `Above ${formatMoney(FOUR_EYES_REFUND_KOBO)}: a different staff member must approve it in Queues → Refunds.` : undefined}>{(p) => <Input {...p} type="number" min={1} step="0.01" {...form.bind('naira')} />}</Field>
          <Field label="Reason" required>{(p) => <Input {...p} {...form.bind('reason')} />}</Field>
          <Button type="submit" loading={issue.isPending} disabled={!form.values.paymentIntentId.trim() || kobo <= 0 || !form.values.reason.trim()}>Issue refund</Button>
        </form>
        <AdminError error={issue.error} className="mt-3" />
        {result ? <KeyValue className="mt-4" cols={3} items={[['Refund', <Id value={result.refundId} />], ['Status', <StatusPill status={result.status} />], ['Amount', formatMoney(result.amount)]]} /> : null}
      </Card>
      <Alert tone="info" title="Cancellation policy (bookings)">
        More than 24 hours before a session: full refund. Within 24 hours: 50%. Tutor cancels: 100%. Course refunds follow the published window. Pending four-eyes refunds are listed under Queues → Refunds.
      </Alert>
    </div>
  );
}

