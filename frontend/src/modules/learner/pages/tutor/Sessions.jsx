import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { CalendarDays, Video } from 'lucide-react';
import { Alert, Button, Card, CardHeader, Chip, ConfirmDialog, EmptyState, ErrorAlert, Field, Input, Loading, Modal, PageHeader, Pagination, StatusPill, useToast } from '../../../../shared/components/ui';
import { useAction, useCursor } from '../../../../shared/lib/hooks';
import { formatDateTime, formatMoney, formatTime, relativeTime } from '../../../../shared/lib/format';
import { bookingApi, lmsApi, tutorKeys } from '../../api/tutor';
import { groupByDay, toIso } from './shared';

/*
 * /tutor/sessions — bookings as tutor (GET /bookings/me?role=tutor) with cancel / reschedule, and live sessions
 * (GET /lms/live-sessions/upcoming) with host join tokens (POST /lms/live-sessions/{id}/join-token).
 */
export default function TutorSessions() {
  const [params, setParams] = useSearchParams();
  const [view, setView] = useState('upcoming');
  const cursor = useCursor();
  const bookings = useQuery({ queryKey: [...tutorKeys.bookings(view === 'upcoming'), cursor.cursor], queryFn: () => bookingApi.mine({ upcoming: view === 'upcoming', cursor: cursor.cursor, limit: 25 }) });
  const live = useQuery({ queryKey: tutorKeys.upcomingSessions(14), queryFn: () => lmsApi.upcoming(14) });
  const [cancelling, setCancelling] = useState(null);
  const [rescheduling, setRescheduling] = useState(null);

  // /tutor/sessions?join=<liveSessionId> from the dashboard
  const joinId = params.get('join');
  useEffect(() => {
    if (joinId && live.data) {
      const el = document.getElementById(`live-${joinId}`);
      el?.scrollIntoView({ behavior: 'smooth', block: 'center' });
      setParams({}, { replace: true });
    }
  }, [joinId, live.data, setParams]);

  const items = bookings.data?.items || [];
  const liveHosted = (live.data || []).filter((s) => s.isHost);

  return (
    <div>
      <PageHeader title="Classes & sessions" description="Everything you host: booked one-to-ones and cohort live classes." />

      <Card className="mb-6">
        <CardHeader title="Live classes (next 14 days)" description="Start from here when the room opens, 15 minutes before the scheduled time." />
        {live.isPending ? <Loading /> : null}
        {live.isError ? <ErrorAlert error={live.error} /> : null}
        {live.data && liveHosted.length === 0 ? <EmptyState icon={Video} title="No live classes coming up" description="Schedule them from a cohort." action={<Button to="/tutor/cohorts" variant="secondary">Go to cohorts</Button>} /> : null}
        {groupByDay(liveHosted, 'startsAt').map(([day, list]) => (
          <div key={day} className="py-2">
            <h3 className="mb-1 text-xs font-bold uppercase tracking-wide text-muted">{day}</h3>
            <ul className="divide-y divide-line">
              {list.map((s) => (
                <li key={s.liveSessionId} id={`live-${s.liveSessionId}`} className="flex flex-wrap items-center justify-between gap-3 py-2">
                  <div>
                    <div className="font-semibold text-navy">{s.title}</div>
                    <div className="text-sm text-muted">{formatTime(s.startsAt)} – {formatTime(s.endsAt)} · {s.source} · {relativeTime(s.startsAt)}</div>
                  </div>
                  <div className="flex items-center gap-2"><StatusPill status={s.status} /><JoinButton session={s} /></div>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </Card>

      <Card>
        <CardHeader title="One-to-one bookings" action={<div className="flex gap-2">{[['upcoming', 'Upcoming'], ['all', 'All']].map(([v, l]) => <Chip key={v} active={view === v} onClick={() => { setView(v); cursor.reset(); }}>{l}</Chip>)}</div>} />
        <Alert tone="info" className="mb-4">Cancelling as the tutor refunds the learner 100% and counts as a strike against your profile; rescheduling is free at any notice.</Alert>
        {bookings.isPending ? <Loading /> : null}
        {bookings.isError ? <ErrorAlert error={bookings.error} /> : null}
        {bookings.data && items.length === 0 ? <EmptyState icon={CalendarDays} title={view === 'upcoming' ? 'No upcoming bookings' : 'No bookings yet'} description="Learners book one-hour slots inside your availability windows." action={<Button to="/tutor/profile" variant="secondary">Set availability</Button>} /> : null}
        {groupByDay(items, 'slotStart').map(([day, list]) => (
          <div key={day} className="py-2">
            <h3 className="mb-1 text-xs font-bold uppercase tracking-wide text-muted">{day}</h3>
            <ul className="divide-y divide-line">
              {list.map((b) => (
                <li key={b.bookingId} className="flex flex-wrap items-center justify-between gap-3 py-2">
                  <div>
                    <div className="font-semibold text-navy">{formatTime(b.slotStart)} – {formatTime(b.slotEnd)} · {b.format.replace(/_/g, ' ')}</div>
                    <div className="text-sm text-muted">Learner {String(b.learnerId).slice(0, 8)} · {formatMoney(b.price)}{b.packageId ? ' · package' : ''}</div>
                  </div>
                  <div className="flex items-center gap-2">
                    <StatusPill status={b.status} />
                    {b.status === 'confirmed' && new Date(b.slotStart) > new Date() ? (
                      <>
                        <Button size="sm" variant="secondary" onClick={() => setRescheduling(b)}>Reschedule</Button>
                        <Button size="sm" variant="ghost" className="text-danger" onClick={() => setCancelling(b)}>Cancel</Button>
                      </>
                    ) : null}
                  </div>
                </li>
              ))}
            </ul>
          </div>
        ))}
        <Pagination hasMore={Boolean(bookings.data?.nextCursor)} onMore={() => cursor.next(bookings.data?.nextCursor)} canBack={cursor.canBack} onBack={cursor.back} loading={bookings.isFetching} />
      </Card>

      <CancelDialog booking={cancelling} onClose={() => setCancelling(null)} />
      <RescheduleDialog booking={rescheduling} onClose={() => setRescheduling(null)} />
    </div>
  );
}

/** Issues a host/participant token and opens the provider room; shows the audio-only hint on poor connections. */
export function JoinButton({ session }) {
  const toast = useToast();
  const join = useAction(() => lmsApi.joinToken(session.liveSessionId), {
    silent: true,
    onSuccess: (r) => {
      const url = r.joinUrl ? new URL(r.joinUrl) : null;
      if (url) {
        url.searchParams.set('token', r.token);
        window.open(url.toString(), '_blank', 'noopener');
        toast.info(session.isHost ? 'Room opened as host' : 'Joining', `${r.provider} room ${r.roomId}${r.audioOnlyFallback ? ' · audio-only fallback available' : ''}; link valid until ${formatTime(r.expiresAt)}.`);
      } else {
        toast.info('Join token issued', `Room ${r.roomId} on ${r.provider}.`);
      }
    },
    onError: (e) => toast.error(e, 'Could not join'),
  });
  if (!session.joinOpen) return <span className="text-xs text-muted">Opens {relativeTime(new Date(new Date(session.startsAt).getTime() - 15 * 60000))}</span>;
  return <Button size="sm" icon={Video} loading={join.isPending} onClick={() => join.mutate()}>{session.isHost ? 'Start' : 'Join'}</Button>;
}

function CancelDialog({ booking, onClose }) {
  const cancel = useAction((reason) => bookingApi.cancel(booking.bookingId, { reason, tutorNoShow: false, refundTo: 'credit' }), {
    success: (r) => `Booking cancelled. Learner refunded ${r.refundPercent}%${r.tutorStrike ? ' · strike recorded' : ''}.`,
    invalidate: [['tutor', 'bookings']],
    onSuccess: onClose,
  });
  return (
    <ConfirmDialog
      open={Boolean(booking)}
      onClose={onClose}
      onConfirm={(reason) => cancel.mutate(reason)}
      title="Cancel this session?"
      description={booking ? `${formatDateTime(booking.slotStart)}. Tutor cancellations refund the learner 100% and count as a strike. Consider rescheduling instead.` : ''}
      confirmLabel="Cancel session"
      tone="danger"
      reason
      reasonLabel="Reason shown to the learner"
      loading={cancel.isPending}
      error={cancel.error}
    />
  );
}

function RescheduleDialog({ booking, onClose }) {
  const [when, setWhen] = useState('');
  useEffect(() => setWhen(''), [booking]);
  const move = useAction(() => bookingApi.reschedule(booking.bookingId, toIso(when)), {
    success: (r) => `Moved to ${formatDateTime(r.slotStart)}.`,
    invalidate: [['tutor', 'bookings'], ['tutor', 'live-sessions']],
    onSuccess: onClose,
  });
  return (
    <Modal open={Boolean(booking)} onClose={onClose} size="sm" title="Reschedule session" description={booking ? `Currently ${formatDateTime(booking.slotStart)}. The learner is notified and can decline.` : ''} footer={<><Button variant="secondary" onClick={onClose}>Back</Button><Button loading={move.isPending} disabled={!when || new Date(when) <= new Date()} onClick={() => move.mutate()}>Move session</Button></>}>
      <ErrorAlert error={move.error} className="mb-3" />
      <Field label="New start time" hint="Must be inside your availability and not clash with another booking." required>{(p) => <Input {...p} type="datetime-local" value={when} onChange={(e) => setWhen(e.target.value)} />}</Field>
    </Modal>
  );
}
