import { useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Armchair } from 'lucide-react';
import { Card, EmptyState, Field, Loading, PageHeader, Select, Stat, StatusPill, Table } from '../../../shared/components/ui';
import { useQueryParams } from '../../../shared/lib/hooks';
import { formatDate, formatMoney } from '../../../shared/lib/format';
import { formatUnits } from '../../../shared/lib/units.jsx';
import { adminApi, adminKeys } from '../api/admin';
import { AdminError, Id, IdLookup, LimitSelect } from './_shared';

const DEFAULTS = { status: '', limit: '100' };
const STATUSES = [['', 'Any'], ['active', 'Active'], ['closed', 'Closed']];

/** /admin/seat-blocks — GET /admin/sponsorship/seat-blocks?status=&limit= → SeatBlockView[] across every organisation (ADR 0021, read-only). */
export default function SeatBlocks() {
  const navigate = useNavigate();
  const [params, setParam] = useQueryParams(DEFAULTS);
  const limit = Number(params.limit) || 100;
  const q = useQuery({ queryKey: adminKeys.seatBlocks(`${params.status}|${limit}`), queryFn: () => adminApi.seatBlocks(params.status, limit), placeholderData: (prev) => prev });
  const rows = q.data || [];
  const totals = rows.reduce((t, b) => ({ seats: t.seats + (b.seats || 0), allocated: t.allocated + (b.allocated || 0), open: t.open + (b.open || 0), released: t.released + (b.released || 0) }), { seats: 0, allocated: 0, open: 0, released: 0 });

  return (
    <div>
      <PageHeader title="Seat blocks" description="Seats organisations have bought in cohorts and given to learners. Read-only: sponsors buy, give and release seats themselves; open seats are released automatically when a cohort ends." />

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <Stat label="Blocks shown" value={q.data ? rows.length : '—'} icon={Armchair} />
        <Stat label="Seats bought" value={q.data ? totals.seats : '—'} hint={`${totals.released} released`} />
        <Stat label="Given out" value={q.data ? totals.allocated : '—'} tone="teal" />
        <Stat label="Open" value={q.data ? totals.open : '—'} tone="royal" />
      </div>

      <Card className="my-6">
        <div className="grid gap-3 sm:grid-cols-[1fr_auto]">
          <Field label="Status">
            {(p) => <Select {...p} value={params.status} onChange={(e) => setParam('status', e.target.value)}>{STATUSES.map(([v, l]) => <option key={v} value={v}>{l}</option>)}</Select>}
          </Field>
          <Field label="Rows">{() => <LimitSelect value={limit} onChange={(v) => setParam('limit', String(v))} />}</Field>
        </div>
      </Card>

      {q.isPending ? <Loading /> : null}
      {q.isError ? <AdminError error={q.error} /> : null}
      {q.data ? (
        rows.length ? (
          <Table
            rows={rows}
            rowKey={(r) => r.blockId}
            onRowClick={(r) => navigate(`/seat-blocks/${r.blockId}`)}
            columns={[
              { key: 'sponsor', header: 'Sponsor', render: (r) => <><div className="font-semibold">{r.sponsorName || '—'}</div><div className="text-[11px] text-muted"><Id value={r.organisationId} /></div></> },
              { key: 'course', header: 'Course · cohort', render: (r) => <><div className="font-semibold">{r.courseTitle}</div><div className="text-[11px] text-muted">{r.cohortName} · {formatDate(r.startsAt)} → {formatDate(r.endsAt)}</div></> },
              { key: 'seats', header: 'Seats', align: 'right', render: (r) => <span className="tabular">{r.allocated}/{r.seats} given · {r.open} open · {r.released} released</span> },
              { key: 'units', header: 'Paid', align: 'right', render: (r) => <span className="tabular text-xs">{formatUnits(r.unitsRedeemed)}<br /><span className="text-muted">{formatMoney(r.pricePerSeat)} / seat</span></span> },
              { key: 'status', header: 'Status', render: (r) => <StatusPill status={r.status} /> },
              { key: 'purchasedAt', header: 'Bought', render: (r) => <span className="text-xs text-muted">{formatDate(r.purchasedAt)}</span> },
            ]}
          />
        ) : (
          <EmptyState icon={Armchair} title="No seat blocks" description={params.status ? 'None with this status.' : 'Blocks appear here once a sponsor buys seats in a cohort.'} />
        )
      ) : null}

      <Card className="mt-6">
        <IdLookup label="Open a seat block by id" placeholder="block GUID" onOpen={(id) => navigate(`/seat-blocks/${id}`)} />
      </Card>
    </div>
  );
}
