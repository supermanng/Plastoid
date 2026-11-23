import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Building2, CalendarDays, Users } from 'lucide-react';
import { Card, ErrorAlert, Skeleton, StatusPill } from '../../../../shared/components/ui';
import { formatDate, pluralise } from '../../../../shared/lib/format';
import { UnitPrice } from '../../../../shared/lib/units.jsx';
import { cohortItems, listCohorts, queryKeys } from '../../../../shared/api/public';

/*
 * Upcoming cohorts (ADR 0021): dated runs of a course that sponsors buy seats in. Public read: GET /lms/cohorts.
 * CohortListing { cohortId, courseId, courseSlug, courseTitle, name, startsAt, endsAt, capacity, enrolled, reserved, seatsLeft, pricePerSeat, providerName? }
 */
export function useUpcomingCohorts(filters = {}, options = {}) {
  return useQuery({ queryKey: queryKeys.cohorts(filters), queryFn: () => listCohorts(filters), staleTime: 60_000, ...options });
}

/** Card linking to the course page; `showCourse=false` on the course page itself. */
export function CohortCard({ cohort: c, showCourse = true, compact = false }) {
  const left = c.seatsLeft ?? Math.max(0, (c.capacity || 0) - (c.enrolled || 0) - (c.reserved || 0));
  const started = new Date(c.startsAt) <= new Date();
  const body = (
    <>
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          {showCourse ? <h3 className="truncate text-base font-bold leading-snug text-navy">{c.courseTitle}</h3> : null}
          <p className={`truncate ${showCourse ? 'mt-0.5 text-sm text-muted' : 'text-base font-bold text-navy'}`}>{c.name}</p>
          {showCourse && c.providerName ? <p className="mt-0.5 flex items-center gap-1 text-xs font-semibold text-muted"><Building2 className="h-3.5 w-3.5 text-cyan" aria-hidden /> {c.providerName}</p> : null}
        </div>
        {left <= 0 ? <StatusPill tone="neutral" status="full" label="Full" /> : started ? <StatusPill tone="info" status="running" label="Running" /> : <StatusPill tone="success" status="open" label="Open" />}
      </div>
      <ul className={`flex flex-wrap gap-x-4 gap-y-1 text-xs text-muted ${compact ? '' : 'mt-1'}`}>
        <li className="inline-flex items-center gap-1"><CalendarDays className="h-3.5 w-3.5 text-cyan" aria-hidden /> {formatDate(c.startsAt)} → {formatDate(c.endsAt)}</li>
        <li className="inline-flex items-center gap-1"><Users className="h-3.5 w-3.5 text-cyan" aria-hidden /> {left > 0 ? `${pluralise(left, 'seat')} left` : 'No seats left'}{c.capacity ? ` of ${c.capacity}` : ''}</li>
      </ul>
      {!compact ? (
        <div className="mt-auto flex items-center justify-between text-xs text-muted">
          <span>per seat</span>
          <UnitPrice money={c.pricePerSeat} size="sm" inline />
        </div>
      ) : null}
    </>
  );
  if (!showCourse || !c.courseSlug) return <Card className={`flex flex-col ${compact ? 'gap-1.5' : 'gap-3'}`}>{body}</Card>;
  return <Card as={Link} to={`/courses/${c.courseSlug}`} className={`flex flex-col ${compact ? 'gap-1.5' : 'gap-3'} transition-shadow hover:shadow-md`}>{body}</Card>;
}

/** Loading, error and empty states around a grid of cohort cards. */
export function CohortGrid({ query, columns = 'sm:grid-cols-2 lg:grid-cols-3', empty, showCourse = true, compact = false, limit }) {
  if (query.isPending) {
    return (
      <div className={`grid gap-4 ${columns}`}>
        {[0, 1, 2].map((i) => <Skeleton key={i} className="h-36" />)}
      </div>
    );
  }
  if (query.isError) return <ErrorAlert error={query.error} title="Upcoming cohorts are unavailable right now" />;
  const items = cohortItems(query.data).slice(0, limit || undefined);
  if (!items.length) return empty || <p className="text-sm text-muted">No upcoming cohorts right now.</p>;
  return (
    <div className={`grid gap-4 ${columns}`}>
      {items.map((c) => <CohortCard key={c.cohortId} cohort={c} showCourse={showCourse} compact={compact} />)}
    </div>
  );
}
