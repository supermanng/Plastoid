import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { ArrowRight, BookOpen, CalendarDays, ClipboardList, Sparkles, Target, Video } from 'lucide-react';
import { Alert, Button, Card, CardHeader, EmptyState, ErrorAlert, PageHeader, ProgressRing, Skeleton, StatusPill } from '../../../../shared/components/ui';
import { useAuth } from '../../../../shared/lib/auth';
import { formatDate, formatDateTime, relativeTime } from '../../../../shared/lib/format';
import { OpportunityCard } from '../public/Home';
import { applications, keys, lms } from '../../api/learn';

const APPLICATION_LABELS = {
  draft: 'Draft (not submitted)',
  submitted: 'Under review',
  merged: 'Under review',
  accepted: 'Accepted — awaiting offer',
  rejected: 'Not selected',
  waitlisted: 'Waitlisted',
  offered: 'Offer received',
  pendingCoPay: 'Offer accepted — co-payment due',
  enrolled: 'Enrolled',
  declined: 'Declined',
  expired: 'Offer expired',
  withdrawn: 'Withdrawn',
};

export function applicationLabel(status) {
  return APPLICATION_LABELS[status] || status;
}

export default function Dashboard() {
  const auth = useAuth();
  const me = auth.me;
  const enrolments = useQuery({ queryKey: keys.enrolments, queryFn: lms.enrolments });
  const upcoming = useQuery({ queryKey: keys.upcoming(7), queryFn: () => lms.upcoming(7) });
  const apps = useQuery({ queryKey: keys.applications, queryFn: applications.mine });
  const opportunities = useQuery({ queryKey: [...keys.opportunities, 'dash'], queryFn: () => applications.opportunities({ limit: 3 }) });

  const active = (enrolments.data || []).filter((e) => e.status === 'active').sort((a, b) => b.progressPct - a.progressPct);
  const openApps = (apps.data || []).filter((a) => !['enrolled', 'declined', 'withdrawn', 'expired'].includes(a.status));
  const offers = (apps.data || []).filter((a) => a.status === 'offered' && a.offerExpiresAt);
  const newLearner = !me?.careerGoal && (me?.interests?.length ?? 0) === 0;

  return (
    <div>
      <PageHeader
        title={`${greeting()}, ${me?.displayName?.split(' ')[0] || 'there'}`}
        description={me?.careerGoal ? <span className="inline-flex items-center gap-1.5"><Target className="h-4 w-4 text-teal" /> Goal: {me.careerGoal}</span> : 'Set a career goal so we can recommend courses and sponsored seats.'}
        actions={<Button to="/courses" variant="secondary" icon={BookOpen}>Explore courses</Button>}
      />

      {newLearner ? (
        <Alert tone="info" title="Welcome to Plastoids" className="mb-6">
          Three quick questions help us match you with sponsored seats you qualify for. <Link to="/learn/onboarding" className="font-semibold">Set up your profile</Link>
        </Alert>
      ) : null}

      {offers.map((a) => (
        <Alert key={a.applicationId} tone="warn" title="You have an offer" className="mb-4">
          Accept it before {formatDateTime(a.offerExpiresAt)} ({relativeTime(a.offerExpiresAt)}) or the seat goes to the waitlist. <Link to={`/learn/applications/${a.applicationId}`} className="font-semibold">View offer</Link>
        </Alert>
      ))}

      <div className="grid gap-6 lg:grid-cols-3">
        <section className="lg:col-span-2 space-y-6">
          <Card>
            <CardHeader title="Continue learning" action={active.length ? <Button to="/learn/courses" variant="ghost" size="sm">All courses <ArrowRight className="h-4 w-4" /></Button> : null} />
            {enrolments.isPending ? <div className="space-y-3"><Skeleton className="h-16" /><Skeleton className="h-16" /></div> : null}
            <ErrorAlert error={enrolments.error} />
            {enrolments.data && !active.length ? (
              <EmptyState icon={BookOpen} title="No active courses yet" description="Enrol in a course or apply for a sponsored seat to start learning." action={<Button to="/courses" size="sm">Browse courses</Button>} />
            ) : null}
            <ul className="divide-y divide-line">
              {active.slice(0, 4).map((e) => (
                <li key={e.enrolmentId}>
                  <Link to={`/learn/courses/${e.enrolmentId}`} className="flex items-center gap-4 py-3 hover:bg-surface/60 -mx-2 px-2 rounded-input">
                    <ProgressRing value={Number(e.progressPct)} size={52} />
                    <div className="min-w-0 flex-1">
                      <div className="truncate font-semibold text-navy">{e.courseSlug ? titleFromSlug(e.courseSlug) : 'Course'}</div>
                      <div className="text-xs text-muted">{e.completedLessons} of {e.requiredLessons} required lessons · {e.fundingSource === 'sponsored' ? 'Sponsored seat' : 'Paid'} · access until {formatDate(e.expiresAt)}</div>
                    </div>
                    <ArrowRight className="h-4 w-4 shrink-0 text-muted" />
                  </Link>
                </li>
              ))}
            </ul>
          </Card>

          <Card>
            <CardHeader title="Application tracker" description="Sponsored seats you have applied for." action={<Button to="/learn/applications" variant="ghost" size="sm">All applications <ArrowRight className="h-4 w-4" /></Button>} />
            {apps.isPending ? <Skeleton className="h-16" /> : null}
            <ErrorAlert error={apps.error} />
            {apps.data && !openApps.length ? (
              <EmptyState icon={ClipboardList} title="No open applications" description="Sponsored programmes pay your tuition. Check what you qualify for." action={<Button to="/opportunities" size="sm" variant="secondary">See opportunities</Button>} />
            ) : null}
            <ul className="divide-y divide-line">
              {openApps.slice(0, 4).map((a) => (
                <li key={a.applicationId}>
                  <Link to={`/learn/applications/${a.applicationId}`} className="flex items-center justify-between gap-3 py-3">
                    <div className="min-w-0">
                      <div className="text-sm font-semibold text-navy">{applicationLabel(a.status)}</div>
                      <div className="text-xs text-muted">{a.submittedAt ? `Submitted ${formatDate(a.submittedAt)}` : 'Not submitted yet'}{a.offerExpiresAt ? ` · offer expires ${formatDate(a.offerExpiresAt)}` : ''}</div>
                    </div>
                    <StatusPill status={a.status} />
                  </Link>
                </li>
              ))}
            </ul>
          </Card>
        </section>

        <aside className="space-y-6">
          <Card>
            <CardHeader title="Upcoming classes" description="Next 7 days" />
            {upcoming.isPending ? <Skeleton className="h-12" /> : null}
            <ErrorAlert error={upcoming.error} />
            {upcoming.data && !upcoming.data.length ? <p className="text-sm text-muted">No live classes or tutor sessions scheduled.</p> : null}
            <ul className="space-y-3">
              {(upcoming.data || []).slice(0, 5).map((s) => (
                <li key={s.liveSessionId} className="flex items-start gap-3">
                  <span className="grid h-9 w-9 shrink-0 place-items-center rounded-full bg-cyan-50 text-royal">{s.source === 'booking' ? <CalendarDays className="h-4 w-4" /> : <Video className="h-4 w-4" />}</span>
                  <div className="min-w-0 flex-1">
                    <div className="truncate text-sm font-semibold text-navy">{s.title}</div>
                    <div className="text-xs text-muted">{formatDateTime(s.startsAt)}</div>
                  </div>
                  {s.joinOpen ? <Button to={`/learn/live/${s.liveSessionId}`} size="sm" variant="teal">Join</Button> : <span className="text-xs text-muted">{relativeTime(s.startsAt)}</span>}
                </li>
              ))}
            </ul>
          </Card>

          <Card>
            <CardHeader title="Sponsored seats" description="Open for applications" action={<Sparkles className="h-5 w-5 text-teal" />} />
            {opportunities.isPending ? <Skeleton className="h-24" /> : null}
            {opportunities.isError ? <p className="text-sm text-muted">Unavailable right now.</p> : null}
            <div className="space-y-3">
              {(opportunities.data?.items || []).map((o) => <OpportunityCard key={o.programmeId} o={o} />)}
            </div>
            {opportunities.data && !opportunities.data.items?.length ? <p className="text-sm text-muted">Nothing open right now. Check back soon.</p> : null}
            <Button to="/opportunities" variant="ghost" size="sm" className="mt-3">See all <ArrowRight className="h-4 w-4" /></Button>
          </Card>
        </aside>
      </div>
    </div>
  );
}

function greeting() {
  const h = new Date().getHours();
  return h < 12 ? 'Good morning' : h < 17 ? 'Good afternoon' : 'Good evening';
}

export function titleFromSlug(slug) {
  return String(slug || '').replace(/-[a-z0-9]{4,}$/i, '').replace(/-/g, ' ').replace(/(^|\s)\w/g, (c) => c.toUpperCase());
}
