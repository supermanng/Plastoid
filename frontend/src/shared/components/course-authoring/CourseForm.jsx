import { Button, ErrorAlert, Field, FormGrid, Input, Modal, Select, Textarea } from '../ui';
import { useAction, useForm } from '../../lib/hooks';
import { courseApi, courseKeys } from '../../api/course';
import { splitList } from '../../lib/authoring';

/** Create-course form and modal, shared by the tutor's course list (learner module) and the organisation portal (sponsor module). */
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
export function CreateCourseModal({ open, onClose, onCreated, create = courseApi.create, invalidate = [courseKeys.myCourses] }) {
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
