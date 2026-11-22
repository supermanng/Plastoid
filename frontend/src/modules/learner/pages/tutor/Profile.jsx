import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { CalendarCheck, Plus, Trash2 } from 'lucide-react';
import { Alert, Button, Card, CardHeader, Checkbox, ErrorAlert, Field, FormGrid, Input, Loading, PageHeader, Select, StatusPill } from '../../../../shared/components/ui';
import { useAction, useForm } from '../../../../shared/lib/hooks';
import { formatMoney, nairaToKobo } from '../../../../shared/lib/format';
import { tutorApi, tutorKeys } from '../../api/tutor';
import { PayoutAccountForm } from './Onboarding';
import { TEACHING_FORMATS, WEEKDAYS, localTimeZone, toTimeOnly } from './shared';

/*
 * /tutor/profile — availability (PUT /tutors/me/availability), pricing (PUT /tutors/me/pricing),
 * payout account (PUT /tutors/me/payout-account), Google calendar (POST /tutors/me/calendar/google).
 * The dashboard read only reports counts (availabilityRules, priceRules), so the editors start from sensible defaults
 * and every save replaces the full set on the server.
 */
export default function TutorProfile() {
  const me = useQuery({ queryKey: tutorKeys.me, queryFn: tutorApi.me });
  if (me.isPending) return <Loading />;
  if (me.isError) return <ErrorAlert error={me.error} />;
  const d = me.data;
  return (
    <div className="mx-auto max-w-4xl">
      <PageHeader title="Profile & settings" description={d.headline} />
      <div className="space-y-6">
        <AvailabilityCard dashboard={d} />
        <PricingCard dashboard={d} />
        <Card id="payout">
          <CardHeader title="Payout account" description="Weekly payouts go here once earnings clear the hold period. The account name must match your verified identity." />
          <PayoutAccountForm current={d.payoutAccountMasked} />
        </Card>
        <CalendarCard dashboard={d} />
      </div>
    </div>
  );
}

/* ---------------------------------------------------------------- Availability */

const DEFAULT_WINDOWS = WEEKDAYS.slice(0, 5).map((day) => ({ day, start: '09:00', end: '17:00' }));

