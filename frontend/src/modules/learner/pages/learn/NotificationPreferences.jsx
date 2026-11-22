import { useEffect, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { ArrowDown, ArrowUp, Moon } from 'lucide-react';
import { Alert, Breadcrumb, Button, Card, CardHeader, ErrorAlert, Field, Input, Loading, Switch } from '../../../../shared/components/ui';
import { useAction } from '../../../../shared/lib/hooks';
import { keys, notifications } from '../../api/learn';

const CHANNELS = { push: 'Push (app)', whatsapp: 'WhatsApp', sms: 'SMS', email: 'Email' };
const TIME_ZONES = ['Africa/Lagos', 'Africa/Accra', 'Africa/Nairobi', 'Africa/Johannesburg', 'Europe/London', 'UTC'];

/** GET/PUT /notifications/preferences: channel order, opt-outs, quiet hours, time zone. */
export default function NotificationPreferences() {
  const query = useQuery({ queryKey: keys.preferences, queryFn: notifications.preferences });
  const [order, setOrder] = useState([]);
  const [optedOut, setOptedOut] = useState([]);
  const [quietStart, setQuietStart] = useState('');
  const [quietEnd, setQuietEnd] = useState('');
  const [timeZone, setTimeZone] = useState('Africa/Lagos');

  useEffect(() => {
    if (!query.data) return;
    const known = Object.keys(CHANNELS);
    setOrder([...(query.data.channelOrder || []).filter((c) => known.includes(c)), ...known.filter((c) => !(query.data.channelOrder || []).includes(c))]);
    setOptedOut(query.data.optedOut || []);
    setQuietStart(hhmm(query.data.quietStart));
    setQuietEnd(hhmm(query.data.quietEnd));
    setTimeZone(query.data.timeZone || 'Africa/Lagos');
  }, [query.data]);

  const save = useAction(() => notifications.updatePreferences({ channelOrder: order, optedOut, quietStart: quietStart || null, quietEnd: quietEnd || null, timeZone }), { success: 'Preferences saved', invalidate: [keys.preferences] });

  const move = (i, dir) => setOrder((o) => {
    const j = i + dir;
    if (j < 0 || j >= o.length) return o;
    const next = [...o];
    [next[i], next[j]] = [next[j], next[i]];
    return next;
  });
  const toggleOut = (c) => setOptedOut((list) => (list.includes(c) ? list.filter((x) => x !== c) : [...list, c]));

  if (query.isPending) return <Loading />;
  if (query.isError) return <ErrorAlert error={query.error} />;

  return (
    <div className="mx-auto max-w-2xl">
      <Breadcrumb items={[{ to: '/learn/notifications', label: 'Notifications' }, { label: 'Preferences' }]} />
      <h1 className="mt-2 text-2xl">Notification preferences</h1>
      <p className="text-sm text-muted">We try the first channel; if it is not delivered we fall through to the next. Urgent items (offer expiring, class starting) ignore quiet hours.</p>
      {query.data?.quietNow ? <Alert tone="info" className="mt-4" title="Quiet hours are active now">Non-urgent notifications are held until {quietEnd}.</Alert> : null}

      <Card className="mt-6">
        <CardHeader title="Channel order" description="Drag not required: use the arrows. Switch a channel off to never use it." />
        <ol className="divide-y divide-line">
          {order.map((c, i) => (
            <li key={c} className="flex items-center gap-3 py-2">
              <span className="w-6 text-center text-sm font-bold text-muted">{i + 1}</span>
              <span className={`flex-1 text-sm font-semibold ${optedOut.includes(c) ? 'text-muted line-through' : 'text-navy'}`}>{CHANNELS[c]}</span>
              <button type="button" onClick={() => move(i, -1)} disabled={i === 0} className="grid h-10 w-10 place-items-center rounded-full text-muted hover:bg-surface disabled:opacity-30" aria-label={`Move ${CHANNELS[c]} up`}><ArrowUp className="h-4 w-4" /></button>
              <button type="button" onClick={() => move(i, 1)} disabled={i === order.length - 1} className="grid h-10 w-10 place-items-center rounded-full text-muted hover:bg-surface disabled:opacity-30" aria-label={`Move ${CHANNELS[c]} down`}><ArrowDown className="h-4 w-4" /></button>
              <Switch checked={!optedOut.includes(c)} onChange={() => toggleOut(c)} label="" className="w-12" />
            </li>
          ))}
        </ol>
        {optedOut.includes('sms') && optedOut.includes('whatsapp') ? <Alert tone="warn" className="mt-3">With SMS and WhatsApp off you may miss time-critical reminders if push is unavailable.</Alert> : null}
      </Card>

      <Card className="mt-6">
        <CardHeader title={<span className="inline-flex items-center gap-2"><Moon className="h-4 w-4 text-royal" /> Quiet hours</span>} description="Non-urgent notifications wait until quiet hours end." />
        <div className="grid gap-4 sm:grid-cols-3">
          <Field label="From">{(p) => <Input {...p} type="time" value={quietStart} onChange={(e) => setQuietStart(e.target.value)} />}</Field>
          <Field label="Until">{(p) => <Input {...p} type="time" value={quietEnd} onChange={(e) => setQuietEnd(e.target.value)} />}</Field>
          <Field label="Time zone">
            {(p) => (
              <select {...p} className="input" value={timeZone} onChange={(e) => setTimeZone(e.target.value)}>
                {[...new Set([timeZone, ...TIME_ZONES])].map((z) => <option key={z} value={z}>{z}</option>)}
              </select>
            )}
          </Field>
        </div>
        <button type="button" className="mt-2 text-xs font-semibold text-royal hover:underline" onClick={() => { setQuietStart(''); setQuietEnd(''); }}>Clear quiet hours</button>
      </Card>

      <ErrorAlert error={save.error} className="mt-4" />
      <div className="mt-6 flex justify-end">
        <Button loading={save.isPending} onClick={() => save.mutate()}>Save preferences</Button>
      </div>
    </div>
  );
}

function hhmm(t) {
  if (!t) return '';
  return String(t).slice(0, 5);
}
