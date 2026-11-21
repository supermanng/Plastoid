import { useMemo, useState } from 'react';
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { BadgeCheck, CalendarDays, Clock, Globe, Package, Star, Users } from 'lucide-react';
import { Alert, Avatar, Button, Card, CardHeader, EmptyState, ErrorAlert, Field, KeyValue, Loading, Modal, Select, StatusPill, Textarea, useToast } from '../../components/ui';
import { useAuth } from '../../lib/auth';
import { useAction } from '../../lib/hooks';
import { formatDate, formatDateTime, formatMoney, formatTime, titleCase } from '../../lib/format';
import { createBooking, createPackage, findTutor, getAvailability, goToCheckout, lockSlot, queryKeys, quoteBooking, TEACHING_FORMATS } from '../../api/public';

const DURATIONS = [30, 60, 90];

/** /tutors/:tutorId — card from the discovery list, 7-day availability, slot booking and session packages. */
export default function TutorProfile() {
  const { tutorId } = useParams();
  const tutor = useQuery({ queryKey: queryKeys.tutor(tutorId), queryFn: () => findTutor(tutorId), staleTime: 5 * 60_000 });
  const [days, setDays] = useState(7);
  const availability = useQuery({ queryKey: queryKeys.availability(tutorId, days), queryFn: () => getAvailability(tutorId, days) });

  if (tutor.isPending) return <Loading className="min-h-[50vh]" />;
  if (tutor.isError) return <div className="mx-auto max-w-3xl px-4 py-10 sm:px-6"><ErrorAlert error={tutor.error} /></div>;
  const t = tutor.data;
  if (!t) {
    return <div className="mx-auto max-w-3xl px-4 py-10 sm:px-6"><EmptyState icon={Users} title="Tutor not found" description="This tutor is not listed for discovery." action={<Button to="/tutors">Find a tutor</Button>} /></div>;
  }
  const bookable = (t.formats || []).filter((f) => f !== 'course');

  return (
    <div className="mx-auto max-w-[1280px] px-4 py-8 sm:px-6">
      <nav className="text-sm text-muted" aria-label="Breadcrumb"><Link to="/tutors" className="hover:text-navy">Tutors</Link> / <span className="text-navy">{t.headline}</span></nav>
      <div className="mt-3 grid gap-8 lg:grid-cols-[1fr_400px]">
        <div className="min-w-0">
          <div className="flex items-start gap-4">
            <Avatar name={t.headline} size={64} />
            <div>
              <h1 className="flex items-center gap-2 text-2xl sm:text-3xl">{t.headline} <BadgeCheck className="h-6 w-6 text-teal" aria-label="Verified tutor" /></h1>
              <div className="mt-1 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-muted">
                <span className="capitalize">{titleCase(t.level)}</span>
                <span className="flex items-center gap-1"><Star className="h-4 w-4 text-warn" aria-hidden />{t.ratingCount ? `${Number(t.ratingAverage).toFixed(1)} (${t.ratingCount} reviews)` : 'New tutor'}</span>
                <span className="flex items-center gap-1"><Globe className="h-4 w-4 text-cyan" aria-hidden />{(t.languages || []).map((l) => l.toUpperCase()).join(', ')}</span>
                <span className="flex items-center gap-1"><Clock className="h-4 w-4 text-cyan" aria-hidden />{t.timeZone}</span>
              </div>
            </div>
          </div>
          <KeyValue className="mt-6" cols={3} items={[
            ['Hourly rate', `${formatMoney(t.hourlyRate)} / hour`],
            ['Formats', (t.formats || []).map(titleCase).join(', ') || '—'],
            ['Verification', <StatusPill tone="success" status="verified" label="Identity, credentials and demo lesson" />],
          ]} />
          <section className="mt-8">
            <h2 className="text-xl">Subjects</h2>
            <div className="mt-3 flex flex-wrap gap-2">{(t.subjects || []).map((s) => <Link key={s} to={`/tutors?skill=${encodeURIComponent(s)}`} className="rounded-chip border border-line px-3 py-1 text-sm font-semibold text-navy hover:border-royal">{s}</Link>)}</div>
          </section>
          <section className="mt-8">
            <div className="flex flex-wrap items-end justify-between gap-2">
              <h2 className="text-xl">Available slots</h2>
              <Field label="Show" className="w-40">{(p) => <Select {...p} value={days} onChange={(e) => setDays(Number(e.target.value))}><option value={7}>Next 7 days</option><option value={14}>Next 14 days</option></Select>}</Field>
            </div>
            <p className="mt-1 text-sm text-muted">Times are shown in your local time zone. Slots are one hour; book 30, 60 or 90 minutes from a slot start.</p>
            {availability.isPending ? <Loading /> : null}
            <ErrorAlert error={availability.isError ? availability.error : null} className="mt-3" />
            {availability.data ? <SlotPicker tutor={t} slots={availability.data.slots || []} formats={bookable} /> : null}
          </section>
        </div>
        <aside><div className="lg:sticky lg:top-20"><PackagePanel tutor={t} formats={bookable} /></div></aside>
      </div>
    </div>
  );
}

