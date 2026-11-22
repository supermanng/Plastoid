import { Wallet } from 'lucide-react';
import { Alert, Card, CardHeader, EmptyState, Stat, StatusPill, Table } from './ui';
import { formatDate, formatDateTime, formatMoney, koboOf } from '../lib/format';

/*
 * Earnings statement shared by the tutor (GET /payouts/me) and the organisation (GET /payouts/organisations/{id}/earnings).
 * Fields: gross, commission, net, held, available|payable, pending, paid (Money), disputeHold, recentPayouts[], recentEarnings[]?
 * Money flow: gross sale → platform commission → net earning → held for the hold period (7 days for course sales,
 * after the session for bookings) → payable → pending once drafted into the weekly batch → paid when the bank settles.
 */

export function EarningsStats({ data }) {
  const payable = data.payable ?? data.available;
  return (
    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
      <Stat label="Payable" value={formatMoney(payable)} hint="Cleared and payable in the next weekly run" tone="teal" icon={Wallet} />
      <Stat label="Pending" value={formatMoney(data.pending)} hint="In an approved or submitted payout batch" />
      <Stat label="On hold" value={formatMoney(data.held)} hint="Clearing: 7 days for course sales" />
      <Stat label="Paid to date" value={formatMoney(data.paid)} />
    </div>
  );
}

export function EarningsBreakdown({ data, note }) {
  return (
    <Card>
      <CardHeader title="How earnings are calculated" />
      <dl className="space-y-2 text-sm">
        <Row label="Gross sales" value={formatMoney(data.gross)} />
        <Row label="Platform commission" value={`− ${formatMoney(data.commission)}`} />
        {data.tutorShare ? <Row label="Tutor shares" value={`− ${formatMoney(data.tutorShare)}`} /> : null}
        <Row label="Net earnings" value={formatMoney(data.net)} strong />
      </dl>
      {note ? <p className="mt-4 text-xs text-muted">{note}</p> : null}
    </Card>
  );
}

export function RecentPayouts({ data, emptyHint }) {
  const payouts = data.recentPayouts || [];
  const payable = data.payable ?? data.available;
  return (
    <Card>
      <CardHeader title="Recent payouts" description="Transfers to the payout account." />
      <Table
        columns={[
          { key: 'createdAt', header: 'Created', render: (p) => formatDateTime(p.createdAt) },
          { key: 'amount', header: 'Amount', align: 'right', render: (p) => formatMoney(p.amount) },
          { key: 'accountNumberMasked', header: 'Account' },
          { key: 'status', header: 'Status', render: (p) => <StatusPill status={p.status} /> },
          { key: 'settledAt', header: 'Settled', render: (p) => (p.settledAt ? formatDate(p.settledAt) : p.failureReason ? <span className="text-danger">{p.failureReason}</span> : '—') },
          { key: 'bankReference', header: 'Bank ref', render: (p) => p.bankReference || '—' },
        ]}
        rows={payouts}
        rowKey={(p) => p.payoutId}
        empty={<EmptyState icon={Wallet} title="No payouts yet" description={koboOf(payable) > 0 ? 'The payable balance joins the next weekly run.' : emptyHint} />}
      />
    </Card>
  );
}

/** Rendered only when the statement carries a recent earnings list (per-sale lines). */
export function RecentEarnings({ data }) {
  const rows = data.recentEarnings || data.earnings;
  if (!Array.isArray(rows) || rows.length === 0) return null;
  return (
    <Card>
      <CardHeader title="Recent earnings" description="Each sale, net of commission, and when it clears." />
      <Table
        columns={[
          { key: 'createdAt', header: 'Date', render: (e) => formatDateTime(e.createdAt || e.earnedAt || e.occurredAt) },
          { key: 'kind', header: 'Source', render: (e) => <span className="capitalize">{String(e.kind || e.source || e.type || 'sale').replace(/_/g, ' ')}</span> },
          { key: 'gross', header: 'Gross', align: 'right', render: (e) => formatMoney(e.gross) },
          { key: 'commission', header: 'Commission', align: 'right', render: (e) => formatMoney(e.commission) },
          { key: 'net', header: 'Net', align: 'right', render: (e) => formatMoney(e.net ?? e.amount) },
          { key: 'status', header: 'Status', render: (e) => <StatusPill status={e.status} /> },
          { key: 'availableAt', header: 'Clears', render: (e) => formatDate(e.availableAt || e.heldUntil || e.clearsAt) },
        ]}
        rows={rows}
        rowKey={(e, i) => e.earningId || e.id || i}
      />
    </Card>
  );
}

export function DisputeHoldAlert({ data, className }) {
  if (!data.disputeHold) return null;
  return (
    <Alert tone="danger" className={className} title="Payouts paused by a dispute hold">
      A dispute hold keeps the payable balance out of the weekly run until staff release it (holds auto-release when the dispute closes).
    </Alert>
  );
}

function Row({ label, value, strong }) {
  return (
    <div className="flex items-center justify-between gap-3">
      <dt className="text-muted">{label}</dt>
      <dd className={`tabular ${strong ? 'text-lg font-extrabold font-display text-navy' : 'font-medium text-navy'}`}>{value}</dd>
    </div>
  );
}
