import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { CalendarClock, CreditCard, Video, XCircle } from 'lucide-react';
import { Alert, Breadcrumb, Button, Card, CardHeader, ConfirmDialog, ErrorAlert, Field, Input, KeyValue, Loading, Modal, Select, StatusPill } from '../../components/ui';
import { useAction } from '../../lib/hooks';
import { formatDate, formatDateTime, formatMoney, formatTime, toDateInput } from '../../lib/format';
import { bookings, keys, lms } from '../../api/learn';
import { BOOKING_LABELS } from './Bookings';

export default function BookingDetail() {
  const { bookingId } = useParams();
  const query = useQuery({ queryKey: keys.booking(bookingId), queryFn: () => bookings.get(bookingId) });
  const upcoming = useQuery({ queryKey: keys.upcoming(30), queryFn: () => lms.upcoming(30) });
  const [cancelOpen, setCancelOpen] = useState(false);
  const [rescheduleOpen, setRescheduleOpen] = useState(false);
  const refundTo = 'original';

  const cancel = useAction((reason) => bookings.cancel(bookingId, { reason, refundTo }), {
    invalidate: [keys.booking(bookingId), ['bookings'], keys.payments()],
    success: (r) => `Cancelled. Refund: ${r.refundPercent}%${r.refundPercent ? ` to ${refundTo === 'wallet' ? 'your Plastoids balance' : 'your payment method'}` : ''}.`,
    onSuccess: () => setCancelOpen(false),
  });

  if (query.isPending) return <Loading />;
  if (query.isError) return <ErrorAlert error={query.error} />;
  const b = query.data;
  const start = new Date(b.slotStart);
  const hoursAway = (start - Date.now()) / 36e5;
  const refundPct = hoursAway > 24 ? 100 : 50;
  const live = upcoming.data?.find((s) => s.bookingId === bookingId);
  const canChange = b.status === 'confirmed' && hoursAway > 0;

  return (
    <div className="mx-auto max-w-3xl">
      <Breadcrumb items={[{ to: '/learn/bookings', label: 'Tutor sessions' }, { label: formatDate(b.slotStart) }]} />
      <div className="mt-2 flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl">{formatDate(b.slotStart, { weekday: 'long' })}</h1>
          <p className="text-sm text-muted tabular">{formatTime(b.slotStart)} – {formatTime(b.slotEnd)} · {b.durationMinutes} min · <span className="capitalize">{b.format.replace(/_/g, ' ')}</span></p>
        </div>
        <StatusPill status={b.status} label={BOOKING_LABELS[b.status] || b.status} />
      </div>

      {b.status === 'pendingPayment' ? (
        <Alert tone="warn" className="mt-4" title="Payment pending">The slot is held for 10 minutes while you pay.{b.paymentIntentId ? <> <Link to={`/learn/payments/${b.paymentIntentId}`} className="font-semibold">Complete payment</Link></> : null}</Alert>
      ) : null}
      {b.status === 'cancelled' ? <Alert tone="danger" className="mt-4" title={`Cancelled by ${b.cancelledBy || 'you'}`}>{b.cancellationReason}{b.refundPercent ? ` · ${b.refundPercent}% refunded` : ''}{b.cancelledAt ? ` · ${formatDateTime(b.cancelledAt)}` : ''}</Alert> : null}
      {b.rescheduledFrom ? <Alert tone="info" className="mt-4">Rescheduled from {formatDateTime(b.rescheduledFrom)}.</Alert> : null}

      <div className="mt-6 grid gap-6 md:grid-cols-[1fr_280px]">
        <div className="space-y-6">
          {b.status === 'confirmed' ? (
            <Card className={live?.joinOpen ? 'border-teal/50' : ''}>
              <CardHeader title="Join your session" description={live?.joinOpen ? 'The room is open.' : 'The room opens 15 minutes before the start time.'} />
              {live ? <Button to={`/learn/live/${live.liveSessionId}`} icon={Video} variant={live.joinOpen ? 'teal' : 'secondary'} disabled={!live.joinOpen}>Join session</Button> : <p className="text-sm text-muted">The video room appears here once it is created (shortly after confirmation).</p>}
              <p className="mt-3 text-xs text-muted">Poor connection? The room falls back to audio-only automatically.</p>
            </Card>
          ) : null}
          <Card>
            <CardHeader title="Details" />
            <KeyValue items={[
              ['Tutor', <Link to={`/tutors/${b.tutorId}`} className="font-semibold">View profile</Link>],
              ['Price', formatMoney(b.price)],
              ['Confirmed', b.confirmedAt ? formatDateTime(b.confirmedAt) : '—'],
              b.packageId ? ['Package', b.packageId.slice(0, 8)] : null,
              b.paymentIntentId ? ['Payment', <Link to={`/learn/payments/${b.paymentIntentId}`} className="font-semibold">View receipt</Link>] : null,
              b.note ? ['Your note', b.note] : null,
              ['Booking ID', <code className="text-xs">{b.bookingId}</code>],
            ]} />
          </Card>
        </div>
        <aside className="space-y-3">
          <Button to={`/learn/messages?to=${b.tutorUserId}`} variant="secondary" className="w-full">Message tutor</Button>
          {canChange ? <Button variant="secondary" className="w-full" icon={CalendarClock} onClick={() => setRescheduleOpen(true)} disabled={hoursAway < 24} title={hoursAway < 24 ? 'Learners need 24 hours notice to reschedule' : undefined}>Reschedule</Button> : null}
          {canChange ? <Button variant="danger" className="w-full" icon={XCircle} onClick={() => setCancelOpen(true)}>Cancel ({refundPct}% refund)</Button> : null}
          {b.status === 'pendingPayment' && b.paymentIntentId ? <Button to={`/learn/payments/${b.paymentIntentId}`} className="w-full" icon={CreditCard}>Pay now</Button> : null}
          {canChange && hoursAway < 24 ? <p className="text-xs text-muted">Within 24 hours: cancelling refunds 50%; rescheduling needs the tutor to do it.</p> : null}
        </aside>
      </div>

      <ConfirmDialog
        open={cancelOpen}
        onClose={() => setCancelOpen(false)}
        onConfirm={(reason) => cancel.mutate(reason)}
        loading={cancel.isPending}
        error={cancel.error}
        title="Cancel this session?"
        description={`You are ${hoursAway > 24 ? 'more' : 'less'} than 24 hours away, so ${refundPct}% of ${formatMoney(b.price)} is refunded.`}
        confirmLabel="Cancel session"
        tone="danger"
        reason
        reasonLabel="Reason"
      />
      <RescheduleModal b={b} open={rescheduleOpen} onClose={() => setRescheduleOpen(false)} />
    </div>
  );
}

