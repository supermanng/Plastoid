import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { CalendarPlus, Megaphone, MessageSquare, Plus, Video } from 'lucide-react';
import { Alert, Breadcrumb, Button, Card, CardHeader, Checkbox, EmptyState, ErrorAlert, Field, FormGrid, Input, Loading, Modal, StatusPill, Tabs, Textarea, useToast } from '../../../../shared/components/ui';
import { useAction, useForm } from '../../../../shared/lib/hooks';
import { formatDate, formatDateTime, relativeTime } from '../../../../shared/lib/format';
import { announcementApi, lmsApi, tutorKeys } from '../../api/tutor';
import { WEEKDAYS, toIso, toTimeOnly, useRememberedCohorts } from './shared';
import { JoinButton } from './Sessions';

/*
 * /tutor/cohorts/:cohortId
 *  Sessions:      POST /lms/cohorts/{id}/sessions (weekly), POST /lms/live-sessions (one-off), GET /lms/live-sessions/upcoming
 *  Forum:         GET/POST /lms/cohorts/{id}/threads, POST /lms/threads/{threadId}/posts
 *  Announcements: GET/POST /notifications/cohorts/{id}/announcements
 */
export default function CohortDetail() {
  const { cohortId } = useParams();
  const { items } = useRememberedCohorts();
  const cohort = items.find((c) => c.id === cohortId) || { id: cohortId, name: `Cohort ${cohortId.slice(0, 8)}` };
  const [tab, setTab] = useState('sessions');

  return (
    <div>
      <Breadcrumb items={[{ to: '/tutor/cohorts', label: 'Cohorts' }, { label: cohort.name }]} />
      <header className="mb-6 mt-2">
        <h1 className="text-2xl sm:text-3xl">{cohort.name}</h1>
        <p className="mt-1 text-sm text-muted">
          {cohort.courseTitle ? `${cohort.courseTitle} · ` : ''}{cohort.startsAt && cohort.endsAt ? `${formatDate(cohort.startsAt)} – ${formatDate(cohort.endsAt)}` : ''}{cohort.capacity ? ` · ${cohort.capacity} seats` : ''}
        </p>
      </header>
      <Tabs className="mb-6" value={tab} onChange={setTab} tabs={[{ value: 'sessions', label: 'Live classes' }, { value: 'forum', label: 'Forum' }, { value: 'announcements', label: 'Announcements' }]} />
      {tab === 'sessions' ? <SessionsTab cohortId={cohortId} /> : null}
      {tab === 'forum' ? <ForumTab cohortId={cohortId} /> : null}
      {tab === 'announcements' ? <AnnouncementsTab cohortId={cohortId} /> : null}
    </div>
  );
}

/* ---------------------------------------------------------------- Live classes */

function SessionsTab({ cohortId }) {
  const upcoming = useQuery({ queryKey: tutorKeys.upcomingSessions(90), queryFn: () => lmsApi.upcoming(90) });
  const [weekly, setWeekly] = useState(false);
  const [oneOff, setOneOff] = useState(false);
  const mine = (upcoming.data || []).filter((s) => s.cohortId === cohortId);

  return (
    <div className="grid gap-6 lg:grid-cols-3">
      <Card className="lg:col-span-2">
        <CardHeader title="Upcoming live classes" description="Next 90 days. The room opens 15 minutes before the start." action={<div className="flex gap-2"><Button size="sm" variant="secondary" icon={CalendarPlus} onClick={() => setWeekly(true)}>Weekly schedule</Button><Button size="sm" icon={Plus} onClick={() => setOneOff(true)}>One-off class</Button></div>} />
        {upcoming.isPending ? <Loading /> : null}
        {upcoming.isError ? <ErrorAlert error={upcoming.error} /> : null}
        {upcoming.data && mine.length === 0 ? <EmptyState icon={Video} title="No live classes scheduled" description="Create the weekly schedule for the cohort, or add a single class." /> : null}
        <ul className="divide-y divide-line">
          {mine.map((s) => (
            <li key={s.liveSessionId} className="flex flex-wrap items-center justify-between gap-3 py-3">
              <div>
                <div className="font-semibold text-navy">{s.title}</div>
                <div className="text-sm text-muted">{formatDateTime(s.startsAt)} · {Math.round((new Date(s.endsAt) - new Date(s.startsAt)) / 60000)} min · {relativeTime(s.startsAt)}</div>
              </div>
              <div className="flex items-center gap-2">
                <StatusPill status={s.status} />
                <JoinButton session={s} />
              </div>
            </li>
          ))}
        </ul>
      </Card>
      <Card>
        <CardHeader title="Attendance" />
        <p className="text-sm text-muted">Join and leave signals from the video provider mark attendance automatically; sponsors see it on the programme funnel. There is no per-cohort attendance read for tutors yet.</p>
      </Card>
      <WeeklyScheduleModal open={weekly} onClose={() => setWeekly(false)} cohortId={cohortId} />
      <OneOffModal open={oneOff} onClose={() => setOneOff(false)} cohortId={cohortId} />
    </div>
  );
}

