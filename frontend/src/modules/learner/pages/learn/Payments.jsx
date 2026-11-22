import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { CreditCard } from 'lucide-react';
import { EmptyState, ErrorAlert, Loading, PageHeader, Pagination, StatusPill, Table } from '../../../../shared/components/ui';
import { useCursor } from '../../../../shared/lib/hooks';
import { formatDateTime, formatMoney, koboOf, titleCase } from '../../../../shared/lib/format';
import { keys, payments } from '../../api/learn';

/** GET /payments/me: every payment intent (course, booking, package, co-pay) with status. */
export default function Payments() {
  const cursor = useCursor();
  const query = useQuery({ queryKey: keys.payments(cursor.cursor), queryFn: () => payments.mine({ cursor: cursor.cursor, limit: 25 }) });
  const items = query.data?.items || [];
  const pending = items.filter((p) => p.status === 'pending');

  return (
    <div>
      <PageHeader title="Payments" description="Receipts for courses, tutor sessions and co-payments. Refunds show against the original payment." />
      {query.isPending ? <Loading /> : null}
      <ErrorAlert error={query.error} />
      {pending.length ? (
        <div className="mb-4 rounded-card border border-warn/30 bg-warn-50 p-4 text-sm text-navy">
          {pending.length} payment{pending.length === 1 ? '' : 's'} not completed. Open one to finish checkout or verify it.
        </div>
      ) : null}
      {query.data ? (
        <Table
          rows={items}
          rowKey={(r) => r.paymentIntentId}
          empty={<EmptyState icon={CreditCard} title="No payments yet" description="Payments appear here when you enrol in a paid course or book a tutor." />}
          columns={[
            { key: 'createdAt', header: 'Date', render: (r) => formatDateTime(r.createdAt) },
            { key: 'description', header: 'What for', render: (r) => <Link to={`/learn/payments/${r.paymentIntentId}`} className="font-medium">{r.description || titleCase(r.purpose)}</Link> },
            { key: 'purpose', header: 'Type', render: (r) => <span className="capitalize">{titleCase(r.purpose)}</span> },
            { key: 'amount', header: 'Amount', align: 'right', render: (r) => formatMoney(r.amount) },
            { key: 'refunded', header: 'Refunded', align: 'right', render: (r) => (koboOf(r.refundedAmount) ? formatMoney(r.refundedAmount) : '—') },
            { key: 'status', header: 'Status', render: (r) => <StatusPill status={r.status} /> },
          ]}
        />
      ) : null}
      <Pagination hasMore={Boolean(query.data?.nextCursor)} onMore={() => cursor.next(query.data.nextCursor)} canBack={cursor.canBack} onBack={cursor.back} loading={query.isFetching} />
    </div>
  );
}
