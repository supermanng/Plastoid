import { Link, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { CreditCard, Printer, RefreshCw } from 'lucide-react';
import { Alert, Breadcrumb, Button, Card, CardHeader, ErrorAlert, KeyValue, Loading, StatusPill } from '../../../../shared/components/ui';
import { useAction } from '../../../../shared/lib/hooks';
import { formatDateTime, formatMoney, koboOf, titleCase } from '../../../../shared/lib/format';
import { isDevCheckout, keys, payments } from '../../api/learn';

export default function PaymentDetail() {
  const { paymentIntentId } = useParams();
  const query = useQuery({ queryKey: keys.payment(paymentIntentId), queryFn: () => payments.intent(paymentIntentId) });
  const receipt = useQuery({ queryKey: keys.receipt(paymentIntentId), queryFn: () => payments.receipt(paymentIntentId), enabled: query.data?.status === 'succeeded', retry: false });
  const verify = useAction(() => payments.verify(paymentIntentId), {
    invalidate: [keys.payment(paymentIntentId), ['payments'], ['bookings'], keys.enrolments],
    success: (r) => (r.status === 'succeeded' ? 'Payment confirmed' : `Gateway says: ${r.gatewayStatus}`),
  });

  if (query.isPending) return <Loading />;
  if (query.isError) return <ErrorAlert error={query.error} />;
  const p = query.data;
  const checkout = String(p.checkoutUrl || '');

  return (
    <div className="mx-auto max-w-3xl">
      <Breadcrumb items={[{ to: '/learn/payments', label: 'Payments' }, { label: p.description || titleCase(p.purpose) }]} />
      <div className="mt-2 flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl">{p.description || titleCase(p.purpose)}</h1>
          <p className="text-sm text-muted">{formatMoney(p.amount)} · {titleCase(p.purpose)} · created {formatDateTime(p.createdAt)}</p>
        </div>
        <StatusPill status={p.status} />
      </div>

      {p.status === 'pending' ? (
        <Card className="mt-6 border-warn/40">
          <CardHeader title="Finish this payment" description="Pay through the secure gateway, then come back and verify." />
          <div className="flex flex-wrap gap-2">
            <Button icon={CreditCard} href={checkout}>Pay {formatMoney(p.amount)}</Button>
            <Button variant="secondary" icon={RefreshCw} loading={verify.isPending} onClick={() => verify.mutate()}>I have paid — verify</Button>
          </div>
          <ErrorAlert error={verify.error} className="mt-3" />
          {isDevCheckout(checkout) ? <Alert tone="info" className="mt-3" title="Development gateway">The fake gateway has no checkout page. Trigger the webhook (see the Payments runbook) or run the fake settlement, then click verify.</Alert> : null}
        </Card>
      ) : null}
      {p.status === 'failed' ? (
        <Alert tone="danger" className="mt-6" title="Payment failed">{p.failureReason || 'The gateway declined the payment.'} Start again from the course, booking or offer page to create a new payment.</Alert>
      ) : null}
      {p.status === 'expired' ? <Alert tone="warn" className="mt-6" title="Payment expired">The checkout window closed. Start again from the course, booking or offer page.</Alert> : null}

      {p.status === 'succeeded' ? (
        <Card className="mt-6 print:shadow-none" id="receipt">
          <CardHeader title="Receipt" description={receipt.data ? `No. ${receipt.data.receiptNumber}` : undefined} action={<Button variant="secondary" size="sm" icon={Printer} onClick={() => window.print()}>Print / save PDF</Button>} />
          {receipt.isPending ? <Loading /> : null}
          <ErrorAlert error={receipt.error} />
          <KeyValue items={[
            ['Paid', formatDateTime(receipt.data?.paidAt || p.succeededAt)],
            ['Amount', formatMoney(p.amount)],
            ['Description', receipt.data?.description || p.description],
            ['Gateway', `${titleCase(p.gateway)} · ${p.gatewayReference}`],
            koboOf(p.refundedAmount) ? ['Refunded', formatMoney(p.refundedAmount)] : null,
            ['Payment ID', <code className="text-xs">{p.paymentIntentId}</code>],
          ]} />
          <p className="mt-4 text-xs text-muted">Plastoids collects payment on behalf of the tutor or sponsor; VAT where applicable is included. Keep this receipt for refunds.</p>
        </Card>
      ) : null}

      <div className="mt-6 flex flex-wrap gap-2 text-sm">
        {p.purpose === 'booking' || p.purpose === 'package' ? <Link to="/learn/bookings" className="font-semibold">Go to tutor sessions</Link> : null}
        {p.purpose === 'course' || p.purpose === 'enrolment' ? <Link to="/learn/courses" className="font-semibold">Go to my courses</Link> : null}
      </div>
    </div>
  );
}