function groupByDay(slots) {
  const map = new Map();
  for (const s of slots) {
    const key = new Date(s.startUtc).toDateString();
    if (!map.has(key)) map.set(key, []);
    map.get(key).push(s);
  }
  return [...map.entries()];
}

function SlotPicker({ tutor, slots, formats }) {
  const [selected, setSelected] = useState(null);
  const groups = useMemo(() => groupByDay(slots), [slots]);
  if (!slots.length) return <EmptyState className="mt-4" icon={CalendarDays} title="No open slots in this window" description="Try a longer window, or message the tutor once you have booked a package." />;
  return (
    <>
      <div className="mt-4 space-y-4">
        {groups.map(([day, list]) => (
          <div key={day}>
            <div className="text-sm font-semibold text-navy">{formatDate(list[0].startUtc, { weekday: 'short' })}</div>
            <div className="mt-2 flex flex-wrap gap-2">
              {list.map((s) => <button key={s.startUtc} type="button" onClick={() => setSelected(s)} className="min-h-11 rounded-input border border-line bg-white px-3 text-sm font-semibold text-navy hover:border-royal hover:text-royal">{formatTime(s.startUtc)}</button>)}
            </div>
          </div>
        ))}
      </div>
      <BookingDialog tutor={tutor} slot={selected} formats={formats} alternatives={slots} onClose={() => setSelected(null)} onPick={setSelected} />
    </>
  );
}

function BookingDialog({ tutor, slot, formats, alternatives, onClose, onPick }) {
  const auth = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const toast = useToast();
  const [duration, setDuration] = useState(60);
  const [format, setFormat] = useState(formats[0] || 'one_to_one');
  const [note, setNote] = useState('');
  const [result, setResult] = useState(null);
  const [conflict, setConflict] = useState(false);
  const open = Boolean(slot);
  const base = slot ? { tutorId: tutor.tutorId, slotStart: slot.startUtc, durationMinutes: duration, format } : null;

  const quote = useQuery({ queryKey: ['booking', 'quote', base], queryFn: () => quoteBooking(base), enabled: open && auth.isAuthenticated, retry: false });

  const book = useAction(async () => {
    await lockSlot(base);
    return createBooking({ ...base, packageId: null, note: note.trim() || null, learnerSource: null });
  }, {
    silent: true,
    invalidate: [['bookings']],
    onSuccess: (res) => {
      setResult(res);
      if (res.checkoutUrl && !goToCheckout(res.checkoutUrl)) toast.info('Booking created', 'The gateway is simulated here; open the payment to continue.');
    },
    onError: (err) => {
      if (err?.status === 409 && /slot_locked|slot_taken|unavailable/.test(err.code || '')) setConflict(true);
      else toast.error(err, 'Could not book this slot');
    },
  });

  function close() { setResult(null); setConflict(false); setNote(''); onClose(); }

  if (!open) return null;
  if (!auth.isAuthenticated) {
    return (
      <Modal open onClose={close} title="Sign in to book" description={`${formatDateTime(slot.startUtc)} with ${tutor.headline}`} size="sm" footer={<><Button variant="secondary" onClick={close}>Cancel</Button><Button onClick={() => navigate('/auth/sign-in', { state: { from: location.pathname } })}>Sign in</Button></>}>
        <p className="text-sm text-muted">Booking takes a minute once you are signed in with your phone number.</p>
      </Modal>
    );
  }

  const others = alternatives.filter((s) => s.startUtc !== slot.startUtc).slice(0, 6);
  return (
    <Modal open onClose={close} title="Book a session" description={`${formatDateTime(slot.startUtc)} · ${tutor.headline}`} size="sm"
      footer={result ? <Button onClick={close}>Done</Button> : <><Button variant="secondary" onClick={close}>Cancel</Button><Button loading={book.isPending} disabled={!quote.data} onClick={() => book.mutate()}>{quote.data ? `Pay ${formatMoney(quote.data.price)}` : 'Book'}</Button></>}>
      {result ? (
        <Alert tone="success" title={result.status === 'confirmed' ? 'Session confirmed' : 'Booking created'}>
          {formatDateTime(result.slotStart)} → {formatTime(result.slotEnd)} · {formatMoney(result.price)}. <StatusPill status={result.status} className="ml-1" />
          <div className="mt-2 flex flex-wrap gap-3">
            <Link to={`/learn/bookings/${result.bookingId}`} className="font-semibold">View booking</Link>
            {result.paymentIntentId ? <Link to={`/learn/payments/${result.paymentIntentId}`} className="font-semibold">Open payment</Link> : null}
            {result.checkoutUrl ? <a href={result.checkoutUrl} className="font-semibold">Go to gateway</a> : null}
          </div>
        </Alert>
      ) : conflict ? (
        <div className="space-y-3">
          <Alert tone="warn" title="Someone else just took this slot">Pick another time below.</Alert>
          <div className="flex flex-wrap gap-2">{others.map((s) => <Button key={s.startUtc} size="sm" variant="secondary" onClick={() => { setConflict(false); onPick(s); }}>{formatDate(s.startUtc, { weekday: 'short' })} {formatTime(s.startUtc)}</Button>)}</div>
        </div>
      ) : (
        <div className="space-y-3">
          <div className="grid grid-cols-2 gap-3">
            <Field label="Duration">{(p) => <Select {...p} value={duration} onChange={(e) => setDuration(Number(e.target.value))}>{DURATIONS.map((d) => <option key={d} value={d}>{d} minutes</option>)}</Select>}</Field>
            <Field label="Format">{(p) => <Select {...p} value={format} onChange={(e) => setFormat(e.target.value)}>{formats.map((f) => <option key={f} value={f}>{TEACHING_FORMATS.find(([v]) => v === f)?.[1] || titleCase(f)}</option>)}</Select>}</Field>
          </div>
          <Field label="Note for the tutor (optional)">{(p) => <Textarea {...p} rows={2} maxLength={500} value={note} onChange={(e) => setNote(e.target.value)} placeholder="What do you want to cover?" />}</Field>
          {quote.isPending ? <p className="text-sm text-muted">Getting a price…</p> : null}
          {quote.isError ? <ErrorAlert error={quote.error} title="No price for this combination" /> : null}
          {quote.data ? (
            <div className="rounded-card bg-surface p-3 text-sm">
              <div className="flex justify-between"><span className="text-muted">{quote.data.durationMinutes} min · {titleCase(quote.data.format)}</span><strong className="tabular">{formatMoney(quote.data.price)}</strong></div>
              <div className="mt-1 text-xs text-muted">Slot held for 10 minutes while you pay. Cancel more than 24 h ahead for a full refund; 50% within 24 h.</div>
            </div>
          ) : null}
        </div>
      )}
    </Modal>
  );
}

