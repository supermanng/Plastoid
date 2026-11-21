import { useEffect, useRef, useState } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import clsx from 'clsx';
import { ChevronLeft, MessageSquare, Plus, Send } from 'lucide-react';
import { Alert, Avatar, Button, Card, EmptyState, ErrorAlert, Field, Input, Loading, Modal, Textarea } from '../../components/ui';
import { useAuth } from '../../lib/auth';
import { useAction } from '../../lib/hooks';
import { formatDateTime, relativeTime } from '../../lib/format';
import { keys, notifications } from '../../api/learn';

/** Learner–tutor messaging kept on the platform (GET/POST /notifications/threads...). Two panes on desktop, stacked on mobile. */
export default function Messages() {
  const { threadId } = useParams();
  const [search, setSearch] = useSearchParams();
  const navigate = useNavigate();
  const auth = useAuth();
  const threads = useQuery({ queryKey: keys.messageThreads, queryFn: notifications.threads, refetchInterval: 30_000 });
  const [composeTo, setComposeTo] = useState(search.get('to') || null);

  useEffect(() => {
    if (search.get('to')) {
      setComposeTo(search.get('to'));
      setSearch({}, { replace: true });
    }
  }, [search, setSearch]);

  const list = [...(threads.data || [])].sort((a, b) => new Date(b.lastMessageAt) - new Date(a.lastMessageAt));

  return (
    <div className="grid gap-4 lg:grid-cols-[320px_1fr]">
      <aside className={clsx(threadId && 'hidden lg:block')}>
        <div className="mb-3 flex items-center justify-between">
          <h1 className="text-xl">Messages</h1>
          <Button size="sm" icon={Plus} onClick={() => setComposeTo('')}>New</Button>
        </div>
        <Card padded={false} className="overflow-hidden">
          {threads.isPending ? <Loading /> : null}
          <ErrorAlert error={threads.error} />
          {threads.data && !list.length ? <EmptyState icon={MessageSquare} title="No conversations" description="Message a tutor from their profile or a booking." className="border-0" /> : null}
          <ul className="max-h-[70dvh] divide-y divide-line overflow-y-auto">
            {list.map((t) => (
              <li key={t.threadId}>
                <Link to={`/learn/messages/${t.threadId}`} className={clsx('flex items-center gap-3 px-4 py-3 hover:bg-surface/70', t.threadId === threadId && 'bg-royal-50')}>
                  <Avatar name={t.subject} size={36} />
                  <div className="min-w-0 flex-1">
                    <div className="flex items-center justify-between gap-2">
                      <span className="truncate text-sm font-semibold text-navy">{t.subject}</span>
                      <span className="shrink-0 text-[11px] text-muted">{relativeTime(t.lastMessageAt)}</span>
                    </div>
                    <div className="text-xs text-muted">{t.messageCount} message{t.messageCount === 1 ? '' : 's'}{t.flagged ? ' · flagged' : ''}</div>
                  </div>
                </Link>
              </li>
            ))}
          </ul>
        </Card>
      </aside>

      <section className={clsx(!threadId && 'hidden lg:block')}>
        {threadId ? <Thread threadId={threadId} meId={auth.me?.id} onBack={() => navigate('/learn/messages')} /> : (
          <Card className="grid min-h-[40dvh] place-items-center text-center">
            <div>
              <MessageSquare className="mx-auto h-10 w-10 text-line" />
              <p className="mt-2 text-sm text-muted">Pick a conversation, or start one with a tutor.</p>
            </div>
          </Card>
        )}
      </section>

      <ComposeModal to={composeTo} onClose={() => setComposeTo(null)} onCreated={(t) => { setComposeTo(null); navigate(`/learn/messages/${t.threadId}`); }} />
    </div>
  );
}

