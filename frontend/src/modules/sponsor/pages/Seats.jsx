import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { Armchair, CalendarDays, Plus } from 'lucide-react';
import { Button, Card, EmptyState, PageHeader, QueryState, Stat, StatusPill } from '../../../shared/components/ui';
import { formatDate, pluralise } from '../../../shared/lib/format';
import { UnitPrice } from '../../../shared/lib/units.jsx';
import { seatsApi, seatsKeys } from '../api/seats';
import { useOrganisation } from './useOrganisation';
import { NoOrganisation, OrganisationSwitcher, RoleHint, VerificationBanner } from './OrganisationSwitcher';

/** /sponsor/seats — every seat block the organisation has bought: GET /sponsorship/organisations/{id}/seat-blocks. */
export default function Seats() {
  const org = useOrganisation();
  const orgId = org.organisationId;
  const q = useQuery({ queryKey: seatsKeys.blocks(orgId), queryFn: () => seatsApi.listBlocks(orgId), enabled: Boolean(orgId) });

  if (!org.organisation) {
    return (
      <div>
        <PageHeader title="Seats" />
        <NoOrganisation />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Seats"
        description="Seats your organisation has bought in course cohorts, and who has them."
        actions={
          <>
            <OrganisationSwitcher org={org} />
            <Button to="/seats/new" icon={Plus} disabled={!org.isFinance}>Buy seats</Button>
          </>
        }
      />
      <VerificationBanner org={org} />
      <RoleHint org={org} needs="finance" />

      <QueryState query={q} empty={<FirstBlock org={org} />}>
        {(blocks) => <BlockList blocks={blocks} />}
      </QueryState>
    </div>
  );
}

function BlockList({ blocks }) {
  const totals = totalsOf(blocks);
  return (
    <>
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <Stat label="Seat blocks" value={blocks.length} hint={`${totals.active} active`} icon={Armchair} />
        <Stat label="Seats bought" value={totals.seats} hint={`${totals.released} released`} />
        <Stat label="Given out" value={totals.allocated} hint={`${totals.enrolled} enrolled · ${totals.invited} invited`} tone="teal" />
        <Stat label="Open" value={totals.open} hint="Seats still to give out" tone={totals.open ? 'royal' : 'default'} />
      </div>
      <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {blocks.map((b) => (
          <li key={b.blockId}><SeatBlockCard block={b} /></li>
        ))}
      </ul>
    </>
  );
}

export function SeatBlockCard({ block: b }) {
  return (
    <Card as={Link} to={`/seats/${b.blockId}`} className="flex h-full flex-col gap-3 transition-shadow hover:shadow-md">
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <h3 className="truncate text-base font-bold text-navy">{b.courseTitle || 'Course'}</h3>
          <p className="truncate text-sm text-muted">{b.cohortName || 'Cohort'}</p>
        </div>
        <StatusPill status={b.status} />
      </div>
      <p className="flex items-center gap-1.5 text-xs text-muted"><CalendarDays className="h-3.5 w-3.5 text-cyan" aria-hidden /> {formatDate(b.startsAt)} → {formatDate(b.endsAt)}</p>
      <dl className="grid grid-cols-4 gap-2 text-center text-xs text-muted">
        <Cell label="Seats" value={b.seats} />
        <Cell label="Given" value={b.allocated} />
        <Cell label="Open" value={b.open} highlight={b.open > 0 && b.status === 'active'} />
        <Cell label="Released" value={b.released} />
      </dl>
      <div className="mt-auto flex items-center justify-between text-xs text-muted">
        <span><UnitPrice money={b.pricePerSeat} size="sm" inline /> per seat</span>
        <span>{pluralise(b.enrolled || 0, 'learner')} enrolled</span>
      </div>
    </Card>
  );
}

function Cell({ label, value, highlight = false }) {
  return (
    <div className="rounded-input bg-surface py-1.5">
      <dt className="text-[11px] uppercase tracking-wide">{label}</dt>
      <dd className={`text-base font-extrabold tabular ${highlight ? 'text-royal' : 'text-navy'}`}>{value ?? 0}</dd>
    </div>
  );
}

function FirstBlock({ org }) {
  return (
    <EmptyState
      icon={Armchair}
      title="No seats bought yet"
      description="Pick a course cohort, pay for a number of seats from the organisation wallet, then give each seat to a learner by phone number or email. People who already have an account are enrolled at once; everyone else is enrolled when they sign up."
      action={<Button to="/seats/new" icon={Plus} disabled={!org.isFinance}>Buy seats</Button>}
    />
  );
}

export function totalsOf(blocks = []) {
  return blocks.reduce(
    (t, b) => ({
      seats: t.seats + (b.seats || 0),
      allocated: t.allocated + (b.allocated || 0),
      enrolled: t.enrolled + (b.enrolled || 0),
      invited: t.invited + (b.invited || 0),
      released: t.released + (b.released || 0),
      open: t.open + (b.open || 0),
      active: t.active + (b.status === 'active' ? 1 : 0),
    }),
    { seats: 0, allocated: 0, enrolled: 0, invited: 0, released: 0, open: 0, active: 0 },
  );
}
