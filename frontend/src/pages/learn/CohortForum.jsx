import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Megaphone, MessageSquare, Plus } from 'lucide-react';
import { Alert, Button, Card, EmptyState, ErrorAlert, Field, Input, Loading, Modal, Textarea } from '../../components/ui';
import { useAction } from '../../lib/hooks';
import { formatDateTime, relativeTime } from '../../lib/format';
import { keys, lms } from '../../api/learn';

/** Cohort forum (GET/POST /lms/cohorts/{id}/threads, POST /lms/threads/{id}/posts) plus tutor announcements. */
export function CohortForum({ cohortId }) {
  const threads = useQuery({ queryKey: keys.threads(cohortId), queryFn: () => lms.threads(cohortId) });
  const announcements = useQuery({ queryKey: keys.announcements(cohortId), queryFn: () => lms.announcements(cohortId), retry: false });
  const [open, setOpen] = useState(false);
  const [reply, setReply] = useState(null);
  const [title, setTitle] = useState('');
  const [body, setBody] = useState('');

  const create = useAction(() => lms.createThread(cohortId, { title: title.trim(), body: body.trim() }), {
    success: 'Thread posted',
    invalidate: [keys.threads(cohortId)],
    onSuccess: () => { setOpen(false); setTitle(''); setBody(''); },
  });
  const post = useAction(() => lms.reply(reply.threadId, body.trim()), {
    success: (r) => (r.flagged ? `Posted, but flagged for review: ${r.flagReason || 'contact details or payment requests are not allowed'}` : 'Reply posted'),
    invalidate: [keys.threads(cohortId)],
    onSuccess: () => { setReply(null); setBody(''); },
  });

  return (
    <div className="space-y-4">
      {announcements.data?.length ? (
        <Card className="border-teal/40 bg-teal-50/40">
          <h3 className="flex items-center gap-2 text-base"><Megaphone className="h-4 w-4 text-teal-600" /> Announcements</h3>
          <ul className="mt-3 space-y-3">
            {announcements.data.map((a) => (
              <li key={a.announcementId} className="text-sm">
                <div className="font-semibold text-navy">{a.title}</div>
                <p className="text-navy/90 whitespace-pre-wrap">{a.body}</p>
                <div className="text-xs text-muted">{formatDateTime(a.postedAt)}</div>
              </li>
            ))}
          </ul>
        </Card>
      ) : null}

      <div className="flex items-center justify-between">
        <h3 className="text-lg">Discussion</h3>
        <Button size="sm" icon={Plus} onClick={() => setOpen(true)}>New thread</Button>
      </div>
      <Alert tone="info">Keep it on the platform: posts that share phone numbers, emails or payment requests are screened and hidden.</Alert>
      {threads.isPending ? <Loading /> : null}
      <ErrorAlert error={threads.error} />
      {threads.data && !threads.data.length ? <EmptyState icon={MessageSquare} title="No threads yet" description="Ask a question or share what you built this week." action={<Button size="sm" onClick={() => setOpen(true)}>Start a thread</Button>} /> : null}
      <ul className="space-y-3">
        {(threads.data || []).map((t) => (
          <Card key={t.threadId} as="li" className="space-y-2">
            <div className="flex items-start justify-between gap-3">
              <div>
                <h4 className="font-bold text-navy">{t.title}</h4>
                <p className="mt-1 whitespace-pre-wrap text-sm text-navy/90">{t.body}</p>
              </div>
              {t.status && t.status !== 'open' ? <span className="rounded-chip bg-surface px-2 py-0.5 text-xs capitalize text-muted">{t.status}</span> : null}
            </div>
            <div className="flex items-center justify-between text-xs text-muted">
              <span>{t.postCount} repl{t.postCount === 1 ? 'y' : 'ies'} · active {relativeTime(t.lastActivityAt)}</span>
              <Button size="sm" variant="ghost" onClick={() => { setReply(t); setBody(''); }}>Reply</Button>
            </div>
          </Card>
        ))}
      </ul>

      <Modal open={open} onClose={() => setOpen(false)} title="New thread" footer={<><Button variant="secondary" onClick={() => setOpen(false)}>Cancel</Button><Button loading={create.isPending} disabled={!title.trim() || !body.trim()} onClick={() => create.mutate()}>Post</Button></>}>
        <ErrorAlert error={create.error} className="mb-3" />
        <div className="space-y-3">
          <Field label="Title" required>{(p) => <Input {...p} value={title} onChange={(e) => setTitle(e.target.value)} maxLength={200} />}</Field>
          <Field label="Message" required>{(p) => <Textarea {...p} value={body} onChange={(e) => setBody(e.target.value)} rows={5} />}</Field>
        </div>
      </Modal>
      <Modal open={Boolean(reply)} onClose={() => setReply(null)} title={reply ? `Reply to “${reply.title}”` : ''} footer={<><Button variant="secondary" onClick={() => setReply(null)}>Cancel</Button><Button loading={post.isPending} disabled={!body.trim()} onClick={() => post.mutate()}>Post reply</Button></>}>
        <ErrorAlert error={post.error} className="mb-3" />
        <Field label="Your reply" required>{(p) => <Textarea {...p} value={body} onChange={(e) => setBody(e.target.value)} rows={5} autoFocus />}</Field>
      </Modal>
    </div>
  );
}
