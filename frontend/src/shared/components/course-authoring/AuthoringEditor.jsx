import { useEffect, useMemo, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowDown, ArrowUp, CheckCircle2, FileText, ListChecks, Plus, Send, Trash2, Upload, Video, XCircle } from 'lucide-react';
import { Alert, Breadcrumb, Button, Card, CardHeader, EmptyState, ErrorAlert, Field, FormGrid, Input, Loading, StatusPill, Textarea, useToast } from '../ui';
import { useAction, useForm } from '../../lib/hooks';
import { formatDateTime } from '../../lib/format';
import { AppLink } from '../../lib/navigation';
import { courseApi, courseKeys } from '../../api/course';
import { splitList, uploadToPresignedUrl } from '../../lib/authoring';
import { LessonModal, OfferingsPanel, QuizModal } from './CourseBuilderParts';

/*
 * Course builder shared by the tutor area (learner module, /tutor/courses/:courseId) and the organisation portal
 * (sponsor module, /sponsor/courses/:courseId). `basePath` is the host module's own course list route (relative to
 * its router); links to the public course page go through <AppLink> because it lives in the learner module.
 *
 * GET  /catalogue/courses/{id}/authoring                       draft outline: course fields, status, modules[lessons], offerings
 * PUT  /catalogue/courses/{id}/outline                         metadata + ordered modules (ids come back for new ones)
 * POST /catalogue/courses/{id}/modules/{moduleId}/lessons      video | doc | assignment | live
 * POST /catalogue/courses/{id}/modules/{moduleId}/quizzes      MCQ quiz
 * POST /catalogue/lessons/{lessonId}/video                     direct-upload URL
 * POST /catalogue/courses/{id}/offerings                       self_paced | cohort | one_to_one (+ tutorUserId on organisation courses)
 * POST /catalogue/courses/{id}/submit · /publish
 */

/** Normalise the authoring view (and tolerate the public CourseDetail field names) into the shape the builder edits. */
export function normaliseAuthoring(v) {
  if (!v) return null;
  const modules = v.modules || v.outline || [];
  const offerings = v.offerings || [];
  return {
    id: v.courseId || v.id,
    slug: v.slug,
    title: v.title,
    summary: v.summary,
    level: v.level,
    language: v.language,
    category: v.category,
    skills: v.skills || [],
    status: String(v.status || (v.publishedAt ? 'published' : 'draft')).toLowerCase(),
    ownerType: v.ownerType,
    organisationId: v.organisationId,
    publishedAt: v.publishedAt,
    updatedAt: v.updatedAt,
    versionNo: v.currentVersionNo ?? v.versionNo,
    modules: modules
      .slice()
      .sort((a, b) => a.order - b.order)
      .map((m) => ({
        id: m.id || m.moduleId,
        title: m.title,
        order: m.order,
        lessons: (m.lessons || [])
          .slice()
          .sort((a, b) => a.order - b.order)
          .map((l) => ({ id: l.id || l.lessonId, type: String(l.type || '').toLowerCase(), title: l.title, order: l.order, durationMinutes: l.durationMinutes, isPreview: l.isPreview, videoStatus: l.videoStatus || l.mediaStatus || (l.mediaAssetId ? 'uploaded' : undefined), mediaAssetId: l.mediaAssetId })),
      })),
    offerings: offerings.map((o) => ({ id: o.offeringId || o.id, format: o.format, price: o.price, fundingTypes: o.fundingTypes || [], capacity: o.capacity, startAt: o.startAt, durationWeeks: o.durationWeeks, tutorUserId: o.tutorUserId, engagementId: o.engagementId })),
  };
}

/**
 * The builder proper. `tutors` (organisation courses only) is the list of active engagements { tutorUserId, label }
 * offered in the offering form's "Teaching tutor" select; `aside` renders extra cards in the right column.
 */