function PackagePanel({ tutor, formats }) {
  const auth = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const toast = useToast();
  const [sessions, setSessions] = useState(5);
  const [duration, setDuration] = useState(60);
  const [format, setFormat] = useState(formats[0] || 'one_to_one');
  const [result, setResult] = useState(null);

  const buy = useAction(() => createPackage({ tutorId: tutor.tutorId, sessions, durationMinutes: duration, format }), {
    silent: true,
    onSuccess: (res) => {
      setResult(res);
      if (!goToCheckout(res.checkoutUrl)) toast.info('Package created', 'The gateway is simulated here; open the payment to continue.');
    },
    onError: (err) => toast.error(err, 'Could not create the package'),
  });

  const hourly = tutor.hourlyRate;
  const estimate = hourly ? Math.round(((hourly.amount ?? 0) * duration) / 60) * sessions * (sessions === 10 ? 0.9 : 1) : null;

  return (
    <Card>
      <CardHeader title="Session packages" description="Pay up front for 5 or 10 sessions; the ten-pack is 10% off. Use them on any open slot." />
      <div className="grid grid-cols-2 gap-2">
        {[5, 10].map((n) => (
          <button key={n} type="button" onClick={() => setSessions(n)} aria-pressed={sessions === n} className={`rounded-card border p-3 text-left ${sessions === n ? 'border-royal bg-royal-50' : 'border-line bg-white'}`}>
            <div className="flex items-center gap-2 font-bold text-navy"><Package className="h-4 w-4 text-royal" />{n} sessions</div>
            <div className="text-xs text-muted">{n === 10 ? 'Save 10%' : 'Standard price'}</div>
          </button>
        ))}
      </div>
      <div className="mt-3 grid grid-cols-2 gap-3">
        <Field label="Length">{(p) => <Select {...p} value={duration} onChange={(e) => setDuration(Number(e.target.value))}>{DURATIONS.map((d) => <option key={d} value={d}>{d} min</option>)}</Select>}</Field>
        <Field label="Format">{(p) => <Select {...p} value={format} onChange={(e) => setFormat(e.target.value)}>{formats.map((f) => <option key={f} value={f}>{TEACHING_FORMATS.find(([v]) => v === f)?.[1] || titleCase(f)}</option>)}</Select>}</Field>
      </div>
      {estimate ? <p className="mt-3 text-sm text-muted">Estimated total <strong className="text-navy tabular">{formatMoney({ amount: estimate, currency: 'NGN' })}</strong> based on the hourly rate; the exact price is confirmed at checkout.</p> : null}
      <Button className="mt-4 w-full" size="lg" loading={buy.isPending} onClick={() => (auth.isAuthenticated ? buy.mutate() : navigate('/auth/sign-in', { state: { from: location.pathname } }))}>
        {auth.isAuthenticated ? `Buy ${sessions} sessions` : 'Sign in to buy a package'}
      </Button>
      {result ? (
        <Alert tone="success" className="mt-4" title={`${result.sessions}-session package created`}>
          {formatMoney(result.pricePerSession)} per session · {formatMoney(result.total)} total. <Link to={`/learn/payments/${result.paymentIntentId}`} className="font-semibold">Open payment</Link>{result.checkoutUrl ? <> or <a href={result.checkoutUrl} className="font-semibold">go to the gateway</a></> : null}.
        </Alert>
      ) : null}
    </Card>
  );
}
