import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Coins, CreditCard, Layers, ReceiptText } from 'lucide-react';
import { Alert, Button, Card, CardHeader, EmptyState, ErrorAlert, Field, Input, Loading, Pagination, StatusPill, Table, useToast } from './ui';
import { useCursor, pageOf } from '../lib/hooks';
import { formatDateTime, formatMoney, nairaToKobo, titleCase } from '../lib/format';
import { formatUnits, valueOf } from '../lib/units.jsx';
import { PURCHASE_PRESETS, walletApi, walletKeys } from '../api/wallet';
import { AppLink } from '../lib/navigation';

/*
 * Building blocks shared by the learner wallet (/learn/wallet) and the organisation wallet (/sponsor/wallet).
 * `wallet` is a WalletView; `organisationId` switches purchase and transaction calls to the organisation's wallet.
 */

export function useWallet(organisationId, options = {}) {
  return useQuery({
    queryKey: organisationId ? walletKeys.organisation(organisationId) : walletKeys.me,
    queryFn: () => (organisationId ? walletApi.organisation(organisationId) : walletApi.me()),
    refetchInterval: (q) => (q.state.data?.pendingPurchases?.length ? 10_000 : false),
    ...options,
  });
}

export function BalanceHero({ wallet, title = 'Your units' }) {
  return (
    <Card className="relative overflow-hidden bg-navy text-white">
      <div className="brand-gradient absolute inset-x-0 bottom-0 h-1" aria-hidden />
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <div className="text-xs font-semibold uppercase tracking-wide text-teal">{title}</div>
          <div className="mt-1 font-display text-4xl font-extrabold tabular">{formatUnits(wallet.balance)}</div>
          <div className="mt-1 text-sm text-white/75">Worth {formatMoney(wallet.balanceValue)} at today's rate of {formatMoney({ amount: wallet.rateKobo, currency: 'NGN' })} per unit.</div>
        </div>
        <div className="grid grid-cols-2 gap-4 text-sm">
          <div><div className="text-xs text-white/60">Bought</div><div className="font-bold tabular">{formatUnits(wallet.lifetimePurchased)}</div></div>
          <div><div className="text-xs text-white/60">Spent</div><div className="font-bold tabular">{formatUnits(wallet.lifetimeRedeemed)}</div></div>
        </div>
      </div>
    </Card>
  );
}