export function AuthoringEditor({ courseId, basePath, listLabel = 'Courses', tutors, aside, readOnly = false }) {
  const toast = useToast();
  const qc = useQueryClient();
  const key = courseKeys.authoring(courseId);
  const query = useQuery({
    queryKey: key,
    queryFn: async () => normaliseAuthoring(await courseApi.authoring(courseId)),
    refetchOnWindowFocus: false, // unsaved module edits live in the page; do not reset them on focus
  });

  if (query.isPending) return <Loading />;
  if (query.isError) {
    const status = query.error?.status;
    return (
      <div>
        <Breadcrumb items={[{ to: basePath, label: listLabel }, { label: 'Course' }]} />
        <div className="mt-4">
          {status === 404 || status === 403 ? (
            <EmptyState icon={FileText} title={status === 403 ? 'You cannot edit this course' : 'Course not found'} description={status === 403 ? 'Only the course owner, or an owner or author of the owning organisation, can open the builder.' : 'The course may have been removed, or the link is old.'} action={<Button to={basePath}>Back to {listLabel.toLowerCase()}</Button>} />
          ) : <ErrorAlert error={query.error} />}
        </div>
      </div>
    );
  }

  const course = query.data;
  // Apply what a mutation returned straight away, then reconcile with the server's authoring view
  // (skipped for upload progress the view may not carry yet).
  const update = (patch, { reconcile = true } = {}) => {
    qc.setQueryData(key, (prev) => ({ ...(prev || course), ...patch }));
    if (reconcile) qc.invalidateQueries({ queryKey: key });
  };
  const modules = course.modules || [];
  const lessonCount = modules.reduce((n, m) => n + (m.lessons?.length || 0), 0);
  const hasQuiz = modules.some((m) => m.lessons?.some((l) => l.type === 'quiz'));
  const blockers = [
    modules.length === 0 && 'Add at least one module.',
    lessonCount === 0 && 'Add at least one lesson.',
    !hasQuiz && 'Add a quiz so completion can be assessed.',
    !(course.offerings || []).length && 'Add an offering (how the course is sold).',
    !course.summary && 'Write a summary.',
  ].filter(Boolean);

  return (
    <div>
      <Breadcrumb items={[{ to: basePath, label: listLabel }, { label: course.title }]} />
      <header className="mb-6 mt-2 flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div className="min-w-0">
          <h1 className="text-2xl sm:text-3xl">{course.title}</h1>
          <div className="mt-1 flex flex-wrap items-center gap-2 text-sm text-muted">
            <StatusPill status={course.status || 'draft'} />
            <span>{course.level} · {course.language} · {course.category}</span>
            {course.versionNo ? <span>· v{course.versionNo}</span> : null}
            {course.publishedAt ? <span>· published {formatDateTime(course.publishedAt)}</span> : null}
          </div>
        </div>
        <div className="flex flex-wrap gap-2">
          {course.status === 'published' && basePath === '/tutor/courses' ? <Button to={`${basePath}/${course.id}/gradebook`} variant="secondary">Gradebook</Button> : null}
          {course.slug && course.status === 'published' ? <Button as={AppLink} to={`/courses/${course.slug}`} variant="ghost">View as learner</Button> : null}
        </div>
      </header>

      {readOnly ? <Alert tone="info" className="mb-4">You can view this course but not change it: only an owner or author of the organisation can edit courses.</Alert> : null}

      <div className="grid gap-6 lg:grid-cols-3">
        <div className="space-y-6 lg:col-span-2">
          <MetadataCard course={course} onSaved={update} readOnly={readOnly} />
          <ModulesCard course={course} onChange={update} readOnly={readOnly} />
          <OfferingsPanel course={course} onChange={update} tutors={tutors} readOnly={readOnly} />
        </div>
        <div className="space-y-6">
          {readOnly ? null : <PublishCard course={course} blockers={blockers} onChange={update} toast={toast} />}
          <Card>
            <CardHeader title="Checklist" />
            <ul className="space-y-2 text-sm">
              {[
                [modules.length > 0, `${modules.length} module${modules.length === 1 ? '' : 's'}`],
                [lessonCount > 0, `${lessonCount} lesson${lessonCount === 1 ? '' : 's'}`],
                [hasQuiz, 'At least one quiz'],
                [(course.offerings || []).length > 0, `${(course.offerings || []).length} offering${(course.offerings || []).length === 1 ? '' : 's'}`],
                [Boolean(course.summary), 'Summary written'],
              ].map(([ok, label]) => (
                <li key={label} className="flex items-center gap-2">
                  {ok ? <CheckCircle2 className="h-4 w-4 text-teal-600" /> : <XCircle className="h-4 w-4 text-muted" />}
                  <span className={ok ? 'text-navy' : 'text-muted'}>{label}</span>
                </li>
              ))}
            </ul>
          </Card>
          {aside}
        </div>
      </div>
    </div>
  );
}

