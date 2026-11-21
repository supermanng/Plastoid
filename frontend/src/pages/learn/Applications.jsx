import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { ClipboardList, Sparkles } from 'lucide-react';
import { Alert, Button, Card, EmptyState, ErrorAlert, Loading, PageHeader, StatusPill } from '../../components/ui';
import { formatDate, formatDateTime, formatMoney, koboOf, relativeTime } from '../../lib/format';
import { applications, keys } from '../../api/learn';
import { applicationLabel } from './Dashboard';

export const OFFER_DAYS = 5;
export const APPEAL_DAYS = 7;

/** Maps an application to the programme name using the public opportunities list (no per-programme detail endpoint). */
export function useProgrammeNames() {
  const opps = useQuery({ queryKey: [...keys.opportunities, 'all'], queryFn: () => applications.opportunities({ limit: 100 }), staleTime: 5 * 60_000 });
  const byId = Object.fromEntries((opps.data?.items || []).map((o) => [o.programmeId, o]));
  return { byId, isPending: opps.isPending };
}

export default function Applications() {
  const apps = useQuery({ queryKey: keys.applications, queryFn: applications.mine });
  const { byId } = useProgrammeNames();
  const items = [...(apps.data || [])].sort((a, b) => new Date(b.submittedAt || 0) - new Date(a.submittedAt || 0));
  const missed = items.filter((a) => a.status === 'expired');

  return (
    <div>
      <PageHeader title="My applications" description="Sponsored seats you have applied for. Offers expire after five days; rejections can be appealed within seven." actions={<Button to="/opportunities" variant="secondary" icon={Sparkles}>Find opportunities</Button>} />
      {missed.length ? <Alert tone="warn" title={`${missed.length} offer${missed.length === 1 ? '' : 's'} expired`} className="mb-4">The seat went to the waitlist. You can apply to other open programmes.</Alert> : null}
      {apps.isPending ? <Loading /> : null}
      <ErrorAlert error={apps.error} />
      {apps.data && !items.length ? (
        <EmptyState icon={ClipboardList} title="No applications yet" description="Sponsors fund seats on selected courses. Check your eligibility and apply in under ten minutes." action={<Button to="/opportunities">See sponsored seats</Button>} />
      ) : null}
      <ul className="grid gap-4 md:grid-cols-2">
        {items.map((a) => {
          const prog = byId[a.programmeId];
          const coPay = koboOf(a.coPayAmount);
          return (
            <Card key={a.applicationId} as="li" className="flex flex-col gap-3">
              <div className="flex items-start justify-between gap-3">
                <div className="min-w-0">
                  <h3 className="truncate text-base font-bold text-navy">{prog?.name || 'Sponsored programme'}</h3>
                  <div className="text-xs text-muted">{prog ? `${prog.courseTitle || 'Custom programme'} · ${prog.sponsorName}` : `Programme ${a.programmeId.slice(0, 8)}`}</div>
                </div>
                <StatusPill status={a.status} label={applicationLabel(a.status)} />
              </div>
              <dl className="grid grid-cols-2 gap-2 text-xs text-muted">
                <div><dt>Submitted</dt><dd className="text-navy">{a.submittedAt ? formatDate(a.submittedAt) : 'Draft'}</dd></div>
                {a.assessmentScore != null ? <div><dt>Assessment</dt><dd className="text-navy">{Number(a.assessmentScore).toFixed(0)}% {a.assessmentPassed ? '· passed' : ''}</dd></div> : null}
                {a.eligible != null ? <div><dt>Eligibility</dt><dd className="text-navy">{a.eligible ? 'Eligible' : 'Not eligible'}</dd></div> : null}
                {coPay ? <div><dt>Co-payment</dt><dd className="text-navy">{formatMoney(a.coPayAmount)}</dd></div> : null}
              </dl>
              {a.status === 'offered' && a.offerExpiresAt ? (
                <Alert tone={new Date(a.offerExpiresAt) < new Date() ? 'danger' : 'warn'}>
                  Offer {new Date(a.offerExpiresAt) < new Date() ? 'expired' : 'expires'} {formatDateTime(a.offerExpiresAt)} ({relativeTime(a.offerExpiresAt)}).
                </Alert>
              ) : null}
              {a.flagged ? <Alert tone="info">Flagged for manual review{a.flagReason ? `: ${a.flagReason}` : ''}.</Alert> : null}
              <div className="mt-auto flex gap-2">
                <Button to={`/learn/applications/${a.applicationId}`} size="sm" variant={a.status === 'offered' ? 'teal' : 'primary'}>{a.status === 'offered' ? 'Respond to offer' : a.status === 'draft' ? 'Continue draft' : 'View'}</Button>
                {a.status === 'draft' ? <Button to={`/learn/apply/${a.programmeId}`} size="sm" variant="secondary">Edit answers</Button> : null}
              </div>
            </Card>
          );
        })}
      </ul>
      <p className="mt-6 text-xs text-muted"><Link to="/opportunities">Browse more sponsored opportunities</Link></p>
    </div>
  );
}
