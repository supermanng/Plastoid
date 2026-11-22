import { useNavigate, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Users } from 'lucide-react';
import { Alert, Breadcrumb, Button, Card, CardHeader, ErrorAlert, PageHeader, StatusPill } from '../../components/ui';
import { useAction, useForm } from '../../lib/hooks';
import { describeTerms, providerApi, providerKeys } from '../../api/provider';
import { AuthoringEditor } from '../tutor/CourseBuilder';
import { CourseFields, EMPTY_COURSE, courseBody, courseValid } from '../tutor/Courses';
import { useOrganisation } from './useOrganisation';
import { NoOrganisation, RoleHint, VerificationBanner } from './OrganisationSwitcher';

/*
 * /sponsor/courses/new            — create a draft: POST /catalogue/organisations/{orgId}/courses → then the editor.
 * /sponsor/courses/:courseId      — the shared builder (tutor/CourseBuilder.jsx) on GET /catalogue/courses/{id}/authoring.
 * The offering form's "Teaching tutor" select is fed from the course's active engagements.
 */
export default function SponsorCourseEditor() {
  const { courseId } = useParams();
  const org = useOrganisation();

  if (!org.organisation) {
    return (
      <div>
        <PageHeader title="Courses" />
        <NoOrganisation />
      </div>
    );
  }
  return courseId ? <EditCourse org={org} courseId={courseId} /> : <NewCourse org={org} />;
}

function NewCourse({ org }) {
  const navigate = useNavigate();
  const { values, bind } = useForm(EMPTY_COURSE);
  const create = useAction(() => providerApi.createCourse(org.organisationId, courseBody(values)), {
    success: 'Draft created. Build the outline and engage a tutor next.',
    invalidate: [providerKeys.courses(org.organisationId)],
    onSuccess: (res) => navigate(`/sponsor/courses/${res.courseId}`, { replace: true }),
  });
  const blocked = !org.isAuthor || !org.verified;

  return (
    <div className="mx-auto max-w-2xl">
      <Breadcrumb items={[{ to: '/sponsor/courses', label: 'Courses' }, { label: 'New course' }]} />
      <PageHeader className="mt-2" title="New course" description={`A draft owned by ${org.organisation.name}. Only owners and authors see it until it is published.`} />
      <VerificationBanner org={org} />
      {!org.isAuthor ? <Alert tone="warn" className="mt-4">Only an organisation owner or author can create courses.</Alert> : null}
      <Card className="mt-4">
        <ErrorAlert error={create.error} className="mb-4" />
        <form onSubmit={(e) => { e.preventDefault(); if (!blocked && courseValid(values)) create.mutate(); }}>
          <fieldset disabled={blocked}>
            <CourseFields bind={bind} error={create.error} />
          </fieldset>
          <div className="mt-5 flex flex-wrap items-center justify-between gap-3">
            <RoleHint org={org} needs="author" />
            <div className="flex gap-2">
              <Button variant="secondary" to="/sponsor/courses">Cancel</Button>
              <Button type="submit" loading={create.isPending} disabled={blocked || !courseValid(values)}>Create draft</Button>
            </div>
          </div>
        </form>
      </Card>
    </div>
  );
}

function EditCourse({ org, courseId }) {
  const engagements = useQuery({
    queryKey: providerKeys.engagements(org.organisationId, courseId),
    queryFn: () => providerApi.listEngagements(org.organisationId, courseId),
  });
  const active = (engagements.data || []).filter((e) => String(e.status).toLowerCase() === 'active');
  const tutors = active.map((e) => ({ tutorUserId: e.tutorUserId, label: `Tutor ${String(e.tutorId || e.tutorUserId).slice(0, 8)} · ${describeTerms(e.terms)}` }));

  return (
    <AuthoringEditor
      courseId={courseId}
      basePath="/sponsor/courses"
      listLabel="Courses"
      tutors={tutors}
      readOnly={!org.isAuthor}
      aside={
        <Card>
          <CardHeader title="Teaching tutors" description="Engaged tutors teach cohort and one-to-one offerings." action={<Button to={`/sponsor/courses/${courseId}/tutors`} size="sm" variant="secondary" icon={Users}>Manage</Button>} />
          <ErrorAlert error={engagements.error} className="mb-2" />
          {engagements.isPending ? <p className="text-sm text-muted">Loading…</p> : null}
          {engagements.data && !engagements.data.length ? <p className="text-sm text-muted">No tutors engaged yet. Engage one before adding a cohort or one-to-one offering.</p> : null}
          <ul className="space-y-2 text-sm">
            {(engagements.data || []).map((e) => (
              <li key={e.engagementId} className="flex flex-wrap items-center justify-between gap-2">
                <span className="text-navy">Tutor {String(e.tutorId || e.tutorUserId).slice(0, 8)} · {describeTerms(e.terms)}</span>
                <StatusPill status={e.status} />
              </li>
            ))}
          </ul>
        </Card>
      }
    />
  );
}