/* ---------------------------------------------------------------- Metadata + modules (one PUT /outline) */

function MetadataCard({ course, onSaved, readOnly }) {
  const { values, bind, setValues } = useForm({ title: course.title || '', summary: course.summary || '', level: course.level || 'beginner', language: course.language || '', category: course.category || '', skills: (course.skills || []).join(', ') });
  useEffect(() => {
    setValues({ title: course.title || '', summary: course.summary || '', level: course.level || 'beginner', language: course.language || '', category: course.category || '', skills: (course.skills || []).join(', ') });
  }, [course.id, course.title, course.summary, course.level, course.language, course.category, course.skills, setValues]);

  const save = useAction(
    () => courseApi.updateOutline(course.id, {
      title: values.title.trim(),
      summary: values.summary.trim(),
      level: values.level,
      language: values.language.trim(),
      category: values.category.trim(),
      skills: splitList(values.skills),
      modules: (course.modules || []).map((m) => ({ id: m.id, title: m.title, order: m.order })),
    }),
    {
      success: 'Course details saved.',
      onSuccess: (res) => onSaved({ title: values.title.trim(), summary: values.summary.trim(), level: values.level, language: values.language.trim(), category: values.category.trim(), skills: splitList(values.skills), status: String(res.status || course.status).toLowerCase(), modules: mergeModuleIds(course.modules, res.modules) }),
    },
  );
  return (
    <Card>
      <CardHeader title="Course details" />
      <ErrorAlert error={save.error} className="mb-3" />
      <fieldset disabled={readOnly} className="space-y-4">
        <Field label="Title" required>{(p) => <Input {...p} {...bind('title')} />}</Field>
        <Field label="Summary" required>{(p) => <Textarea {...p} {...bind('summary')} rows={3} />}</Field>
        <FormGrid>
          <Field label="Level">{(p) => (
            <select {...p} {...bind('level')} className="input">
              <option value="beginner">Beginner</option><option value="intermediate">Intermediate</option><option value="advanced">Advanced</option>
            </select>
          )}</Field>
          <Field label="Language">{(p) => <Input {...p} {...bind('language')} />}</Field>
          <Field label="Category">{(p) => <Input {...p} {...bind('category')} />}</Field>
          <Field label="Skills" hint="Comma-separated.">{(p) => <Input {...p} {...bind('skills')} />}</Field>
        </FormGrid>
        {readOnly ? null : <div className="flex justify-end"><Button loading={save.isPending} disabled={!values.title.trim() || !values.summary.trim()} onClick={() => save.mutate()}>Save details</Button></div>}
      </fieldset>
    </Card>
  );
}

function mergeModuleIds(local = [], fromServer = []) {
  // The server returns { id, title, order } for every module; new ones get ids by matching title + order.
  return fromServer
    .slice()
    .sort((a, b) => a.order - b.order)
    .map((m) => {
      const existing = local.find((x) => x.id === m.id) || local.find((x) => !x.id && x.title === m.title && x.order === m.order) || local.find((x) => x.title === m.title);
      return { id: m.id, title: m.title, order: m.order, lessons: existing?.lessons || [] };
    });
}

