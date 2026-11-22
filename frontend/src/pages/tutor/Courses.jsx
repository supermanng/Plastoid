import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { BookOpen, Plus } from 'lucide-react';
import { Button, Card, EmptyState, ErrorAlert, Field, FormGrid, Input, Modal, PageHeader, QueryState, Select, StatusPill, Textarea } from '../../components/ui';
import { useAction, useForm } from '../../lib/hooks';
import { formatDateTime } from '../../lib/format';
import { courseApi, tutorKeys } from '../../api/tutor';
import { splitList } from './shared';

/*
 * /tutor/courses — GET /catalogue/courses/mine (CourseSummary[]) for the signed-in tutor; the builder loads each
 * draft from GET /catalogue/courses/{id}/authoring, so nothing is kept in the browser any more.
 */
export default function TutorCourses() {
  const navigate = useNavigate();
  const [creating, setCreating] = useState(false);
  const courses = useQuery({ queryKey: tutorKeys.myCourses, queryFn: courseApi.listMine });

  return (
    <div>
      <PageHeader
        title="My courses"
        description="Draft → automated checks and content review → approved → published as an immutable version."
        actions={<Button icon={Plus} onClick={() => setCreating(true)}>Create course</Button>}
      />

      <QueryState query={courses} empty={<EmptyState icon={BookOpen} title="No courses yet" description="Start a draft, add modules and lessons, then submit it for review." action={<Button icon={Plus} onClick={() => setCreating(true)}>Create course</Button>} />}>
        {(items) => (
          <ul className="space-y-3">
            {items.map((c) => (
              <li key={c.courseId}>
                <Card className="flex flex-wrap items-center justify-between gap-3">
                  <div className="min-w-0">
                    <Link to={`/tutor/courses/${c.courseId}`} className="text-base font-bold text-navy hover:underline">{c.title}</Link>
                    <div className="text-sm text-muted">
                      {c.level} · {c.category}{c.slug ? ` · /${c.slug}` : ''}{c.updatedAt ? ` · edited ${formatDateTime(c.updatedAt)}` : ''}
                      {c.offeringCount ? ` · ${c.offeringCount} offering${c.offeringCount === 1 ? '' : 's'}` : ''}
                    </div>
                  </div>
                  <div className="flex flex-wrap items-center gap-2">
                    <StatusPill status={c.status || 'draft'} />
                    <Button to={`/tutor/courses/${c.courseId}`} size="sm" variant="secondary">Open</Button>
                    {String(c.status).toLowerCase() === 'published' ? <Button to={`/tutor/courses/${c.courseId}/gradebook`} size="sm" variant="ghost">Gradebook</Button> : null}
                  </div>
                </Card>
              </li>
            ))}
          </ul>
        )}
      </QueryState>

      <CreateCourseModal open={creating} onClose={() => setCreating(false)} onCreated={(c) => navigate(`/tutor/courses/${c.id}`)} />
    </div>
  );
}

export const EMPTY_COURSE = Object.freeze({ title: '', summary: '', level: 'beginner', language: 'English', category: '', skills: '' });

/** Request body for POST /catalogue/courses and POST /catalogue/organisations/{id}/courses. */
export function courseBody(values) {
  return { title: values.title.trim(), summary: values.summary.trim(), level: values.level, language: values.language.trim(), category: values.category.trim(), skills: splitList(values.skills) };
}

export function courseValid(values) {
  return Boolean(values.title.trim() && values.summary.trim() && values.category.trim() && splitList(values.skills).length > 0);
}

/** The create-course fields, shared by the tutor modal and the organisation's /sponsor/courses/new page. */
export function CourseFields({ bind, error }) {
  return (
    <div className="space-y-4">
      <Field label="Title" required error={error?.field?.('title')}>{(p) => <Input {...p} {...bind('title')} placeholder="Excel for beginners" />}</Field>
      <Field label="Summary" required hint="One paragraph a learner reads on the course card." error={error?.field?.('summary')}>{(p) => <Textarea {...p} {...bind('summary')} rows={3} />}</Field>
      <FormGrid>
        <Field label="Level">{(p) => <Select {...p} {...bind('level')}><option value="beginner">Beginner</option><option value="intermediate">Intermediate</option><option value="advanced">Advanced</option></Select>}</Field>
        <Field label="Language" required>{(p) => <Input {...p} {...bind('language')} />}</Field>
        <Field label="Category" required>{(p) => <Input {...p} {...bind('category')} placeholder="Data" />}</Field>
        <Field label="Skills" hint="Comma-separated." required>{(p) => <Input {...p} {...bind('skills')} placeholder="Excel, Pivot tables" />}</Field>
      </FormGrid>
    </div>
  );
}

/** `create(body)` defaults to the tutor's POST /catalogue/courses; the organisation portal passes its own. */
export function CreateCourseModal({ open, onClose, onCreated, create = courseApi.create, invalidate = [tutorKeys.myCourses] }) {
  const { values, bind, reset } = useForm(EMPTY_COURSE);
  const action = useAction(() => create(courseBody(values)), {
    success: 'Draft created. Add modules and lessons next.',
    invalidate,
    onSuccess: (res) => {
      onCreated({ id: res.courseId, slug: res.slug, status: res.status, ...courseBody(values) });
      reset();
      onClose();
    },
  });
  return (
    <Modal
      open={open}
      onClose={onClose}
      title="Create a course"
      description="A draft only you can see. You can change everything before submitting for review."
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>Cancel</Button>
          <Button loading={action.isPending} disabled={!courseValid(values)} onClick={() => action.mutate()}>Create draft</Button>
        </>
      }
    >
      <ErrorAlert error={action.error} className="mb-3" />
      <CourseFields bind={bind} error={action.error} />
    </Modal>
  );
}
