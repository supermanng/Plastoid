import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Award, BookOpen, Download, ListChecks } from 'lucide-react';
import { Alert, Button, Card, EmptyState, ErrorAlert, KeyValue, Loading, Modal, PageHeader, ProgressBar, StatusPill, Table, Tabs } from '../../../../shared/components/ui';
import { useAction } from '../../../../shared/lib/hooks';
import { formatDate, formatDateTime } from '../../../../shared/lib/format';
import { keys, lms } from '../../api/learn';
import { titleFromSlug } from './Dashboard';
import { SponsoredChip } from './SponsoredChip';

/** GET /lms/enrolments/me → cards with progress; complete, gradebook and offline package per enrolment. */
export default function MyCourses() {
  const [tab, setTab] = useState('active');
  const enrolments = useQuery({ queryKey: keys.enrolments, queryFn: lms.enrolments });
  const [gradebookFor, setGradebookFor] = useState(null);
  const [offlineFor, setOfflineFor] = useState(null);

  const all = enrolments.data || [];
  const counts = { active: all.filter((e) => e.status === 'active').length, completed: all.filter((e) => e.status === 'completed').length, withdrawn: all.filter((e) => e.status === 'withdrawn').length };
  const rows = all.filter((e) => e.status === tab);

  return (
    <div>
      <PageHeader title="My courses" description="Everything you are enrolled in, with progress and scores." actions={<Button to="/courses" variant="secondary" icon={BookOpen}>Find a course</Button>} />
      <Tabs
        value={tab}
        onChange={setTab}
        tabs={[
          { value: 'active', label: 'Active', count: counts.active },
          { value: 'completed', label: 'Completed', count: counts.completed },
          { value: 'withdrawn', label: 'Withdrawn', count: counts.withdrawn },
        ]}
        className="mb-6"
      />
      {enrolments.isPending ? <Loading /> : null}
      <ErrorAlert error={enrolments.error} />
      {enrolments.data && !rows.length ? (
        <EmptyState
          icon={tab === 'completed' ? Award : BookOpen}
          title={tab === 'active' ? 'No active courses' : `No ${tab} courses`}
          description={tab === 'active' ? 'Enrol in a course to start learning. A seat a sponsor gives you appears here on its own.' : undefined}
          action={tab === 'active' ? <Button to="/courses" size="sm">Browse courses</Button> : null}
        />
      ) : null}
      <div className="grid gap-4 md:grid-cols-2">
        {rows.map((e) => (
          <EnrolmentCard key={e.enrolmentId} e={e} onGradebook={() => setGradebookFor(e)} onOffline={() => setOfflineFor(e)} />
        ))}
      </div>
      <GradebookModal enrolment={gradebookFor} onClose={() => setGradebookFor(null)} />
      <OfflineModal enrolment={offlineFor} onClose={() => setOfflineFor(null)} />
    </div>
  );
}

function EnrolmentCard({ e, onGradebook, onOffline }) {
  const complete = useAction(() => lms.complete(e.enrolmentId), {
    invalidate: [keys.enrolments, keys.certificates],
    success: (r) => (r.completedNow ? 'Course completed. Your certificate is being issued.' : `Not complete yet: ${Math.round(r.progressPct)}% of required lessons done.`),
  });
  const expired = new Date(e.expiresAt) < new Date();
  return (
    <Card className="flex flex-col gap-3">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <h3 className="truncate text-base font-bold text-navy">{e.courseSlug ? titleFromSlug(e.courseSlug) : 'Course'}</h3>
          <SponsoredChip enrolment={e} className="mt-1" />
          <div className="mt-0.5 text-xs text-muted">
            {e.fundingSource === 'sponsored' ? 'Sponsored seat' : e.fundingSource === 'paid' ? 'Paid' : e.fundingSource} · v{e.versionNo} · started {formatDate(e.startedAt)}
            {e.cohortId ? ' · cohort' : ''}
          </div>
        </div>
        <StatusPill status={e.status} />
      </div>
      <ProgressBar value={Number(e.progressPct)} label={`${e.completedLessons} of ${e.requiredLessons} required lessons`} />
      {e.finalScore != null ? <div className="text-sm text-navy">Final score: <strong className="tabular">{Number(e.finalScore).toFixed(0)}%</strong></div> : null}
      {expired && e.status === 'active' ? <Alert tone="warn">Access ended {formatDate(e.expiresAt)}.</Alert> : <div className="text-xs text-muted">Access until {formatDate(e.expiresAt)}{e.completedAt ? ` · completed ${formatDate(e.completedAt)}` : ''}</div>}
      <div className="mt-auto flex flex-wrap gap-2">
        <Button to={`/learn/courses/${e.enrolmentId}`} size="sm">{e.status === 'active' ? 'Continue' : 'Open'}</Button>
        <Button variant="secondary" size="sm" icon={ListChecks} onClick={onGradebook}>Gradebook</Button>
        {e.status === 'active' ? <Button variant="secondary" size="sm" icon={Download} onClick={onOffline}>Offline</Button> : null}
        {e.status === 'active' && Number(e.progressPct) >= 100 ? <Button variant="teal" size="sm" icon={Award} loading={complete.isPending} onClick={() => complete.mutate()}>Complete course</Button> : null}
      </div>
    </Card>
  );
}