function RescheduleModal({ b, open, onClose }) {
  const [from, setFrom] = useState(toDateInput(new Date()));
  const [slot, setSlot] = useState('');
  const avail = useQuery({ queryKey: keys.availability(b.tutorId, from, 7), queryFn: () => bookings.availability(b.tutorId, { from, days: 7 }), enabled: open });
  const reschedule = useAction(() => bookings.reschedule(b.bookingId, slot), { invalidate: [keys.booking(b.bookingId), ['bookings'], ['lms', 'live']], success: 'Session moved', onSuccess: onClose });
  const slots = (avail.data?.slots || []).filter((s) => new Date(s.startUtc) > new Date());
  return (
    <Modal open={open} onClose={onClose} title="Reschedule" description="Pick a free one-hour slot in the next seven days." footer={<><Button variant="secondary" onClick={onClose}>Back</Button><Button loading={reschedule.isPending} disabled={!slot} onClick={() => reschedule.mutate()}>Move session</Button></>}>
      <ErrorAlert error={reschedule.error || avail.error} className="mb-3" />
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label="From date">{(p) => <Input {...p} type="date" value={from} min={toDateInput(new Date())} onChange={(e) => { setFrom(e.target.value); setSlot(''); }} />}</Field>
        <Field label="Available slot" hint={avail.data ? `Tutor time zone ${avail.data.timeZone}` : undefined}>
          {(p) => (
            <Select {...p} value={slot} onChange={(e) => setSlot(e.target.value)} disabled={avail.isPending}>
              <option value="">{avail.isPending ? 'Loading…' : slots.length ? 'Choose a slot' : 'No free slots'}</option>
              {slots.map((s) => <option key={s.startUtc} value={s.startUtc}>{formatDateTime(s.startUtc)}</option>)}
            </Select>
          )}
        </Field>
      </div>
    </Modal>
  );
}