function ModulesCard({ course, onChange, readOnly }) {
  const [draftModules, setDraftModules] = useState(course.modules || []);
  const [lessonTarget, setLessonTarget] = useState(null); // moduleId
  const [quizTarget, setQuizTarget] = useState(null);
  useEffect(() => setDraftModules(course.modules || []), [course.modules]);

  const dirty = useMemo(() => JSON.stringify(draftModules.map((m) => [m.id, m.title, m.order])) !== JSON.stringify((course.modules || []).map((m) => [m.id, m.title, m.order])), [draftModules, course.modules]);

  const save = useAction(
    () => courseApi.updateOutline(course.id, { modules: draftModules.map((m, i) => ({ id: m.id || null, title: m.title.trim(), order: i + 1 })) }),
    { success: 'Outline saved.', onSuccess: (res) => onChange({ status: String(res.status || course.status).toLowerCase(), modules: mergeModuleIds(draftModules.map((m, i) => ({ ...m, order: i + 1 })), res.modules) }) },
  );

  const move = (i, dir) => setDraftModules((ms) => {
    const next = ms.slice();
    const j = i + dir;
    if (j < 0 || j >= next.length) return ms;
    [next[i], next[j]] = [next[j], next[i]];
    return next.map((m, k) => ({ ...m, order: k + 1 }));
  });

  const addLessonLocal = (moduleId, lesson) => onChange({ modules: (course.modules || []).map((m) => (m.id === moduleId ? { ...m, lessons: [...(m.lessons || []), lesson].sort((a, b) => a.order - b.order) } : m)) });
  const updateLessonLocal = (lessonId, patch) => onChange({ modules: (course.modules || []).map((m) => ({ ...m, lessons: (m.lessons || []).map((l) => (l.id === lessonId ? { ...l, ...patch } : l)) })) }, { reconcile: false });

  return (
    <Card>
      <CardHeader title="Modules & lessons" description="Order modules, then add video, document, assignment, live and quiz lessons. Save the outline before adding lessons to a new module." action={readOnly ? null : <Button size="sm" variant="secondary" icon={Plus} onClick={() => setDraftModules((ms) => [...ms, { id: null, title: `Module ${ms.length + 1}`, order: ms.length + 1, lessons: [] }])}>Add module</Button>} />
      <ErrorAlert error={save.error} className="mb-3" />
      {draftModules.length === 0 ? <p className="text-sm text-muted">No modules yet.</p> : null}
      <ol className="space-y-3">
        {draftModules.map((m, i) => (
          <li key={m.id || `new-${i}`} className="rounded-card border border-line">
            <div className="flex flex-wrap items-center gap-2 border-b border-line bg-surface/60 px-3 py-2">
              <span className="grid h-7 w-7 place-items-center rounded-full bg-white text-xs font-bold text-navy">{i + 1}</span>
              <Input aria-label="Module title" value={m.title} disabled={readOnly} onChange={(e) => setDraftModules((ms) => ms.map((x, j) => (j === i ? { ...x, title: e.target.value } : x)))} className="min-h-9 flex-1" />
              {readOnly ? null : (
                <>
                  <Button variant="ghost" size="sm" aria-label="Move up" disabled={i === 0} onClick={() => move(i, -1)}><ArrowUp className="h-4 w-4" /></Button>
                  <Button variant="ghost" size="sm" aria-label="Move down" disabled={i === draftModules.length - 1} onClick={() => move(i, 1)}><ArrowDown className="h-4 w-4" /></Button>
                  <Button variant="ghost" size="sm" aria-label="Remove module" onClick={() => setDraftModules((ms) => ms.filter((_, j) => j !== i))}><Trash2 className="h-4 w-4" /></Button>
                </>
              )}
            </div>
            <ul className="divide-y divide-line">
              {(m.lessons || []).map((l) => (
                <LessonRow key={l.id} lesson={l} readOnly={readOnly} onUpdated={(patch) => updateLessonLocal(l.id, patch)} />
              ))}
              {(m.lessons || []).length === 0 ? <li className="px-3 py-2 text-sm text-muted">No lessons in this module.</li> : null}
            </ul>
            {readOnly ? null : (
              <div className="flex flex-wrap gap-2 px-3 py-2">
                <Button size="sm" variant="secondary" icon={Plus} disabled={!m.id || dirty} onClick={() => setLessonTarget(m.id)}>Lesson</Button>
                <Button size="sm" variant="secondary" icon={ListChecks} disabled={!m.id || dirty} onClick={() => setQuizTarget(m.id)}>Quiz</Button>
                {!m.id || dirty ? <span className="self-center text-xs text-muted">Save the outline first.</span> : null}
              </div>
            )}
          </li>
        ))}
      </ol>
      {dirty && !readOnly ? (
        <div className="mt-4 flex justify-end">
          <Button loading={save.isPending} disabled={draftModules.some((m) => !m.title.trim())} onClick={() => save.mutate()}>Save outline</Button>
        </div>
      ) : null}

      <LessonModal open={Boolean(lessonTarget)} onClose={() => setLessonTarget(null)} courseId={course.id} moduleId={lessonTarget} nextOrder={(draftModules.find((m) => m.id === lessonTarget)?.lessons?.length || 0) + 1} onCreated={(lesson) => addLessonLocal(lessonTarget, lesson)} />
      <QuizModal open={Boolean(quizTarget)} onClose={() => setQuizTarget(null)} courseId={course.id} moduleId={quizTarget} nextOrder={(draftModules.find((m) => m.id === quizTarget)?.lessons?.length || 0) + 1} onCreated={(lesson) => addLessonLocal(quizTarget, lesson)} />
    </Card>
  );
}

