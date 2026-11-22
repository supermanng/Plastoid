import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { AlertTriangle, ArrowRight, Landmark, RefreshCw, Users, Wallet } from 'lucide-react';
import { Alert, Button, Card, CardHeader, ConfirmDialog, EmptyState, KeyValue, Loading, PageHeader, Stat, StatusPill, Table } from '../../../shared/components/ui';
import { formatDateTime, formatMoney, formatPercent } from '../../../shared/lib/format';
import { useAction } from '../../../shared/lib/hooks';
import { adminApi, adminKeys } from '../api/admin';
import { AdminError, Freshness, Id, SeverityPill } from './_shared';

/** Executive dashboard: GET /admin/backoffice/dashboard (DashboardView). */
export default function Dashboard() {
  const q = useQuery({ queryKey: adminKeys.dashboard, queryFn: adminApi.dashboard, refetchInterval: 120_000 });
  const [alert, setAlert] = useState(null);
  const resolve = useAction((reason) => adminApi.resolveRiskAlert(alert.alertId, reason), {
    success: 'Risk alert resolved',
    invalidate: [adminKeys.dashboard],
    onSuccess: () => setAlert(null),
  });

  if (q.isPending) return <Loading />;
  if (q.isError) return <AdminError error={q.error} />;
  const d = q.data;
  const rev = d.revenue || {};
  const liab = d.liabilities || {};
  const people = d.participants || {};
  const prog = d.programmes || {};
  const pay = d.payouts || {};

  return (
    <div>
      <PageHeader
        title="Back office"
        description="GMV, revenue, participants, programme health, the payout queue and open risk alerts."
        actions={
          <>
            <Freshness at={d.generatedAt} />
            <Button variant="secondary" size="sm" icon={RefreshCw} loading={q.isFetching} onClick={() => q.refetch()}>Refresh</Button>
          </>
        }
      />

      {pay.payoutsFrozen ? (
        <Alert tone="danger" title="Payouts are frozen" className="mb-6">
          The reconciliation found a shortfall. Review it under <Link to="/finance?tab=reconciliation" className="font-semibold">Finance → Reconciliation</Link> and lift the freeze with a reason once resolved.
        </Alert>
      ) : null}

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <Stat label="GMV" value={formatMoney(rev.gmv)} hint={`Refunds ${formatMoney(rev.refunds)}`} />
        <Stat label="Net revenue" value={formatMoney(rev.netRevenue)} tone="teal" hint={`Processing fees ${formatMoney(rev.processingFees)}`} />
        <Stat label="Learners" value={num(people.learnersRegistered)} hint={`${num(people.tutorsVerified)} tutors · ${num(people.sponsorsVerified)} sponsors verified`} icon={Users} />
        <Stat label="Completion rate" value={formatPercent(prog.completionRatePercent)} hint={`${num(prog.enrolmentsCompleted)} of ${num(prog.enrolmentsCreated)} enrolments`} />
      </div>

      <div className="mt-6 grid gap-6 lg:grid-cols-3">
        <Card>
          <CardHeader title="Revenue breakdown" action={<Link to="/finance" className="text-sm font-semibold text-royal">Ledger <ArrowRight className="inline h-4 w-4" /></Link>} />
          <KeyValue
            cols={1}
            items={[
              ['Course commission (4010)', formatMoney(rev.courseCommission)],
              ['Tutoring commission (4020)', formatMoney(rev.tutoringCommission)],
              ['Programme management fees (4030)', formatMoney(rev.programmeManagementFees)],
              ['Platform seat fees (4040)', formatMoney(rev.platformSeatFees)],
              ['Processing fees (5010)', formatMoney(rev.processingFees)],
            ]}
          />
        </Card>
        <Card>
          <CardHeader title="Liabilities" description="What the platform holds for others." action={<Landmark className="h-5 w-5 text-cyan" />} />
          <KeyValue
            cols={1}
            items={[
              ['Client funds (1020)', formatMoney(liab.clientFunds)],
              ['Gateway in transit (1030)', formatMoney(liab.gatewayInTransit)],
              ['Tutor payable (2010)', formatMoney(liab.tutorPayable)],
              ['Programme funds held (2020)', formatMoney(liab.programmeFundsHeld)],
              ['Learner refund credit (2030)', formatMoney(liab.learnerRefundCredit)],
              ['VAT payable (2040)', formatMoney(liab.vatPayable)],
            ]}
          />
        </Card>
        <Card>
          <CardHeader title="Payout queue" action={<Link to="/payouts" className="text-sm font-semibold text-royal">Payouts <ArrowRight className="inline h-4 w-4" /></Link>} />
          <div className="flex items-center gap-2">
            <Wallet className="h-5 w-5 text-cyan" />
            <StatusPill status={pay.payoutsFrozen ? 'frozen' : 'active'} label={pay.payoutsFrozen ? 'Frozen' : 'Running'} />
          </div>
          <KeyValue
            className="mt-4"
            cols={1}
            items={[
              ['Approved batches', `${num(pay.batchesApproved)} · ${formatMoney(pay.approvedTotal)}`],
              ['Settled payouts', `${num(pay.settled)} · ${formatMoney(pay.settledTotal)}`],
              ['Failed payouts', `${num(pay.failed)} · ${formatMoney(pay.failedTotal)}`],
            ]}
          />
        </Card>
      </div>

      <div className="mt-6 grid gap-6 lg:grid-cols-[2fr_1fr]">
        <Card>
          <CardHeader title="Risk alerts" description="Open alerts raised by consumers and jobs. Resolving records your note in the audit log." />
          {d.riskAlerts?.length ? (
            <Table
              columns={[
                { key: 'severity', header: 'Severity', render: (r) => <SeverityPill severity={r.severity} /> },
                { key: 'kind', header: 'Kind', render: (r) => <span className="font-mono text-xs">{r.kind}</span> },
                { key: 'message', header: 'Message', render: (r) => <span className="text-sm">{r.message}</span> },
                { key: 'referenceId', header: 'Ref', render: (r) => <Id value={r.referenceId} /> },
                { key: 'raisedAt', header: 'Raised', render: (r) => <span className="text-xs text-muted">{formatDateTime(r.raisedAt)}</span> },
                { key: 'act', header: '', align: 'right', render: (r) => <Button size="sm" variant="secondary" onClick={() => setAlert(r)}>Resolve</Button> },
              ]}
              rows={d.riskAlerts}
              rowKey={(r) => r.alertId}
            />
          ) : (
            <EmptyState icon={AlertTriangle} title="No open risk alerts" description="Alerts appear here when a payout fails, a webhook falls behind or reconciliation finds a mismatch." />
          )}
        </Card>
        <Card>
          <CardHeader title="Programme health" action={<Link to="/programmes" className="text-sm font-semibold text-royal">Programmes <ArrowRight className="inline h-4 w-4" /></Link>} />
          <KeyValue cols={1} items={[['Funded', num(prog.funded)], ['Published', num(prog.published)], ['Enrolments created', num(prog.enrolmentsCreated)], ['Enrolments completed', num(prog.enrolmentsCompleted)]]} />
          {d.metrics?.length ? (
            <>
              <h4 className="mt-5 text-xs font-semibold uppercase tracking-wide text-muted">Event counters</h4>
              <ul className="mt-2 divide-y divide-line text-sm">
                {d.metrics.map((m) => (
                  <li key={m.key} className="flex items-center justify-between py-1.5">
                    <span className="font-mono text-xs">{m.key}</span>
                    <span className="tabular text-right">
                      {num(m.count)}{m.amount?.amount ? <span className="text-muted"> · {formatMoney(m.amount)}</span> : null}
                      {m.lastEventAt ? <span className="block text-[11px] text-muted">{formatDateTime(m.lastEventAt)}</span> : null}
                    </span>
                  </li>
                ))}
              </ul>
            </>
          ) : null}
        </Card>
      </div>

      <ConfirmDialog
        open={Boolean(alert)}
        onClose={() => setAlert(null)}
        title="Resolve risk alert"
        description={alert ? `${alert.kind}: ${alert.message}` : ''}
        confirmLabel="Resolve"
        reason
        reasonLabel="Resolution"
        loading={resolve.isPending}
        error={resolve.error}
        onConfirm={(reason) => resolve.mutate(reason)}
      />
    </div>
  );
}

function num(v) {
  return v === null || v === undefined ? '—' : new Intl.NumberFormat('en-NG').format(v);
}
