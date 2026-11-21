import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { BookOpen, Plus, Search } from 'lucide-react';
import { Alert, Button, Card, CardHeader, EmptyState, ErrorAlert, Field, FormGrid, Input, Modal, Select, StatusPill, Textarea } from '../../components/ui';
import { useAction, useForm } from '../../lib/hooks';
import { formatDateTime } from '../../lib/format';
import { courseApi } from '../../api/tutor';
import { splitList, useRememberedCourses } from './shared';

/*
 * /tutor/courses — the API has no "list my courses" read (SearchCourses returns published courses only, with no
 * owner filter, and drafts are not readable by slug), so this page lists the courses this browser created or opened
 * (localStorage `plastoids.tutor.courses`) and offers "Find by slug" for published/unpublished ones.
 */
export default function TutorCourses() {
  const navigate = useNavigate();
  const { items, remember, forget } = useRememberedCourses();
  const [creating, setCreating] = useState(false);
  const [slug, setSlug] = useState('');

  const find = useAction((s) => courseApi.bySlug(s.trim()), {
    silent: true,
    onSuccess: (c) => {
      remember({ id: c.courseId, slug: c.slug, title: c.title, summary: c.summary, level: c.level, language: c.language, category: c.category, skills: c.skills, status: c.publishedAt ? 'published' : 'unpublished', publishedAt: c.publishedAt, versionNo: c.versionNo });
      navigate(`/tutor/courses/${c.courseId}`);
    },
  });

  return (
    <div>
      <PageHeaderRow onCreate={() => setCreating(true)} />
      <Alert tone="info" className="mb-6">
        Courses you create or open here are remembered on this device. Drafts cannot be re-opened from the server until they are published, so keep working in the same browser or note the course id.
      </Alert>

      <div className="grid gap-6 lg:grid-cols-3">
        <div className="lg:col-span-2">
          {items.length === 0 ? (
            <EmptyState icon={BookOpen} title="No courses yet" description="Start a draft, add modules and lessons, then submit it for review." action={<Button icon={Plus} onClick={() => setCreating(true)}>Create course</Button>} />
          ) : (
            <ul className="space-y-3">
              {items.map((c) => (
                <li key={c.id}>
                  <Card className="flex flex-wrap items-center justify-between gap-3">
                    <div className="min-w-0">
                      <Link to={`/tutor/courses/${c.id}`} className="text-base font-bold text-navy hover:underline">{c.title}</Link>
                      <div className="text-sm text-muted">
                        {c.level} · {c.category}{c.slug ? ` · /${c.slug}` : ''}{c.updatedAt ? ` · edited ${formatDateTime(c.updatedAt)}` : ''}
                      </div>
                    </div>
                    <div className="flex items-center gap-2">
                      <StatusPill status={c.status || 'draft'} />
                      <Button to={`/tutor/courses/${c.id}`} size="sm" variant="secondary">Open</Button>
                      {c.status === 'published' ? <Button to={`/tutor/courses/${c.id}/gradebook`} size="sm" variant="ghost">Gradebook</Button> : null}
                      <Button size="sm" variant="ghost" aria-label="Forget this course on this device" onClick={() => forget(c.id)}>Forget</Button>
                    </div>
                  </Card>
                </li>
              ))}
            </ul>
          )}
        </div>
        <Card>
          <CardHeader title="Find by slug" description="Loads a published (or unpublished) course of yours into this list." />
          <ErrorAlert error={find.error} className="mb-3" />
          <form className="flex gap-2" onSubmit={(e) => { e.preventDefault(); if (slug.trim()) find.mutate(slug); }}>
            <Input aria-label="Course slug" placeholder="excel-for-beginners" value={slug} onChange={(e) => setSlug(e.target.value)} />
            <Button type="submit" icon={Search} loading={find.isPending} disabled={!slug.trim()}>Find</Button>
          </form>
        </Card>
      </div>

      <CreateCourseModal open={creating} onClose={() => setCreating(false)} onCreated={(c) => { remember(c); navigate(`/tutor/courses/${c.id}`); }} />
    </div>
  );
}

function PageHeaderRow({ onCreate }) {
  return (
    <header className="mb-6 flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
      <div>
        <h1 className="text-2xl sm:text-3xl">My courses</h1>
        <p className="mt-1 text-sm text-muted">Draft → automated checks and content review → approved → published as an immutable version.</p>
      </div>
      <Button icon={Plus} onClick={onCreate}>Create course</Button>
    </header>
  );
}

export function CreateCourseModal({ open, onClose, onCreated }) {
  const { values, bind, reset } = useForm({ title: '', summary: '', level: 'beginner', language: 'English', category: '', skills: '' });
  const create = useAction(
    () => courseApi.create({ title: values.title.trim(), summary: values.summary.trim(), level: values.level, language: values.language.trim(), category: values.category.trim(), skills: splitList(values.skills) }),
    {
      success: 'Draft created. Add modules and lessons next.',
      onSuccess: (res) => {
        onCreated({ id: res.courseId, slug: res.slug, status: res.status, title: values.title.trim(), summary: values.summary.trim(), level: values.level, language: values.language.trim(), category: values.category.trim(), skills: splitList(values.skills), modules: [], offerings: [] });
        reset();
        onClose();
      },
    },
  );
  const valid = values.title.trim() && values.summary.trim() && values.category.trim() && splitList(values.skills).length > 0;
  return (
    <Modal
      open={open}
      onClose={onClose}
      title="Create a course"
      description="A draft only you can see. You can change everything before submitting for review."
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>Cancel</Button>
          <Button loading={create.isPending} disabled={!valid} onClick={() => create.mutate()}>Create draft</Button>
        </>
      }
    >
      <ErrorAlert error={create.error} className="mb-3" />
      <div className="space-y-4">
        <Field label="Title" required error={create.error?.field?.('title')}>{(p) => <Input {...p} {...bind('title')} placeholder="Excel for beginners" />}</Field>
        <Field label="Summary" required hint="One paragraph a learner reads on the course card." error={create.error?.field?.('summary')}>{(p) => <Textarea {...p} {...bind('summary')} rows={3} />}</Field>
        <FormGrid>
          <Field label="Level">{(p) => <Select {...p} {...bind('level')}><option value="beginner">Beginner</option><option value="intermediate">Intermediate</option><option value="advanced">Advanced</option></Select>}</Field>
          <Field label="Language" required>{(p) => <Input {...p} {...bind('language')} />}</Field>
          <Field label="Category" required>{(p) => <Input {...p} {...bind('category')} placeholder="Data" />}</Field>
          <Field label="Skills" hint="Comma-separated." required>{(p) => <Input {...p} {...bind('skills')} placeholder="Excel, Pivot tables" />}</Field>
        </FormGrid>
      </div>
    </Modal>
  );
}