const LESSON_ICON = { video: Video, doc: FileText, quiz: ListChecks, assignment: Upload, live: Send };

function LessonRow({ lesson, onUpdated, readOnly }) {
  const toast = useToast();
  const Icon = LESSON_ICON[lesson.type] || FileText;
  const [progress, setProgress] = useState(null);
  const [busy, setBusy] = useState(false);

  async function uploadVideo(file) {
    setBusy(true);
    try {
      const res = await courseApi.videoUploadUrl(lesson.id);
      await uploadToPresignedUrl(res.uploadUrl, file, file.type || 'video/mp4', setProgress);
      onUpdated({ videoStatus: 'uploaded', mediaAssetId: res.mediaAssetId });
      toast.success('Video uploaded', `${res.provider} is processing it; the lesson becomes playable when the provider webhook arrives.`);
    } catch (err) {
      toast.error(err, 'Video upload failed');
      onUpdated({ videoStatus: 'failed' });
    } finally {
      setBusy(false);
      setProgress(null);
    }
  }

  return (
    <li className="flex flex-wrap items-center justify-between gap-2 px-3 py-2 text-sm">
      <div className="flex min-w-0 items-center gap-2">
        <Icon className="h-4 w-4 shrink-0 text-royal" aria-hidden />
        <span className="truncate font-medium text-navy">{lesson.order}. {lesson.title}</span>
        <span className="text-xs text-muted">{lesson.type} · {lesson.durationMinutes} min{lesson.isPreview ? ' · preview' : ''}</span>
      </div>
      {lesson.type === 'video' ? (
        <div className="flex items-center gap-2">
          {lesson.videoStatus ? <StatusPill status={lesson.videoStatus} /> : <StatusPill status="no_video" tone="warn" label="No video" />}
          {readOnly ? null : (
            <label className={`inline-flex min-h-9 cursor-pointer items-center gap-1 rounded-input border border-line px-3 text-xs font-semibold text-navy hover:bg-surface ${busy ? 'opacity-60' : ''}`}>
              <Upload className="h-3.5 w-3.5" /> {busy ? `${progress ?? 0}%` : lesson.videoStatus ? 'Replace' : 'Upload video'}
              <input type="file" accept="video/mp4,video/webm" className="sr-only" disabled={busy} onChange={(e) => e.target.files?.[0] && uploadVideo(e.target.files[0])} />
            </label>
          )}
        </div>
      ) : null}
    </li>
  );
}