function AvailabilityCard({ dashboard }) {
  const [timeZone, setTimeZone] = useState(dashboard.timeZone || localTimeZone());
  const [buffer, setBuffer] = useState(0);
  const [windows, setWindows] = useState(DEFAULT_WINDOWS);
  const [exceptions, setExceptions] = useState([]);

  const save = useAction(
    () => tutorApi.setAvailability({
      timeZone,
      bufferMinutes: Number(buffer) || 0,
      weekly: windows.map((w) => ({ day: w.day, start: toTimeOnly(w.start), end: toTimeOnly(w.end) })),
      exceptions: exceptions.map((e) => ({ date: e.date, available: e.available, start: e.available && e.start ? toTimeOnly(e.start) : null, end: e.available && e.end ? toTimeOnly(e.end) : null })),
    }),
    { success: (r) => `Availability saved: ${r.weeklyRules} weekly windows, ${r.exceptions} exceptions.`, invalidate: [tutorKeys.me] },
  );

  const updateWindow = (i, patch) => setWindows((w) => w.map((x, j) => (j === i ? { ...x, ...patch } : x)));
  const updateException = (i, patch) => setExceptions((w) => w.map((x, j) => (j === i ? { ...x, ...patch } : x)));
  const invalid = windows.some((w) => !w.start || !w.end || w.end <= w.start) || exceptions.some((e) => !e.date || (e.available && (!e.start || !e.end || e.end <= e.start)));

  return (
    <Card>
      <CardHeader title="Availability" description={`Learners book one-hour slots inside these windows. Currently ${dashboard.availabilityRules} weekly window${dashboard.availabilityRules === 1 ? '' : 's'} on the server; saving replaces them.`} />
      <ErrorAlert error={save.error} className="mb-3" />
      <FormGrid>
        <Field label="Time zone" hint="IANA name, e.g. Africa/Lagos.">{(p) => <Input {...p} value={timeZone} onChange={(e) => setTimeZone(e.target.value)} />}</Field>
        <Field label="Buffer between sessions (minutes)" hint="0–120.">{(p) => <Input {...p} type="number" min={0} max={120} value={buffer} onChange={(e) => setBuffer(e.target.value)} />}</Field>
      </FormGrid>

      <h4 className="mt-5 text-sm font-bold">Weekly windows</h4>
      <ul className="mt-2 space-y-2">
        {windows.map((w, i) => (
          <li key={i} className="grid grid-cols-[1fr_auto_auto_auto] items-center gap-2">
            <Select aria-label="Day" value={w.day} onChange={(e) => updateWindow(i, { day: e.target.value })}>
              {WEEKDAYS.map((d) => <option key={d} value={d}>{d[0].toUpperCase() + d.slice(1)}</option>)}
            </Select>
            <Input aria-label="Start" type="time" value={w.start} onChange={(e) => updateWindow(i, { start: e.target.value })} className="w-28" />
            <Input aria-label="End" type="time" value={w.end} onChange={(e) => updateWindow(i, { end: e.target.value })} className="w-28" />
            <Button variant="ghost" size="sm" aria-label="Remove window" onClick={() => setWindows((x) => x.filter((_, j) => j !== i))}><Trash2 className="h-4 w-4" /></Button>
          </li>
        ))}
      </ul>
      <Button variant="secondary" size="sm" icon={Plus} className="mt-2" disabled={windows.length >= 50} onClick={() => setWindows((w) => [...w, { day: 'saturday', start: '10:00', end: '13:00' }])}>Add window</Button>

      <h4 className="mt-5 text-sm font-bold">Dated exceptions</h4>
      <p className="text-xs text-muted">Block a day off, or open extra hours on a specific date.</p>
      <ul className="mt-2 space-y-2">
        {exceptions.map((e, i) => (
          <li key={i} className="grid grid-cols-[1fr_auto] items-center gap-2 sm:grid-cols-[1fr_auto_auto_auto_auto]">
            <Input aria-label="Date" type="date" value={e.date} onChange={(ev) => updateException(i, { date: ev.target.value })} />
            <Checkbox label="Available" checked={e.available} onChange={(ev) => updateException(i, { available: ev.target.checked })} />
            <Input aria-label="Start" type="time" value={e.start} disabled={!e.available} onChange={(ev) => updateException(i, { start: ev.target.value })} className="w-28" />
            <Input aria-label="End" type="time" value={e.end} disabled={!e.available} onChange={(ev) => updateException(i, { end: ev.target.value })} className="w-28" />
            <Button variant="ghost" size="sm" aria-label="Remove exception" onClick={() => setExceptions((x) => x.filter((_, j) => j !== i))}><Trash2 className="h-4 w-4" /></Button>
          </li>
        ))}
      </ul>
      <Button variant="secondary" size="sm" icon={Plus} className="mt-2" disabled={exceptions.length >= 100} onClick={() => setExceptions((x) => [...x, { date: '', available: false, start: '', end: '' }])}>Add exception</Button>

      <div className="mt-5 flex justify-end">
        <Button loading={save.isPending} disabled={invalid || !timeZone.trim()} onClick={() => save.mutate()}>Save availability</Button>
      </div>
    </Card>
  );
}

/* ---------------------------------------------------------------- Pricing */

