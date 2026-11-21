import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { BookOpen, CalendarDays, Clock, Plus, Star, Users, Video, Wallet } from 'lucide-react';
import { Button, Card, CardHeader, EmptyState, ErrorAlert, Loading, PageHeader, Skeleton, Stat, StatusPill } from '../../components/ui';
import { useAuth } from '../../lib/auth';
import { formatDateTime, formatMoney, formatTime, koboOf } from '../../lib/format';
import { bookingApi, earningsApi, lmsApi, tutorApi, tutorKeys } from '../../api/tutor';
import { TutorStatusBanners, VerificationSummary } from './shared';

/*
 * /tutor — today's sessions, earnings card, verification tracker, quick actions.
 * GET /tutors/me · GET /bookings/me?role=tutor&upcoming=true · GET /lms/live-sessions/upcoming · GET /payouts/me
 */
export default function TutorDashboard() {
  const auth = useAuth();
  const me = useQuery({ queryKey: tutorKeys.me, queryFn: tutorApi.me });
  const bookings = useQuery({ queryKey: tutorKeys.bookings(true), queryFn: () => bookingApi.mine({ upcoming: true, limit: 20 }) });
  const live = useQuery({ queryKey: tutorKeys.upcomingSessions(7), queryFn: () => lmsApi.upcoming(7) });
  const earnings = useQuery({ queryKey: tutorKeys.earnings, queryFn: earningsApi.me });

  if (me.isPending) return <Loading />;
  if (me.isError) {
    return (
      <div>
        <PageHeader title="Tutor dashboard" />
        {me.error?.status === 404 ? (
          <EmptyState icon={BookOpen} title="No tutor profile yet" description="Apply to teach to create your profile and start verification." action={<Button to="/tutor/onboarding">Apply to teach</Button>} />
        ) : <ErrorAlert error={me.error} />}
      </div>
    );
  }

  const d = me.data;
  const today = new Date().toDateString();
  const todaysSessions = (live.data || []).filter((s) => new Date(s.startsAt).toDateString() === today);
  const nextBooking = (bookings.data?.items || [])[0];
  const payoutStage = d.verification?.find((s) => s.name === 'payout_account');

  return (
    <div>
      <PageHeader
        title={`Hello, ${auth.me?.displayName?.split(' ')[0] || 'tutor'}`}
        description={d.headline}
        actions={
          <>
            <Button to="/tutor/courses" icon={Plus}>Create course</Button>
            <Button to="/tutor/profile" variant="secondary" icon={CalendarDays}>Set availability</Button>
          </>
        }
      />
      <TutorStatusBanners dashboard={d} />

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <Stat label="Sessions delivered" value={d.sessionsDelivered ?? 0} icon={Video} />
        <Stat label="Learners taught" value={d.enrolmentsTaught ?? 0} icon={Users} />
        <Stat label="Rating" value={d.ratingAverage ? Number(d.ratingAverage).toFixed(1) : 'New'} hint={`Quality score ${Number(d.qualityScore ?? 0).toFixed(0)} · ${d.level} tutor`} icon={Star} />
        <Stat label="Hourly rate" value={formatMoney(d.hourlyRate)} hint={`${d.priceRules} price rule${d.priceRules === 1 ? '' : 's'}`} icon={Wallet} />
      </div>

      <div className="mt-6 grid gap-6 lg:grid-cols-3">
        {/* Today */}
        <Card className="lg:col-span-2">
          <CardHeader title="Today's sessions" description="Live classes and booked one-to-ones you host." action={<Button to="/tutor/sessions" variant="ghost" size="sm">All sessions</Button>} />
          {live.isPending ? <Skeleton className="h-24" /> : null}
          {live.isError ? <ErrorAlert error={live.error} /> : null}
          {live.data && todaysSessions.length === 0 ? (
            <EmptyState icon={Clock} title="Nothing scheduled today" description={nextBooking ? `Next booking ${formatDateTime(nextBooking.slotStart)}.` : 'Open your availability so learners can book you.'} className="py-8" />
          ) : null}
          {todaysSessions.length ? (
            <ul className="divide-y divide-line">
              {todaysSessions.map((s) => (
                <li key={s.liveSessionId} className="flex flex-wrap items-center justify-between gap-3 py-3">
                  <div>
                    <div className="font-semibold text-navy">{s.title}</div>
                    <div className="text-sm text-muted">{formatTime(s.startsAt)} – {formatTime(s.endsAt)} · {s.source}</div>
                  </div>
                  <div className="flex items-center gap-2">
                    <StatusPill status={s.status} />
                    {s.joinOpen ? <Button to={`/tutor/sessions?join=${s.liveSessionId}`} size="sm">Start session</Button> : null}
                  </div>
                </li>
              ))}
            </ul>
          ) : null}
        </Card>

        {/* Earnings */}
        <Card>
          <CardHeader title="Earnings" action={<Button to="/tutor/earnings" variant="ghost" size="sm">Details</Button>} />
          {earnings.isPending ? <Skeleton className="h-28" /> : null}
          {earnings.isError ? <ErrorAlert error={earnings.error} /> : null}
          {earnings.data ? (
            <dl className="space-y-3 text-sm">
              <Row label="Available to pay out" value={formatMoney(earnings.data.available)} strong />
              <Row label="Pending (in a payout run)" value={formatMoney(earnings.data.pending)} />
              <Row label="On hold (clearing)" value={formatMoney(earnings.data.held)} />
              <Row label="Paid to date" value={formatMoney(earnings.data.paid)} />
              {earnings.data.disputeHold ? <p className="text-xs font-semibold text-danger">A dispute hold is pausing your payouts.</p> : null}
              {koboOf(earnings.data.available) >= 500000 && payoutStage?.status === 'passed' ? <p className="text-xs text-muted">Next weekly run pays everything above ₦5,000.</p> : null}
            </dl>
          ) : null}
        </Card>

        {/* Verification + profile */}
        <Card>
          <VerificationSummary dashboard={d} />
        </Card>
        <Card>
          <CardHeader title="Profile" action={<Button to="/tutor/profile" variant="ghost" size="sm">Edit</Button>} />
          <dl className="space-y-2 text-sm">
            <Row label="Status" value={<StatusPill status={d.status} />} />
            <Row label="Subjects" value={d.subjects?.join(', ') || '—'} />
            <Row label="Formats" value={d.formats?.map((f) => f.replace(/_/g, ' ')).join(', ') || '—'} />
            <Row label="Availability" value={d.availabilityRules ? `${d.availabilityRules} weekly windows` : 'Not set'} />
            <Row label="Time zone" value={d.timeZone} />
            <Row label="Calendar" value={d.calendarConnected ? 'Google connected' : 'Not connected'} />
            <Row label="Payout account" value={d.payoutAccountMasked || 'Not set'} />
          </dl>
        </Card>

        {/* Upcoming bookings */}
        <Card>
          <CardHeader title="Upcoming bookings" action={<Button to="/tutor/sessions" variant="ghost" size="sm">Manage</Button>} />
          {bookings.isPending ? <Skeleton className="h-24" /> : null}
          {bookings.isError ? <ErrorAlert error={bookings.error} /> : null}
          {bookings.data && !bookings.data.items?.length ? <p className="text-sm text-muted">No confirmed bookings ahead.</p> : null}
          <ul className="divide-y divide-line text-sm">
            {(bookings.data?.items || []).slice(0, 5).map((b) => (
              <li key={b.bookingId} className="flex items-center justify-between gap-2 py-2">
                <div>
                  <div className="font-semibold text-navy">{formatDateTime(b.slotStart)}</div>
                  <div className="text-muted">{b.format.replace(/_/g, ' ')} · {formatMoney(b.price)}</div>
                </div>
                <StatusPill status={b.status} />
              </li>
            ))}
          </ul>
        </Card>
      </div>

      <p className="mt-6 text-xs text-muted">
        Need a cohort? <Link to="/tutor/cohorts" className="font-semibold text-royal">Open one</Link> on a cohort-format offering, then schedule its weekly live classes.
      </p>
    </div>
  );
}

function Row({ label, value, strong }) {
  return (
    <div className="flex items-center justify-between gap-3">
      <dt className="text-muted">{label}</dt>
      <dd className={`text-right tabular ${strong ? 'text-lg font-extrabold font-display text-navy' : 'font-medium text-navy'}`}>{value}</dd>
    </div>
  );
}
