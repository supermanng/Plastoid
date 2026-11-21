import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Copy, Landmark, Printer } from 'lucide-react';
import { Alert, Breadcrumb, Button, Card, CardHeader, ErrorAlert, KeyValue, Loading, PageHeader, StatusPill, Table } from '../../components/ui';
import { formatDate, formatDateTime, formatMoney, koboOf } from '../../lib/format';
import { invoiceMemory, sponsorApi, sponsorKeys } from '../../api/sponsor';
import { useOrganisation } from './useOrganisation';
import { NoOrganisation } from './OrganisationSwitcher';

/*
 * /sponsor/invoices/:invoiceId — GET /payments/organisations/{orgId}/invoices/{invoiceId}.
 * Payment: sponsors pay by bank transfer to the invoice's dedicated virtual account; the gateway webhook
 * matches the credit to the invoice (partial → flagged, over-payment → held) and the programme becomes funded.
 * There is no card checkout / payment intent for invoices, so "Pay now" shows the transfer details.
 */
export default function InvoiceDetail() {
  const org = useOrganisation();
  const { invoiceId } = useParams();
  const orgId = org.organisationId;
  const [copied, setCopied] = useState(false);

  const q = useQuery({
    queryKey: sponsorKeys.invoice(orgId, invoiceId),
    queryFn: async () => {
      const inv = await sponsorApi.getInvoice(orgId, invoiceId);
      invoiceMemory.remember(orgId, invoiceId);
      return inv;
    },
    enabled: Boolean(orgId),
    refetchInterval: (query) => (query.state.data && koboOf(query.state.data.outstanding) > 0 ? 30_000 : false),
  });

  if (!org.organisation) return <div><PageHeader title="Invoice" /><NoOrganisation /></div>;
  if (q.isPending) return <Loading />;
  if (q.isError) return <ErrorAlert error={q.error} />;
  const inv = q.data;
  const outstanding = koboOf(inv.outstanding) || 0;
  const overdue = outstanding > 0 && new Date(inv.dueAt) < new Date();

  function copy(text) {
    navigator.clipboard?.writeText(text).then(() => {
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    });
  }

  return (
    <div className="space-y-6">
      <PageHeader
        breadcrumb={<Breadcrumb items={[{ to: '/sponsor/invoices', label: 'Invoices' }, { label: inv.number }]} />}
        title={`Invoice ${inv.number}`}
        description={`Issued ${formatDate(inv.issuedAt)} · due ${formatDate(inv.dueAt)}`}
        actions={
          <>
            <StatusPill status={overdue ? 'overdue' : inv.status} tone={overdue ? 'danger' : undefined} />
            <Button variant="secondary" size="sm" icon={Printer} onClick={() => window.print()}>Print</Button>
          </>
        }
      />

      {overdue ? <Alert tone="danger" title="Invoice overdue">The campaign stays blocked until funds arrive. Pay the outstanding {formatMoney(inv.outstanding)} by transfer below.</Alert> : null}
      {outstanding === 0 ? <Alert tone="success" title="Paid in full">{inv.paidAt ? `Received ${formatDateTime(inv.paidAt)}. ` : ''}The programme is funded{inv.programmeId ? '' : ''}.</Alert> : null}

      <div className="grid gap-6 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader title="Lines" />
          <Table
            dense
            columns={[
              { key: 'line', header: '#' },
              { key: 'description', header: 'Description', render: (r) => <><div>{r.description}</div><div className="text-xs capitalize text-muted">{String(r.kind).replace(/_/g, ' ')}</div></> },
              { key: 'quantity', header: 'Qty', align: 'right' },
              { key: 'unitAmount', header: 'Unit', align: 'right', render: (r) => formatMoney(r.unitAmount) },
              { key: 'amount', header: 'Amount', align: 'right', render: (r) => formatMoney(r.amount) },
            ]}
            rows={inv.lines || []}
            rowKey={(r) => r.line}
          />
          <dl className="ml-auto mt-4 grid max-w-xs grid-cols-2 gap-y-1 text-sm">
            <dt className="text-muted">Subtotal</dt><dd className="text-right tabular">{formatMoney(inv.subtotal)}</dd>
            <dt className="text-muted">VAT (7.5% on fees)</dt><dd className="text-right tabular">{formatMoney(inv.vat)}</dd>
            <dt className="font-extrabold text-navy">Total</dt><dd className="text-right font-extrabold text-navy tabular">{formatMoney(inv.total)}</dd>
            <dt className="text-muted">Paid</dt><dd className="text-right tabular">{formatMoney(inv.paidAmount)}</dd>
            <dt className="font-semibold text-navy">Outstanding</dt><dd className="text-right font-semibold text-navy tabular">{formatMoney(inv.outstanding)}</dd>
          </dl>
        </Card>

        <div className="space-y-4">
          <Card>
            <CardHeader title="Pay by bank transfer" description="Dedicated virtual account for this invoice; payments are matched automatically." />
            {inv.virtualAccountNumber ? (
              <div className="space-y-3">
                <div className="rounded-card bg-surface p-4">
                  <div className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-muted"><Landmark className="h-4 w-4" /> {inv.virtualAccountBank}</div>
                  <div className="mt-1 font-display text-2xl font-extrabold tracking-wider text-navy tabular">{inv.virtualAccountNumber}</div>
                  <div className="text-sm text-muted">{inv.virtualAccountName}</div>
                </div>
                <Button variant="secondary" size="sm" icon={Copy} className="w-full" onClick={() => copy(inv.virtualAccountNumber)}>{copied ? 'Copied' : 'Copy account number'}</Button>
                <p className="text-xs text-muted">Transfer exactly {formatMoney(inv.outstanding)}. Part-payments are flagged for review; over-payments are held and refunded. This page refreshes every 30 seconds until paid.</p>
              </div>
            ) : (
              <Alert tone="info">No virtual account was attached. Contact Plastoids finance for transfer details, quoting {inv.number}.</Alert>
            )}
          </Card>
          <Card>
            <CardHeader title="Details" />
            <KeyValue cols={1} items={[['Status', <StatusPill status={inv.status} />], ['Issued', formatDateTime(inv.issuedAt)], ['Due', formatDate(inv.dueAt)], ['Paid', inv.paidAt ? formatDateTime(inv.paidAt) : '—'], inv.programmeId ? ['Programme', <Button size="sm" variant="ghost" to={`/sponsor/programmes/${inv.programmeId}`}>Open programme</Button>] : null, ['Invoice id', <code className="text-xs">{inv.invoiceId}</code>]]} />
          </Card>
        </div>
      </div>
    </div>
  );
}
