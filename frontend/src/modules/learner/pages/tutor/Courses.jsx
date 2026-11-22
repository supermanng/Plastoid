import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { BookOpen, Plus } from 'lucide-react';
import { Button, Card, EmptyState, PageHeader, QueryState, StatusPill } from '../../../../shared/components/ui';
import { formatDateTime } from '../../../../shared/lib/format';
import { courseApi, tutorKeys } from '../../api/tutor';
import { CreateCourseModal } from '../../../../shared/components/course-authoring/CourseForm';

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