function PricingCard({ dashboard }) {
  const [hourly, setHourly] = useState(dashboard.hourlyRate ? String(dashboard.hourlyRate.amount / 100) : '');
  const [rules, setRules] = useState([{ format: 'one_to_one', durationMinutes: 60, priceNaira: dashboard.hourlyRate ? String(dashboard.hourlyRate.amount / 100) : '', maxLearners: '' }]);

  const save = useAction(
    () => tutorApi.setPricing({
      hourlyRate: { amount: nairaToKobo(hourly), currency: 'NGN' },
      rules: rules.map((r) => ({ format: r.format, durationMinutes: Number(r.durationMinutes), price: { amount: nairaToKobo(r.priceNaira), currency: 'NGN' }, maxLearners: r.format === 'group' && r.maxLearners ? Number(r.maxLearners) : null })),
    }),
    { success: 'Pricing saved.', invalidate: [tutorKeys.me] },
  );
  const update = (i, patch) => setRules((rs) => rs.map((r, j) => (j === i ? { ...r, ...patch } : r)));
  const invalid = !hourly || rules.some((r) => !r.priceNaira || Number(r.durationMinutes) < 15 || Number(r.durationMinutes) > 480);

  return (
    <Card>
      <CardHeader title="Pricing" description={`Hourly rate ${formatMoney(dashboard.hourlyRate)} and ${dashboard.priceRules} per-format price rule${dashboard.priceRules === 1 ? '' : 's'} on the server. Prices are in naira; the API stores kobo.`} />
      <ErrorAlert error={save.error} className="mb-3" />
      <Field label="Hourly rate (₦)" hint="Shown on your tutor card and used for quotes without a matching rule." className="max-w-xs">
        {(p) => <Input {...p} inputMode="numeric" value={hourly} onChange={(e) => setHourly(e.target.value)} />}
      </Field>
      <h4 className="mt-5 text-sm font-bold">Per-format prices</h4>
      <ul className="mt-2 space-y-2">
        {rules.map((r, i) => (
          <li key={i} className="grid grid-cols-2 items-end gap-2 sm:grid-cols-[1fr_auto_auto_auto_auto]">
            <Field label="Format">{(p) => <Select {...p} value={r.format} onChange={(e) => update(i, { format: e.target.value })}>{TEACHING_FORMATS.map(([v, l]) => <option key={v} value={v}>{l}</option>)}</Select>}</Field>
            <Field label="Minutes">{(p) => <Input {...p} type="number" min={15} max={480} step={15} value={r.durationMinutes} onChange={(e) => update(i, { durationMinutes: e.target.value })} className="w-24" />}</Field>
            <Field label="Price (₦)">{(p) => <Input {...p} inputMode="numeric" value={r.priceNaira} onChange={(e) => update(i, { priceNaira: e.target.value })} className="w-32" />}</Field>
            <Field label="Max learners">{(p) => <Input {...p} type="number" min={2} disabled={r.format !== 'group'} value={r.maxLearners} onChange={(e) => update(i, { maxLearners: e.target.value })} className="w-24" />}</Field>
            <Button variant="ghost" size="sm" aria-label="Remove rule" onClick={() => setRules((x) => x.filter((_, j) => j !== i))}><Trash2 className="h-4 w-4" /></Button>
          </li>
        ))}
      </ul>
      <Button variant="secondary" size="sm" icon={Plus} className="mt-2" disabled={rules.length >= 20} onClick={() => setRules((x) => [...x, { format: 'group', durationMinutes: 90, priceNaira: '', maxLearners: '6' }])}>Add rule</Button>
      <div className="mt-5 flex justify-end">
        <Button loading={save.isPending} disabled={invalid} onClick={() => save.mutate()}>Save pricing</Button>
      </div>
    </Card>
  );
}

/* ---------------------------------------------------------------- Calendar */

function CalendarCard({ dashboard }) {
  const { values, bind } = useForm({ code: '' });
  const connect = useAction(() => tutorApi.connectGoogleCalendar(values.code.trim()), { success: (r) => `Connected ${r.provider} calendar for ${r.account}.`, invalidate: [tutorKeys.me] });
  return (
    <Card>
      <CardHeader title="Calendar sync" description="Bookings are mirrored to your Google Calendar and busy slots are hidden from learners." action={<StatusPill status={dashboard.calendarConnected ? 'connected' : 'not_connected'} tone={dashboard.calendarConnected ? 'success' : 'neutral'} label={dashboard.calendarConnected ? 'Connected' : 'Not connected'} />} />
      <Alert tone="info" className="mb-3">
        The Google adapter is a stub in this environment: paste any authorisation code to simulate the OAuth exchange. In production the button opens Google's consent screen and returns here with the code.
      </Alert>
      <ErrorAlert error={connect.error} className="mb-3" />
      <div className="flex flex-col gap-2 sm:flex-row sm:items-end">
        <Field label="Authorisation code" className="flex-1">{(p) => <Input {...p} {...bind('code')} placeholder="4/0Ab…" />}</Field>
        <Button icon={CalendarCheck} loading={connect.isPending} disabled={!values.code.trim()} onClick={() => connect.mutate()}>Connect Google Calendar</Button>
      </div>
    </Card>
  );
}
