import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Download, Wallet } from 'lucide-react';
import { Alert, Button, Card, CardHeader, EmptyState, ErrorAlert, Field, Input, Loading, PageHeader, Stat, Table, StatusPill, useToast } from '../../components/ui';
import { saveBlob } from '../../lib/api';
import { formatDate, formatDateTime, formatMoney, koboOf } from '../../lib/format';
import { earningsApi, tutorKeys } from '../../api/tutor';

/*
 * /tutor/earnings — GET /payouts/me (MyEarningsView) and the CSV statement (GET /payouts/me/statement?from&to).
 * Money flow: gross sale → platform commission → net earning → held for the hold period (7 days for course sales,
 * after the session for bookings) → available → pending once drafted into the weekly batch → paid when the bank settles.
 */
export default function Earnings() {
  const toast = useToast();
  const earnings = useQuery({ queryKey: tutorKeys.earnings, queryFn: earningsApi.me });
  const [from, setFrom] = useState(() => new Date(Date.now() - 30 * 86400000).toISOString().slice(0, 10));
  const [to, setTo] = useState(() => new Date().toISOString().slice(0, 10));
  const [downloading, setDownloading] = useState(false);

  async function download() {
    setDownloading(true);
    try {
      const { blob, fileName } = await earningsApi.statement(from, to);
      saveBlob(blob, fileName?.endsWith('.csv') ? fileName : `plastoids-statement-${from}-to-${to}.csv`);
    } catch (err) {
      toast.error(err, 'Could not download the statement');
    } finally {
      setDownloading(false);
    }
  }

  if (earnings.isPending) return <Loading />;
  if (earnings.isError) return <ErrorAlert error={earnings.error} />;
  const e = earnings.data;
  const payouts = e.recentPayouts || [];

  return (
    <div>
      <PageHeader title="Earnings" description="Gross sales, platform commission and what has been paid to your bank." />

      {e.disputeHold ? (
        <Alert tone="danger" className="mb-6" title="Payouts paused by a dispute hold">
          A dispute hold keeps your available balance out of the weekly run until staff release it (holds auto-release when the dispute closes).
        </Alert>
      ) : null}

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <Stat label="Available" value={formatMoney(e.available)} hint="Cleared and payable in the next weekly run" tone="teal" icon={Wallet} />
        <Stat label="Pending" value={formatMoney(e.pending)} hint="In an approved or submitted payout batch" />
        <Stat label="On hold" value={formatMoney(e.held)} hint="Clearing: 7 days for course sales" />
        <Stat label="Paid to date" value={formatMoney(e.paid)} />
      </div>

      <div className="mt-6 grid gap-6 lg:grid-cols-3">
        <Card>
          <CardHeader title="How your earnings are calculated" />
          <dl className="space-y-2 text-sm">
            <Row label="Gross sales" value={formatMoney(e.gross)} />
            <Row label="Platform commission" value={`− ${formatMoney(e.commission)}`} />
            <Row label="Net earnings" value={formatMoney(e.net)} strong />
          </dl>
          <p className="mt-4 text-xs text-muted">
            Net earnings clear after the hold period (7 days for course sales, after the session for bookings; refunds in that window reduce the earning).
            Weekly runs pay out anything above ₦5,000 to a payout account whose name matches your identity.
          </p>
        </Card>

        <Card className="lg:col-span-2">
          <CardHeader title="Statement" description="CSV of every earning and payout in the period." />
          <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
            <Field label="From">{(p) => <Input {...p} type="date" value={from} max={to} onChange={(ev) => setFrom(ev.target.value)} />}</Field>
            <Field label="To">{(p) => <Input {...p} type="date" value={to} min={from} onChange={(ev) => setTo(ev.target.value)} />}</Field>
            <Button icon={Download} loading={downloading} disabled={!from || !to} onClick={download}>Download CSV</Button>
          </div>
        </Card>
      </div>

      <Card className="mt-6">
        <CardHeader title="Recent payouts" description="Transfers to your bank account." />
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
          empty={<EmptyState icon={Wallet} title="No payouts yet" description={koboOf(e.available) > 0 ? 'Your available balance joins the next weekly run.' : 'Earnings appear here once learners buy your courses or book sessions.'} />}
        />
      </Card>
    </div>
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
