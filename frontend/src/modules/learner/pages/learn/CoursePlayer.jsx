import { useEffect, useMemo, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import clsx from 'clsx';
import { Award, Check, ChevronLeft, ChevronRight, CircleDot, FileText, ListTree, MessageSquare, PlayCircle, Video, ClipboardCheck, Upload, Circle } from 'lucide-react';
import { Alert, Breadcrumb, Button, Card, EmptyState, ErrorAlert, Loading, ProgressBar, StatusPill, Tabs } from '../../../../shared/components/ui';
import { useAction } from '../../../../shared/lib/hooks';
import { formatDate } from '../../../../shared/lib/format';
import { catalogue, keys, lms } from '../../api/learn';
import { LessonBody } from './LessonViews';
import { CohortForum } from './CohortForum';

const TYPE_ICON = { video: PlayCircle, doc: FileText, document: FileText, quiz: ClipboardCheck, assignment: Upload, live: Video };

/**
 * Course player: outline from GET /catalogue/courses/{slug} (the enrolment exposes courseSlug), lesson status from the
 * enrolment gradebook, lesson content from GET /lms/enrolments/{id}/lessons/{lessonId}. Progress is synced in batches.
 */
export default function CoursePlayer() {
  const { enrolmentId, lessonId } = useParams();
  const navigate = useNavigate();
  const qc = useQueryClient();
  const [tab, setTab] = useState('lesson');
  const [outlineOpen, setOutlineOpen] = useState(false);

  const enrolments = useQuery({ queryKey: keys.enrolments, queryFn: lms.enrolments });
  const enrolment = enrolments.data?.find((e) => e.enrolmentId === enrolmentId);
  const course = useQuery({ queryKey: keys.course(enrolment?.courseSlug), queryFn: () => catalogue.course(enrolment.courseSlug), enabled: Boolean(enrolment?.courseSlug) });
  const gradebook = useQuery({ queryKey: keys.gradebook(enrolmentId), queryFn: () => lms.gradebook(enrolmentId) });
  const gradeRow = gradebook.data?.rows?.[0];
  const statusByLesson = useMemo(() => Object.fromEntries((gradeRow?.items || []).map((i) => [i.lessonId, i])), [gradeRow]);

  const lessons = useMemo(() => {
    const out = [];
    for (const m of [...(course.data?.outline || [])].sort((a, b) => a.order - b.order)) {
      for (const l of [...(m.lessons || [])].sort((a, b) => a.order - b.order)) out.push({ ...l, moduleTitle: m.title, moduleId: m.id });
    }
    if (!out.length && gradeRow) return gradeRow.items.map((i) => ({ id: i.lessonId, title: i.title, type: i.type, moduleTitle: 'Lessons' }));
    return out;
  }, [course.data, gradeRow]);

  // No lesson in the URL → open the first unfinished lesson.
  useEffect(() => {
    if (lessonId || !lessons.length) return;
    const next = lessons.find((l) => statusByLesson[l.id]?.status !== 'completed') || lessons[0];
    navigate(`/learn/courses/${enrolmentId}/lessons/${next.id}`, { replace: true });
  }, [lessonId, lessons, statusByLesson, enrolmentId, navigate]);

  const index = lessons.findIndex((l) => l.id === lessonId);
  const current = index >= 0 ? lessons[index] : null;
  const prev = index > 0 ? lessons[index - 1] : null;
  const next = index >= 0 && index < lessons.length - 1 ? lessons[index + 1] : null;

  const progress = useAction((items) => lms.progress(enrolmentId, items), { silent: true, invalidate: [keys.enrolments, keys.gradebook(enrolmentId)] });
  const complete = useAction(() => lms.complete(enrolmentId), {
    invalidate: [keys.enrolments, keys.certificates],
    success: (r) => (r.completedNow ? 'Course completed. Your certificate is on its way.' : `Not complete yet (${Math.round(r.progressPct)}%): finish every required lesson and pass the quizzes.`),
  });

  const reportProgress = (item) => progress.mutate([{ lessonId, status: 'inProgress', positionSeconds: 0, page: 1, percentComplete: 0, clientUpdatedAt: new Date().toISOString(), ...item }]);
  const markComplete = () => reportProgress({ status: 'completed', percentComplete: 100 });

  if (enrolments.isPending) return <Loading />;
  if (enrolments.isError) return <ErrorAlert error={enrolments.error} />;
  if (!enrolment) return <EmptyState title="Enrolment not found" description="It may belong to another account." action={<Button to="/learn/courses">My courses</Button>} />;

  const title = course.data?.title || gradebook.data?.courseTitle || 'Course';
  const allDone = lessons.length > 0 && lessons.every((l) => statusByLesson[l.id]?.status === 'completed' || !isRequired(l));

  return (
    <div>
      <Breadcrumb items={[{ to: '/learn/courses', label: 'My courses' }, { label: title }]} />
      <div className="mt-2 flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-xl sm:text-2xl">{title}</h1>
        <div className="flex items-center gap-2">
          <StatusPill status={enrolment.status} />
          {enrolment.status === 'active' ? <Button size="sm" variant={allDone ? 'teal' : 'secondary'} icon={Award} loading={complete.isPending} onClick={() => complete.mutate()}>Complete course</Button> : null}
          {enrolment.status === 'completed' ? <Button size="sm" variant="teal" to="/learn/certificates" icon={Award}>Certificate</Button> : null}
        </div>
      </div>
      <ProgressBar className="mt-3 max-w-xl" value={Number(enrolment.progressPct)} label={`${enrolment.completedLessons} of ${enrolment.requiredLessons} required lessons · access until ${formatDate(enrolment.expiresAt)}`} />

      <div className="mt-6 grid gap-6 lg:grid-cols-[300px_1fr]">
        {/* Outline */}
        <aside className={clsx('lg:block', outlineOpen ? 'block' : 'hidden')}>
          <Card padded={false} className="overflow-hidden">
            <div className="flex items-center justify-between border-b border-line px-4 py-3 text-sm font-semibold text-navy"><span className="flex items-center gap-2"><ListTree className="h-4 w-4" /> Outline</span><span className="text-xs text-muted">{lessons.length} lessons</span></div>
            {course.isPending && !gradeRow ? <Loading /> : null}
            <ErrorAlert error={course.error} />
            <ol className="max-h-[60dvh] overflow-y-auto">
              {groupByModule(lessons).map(([moduleTitle, items]) => (
                <li key={moduleTitle}>
                  <div className="bg-surface px-4 py-1.5 text-xs font-semibold uppercase tracking-wide text-muted">{moduleTitle}</div>
                  {items.map((l) => {
                    const st = statusByLesson[l.id]?.status;
                    const Icon = TYPE_ICON[l.type] || FileText;
                    return (
                      <Link
                        key={l.id}
                        to={`/learn/courses/${enrolmentId}/lessons/${l.id}`}
                        onClick={() => setOutlineOpen(false)}
                        className={clsx('flex min-h-11 items-center gap-3 px-4 py-2 text-sm hover:bg-surface/70', l.id === lessonId ? 'bg-royal-50 text-royal font-semibold' : 'text-navy')}
                        aria-current={l.id === lessonId ? 'page' : undefined}
                      >
                        {st === 'completed' ? <Check className="h-4 w-4 shrink-0 text-teal" /> : st === 'inProgress' ? <CircleDot className="h-4 w-4 shrink-0 text-royal" /> : <Circle className="h-4 w-4 shrink-0 text-line" />}
                        <Icon className="h-4 w-4 shrink-0 text-muted" />
                        <span className="min-w-0 flex-1 truncate">{l.title}</span>
                        {l.durationMinutes ? <span className="text-xs text-muted">{l.durationMinutes}m</span> : null}
                      </Link>
                    );
                  })}
                </li>
              ))}
            </ol>
          </Card>
        </aside>

        {/* Lesson */}
        <section className="min-w-0">
          <div className="mb-3 flex items-center justify-between lg:hidden">
            <Button variant="secondary" size="sm" icon={ListTree} onClick={() => setOutlineOpen((v) => !v)}>{outlineOpen ? 'Hide outline' : 'Outline'}</Button>
            <span className="text-xs text-muted">{index >= 0 ? `Lesson ${index + 1} of ${lessons.length}` : ''}</span>
          </div>
          {enrolment.cohortId ? (
            <Tabs className="mb-4" value={tab} onChange={setTab} tabs={[{ value: 'lesson', label: 'Lesson' }, { value: 'forum', label: 'Cohort forum' }]} />
          ) : null}
          {tab === 'forum' && enrolment.cohortId ? (
            <CohortForum cohortId={enrolment.cohortId} />
          ) : current ? (
            <LessonBody
              key={lessonId}
              enrolmentId={enrolmentId}
              lessonId={lessonId}
              lesson={current}
              gradeItem={statusByLesson[lessonId]}
              onProgress={reportProgress}
              onComplete={markComplete}
              completing={progress.isPending}
              onRefresh={() => {
                qc.invalidateQueries({ queryKey: keys.gradebook(enrolmentId) });
                qc.invalidateQueries({ queryKey: keys.enrolments });
              }}
            />
          ) : lessons.length ? <Loading /> : (
            <EmptyState title="No lessons yet" description="The tutor has not published lessons for this course version." />
          )}
          {current ? (
            <div className="mt-6 flex items-center justify-between gap-2">
              <Button variant="secondary" size="sm" icon={ChevronLeft} disabled={!prev} to={prev ? `/learn/courses/${enrolmentId}/lessons/${prev.id}` : undefined}>Previous</Button>
              {next ? <Button size="sm" to={`/learn/courses/${enrolmentId}/lessons/${next.id}`}>Next lesson <ChevronRight className="h-4 w-4" /></Button> : <Button size="sm" variant="teal" icon={Award} loading={complete.isPending} onClick={() => complete.mutate()}>Finish course</Button>}
            </div>
          ) : null}
          {enrolment.cohortId && tab === 'lesson' ? (
            <button type="button" onClick={() => setTab('forum')} className="mt-4 inline-flex items-center gap-2 text-sm font-semibold text-royal hover:underline"><MessageSquare className="h-4 w-4" /> Ask your cohort</button>
          ) : null}
        </section>
      </div>
      {enrolment.status === 'active' && new Date(enrolment.expiresAt) < new Date() ? <Alert tone="warn" className="mt-6">Your access ended on {formatDate(enrolment.expiresAt)}. Content may no longer load.</Alert> : null}
    </div>
  );
}

function isRequired(lesson) {
  return lesson.isPreview !== true;
}

function groupByModule(lessons) {
  const map = new Map();
  for (const l of lessons) {
    if (!map.has(l.moduleTitle)) map.set(l.moduleTitle, []);
    map.get(l.moduleTitle).push(l);
  }
  return [...map.entries()];
}