function GradebookModal({ enrolment, onClose }) {
  const query = useQuery({ queryKey: keys.gradebook(enrolment?.enrolmentId), queryFn: () => lms.gradebook(enrolment.enrolmentId), enabled: Boolean(enrolment) });
  const row = query.data?.rows?.[0];
  return (
    <Modal open={Boolean(enrolment)} onClose={onClose} title={query.data?.courseTitle || 'Gradebook'} description="Your progress and best score per lesson." size="lg">
      {query.isPending ? <Loading /> : null}
      <ErrorAlert error={query.error} />
      {row ? (
        <>
          <KeyValue className="mb-4" cols={3} items={[['Progress', `${Math.round(row.progressPct)}%`], ['Status', <StatusPill status={row.status} />], ['Final score', row.finalScore != null ? `${Number(row.finalScore).toFixed(0)}%` : 'Not yet']]} />
          <Table
            dense
            rowKey={(r) => r.lessonId}
            rows={row.items}
            columns={[
              { key: 'title', header: 'Lesson', render: (r) => <Link to={`/learn/courses/${enrolment.enrolmentId}/lessons/${r.lessonId}`} className="font-medium">{r.title}</Link> },
              { key: 'type', header: 'Type', render: (r) => <span className="capitalize">{r.type}</span> },
              { key: 'status', header: 'Status', render: (r) => <StatusPill status={r.status} /> },
              { key: 'bestScore', header: 'Best score', align: 'right', render: (r) => (r.bestScore != null ? `${Number(r.bestScore).toFixed(0)}%` : '—') },
              { key: 'attempts', header: 'Attempts', align: 'right' },
              { key: 'passed', header: 'Passed', render: (r) => (r.passed ? <StatusPill status="passed" /> : <span className="text-muted">—</span>) },
            ]}
          />
        </>
      ) : null}
    </Modal>
  );
}

function OfflineModal({ enrolment, onClose }) {
  const request = useAction(() => lms.offlinePackage(enrolment.enrolmentId), { silent: true });
  return (
    <Modal
      open={Boolean(enrolment)}
      onClose={() => { request.reset(); onClose(); }}
      title="Offline package"
      description="A lesson manifest with media links that stay valid until your enrolment ends. Download on Wi-Fi, learn anywhere."
      footer={!request.data ? <Button loading={request.isPending} onClick={() => request.mutate()}>Prepare package</Button> : <Button variant="secondary" onClick={onClose}>Done</Button>}
    >
      <ErrorAlert error={request.error} />
      {request.data ? (
        <div className="space-y-3">
          <Alert tone="success" title="Package ready">Links expire {formatDateTime(request.data.expiresAt)}. The content key is shown for the offline player.</Alert>
          <KeyValue items={[['Package', request.data.packageId], ['Key', <code className="break-all text-xs">{request.data.keyBase64}</code>]]} />
          <ul className="divide-y divide-line rounded-card border border-line">
            {request.data.lessons.map((l) => (
              <li key={l.lessonId} className="flex items-center justify-between gap-3 px-3 py-2 text-sm">
                <div className="min-w-0">
                  <div className="truncate font-medium text-navy">{l.title}</div>
                  <div className="text-xs text-muted capitalize">{l.type} · {l.durationMinutes} min</div>
                </div>
                {l.mediaUrl ? <a href={l.mediaUrl} download className="shrink-0 text-royal text-sm font-semibold">Download</a> : <span className="text-xs text-muted">{l.document ? 'Document included' : 'Online only'}</span>}
              </li>
            ))}
          </ul>
        </div>
      ) : (
        <p className="text-sm text-muted">Media is served as HLS/MP4 links; documents and quizzes are embedded in the manifest.</p>
      )}
    </Modal>
  );
}
