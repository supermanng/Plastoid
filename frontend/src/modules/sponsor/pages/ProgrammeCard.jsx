import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Card, ProgressBar, Skeleton, StatusPill } from '../../../shared/components/ui';
import { formatDate, formatMoney, koboOf } from '../../../shared/lib/format';
import { PROGRAMME_STATUS_LABEL, sponsorApi, sponsorKeys } from '../api/sponsor';

export function statusLabel(status) {
  return PROGRAMME_STATUS_LABEL[status] || PROGRAMME_STATUS_LABEL[String(status || '').toLowerCase()] || status;
}

/** Hydrates one programme id and renders a summary card (seats / funded / filled). */
export function ProgrammeCard({ orgId, programmeId, onMissing }) {
  const q = useQuery({
    queryKey: sponsorKeys.programme(orgId, programmeId),
    queryFn: () => sponsorApi.getProgramme(orgId, programmeId),
    retry: false,
  });

  if (q.isPending) return <Skeleton className="h-40" />;
  if (q.isError) {
    if (q.error?.status === 404 || q.error?.status === 403) {
      return (
        <Card className="text-sm text-muted">
          Programme <code className="text-xs">{programmeId}</code> is no longer available.
          {onMissing ? <button type="button" className="ml-2 text-royal hover:underline" onClick={() => onMissing(programmeId)}>Remove</button> : null}
        </Card>
      );
    }
    return <Card className="text-sm text-danger">Could not load programme.</Card>;
  }
  return <ProgrammeSummary p={q.data} />;
}

export function ProgrammeSummary({ p }) {
  const filledPct = p.seats ? (100 * (p.seatsUnlocked || 0)) / p.seats : 0;
  const fundedKobo = koboOf(p.fundedAmount) || 0;
  const totalKobo = koboOf(p.quote?.total) || 0;
  return (
    <Card as={Link} to={`/programmes/${p.programmeId}`} className="flex flex-col gap-3 transition-shadow hover:shadow-md">
      <div className="flex items-start justify-between gap-2">
        <StatusPill status={p.status} label={statusLabel(p.status)} />
        {p.status === 'published' ? <StatusPill tone="success" status="live" label="Open for applications" /> : <StatusPill status={p.reviewStatus} label={`Review: ${String(p.reviewStatus).replace(/([a-z])([A-Z])/g, '$1 $2').toLowerCase()}`} />}
      </div>
      <div>
        <h3 className="text-base font-bold leading-snug text-navy">{p.name || 'Untitled campaign'}</h3>
        <p className="mt-0.5 text-sm text-muted">{p.courseTitle || 'No course selected yet'}</p>
      </div>
      <div className="grid grid-cols-3 gap-2 text-center text-xs text-muted">
        <div><div className="text-lg font-extrabold text-navy tabular">{p.seats || 0}</div>seats</div>
        <div><div className="text-lg font-extrabold text-navy tabular">{p.isFunded ? 'Yes' : totalKobo ? `${Math.round((100 * fundedKobo) / totalKobo)}%` : '—'}</div>funded</div>
        <div><div className="text-lg font-extrabold text-navy tabular">{p.seatsUnlocked || 0}</div>unlocked</div>
      </div>
      <ProgressBar value={filledPct} label="Seats unlocked" />
      <div className="mt-auto flex flex-wrap justify-between gap-2 text-xs text-muted">
        <span>Budget {formatMoney(p.budget)}</span>
        {p.startsAt ? <span>Starts {formatDate(p.startsAt)}</span> : <span>No dates yet</span>}
      </div>
    </Card>
  );
}