/* ---------------------------------------------------------------- Submit / publish */

function PublishCard({ course, blockers, onChange, toast }) {
  const [checks, setChecks] = useState(null);
  const submit = useAction(() => courseApi.submit(course.id), {
    silent: true,
    onSuccess: (res) => {
      setChecks(res);
      onChange({ status: String(res.status || course.status).toLowerCase() });
      toast[res.checks.every((c) => c.passed) ? 'success' : 'info'](res.requiresHumanReview ? 'Queued for content review' : `Status: ${res.status}`);
    },
    onError: (err) => toast.error(err, 'Submit failed'),
  });
  const publish = useAction(() => courseApi.publish(course.id), {
    silent: true,
    onSuccess: (res) => {
      onChange({ status: 'published', slug: res.slug, versionNo: res.versionNo, publishedAt: res.publishedAt });
      toast.success(`Published as version ${res.versionNo}`);
    },
    onError: (err) => toast.error(err, 'Publish failed'),
  });

  const status = String(course.status || 'draft').toLowerCase();
  return (
    <Card>
      <CardHeader title="Review & publish" />
      <ol className="space-y-2 text-sm">
        <Step n={1} done={status !== 'draft'} label="Submit for automated checks and content review" />
        <Step n={2} done={['approved', 'published'].includes(status)} label="Staff approve the course" />
        <Step n={3} done={status === 'published'} label="Publish an immutable version learners can buy" />
      </ol>
      {blockers.length && status === 'draft' ? (
        <Alert tone="warn" className="mt-4" title="Before you submit">
          <ul className="list-disc pl-4">{blockers.map((b) => <li key={b}>{b}</li>)}</ul>
        </Alert>
      ) : null}
      {checks ? (
        <ul className="mt-4 space-y-1 text-sm">
          {checks.checks.map((c) => (
            <li key={c.code} className="flex items-start gap-2">
              {c.passed ? <CheckCircle2 className="mt-0.5 h-4 w-4 text-teal-600" /> : <XCircle className="mt-0.5 h-4 w-4 text-danger" />}
              <span><span className="font-medium">{c.code.replace(/_/g, ' ')}</span>: {c.message}</span>
            </li>
          ))}
        </ul>
      ) : null}
      <div className="mt-4 flex flex-col gap-2">
        {status === 'draft' || status === 'rejected' ? (
          <Button icon={Send} loading={submit.isPending} disabled={blockers.length > 0} onClick={() => submit.mutate()}>Submit for review</Button>
        ) : null}
        {status === 'inreview' ? <p className="text-sm text-muted">Waiting for a content reviewer. You will be notified of the decision.</p> : null}
        {status === 'approved' || status === 'published' || status === 'unpublished' ? (
          <Button icon={CheckCircle2} loading={publish.isPending} onClick={() => publish.mutate()}>{status === 'published' ? 'Publish new version' : 'Publish'}</Button>
        ) : null}
        {status === 'published' && course.slug ? <p className="text-xs text-muted">Live at <AppLink to={`/courses/${course.slug}`} className="font-semibold text-royal">/courses/{course.slug}</AppLink>. Editing after publishing creates a new version on the next publish.</p> : null}
      </div>
    </Card>
  );
}

function Step({ n, done, label }) {
  return (
    <li className="flex items-center gap-2">
      <span className={`grid h-6 w-6 place-items-center rounded-full text-xs font-bold ${done ? 'bg-teal text-navy' : 'bg-surface text-muted'}`}>{done ? '✓' : n}</span>
      <span className={done ? 'text-navy' : 'text-muted'}>{label}</span>
    </li>
  );
}
