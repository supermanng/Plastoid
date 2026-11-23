import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { ArrowRight, BookOpen, CalendarDays, Sparkles, Target, Video } from 'lucide-react';
import { Alert, Button, Card, CardHeader, EmptyState, ErrorAlert, PageHeader, ProgressRing, Skeleton } from '../../../../shared/components/ui';
import { useAuth } from '../../../../shared/lib/auth';
import { formatDate, formatDateTime, relativeTime } from '../../../../shared/lib/format';
import { keys, lms } from '../../api/learn';
import { CohortGrid, useUpcomingCohorts } from '../public/CohortCard';
import { SponsoredChip } from './SponsoredChip';

export default function Dashboard() {
  const auth = useAuth();
  const me = auth.me;
  const enrolments = useQuery({ queryKey: keys.enrolments, queryFn: lms.enrolments });
  const upcoming = useQuery({ queryKey: keys.upcoming(7), queryFn: () => lms.upcoming(7) });
  const cohorts = useUpcomingCohorts({ limit: 3 });

  const active = (enrolments.data || []).filter((e) => e.status === 'active').sort((a, b) => b.progressPct - a.progressPct);
  const sponsored = (enrolments.data || []).filter((e) => e.fundingSource === 'sponsored');
  const newLearner = !me?.careerGoal && (me?.interests?.length ?? 0) === 0;

  return (
    <div>
      <PageHeader
        title={`${greeting()}, ${me?.displayName?.split(' ')[0] || 'there'}`}
        description={me?.careerGoal ? <span className="inline-flex items-center gap-1.5"><Target className="h-4 w-4 text-teal" /> Goal: {me.careerGoal}</span> : 'Set a career goal so we can recommend courses and cohorts.'}
        actions={<Button to="/courses" variant="secondary" icon={BookOpen}>Explore courses</Button>}
      />

      {newLearner ? (
        <Alert tone="info" title="Welcome to Plastoids" className="mb-6">
          Three quick questions help us recommend the right courses. <Link to="/learn/onboarding" className="font-semibold">Set up your profile</Link>
        </Alert>
      ) : null}

      {sponsored.length ? (
        <Alert tone="success" title={sponsored.length === 1 ? 'You have a sponsored seat' : `You have ${sponsored.length} sponsored seats`} className="mb-6">
          {sponsored.slice(0, 3).map((e) => (
            <span key={e.enrolmentId} className="mr-3 inline-flex items-center gap-2">
              <Link to={`/learn/courses/${e.enrolmentId}`} className="font-semibold">{e.courseSlug ? titleFromSlug(e.courseSlug) : 'Course'}</Link>
              <SponsoredChip enrolment={e} />
            </span>
          ))}
        </Alert>
      ) : null}

      <div className="grid gap-6 lg:grid-cols-3">
        <section className="lg:col-span-2 space-y-6">
          <Card>
            <CardHeader title="Continue learning" action={active.length ? <Button to="/learn/courses" variant="ghost" size="sm">All courses <ArrowRight className="h-4 w-4" /></Button> : null} />
            {enrolments.isPending ? <div className="space-y-3"><Skeleton className="h-16" /><Skeleton className="h-16" /></div> : null}
            <ErrorAlert error={enrolments.error} />
            {enrolments.data && !active.length ? (
              <EmptyState icon={BookOpen} title="No active courses yet" description="Enrol in a course to start learning. If an employer or sponsor gives you a seat, it appears here automatically." action={<Button to="/courses" size="sm">Browse courses</Button>} />
            ) : null}
            <ul className="divide-y divide-line">
              {active.slice(0, 4).map((e) => (
                <li key={e.enrolmentId}>
                  <Link to={`/learn/courses/${e.enrolmentId}`} className="flex items-center gap-4 py-3 hover:bg-surface/60 -mx-2 px-2 rounded-input">
                    <ProgressRing value={Number(e.progressPct)} size={52} />
                    <div className="min-w-0 flex-1">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="truncate font-semibold text-navy">{e.courseSlug ? titleFromSlug(e.courseSlug) : 'Course'}</span>
                        <SponsoredChip enrolment={e} />
                      </div>
                      <div className="text-xs text-muted">{e.completedLessons} of {e.requiredLessons} required lessons · {e.fundingSource === 'sponsored' ? 'Sponsored seat' : 'Paid'} · access until {formatDate(e.expiresAt)}</div>
                    </div>
                    <ArrowRight className="h-4 w-4 shrink-0 text-muted" />
                  </Link>
                </li>
              ))}
            </ul>
          </Card>

          <Card>
            <CardHeader title="Upcoming cohorts" description="Dated runs with live classes. Enrol yourself, or ask your employer to sponsor a seat." action={<Sparkles className="h-5 w-5 text-teal" />} />
            <CohortGrid query={cohorts} limit={3} columns="sm:grid-cols-2 xl:grid-cols-3" compact empty={<p className="text-sm text-muted">No cohorts are scheduled right now. Check back soon.</p>} />
            <Button to="/courses?format=cohort" variant="ghost" size="sm" className="mt-3">All cohort courses <ArrowRight className="h-4 w-4" /></Button>
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