function Thread({ threadId, meId, onBack }) {
  const query = useQuery({ queryKey: keys.messageThread(threadId), queryFn: () => notifications.thread(threadId), refetchInterval: 15_000 });
  const [body, setBody] = useState('');
  const endRef = useRef(null);
  const send = useAction(() => notifications.postMessage(threadId, body.trim()), {
    invalidate: [keys.messageThread(threadId), keys.messageThreads],
    success: (m) => (m.flagged ? `Sent, but flagged: ${m.flagReason || 'keep contact details and payments on the platform'}` : undefined),
    onSuccess: () => setBody(''),
  });
  useEffect(() => {
    endRef.current?.scrollIntoView({ block: 'end' });
  }, [query.data?.messages?.length]);

  if (query.isPending) return <Loading />;
  if (query.isError) return <ErrorAlert error={query.error} />;
  const { thread, messages } = query.data;

  return (
    <Card padded={false} className="flex h-[75dvh] flex-col overflow-hidden">
      <header className="flex items-center gap-2 border-b border-line px-4 py-3">
        <button type="button" onClick={onBack} className="grid h-10 w-10 place-items-center rounded-full hover:bg-surface lg:hidden" aria-label="Back to conversations"><ChevronLeft className="h-5 w-5" /></button>
        <Avatar name={thread.subject} size={36} />
        <div className="min-w-0">
          <div className="truncate font-semibold text-navy">{thread.subject}</div>
          <div className="text-xs text-muted">{thread.messageCount} messages</div>
        </div>
      </header>
      <div className="flex-1 space-y-3 overflow-y-auto bg-surface/50 p-4">
        {messages.map((m) => {
          const mine = m.authorUserId === meId;
          return (
            <div key={m.messageId} className={clsx('flex', mine ? 'justify-end' : 'justify-start')}>
              <div className={clsx('max-w-[80%] rounded-card px-3 py-2 text-sm shadow-sm', mine ? 'bg-royal text-white' : 'bg-white text-navy', m.flagged && 'opacity-70')}>
                <p className="whitespace-pre-wrap break-words">{m.body}</p>
                <div className={clsx('mt-1 text-[10px]', mine ? 'text-white/70' : 'text-muted')}>{formatDateTime(m.sentAt)}{m.flagged ? ` · flagged: ${m.flagReason || 'under review'}` : ''}</div>
              </div>
            </div>
          );
        })}
        <div ref={endRef} />
      </div>
      <form className="flex items-end gap-2 border-t border-line p-3" onSubmit={(e) => { e.preventDefault(); if (body.trim()) send.mutate(); }}>
        <Textarea value={body} onChange={(e) => setBody(e.target.value)} rows={1} placeholder="Write a message… (no phone numbers or payment requests)" className="min-h-11 flex-1 resize-none" onKeyDown={(e) => { if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); if (body.trim()) send.mutate(); } }} />
        <Button type="submit" icon={Send} loading={send.isPending} disabled={!body.trim()} aria-label="Send">Send</Button>
      </form>
      <ErrorAlert error={send.error} className="m-3 mt-0" />
    </Card>
  );
}

function ComposeModal({ to, onClose, onCreated }) {
  const [otherUserId, setOther] = useState(to || '');
  const [subject, setSubject] = useState('');
  const [body, setBody] = useState('');
  useEffect(() => setOther(to || ''), [to]);
  const create = useAction(() => notifications.openThread({ otherUserId, subject: subject.trim(), body: body.trim(), contextId: null }), { invalidate: [keys.messageThreads], success: 'Message sent', onSuccess: onCreated });
  const valid = /^[0-9a-f-]{36}$/i.test(otherUserId) && subject.trim() && body.trim();
  return (
    <Modal open={to !== null} onClose={onClose} title="New message" description="Messages stay on Plastoids so both sides are protected." footer={<><Button variant="secondary" onClick={onClose}>Cancel</Button><Button loading={create.isPending} disabled={!valid} onClick={() => create.mutate()}>Send</Button></>}>
      <ErrorAlert error={create.error} className="mb-3" />
      <div className="space-y-3">
        {!to ? <Alert tone="info">Start a conversation from a tutor's profile or a booking so the recipient is filled in for you.</Alert> : null}
        <Field label="Recipient (user id)" hint="Filled automatically from a tutor profile or booking." required>{(p) => <Input {...p} value={otherUserId} onChange={(e) => setOther(e.target.value)} readOnly={Boolean(to)} />}</Field>
        <Field label="Subject" required>{(p) => <Input {...p} value={subject} onChange={(e) => setSubject(e.target.value)} maxLength={120} placeholder="e.g. Question before our session" />}</Field>
        <Field label="Message" required>{(p) => <Textarea {...p} value={body} onChange={(e) => setBody(e.target.value)} rows={5} />}</Field>
      </div>
    </Modal>
  );
}