/** Buy units at today's rate. Starts a hosted checkout; the dev fake gateway has no page, so a fallback link is shown. */
export function BuyUnitsForm({ rateKobo, organisationId, disabled = false, disabledHint, compact = false }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [units, setUnits] = useState(50);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const [result, setResult] = useState(null);
  const total = valueOf(units, rateKobo);

  async function buy(e) {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      const res = await walletApi.purchase(units, organisationId);
      setResult(res);
      qc.invalidateQueries({ queryKey: organisationId ? walletKeys.organisation(organisationId) : walletKeys.me });
      if (!goToCheckout(res.checkoutUrl)) toast.info('Checkout created', 'The payment gateway is simulated here; open the payment to continue.');
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  return (
    <Card as="form" onSubmit={buy}>
      <CardHeader title="Buy units" description={`1 unit = ${formatMoney({ amount: rateKobo, currency: 'NGN' })}. Units never expire and are refunded as units.`} action={<Coins className="h-5 w-5 text-cyan" aria-hidden />} />
      <ErrorAlert error={error} className="mb-3" />
      {disabled && disabledHint ? <Alert tone="info" className="mb-3">{disabledHint}</Alert> : null}
      <div className="flex flex-wrap gap-2">
        {PURCHASE_PRESETS.map((n) => (
          <button key={n} type="button" onClick={() => setUnits(n)} aria-pressed={units === n} className={`min-h-11 rounded-chip border px-4 text-sm font-semibold ${units === n ? 'border-royal bg-royal text-white' : 'border-line bg-white text-navy hover:bg-surface'}`}>{n}</button>
        ))}
      </div>
      <div className={`mt-3 grid gap-3 ${compact ? '' : 'sm:grid-cols-[1fr_auto]'} items-end`}>
        <Field label="Units" hint={`Total ${formatMoney(total)}`}>
          {(p) => <Input {...p} type="number" inputMode="numeric" min={1} max={1_000_000} step={1} value={units} onChange={(e) => setUnits(Math.max(0, Math.floor(Number(e.target.value) || 0)))} />}
        </Field>
        <Button type="submit" icon={CreditCard} loading={busy} disabled={disabled || units < 1} size="lg">Pay {formatMoney(total)}</Button>
      </div>
      {result ? (
        <Alert tone="info" className="mt-4" title={`Purchase of ${formatUnits(result.units)} started`}>
          {formatMoney(result.amount)} by card or transfer. The units arrive once the payment is confirmed.{' '}
          <AppLink to={`/learn/payments/${result.paymentIntentId}`} className="font-semibold">Open payment</AppLink>
          {result.checkoutUrl && !String(result.checkoutUrl).includes('.local') ? <> or <a href={result.checkoutUrl} className="font-semibold">go to the gateway</a>.</> : '.'}
        </Alert>
      ) : null}
    </Card>
  );
}

export function PendingPurchases({ purchases }) {
  if (!purchases?.length) return null;
  return (
    <Card>
      <CardHeader title="Pending purchases" description="Waiting for the payment gateway to confirm. This page refreshes every 10 seconds." />
      <ul className="divide-y divide-line">
        {purchases.map((p) => (
          <li key={p.purchaseId} className="flex flex-wrap items-center justify-between gap-2 py-2 text-sm">
            <div><span className="font-semibold">{formatUnits(p.units)}</span> <span className="text-muted">· {formatMoney(p.amount)} · {formatDateTime(p.createdAt)}</span></div>
            <div className="flex items-center gap-2"><StatusPill status={p.status} /><Button size="sm" variant="secondary" as={AppLink} to={`/learn/payments/${p.paymentIntentId}`}>Payment</Button></div>
          </li>
        ))}
      </ul>
    </Card>
  );
}

export function OpenLots({ lots }) {
  return (
    <Card>
      <CardHeader title="Open lots" description="Units are spent oldest first and release the naira they were bought for." action={<Layers className="h-5 w-5 text-cyan" aria-hidden />} />
      <Table
        dense
        rows={lots || []}
        rowKey={(l) => l.lotId}
        empty={<EmptyState icon={Layers} title="No units yet" description="Buy units to pay for courses, sessions and seats." />}
        columns={[
          { key: 'acquiredAt', header: 'Acquired', render: (l) => formatDateTime(l.acquiredAt) },
          { key: 'kind', header: 'Source', render: (l) => <StatusPill tone="info" status={l.kind} label={titleCase(l.kind)} /> },
          { key: 'unitsRemaining', header: 'Remaining', align: 'right', render: (l) => `${l.unitsRemaining} / ${l.unitsTotal}` },
          { key: 'valueRemaining', header: 'Value', align: 'right', render: (l) => formatMoney(l.valueRemaining) },
        ]}
      />
    </Card>
  );
}

export function TransactionsList({ organisationId }) {
  const cursor = useCursor();
  const ownerKey = organisationId || 'me';
  const q = useQuery({
    queryKey: walletKeys.transactions(ownerKey, cursor.cursor),
    queryFn: () => (organisationId ? walletApi.organisationTransactions(organisationId, cursor.cursor) : walletApi.myTransactions(cursor.cursor)),
  });
  const page = pageOf(q.data);
  return (
    <Card>
      <CardHeader title="Movements" description="Every purchase, payment, refund and adjustment on this wallet." action={<ReceiptText className="h-5 w-5 text-cyan" aria-hidden />} />
      {q.isPending ? <Loading /> : q.isError ? <ErrorAlert error={q.error} /> : (
        <>
          <Table
            dense
            rows={page.items}
            rowKey={(t) => t.transactionId}
            empty={<EmptyState icon={ReceiptText} title="No movements yet" />}
            columns={[
              { key: 'occurredAt', header: 'When', render: (t) => formatDateTime(t.occurredAt) },
              { key: 'kind', header: 'Kind', render: (t) => <StatusPill tone={t.units < 0 ? 'warn' : 'success'} status={t.kind} label={titleCase(t.kind)} /> },
              { key: 'description', header: 'Description', render: (t) => <><div>{t.description || titleCase(t.purpose)}</div>{t.referenceId ? <div className="text-xs text-muted"><code>{String(t.referenceId).slice(0, 8)}</code></div> : null}</> },
              { key: 'units', header: 'Units', align: 'right', render: (t) => <span className={t.units < 0 ? 'text-warn' : 'text-teal-600'}>{t.units > 0 ? '+' : ''}{t.units}</span> },
              { key: 'value', header: 'Value', align: 'right', render: (t) => formatMoney(t.value) },
              { key: 'balanceAfter', header: 'Balance', align: 'right' },
            ]}
          />
          <Pagination hasMore={Boolean(page.nextCursor)} onMore={() => cursor.next(page.nextCursor)} canBack={cursor.canBack} onBack={cursor.back} loading={q.isFetching} />
        </>
      )}
    </Card>
  );
}

/** Same rule as the public pages: never send the browser to the fake gateway's *.local host. */
export function goToCheckout(checkoutUrl) {
  if (!checkoutUrl) return false;
  try {
    const url = new URL(checkoutUrl);
    if (url.hostname.endsWith('.local')) return false;
  } catch {
    return false;
  }
  window.location.assign(checkoutUrl);
  return true;
}

export { nairaToKobo };
