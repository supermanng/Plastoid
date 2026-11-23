import { useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Armchair, Users } from 'lucide-react';
import { Breadcrumb, Button, Card, CardHeader, EmptyState, KeyValue, Loading, PageHeader, Stat, StatusPill, Table } from '../../../shared/components/ui';
import { formatDate, formatDateTime, formatMoney } from '../../../shared/lib/format';
import { formatUnits } from '../../../shared/lib/units.jsx';
import { adminApi, adminKeys } from '../api/admin';
import { AdminError, Id, UserLink } from './_shared';

const ALLOCATION_LABEL = { invited: 'Invited', enrolled: 'Enrolled' };

/** /admin/seat-blocks/:blockId — GET /admin/sponsorship/seat-blocks/{blockId} → SeatBlockDetail (read-only). */
export default function SeatBlockDetail() {
  const { blockId } = useParams();
  const q = useQuery({ queryKey: adminKeys.seatBlock(blockId), queryFn: () => adminApi.seatBlock(blockId) });

  if (q.isPending) return <Loading />;
  if (q.isError) {
    return (
      <div>
        <Breadcrumb items={[{ to: '/seat-blocks', label: 'Seat blocks' }, { label: 'Not found' }]} />
        <div className="mt-4">
          {q.error?.status === 404 ? <EmptyState icon={Armchair} title="Seat block not found" description="Check the id." action={<Button to="/seat-blocks" variant="secondary">All seat blocks</Button>} /> : <AdminError error={q.error} />}
        </div>
      </div>
    );
  }
  const b = q.data;

  return (
    <div>
      <Breadcrumb items={[{ to: '/seat-blocks', label: 'Seat blocks' }, { label: b.courseTitle || 'Seat block' }]} />
      <PageHeader
        className="mt-2"
        title={b.courseTitle || 'Seat block'}
        description={`${b.cohortName} · ${formatDate(b.startsAt)} → ${formatDate(b.endsAt)} · sponsored by ${b.sponsorName || 'an organisation'}`}
        actions={<StatusPill status={b.status} />}
      />

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <Stat label="Seats bought" value={b.seats} hint={`${formatUnits(b.unitsRedeemed)} redeemed`} icon={Armchair} />
        <Stat label="Given out" value={b.allocated} hint={`${b.enrolled} enrolled · ${b.invited} invited`} icon={Users} tone="teal" />
        <Stat label="Open" value={b.open} tone="royal" />
        <Stat label="Released" value={b.released} />
      </div>

      <div className="mt-6 grid gap-6 lg:grid-cols-[1fr_360px]">
        <Card>
          <CardHeader title="Allocations" description="One seat per learner. Invited seats convert when that phone number or email registers." />
          <Table
            rows={b.allocations || []}
            rowKey={(r) => r.allocationId}
            empty={<EmptyState icon={Users} title="No seats given out" />}
            columns={[
              { key: 'seatNo', header: '#', render: (r) => <span className="tabular text-muted">{r.seatNo}</span> },
              { key: 'displayName', header: 'Learner', render: (r) => <><div className="font-semibold">{r.displayName || '—'}</div>{r.learnerId ? <UserLink id={r.learnerId} /> : null}</> },
              { key: 'contact', header: 'Contact', render: (r) => <span className="font-mono text-xs">{r.contact || '—'}</span> },
              { key: 'status', header: 'Status', render: (r) => <StatusPill status={r.status} tone={r.status === 'enrolled' ? 'success' : 'warn'} label={ALLOCATION_LABEL[r.status] || r.status} /> },
              { key: 'when', header: 'When', render: (r) => <span className="text-xs text-muted">Invited {formatDateTime(r.invitedAt)}{r.enrolledAt ? <><br />Enrolled {formatDateTime(r.enrolledAt)}</> : null}</span> },
              { key: 'enrolmentId', header: 'Enrolment', render: (r) => <Id value={r.enrolmentId} /> },
            ]}
          />
        </Card>

        <Card className="h-fit">
          <CardHeader title="Block" />
          <KeyValue
            cols={1}
            items={[
              ['Block id', <Id value={b.blockId} />],
              ['Organisation', <><span>{b.sponsorName}</span> <Id value={b.organisationId} /></>],
              ['Cohort', <><span>{b.cohortName}</span> <Id value={b.cohortId} /></>],
              ['Course', <><span>{b.courseTitle}</span> <Id value={b.courseId} /></>],
              ['Price per seat', formatMoney(b.pricePerSeat)],
              ['Units redeemed', `${formatUnits(b.unitsRedeemed)} at ${formatMoney({ amount: b.unitRateKobo, currency: 'NGN' })}`],
              ['Bought', formatDateTime(b.purchasedAt)],
              b.closedAt ? ['Closed', formatDateTime(b.closedAt)] : null,
              b.walletBalance !== undefined && b.walletBalance !== null ? ['Organisation wallet', formatUnits(b.walletBalance)] : null,
            ]}
          />
          <p className="mt-4 text-xs text-muted">Ledger: entry 14 on purchase (2050 → 2020 held under this block), entry 15 per enrolled seat (commission, tutor share, rest), entry 16 on release (2020 → 2050).</p>
        </Card>
      </div>
    </div>
  );
}
