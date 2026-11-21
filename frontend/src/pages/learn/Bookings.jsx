import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { CalendarDays, Search } from 'lucide-react';
import { Button, Card, EmptyState, ErrorAlert, Loading, PageHeader, Pagination, StatusPill, Tabs } from '../../components/ui';
import { useCursor } from '../../lib/hooks';
import { formatDate, formatMoney, formatTime, relativeTime } from '../../lib/format';
import { bookings, keys } from '../../api/learn';

export const BOOKING_LABELS = { pendingPayment: 'Awaiting payment', confirmed: 'Confirmed', cancelled: 'Cancelled', completed: 'Completed', noShow: 'No-show' };

/** GET /bookings/me (?upcoming=true|false, cursor): one-to-one tutor sessions. */
export default function Bookings() {
  const [tab, setTab] = useState('upcoming');
  const cursor = useCursor();
  const params = { role: 'learner', upcoming: tab === 'upcoming', cursor: cursor.cursor, limit: 20 };
  const query = useQuery({ queryKey: keys.bookings(params), queryFn: () => bookings.mine(params) });
  const items = query.data?.items || [];

  return (
    <div>
      <PageHeader title="Tutor sessions" description="One-to-one sessions you have booked. Cancel more than 24 hours ahead for a full refund; within 24 hours you get 50%." actions={<Button to="/tutors" variant="secondary" icon={Search}>Find a tutor</Button>} />
      <Tabs className="mb-6" value={tab} onChange={(v) => { setTab(v); cursor.reset(); }} tabs={[{ value: 'upcoming', label: 'Upcoming' }, { value: 'past', label: 'Past & cancelled' }]} />
      {query.isPending ? <Loading /> : null}
      <ErrorAlert error={query.error} />
      {query.data && !items.length ? (
        <EmptyState icon={CalendarDays} title={tab === 'upcoming' ? 'No upcoming sessions' : 'No past sessions'} description="Book a verified tutor for a one-hour session or a 5/10-session package." action={<Button to="/tutors" size="sm">Browse tutors</Button>} />
      ) : null}
      <ul className="grid gap-3 md:grid-cols-2">
        {items.map((b) => <BookingCard key={b.bookingId} b={b} />)}
      </ul>
      <Pagination hasMore={Boolean(query.data?.nextCursor)} onMore={() => cursor.next(query.data.nextCursor)} canBack={cursor.canBack} onBack={cursor.back} loading={query.isFetching} />
    </div>
  );
}

export function BookingCard({ b }) {
  const start = new Date(b.slotStart);
  const soon = b.status === 'confirmed' && start - Date.now() < 60 * 60 * 1000 && start - Date.now() > -60 * 60 * 1000;
  return (
    <Card as="li" className="flex flex-col gap-3">
      <div className="flex items-start justify-between gap-3">
        <div>
          <div className="text-xs font-semibold uppercase tracking-wide text-muted">{formatDate(b.slotStart, { weekday: 'short' })}</div>
          <div className="text-lg font-bold text-navy tabular">{formatTime(b.slotStart)} – {formatTime(b.slotEnd)}</div>
          <div className="text-xs text-muted">{relativeTime(b.slotStart)} · <span className="capitalize">{b.format.replace(/_/g, ' ')}</span>{b.packageId ? ' · package session' : ''}</div>
        </div>
        <StatusPill status={b.status} label={BOOKING_LABELS[b.status] || b.status} />
      </div>
      <div className="flex items-center justify-between text-sm">
        <span className="text-muted">Tutor <Link to={`/tutors/${b.tutorId}`} className="font-semibold">view profile</Link></span>
        <span className="font-semibold text-navy">{formatMoney(b.price)}</span>
      </div>
      <div className="mt-auto flex gap-2">
        <Button to={`/learn/bookings/${b.bookingId}`} size="sm" variant={soon ? 'teal' : 'primary'}>{soon ? 'Join / details' : 'Details'}</Button>
        <Button to={`/learn/messages?to=${b.tutorUserId}`} size="sm" variant="secondary">Message tutor</Button>
      </div>
    </Card>
  );
}
