import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { BookOpen, Plus, Users } from 'lucide-react';
import { Alert, Button, Card, EmptyState, PageHeader, QueryState, StatusPill } from '../../components/ui';
import { formatDateTime, pluralise } from '../../lib/format';
import { providerApi, providerKeys } from '../../api/provider';
import { CreateCourseModal } from '../tutor/Courses';
import { useOrganisation } from './useOrganisation';
import { NoOrganisation, OrganisationSwitcher, VerificationBanner } from './OrganisationSwitcher';

/*
 * /sponsor/courses — the organisation's courses (ADR 0019): GET /catalogue/organisations/{id}/courses → CourseSummary[].
 * Owners and authors create courses (the organisation must be verified) and engage tutors; every member can view.
 */
export default function SponsorCourses() {
  const org = useOrganisation();
  const navigate = useNavigate();
  const [creating, setCreating] = useState(false);
  const courses = useQuery({ queryKey: providerKeys.courses(org.organisationId), queryFn: () => providerApi.listCourses(org.organisationId), enabled: Boolean(org.organisationId) });

  if (!org.organisation) {
    return (
      <div>
        <PageHeader title="Courses" />
        <NoOrganisation />
      </div>
    );
  }

  const canCreate = org.isAuthor && org.verified;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Courses"
        description="Courses your organisation owns. Engage verified tutors to teach them; learners pay the organisation, which keeps its share after platform commission and the tutor's share."
        actions={
          <>
            <OrganisationSwitcher org={org} />
            {org.isAuthor ? <Button icon={Plus} disabled={!org.verified} title={org.verified ? undefined : 'Organisation must be verified first'} onClick={() => setCreating(true)}>New course</Button> : null}
          </>
        }
      />
      <VerificationBanner org={org} />
      {org.isAuthor && !org.verified ? <Alert tone="warn" title="Courses open after verification">Once Plastoids verifies the organisation, owners and authors can create courses.</Alert> : null}
      {!org.isAuthor ? <Alert tone="info">You can view courses. Only an organisation owner or author can create or edit them and engage tutors. Your role: <span className="font-semibold capitalize">{org.role || 'member'}</span>.</Alert> : null}

      <QueryState
        query={courses}
        empty={
          <EmptyState
            icon={BookOpen}
            title="No courses yet"
            description={canCreate ? 'Create a draft, build the outline, engage a tutor to teach it and publish.' : 'Courses the organisation creates will be listed here.'}
            action={canCreate ? <Button icon={Plus} onClick={() => setCreating(true)}>Create course</Button> : null}
          />
        }
      >
        {(items) => (
          <ul className="space-y-3">
            {items.map((c) => (
              <li key={c.courseId}>
                <Card className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <Link to={`/sponsor/courses/${c.courseId}`} className="text-base font-bold text-navy hover:underline">{c.title}</Link>
                      <StatusPill status={c.status || 'draft'} />
                    </div>
                    <div className="mt-1 text-sm text-muted">
                      <span className="capitalize">{c.level}</span>{c.category ? ` · ${c.category}` : ''}
                      {` · ${pluralise(c.offeringCount || 0, 'offering')}`}
                      {` · ${pluralise(c.activeEngagements || 0, 'active tutor')}`}
                      {c.updatedAt ? ` · updated ${formatDateTime(c.updatedAt)}` : ''}
                      {c.currentVersionNo ? ` · v${c.currentVersionNo}` : ''}
                    </div>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <Button to={`/sponsor/courses/${c.courseId}`} size="sm" variant="secondary">{org.isAuthor ? 'Edit' : 'View'}</Button>
                    <Button to={`/sponsor/courses/${c.courseId}/tutors`} size="sm" variant="ghost" icon={Users}>Tutors</Button>
                    {String(c.status).toLowerCase() === 'published' && c.slug ? <Button to={`/courses/${c.slug}`} size="sm" variant="ghost">View as learner</Button> : null}
                  </div>
                </Card>
              </li>
            ))}
          </ul>
        )}
      </QueryState>

      <CreateCourseModal
        open={creating}
        onClose={() => setCreating(false)}
        create={(body) => providerApi.createCourse(org.organisationId, body)}
        invalidate={[providerKeys.courses(org.organisationId)]}
        onCreated={(c) => navigate(`/sponsor/courses/${c.id}`)}
      />
    </div>
  );
}
