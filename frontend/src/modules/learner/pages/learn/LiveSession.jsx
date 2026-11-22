import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Copy, ExternalLink, Mic, Video, Wifi } from 'lucide-react';
import { Alert, Button, Card, CardHeader, ErrorAlert, KeyValue, Loading, StatusPill, useToast } from '../../../../shared/components/ui';
import { useAction } from '../../../../shared/lib/hooks';
import { formatDateTime, relativeTime } from '../../../../shared/lib/format';
import { keys, lms } from '../../api/learn';

/**
 * Live class / tutor session room. POST /lms/live-sessions/{id}/join-token returns a short-lived provider token
 * and join URL. The video SDK (Daily/100ms/Zoom) is not embedded here: we hand the learner to the provider room
 * and surface the token for the native app. Attendance is recorded by the provider webhook, not by this page.
 */
export default function LiveSession() {
  const { liveSessionId } = useParams();
  const toast = useToast();
  const upcoming = useQuery({ queryKey: keys.upcoming(30), queryFn: () => lms.upcoming(30) });
  const session = upcoming.data?.find((s) => s.liveSessionId === liveSessionId);
  const [token, setToken] = useState(null);
  const [audioOnly, setAudioOnly] = useState(false);
  const join = useAction(() => lms.joinToken(liveSessionId), { silent: true, onSuccess: setToken });

  const joinUrl = token ? appendAudioOnly(String(token.joinUrl), audioOnly || token.audioOnlyFallback) : null;

  return (
    <div className="mx-auto max-w-2xl">
      <nav className="text-sm text-muted"><Link to="/learn">Dashboard</Link> · <Link to="/learn/bookings">Tutor sessions</Link></nav>
      <div className="mt-2 flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl">{session?.title || 'Live session'}</h1>
          {session ? <p className="text-sm text-muted">{formatDateTime(session.startsAt)} → {formatDateTime(session.endsAt)} · {relativeTime(session.startsAt)}{session.isHost ? ' · you are the host' : ''}</p> : null}
        </div>
        {session ? <StatusPill status={session.status} /> : null}
      </div>

      <Card className="mt-6">
        <CardHeader title="Join the room" description="The room opens 15 minutes before the start. Your join link is personal and expires quickly, so request it when you are ready." />
        {upcoming.isPending ? <Loading /> : null}
        {session && !session.joinOpen && !token ? <Alert tone="info" className="mb-3">Not open yet. Come back {relativeTime(new Date(new Date(session.startsAt).getTime() - 15 * 60000))}.</Alert> : null}
        <ErrorAlert error={join.error} className="mb-3" />
        {!token ? (
          <Button icon={Video} size="lg" loading={join.isPending} onClick={() => join.mutate()} disabled={session ? !session.joinOpen && !join.isPending : false}>Get my join link</Button>
        ) : (
          <div className="space-y-4">
            <Alert tone="success" title={token.isHost ? 'You are the host' : 'You are in'}>Link valid until {formatDateTime(token.expiresAt)} · room {token.roomId} via {token.provider}.</Alert>
            {token.audioOnlyFallback ? <Alert tone="warn" title="Low bandwidth detected">The room will start audio-only. You can turn video on inside once your connection allows.</Alert> : null}
            <label className="flex min-h-11 items-center gap-3 text-sm text-navy">
              <input type="checkbox" className="h-5 w-5 accent-royal" checked={audioOnly} onChange={(e) => setAudioOnly(e.target.checked)} />
              <span className="inline-flex items-center gap-2"><Mic className="h-4 w-4 text-muted" /> Join audio-only (saves data on slow connections)</span>
            </label>
            <div className="flex flex-wrap gap-2">
              <Button href={joinUrl} target="_blank" rel="noreferrer" size="lg" variant="teal" icon={ExternalLink}>Open the room</Button>
              <Button variant="secondary" icon={Copy} onClick={() => navigator.clipboard?.writeText(token.token).then(() => toast.success('Token copied'))}>Copy token</Button>
            </div>
            <KeyValue items={[['Provider', token.provider], ['Room', token.roomId], ['Join URL', <a href={joinUrl} className="break-all text-xs" target="_blank" rel="noreferrer">{joinUrl}</a>]]} />
          </div>
        )}
      </Card>

      <Card className="mt-6">
        <CardHeader title={<span className="inline-flex items-center gap-2"><Wifi className="h-4 w-4 text-cyan" /> Before you join</span>} />
        <ul className="list-disc space-y-1 pl-5 text-sm text-navy">
          <li>Use headphones and find a quiet spot; mute when you are not speaking.</li>
          <li>On a ₦100k Android phone, audio-only uses about 10× less data than video.</li>
          <li>Attendance is recorded automatically when you join and leave; stay for the whole class to count it.</li>
          <li>If the room drops, come back here and request a new link.</li>
        </ul>
      </Card>
    </div>
  );
}

function appendAudioOnly(url, on) {
  if (!on) return url;
  try {
    const u = new URL(url);
    u.searchParams.set('audioOnly', '1');
    return u.toString();
  } catch {
    return url;
  }
}
