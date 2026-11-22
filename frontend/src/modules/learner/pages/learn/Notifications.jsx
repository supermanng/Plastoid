import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import clsx from 'clsx';
import { Bell, CheckCheck, Settings } from 'lucide-react';
import { Button, Card, Chip, EmptyState, ErrorAlert, Loading, PageHeader, Pagination, StatusPill } from '../../../../shared/components/ui';
import { useAction, useCursor } from '../../../../shared/lib/hooks';
import { formatDateTime, relativeTime, titleCase } from '../../../../shared/lib/format';
import { keys, notifications } from '../../api/learn';

/** In-app inbox: GET /notifications/me (unreadOnly, cursor), POST /notifications/{id}/read. */
export default function Notifications() {
  const [unreadOnly, setUnreadOnly] = useState(false);
  const cursor = useCursor();
  const params = { unreadOnly, cursor: cursor.cursor, limit: 25 };
  const query = useQuery({ queryKey: keys.notifications(params), queryFn: () => notifications.inbox(params) });
  const items = query.data?.items || [];
  const unread = items.filter((n) => !n.readAt);

  const markRead = useAction((id) => notifications.markRead(id), { silent: true, invalidate: [['notifications']] });
  const markAll = useAction(async () => {
    for (const n of unread) await notifications.markRead(n.notificationId);
    return unread.length;
  }, { success: (n) => `${n} marked as read`, invalidate: [['notifications']] });

  return (
    <div>
      <PageHeader
        title="Notifications"
        description="Application updates, class reminders, payments and messages. Delivery also goes by WhatsApp, SMS, push or email in the order you choose."
        actions={<>
          <Button variant="secondary" size="sm" icon={CheckCheck} disabled={!unread.length} loading={markAll.isPending} onClick={() => markAll.mutate()}>Mark all read</Button>
          <Button to="/learn/notifications/preferences" variant="secondary" size="sm" icon={Settings}>Preferences</Button>
        </>}
      />
      <div className="mb-4 flex gap-2">
        <Chip active={!unreadOnly} onClick={() => { setUnreadOnly(false); cursor.reset(); }}>All</Chip>
        <Chip active={unreadOnly} onClick={() => { setUnreadOnly(true); cursor.reset(); }}>Unread</Chip>
      </div>
      {query.isPending ? <Loading /> : null}
      <ErrorAlert error={query.error} />
      {query.data && !items.length ? <EmptyState icon={Bell} title={unreadOnly ? 'Nothing unread' : 'No notifications yet'} description="We will tell you about offers, classes and payments here." /> : null}
      <ul className="space-y-2">
        {items.map((n) => (
          <Card key={n.notificationId} as="li" className={clsx('flex gap-3 p-4', !n.readAt && 'border-royal/30 bg-royal-50/40')}>
            <span className={clsx('mt-1.5 h-2.5 w-2.5 shrink-0 rounded-full', n.readAt ? 'bg-line' : 'bg-royal')} aria-label={n.readAt ? 'Read' : 'Unread'} />
            <div className="min-w-0 flex-1">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div className="font-semibold text-navy">{n.subject || titleCase(n.templateKey)}</div>
                <span className="text-xs text-muted" title={formatDateTime(n.createdAt)}>{relativeTime(n.createdAt)}</span>
              </div>
              <p className="mt-1 whitespace-pre-wrap text-sm text-navy/90">{n.body}</p>
              <div className="mt-2 flex flex-wrap items-center gap-2">
                {(n.deliveries || []).map((d) => <StatusPill key={d.channel} status={d.status} label={`${titleCase(d.channel)}: ${titleCase(d.status)}`} className="text-[11px]" />)}
                {!n.readAt ? <button type="button" onClick={() => markRead.mutate(n.notificationId)} className="ml-auto text-xs font-semibold text-royal hover:underline">Mark as read</button> : null}
              </div>
            </div>
          </Card>
        ))}
      </ul>
      <Pagination hasMore={Boolean(query.data?.nextCursor)} onMore={() => cursor.next(query.data.nextCursor)} canBack={cursor.canBack} onBack={cursor.back} loading={query.isFetching} />
    </div>
  );
}