function WeeklyScheduleModal({ open, onClose, cohortId }) {
  const [days, setDays] = useState(['tuesday', 'thursday']);
  const { values, bind } = useForm({ startTimeUtc: '18:00', durationMinutes: 90, titlePrefix: 'Live class' });
  const schedule = useAction(
    () => lmsApi.scheduleCohortSessions(cohortId, { weekdays: days, startTimeUtc: toTimeOnly(values.startTimeUtc), durationMinutes: Number(values.durationMinutes), titlePrefix: values.titlePrefix.trim() || null }),
    { success: (r) => `${r.created} class${r.created === 1 ? '' : 'es'} created${r.firstSession ? ` from ${formatDate(r.firstSession)} to ${formatDate(r.lastSession)}` : ''}.`, invalidate: [['tutor', 'live-sessions']], onSuccess: onClose },
  );
  return (
    <Modal open={open} onClose={onClose} title="Weekly live classes" description="Creates every class between the cohort's start and end dates (idempotent: re-running fills gaps only)." footer={<><Button variant="secondary" onClick={onClose}>Cancel</Button><Button loading={schedule.isPending} disabled={!days.length || !values.startTimeUtc} onClick={() => schedule.mutate()}>Create schedule</Button></>}>
      <ErrorAlert error={schedule.error} className="mb-3" />
      <Field label="Weekdays">
        <div className="flex flex-wrap gap-2">
          {WEEKDAYS.map((d) => (
            <button key={d} type="button" aria-pressed={days.includes(d)} onClick={() => setDays((x) => (x.includes(d) ? x.filter((y) => y !== d) : [...x, d]))} className={`min-h-10 rounded-chip border px-3 text-sm font-semibold capitalize ${days.includes(d) ? 'border-royal bg-royal text-white' : 'border-line bg-white text-navy'}`}>{d.slice(0, 3)}</button>
          ))}
        </div>
      </Field>
      <FormGrid cols={3} className="mt-4">
        <Field label="Start time (UTC)" hint="Lagos is UTC+1.">{(p) => <Input {...p} type="time" {...bind('startTimeUtc')} />}</Field>
        <Field label="Duration (minutes)">{(p) => <Input {...p} type="number" min={15} max={480} {...bind('durationMinutes', 'number')} />}</Field>
        <Field label="Title prefix">{(p) => <Input {...p} {...bind('titlePrefix')} />}</Field>
      </FormGrid>
    </Modal>
  );
}

function OneOffModal({ open, onClose, cohortId }) {
  const { values, bind } = useForm({ title: '', startsAt: '', durationMinutes: 60 });
  const create = useAction(
    () => lmsApi.createLiveSession({ cohortId, title: values.title.trim(), startsAt: toIso(values.startsAt), durationMinutes: Number(values.durationMinutes), participantUserIds: [] }),
    { success: 'Live class scheduled.', invalidate: [['tutor', 'live-sessions']], onSuccess: onClose },
  );
  return (
    <Modal open={open} onClose={onClose} title="Schedule a live class" footer={<><Button variant="secondary" onClick={onClose}>Cancel</Button><Button loading={create.isPending} disabled={!values.title.trim() || !values.startsAt} onClick={() => create.mutate()}>Schedule</Button></>}>
      <ErrorAlert error={create.error} className="mb-3" />
      <div className="space-y-4">
        <Field label="Title" required>{(p) => <Input {...p} {...bind('title')} placeholder="Revision session" />}</Field>
        <FormGrid>
          <Field label="Starts" required>{(p) => <Input {...p} type="datetime-local" {...bind('startsAt')} />}</Field>
          <Field label="Duration (minutes)">{(p) => <Input {...p} type="number" min={15} max={480} {...bind('durationMinutes', 'number')} />}</Field>
        </FormGrid>
        <p className="text-xs text-muted">Every active learner in the cohort is invited; attendance is recorded by the video provider.</p>
      </div>
    </Modal>
  );
}

/* ---------------------------------------------------------------- Forum */

