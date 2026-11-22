import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Download } from 'lucide-react';
import { Button, Card, CardHeader, ErrorAlert, Field, Input, Loading, PageHeader, useToast } from '../../../../shared/components/ui';
import { DisputeHoldAlert, EarningsBreakdown, EarningsStats, RecentEarnings, RecentPayouts } from '../../../../shared/components/EarningsStatement';
import { saveBlob } from '../../../../shared/lib/api';
import { earningsApi, tutorKeys } from '../../api/tutor';

/*
 * /tutor/earnings — GET /payouts/me (MyEarningsView) and the CSV statement (GET /payouts/me/statement?from&to).
 * The statement pieces live in components/EarningsStatement.jsx and are shared with the organisation Payouts page.
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

  return (
    <div className="space-y-6">
      <PageHeader title="Earnings" description="Gross sales, platform commission and what has been paid to your bank." className="mb-0" />
      <DisputeHoldAlert data={e} />
      <EarningsStats data={e} />

      <div className="grid gap-6 lg:grid-cols-3">
        <EarningsBreakdown
          data={e}
          note="Net earnings clear after the hold period (7 days for course sales, after the session for bookings; refunds in that window reduce the earning). Weekly runs pay out anything above ₦5,000 to a payout account whose name matches your identity."
        />
        <Card className="lg:col-span-2">
          <CardHeader title="Statement" description="CSV of every earning and payout in the period." />
          <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
            <Field label="From">{(p) => <Input {...p} type="date" value={from} max={to} onChange={(ev) => setFrom(ev.target.value)} />}</Field>
            <Field label="To">{(p) => <Input {...p} type="date" value={to} min={from} onChange={(ev) => setTo(ev.target.value)} />}</Field>
            <Button icon={Download} loading={downloading} disabled={!from || !to} onClick={download}>Download CSV</Button>
          </div>
        </Card>
      </div>

      <RecentEarnings data={e} />
      <RecentPayouts data={e} emptyHint="Earnings appear here once learners buy your courses or book sessions." />
    </div>
  );
}