function ForumTab({ cohortId }) {
  const threads = useQuery({ queryKey: tutorKeys.threads(cohortId), queryFn: () => lmsApi.threads(cohortId) });
  const [composing, setComposing] = useState(false);
  const [replyTo, setReplyTo] = useState(null);
  const { values, bind, reset } = useForm({ title: '', body: '' });
  const [reply, setReply] = useState('');

  const create = useAction(() => lmsApi.createThread(cohortId, { title: values.title.trim(), body: values.body.trim() }), { success: 'Thread posted.', invalidate: [tutorKeys.threads(cohortId)], onSuccess: () => { reset(); setComposing(false); } });
  const post = useAction(() => lmsApi.reply(replyTo.threadId, { body: reply.trim() }), {
    success: (p) => (p.flagged ? `Reply posted but flagged: ${p.flagReason}` : 'Reply posted.'),
    invalidate: [tutorKeys.threads(cohortId)],
    onSuccess: () => { setReply(''); setReplyTo(null); },
  });

  return (
    <div>
      <Alert tone="info" className="mb-4">Posts are screened for contact details and off-platform payment requests; flagged posts are hidden pending review.</Alert>
      <div className="mb-4 flex justify-end"><Button icon={Plus} onClick={() => setComposing(true)}>New thread</Button></div>
      {threads.isPending ? <Loading /> : null}
      {threads.isError ? <ErrorAlert error={threads.error} /> : null}
      {threads.data && threads.data.length === 0 ? <EmptyState icon={MessageSquare} title="No threads yet" description="Start the conversation with a welcome post or this week's question." /> : null}
      <ul className="space-y-3">
        {(threads.data || []).map((t) => (
          <li key={t.threadId}>
            <Card>
              <div className="flex flex-wrap items-start justify-between gap-2">
                <div>
                  <h3 className="text-base font-bold text-navy">{t.title}</h3>
                  <p className="mt-1 whitespace-pre-wrap text-sm text-navy">{t.body}</p>
                  <p className="mt-2 text-xs text-muted">{t.postCount} repl{t.postCount === 1 ? 'y' : 'ies'} · last activity {relativeTime(t.lastActivityAt)}</p>
                </div>
                <div className="flex items-center gap-2"><StatusPill status={t.status} /><Button size="sm" variant="secondary" onClick={() => setReplyTo(t)}>Reply</Button></div>
              </div>
            </Card>
          </li>
        ))}
      </ul>

      <Modal open={composing} onClose={() => setComposing(false)} title="New thread" footer={<><Button variant="secondary" onClick={() => setComposing(false)}>Cancel</Button><Button loading={create.isPending} disabled={!values.title.trim() || !values.body.trim()} onClick={() => create.mutate()}>Post</Button></>}>
        <ErrorAlert error={create.error} className="mb-3" />
        <div className="space-y-4">
          <Field label="Title" required>{(p) => <Input {...p} {...bind('title')} />}</Field>
          <Field label="Message" required>{(p) => <Textarea {...p} {...bind('body')} rows={5} />}</Field>
        </div>
      </Modal>
      <Modal open={Boolean(replyTo)} onClose={() => setReplyTo(null)} title={replyTo ? `Reply: ${replyTo.title}` : ''} footer={<><Button variant="secondary" onClick={() => setReplyTo(null)}>Cancel</Button><Button loading={post.isPending} disabled={!reply.trim()} onClick={() => post.mutate()}>Post reply</Button></>}>
        <ErrorAlert error={post.error} className="mb-3" />
        <Field label="Your reply" required>{(p) => <Textarea {...p} value={reply} onChange={(e) => setReply(e.target.value)} rows={4} />}</Field>
      </Modal>
    </div>
  );
}

/* ---------------------------------------------------------------- Announcements */

function AnnouncementsTab({ cohortId }) {
  const toast = useToast();
  const list = useQuery({ queryKey: tutorKeys.announcements(cohortId), queryFn: () => announcementApi.list(cohortId) });
  const { values, bind, reset } = useForm({ title: '', body: '' });
  const [confirm, setConfirm] = useState(false);
  const post = useAction(() => announcementApi.post(cohortId, { title: values.title.trim(), body: values.body.trim() }), {
    silent: true,
    invalidate: [tutorKeys.announcements(cohortId)],
    onSuccess: (r) => { toast.success('Announcement sent', `Delivered to ${r.recipients} learner${r.recipients === 1 ? '' : 's'}.`); reset(); },
    onError: (e) => toast.error(e),
  });

  return (
    <div className="grid gap-6 lg:grid-cols-3">
      <Card className="lg:col-span-1">
        <CardHeader title="Announce to the cohort" description="Goes to every active learner through their preferred channels (push, WhatsApp, SMS, email)." />
        <ErrorAlert error={post.error} className="mb-3" />
        <div className="space-y-3">
          <Field label="Title" required>{(p) => <Input {...p} {...bind('title')} />}</Field>
          <Field label="Message" required>{(p) => <Textarea {...p} {...bind('body')} rows={5} />}</Field>
          <Checkbox label="I have checked this is relevant to every learner in the cohort." checked={confirm} onChange={(e) => setConfirm(e.target.checked)} />
          <Button icon={Megaphone} loading={post.isPending} disabled={!confirm || !values.title.trim() || !values.body.trim()} onClick={() => post.mutate()}>Send announcement</Button>
        </div>
      </Card>
      <Card className="lg:col-span-2">
        <CardHeader title="Sent" />
        {list.isPending ? <Loading /> : null}
        {list.isError ? <ErrorAlert error={list.error} /> : null}
        {list.data && list.data.length === 0 ? <EmptyState icon={Megaphone} title="Nothing announced yet" /> : null}
        <ul className="divide-y divide-line">
          {(list.data || []).map((a) => (
            <li key={a.announcementId} className="py-3">
              <div className="flex items-center justify-between gap-2"><h3 className="text-sm font-bold text-navy">{a.title}</h3><span className="text-xs text-muted">{formatDateTime(a.postedAt)}</span></div>
              <p className="mt-1 whitespace-pre-wrap text-sm text-navy">{a.body}</p>
            </li>
          ))}
        </ul>
      </Card>
    </div>
  );
}
