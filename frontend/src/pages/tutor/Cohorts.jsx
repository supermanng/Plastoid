import { useMemo, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Plus, Users } from 'lucide-react';
import { Alert, Button, Card, EmptyState, ErrorAlert, Field, FormGrid, Input, Modal, PageHeader, Select, StatusPill } from '../../components/ui';
import { useAuth } from '../../lib/auth';
import { useAction, useForm } from '../../lib/hooks';
import { formatDate } from '../../lib/format';
import { courseApi, engagementApi, lmsApi, tutorKeys } from '../../api/tutor';
import { toIso, useRememberedCohorts } from './shared';

/*
 * /tutor/cohorts — POST /lms/cohorts { courseId, offeringId, name, startsAt, endsAt, capacity, programmeId?, tutorUserId? }.
 * The course picker offers the tutor's own courses (GET /catalogue/courses/mine) and the organisation courses the tutor
 * is actively engaged on (GET /catalogue/engagements/mine); for the latter `tutorUserId` names the signed-in tutor.
 * The Lms module has no "list my cohorts" read (only per-cohort threads/sessions), so cohorts created here are
 * remembered on this device (localStorage `plastoids.tutor.cohorts`).
 */
export default function TutorCohorts() {
  const navigate = useNavigate();
  const { items: cohorts, remember, forget } = useRememberedCohorts();
  const [open, setOpen] = useState(false);
  const [openId, setOpenId] = useState('');

  const now = Date.now();
  const grouped = {
    active: cohorts.filter((c) => new Date(c.startsAt) <= now && new Date(c.endsAt) >= now),
    upcoming: cohorts.filter((c) => new Date(c.startsAt) > now),
    past: cohorts.filter((c) => new Date(c.endsAt) < now),
  };

  return (
    <div>
      <PageHeader title="Cohorts" description="A cohort is a dated group of learners on a cohort-format offering, taught through weekly live classes." actions={<Button icon={Plus} onClick={() => setOpen(true)}>Open a cohort</Button>} />
      <Alert tone="info" className="mb-6">Cohorts you open here are remembered on this device. Have a cohort id from elsewhere? Paste it below to open it.</Alert>

      <Card className="mb-6 flex flex-col gap-2 sm:flex-row sm:items-end">
        <Field label="Open cohort by id" className="flex-1">{(p) => <Input {...p} value={openId} onChange={(e) => setOpenId(e.target.value)} placeholder="cohort id" className="font-mono text-sm" />}</Field>
        <Button variant="secondary" disabled={!/^[0-9a-f-]{36}$/i.test(openId.trim())} onClick={() => { remember({ id: openId.trim(), name: `Cohort ${openId.trim().slice(0, 8)}`, startsAt: new Date().toISOString(), endsAt: new Date().toISOString(), capacity: null }); navigate(`/tutor/cohorts/${openId.trim()}`); }}>Open</Button>
      </Card>

      {cohorts.length === 0 ? (
        <EmptyState icon={Users} title="No cohorts yet" description="Open one on a cohort-format offering of a published course, then schedule its weekly live classes." action={<Button icon={Plus} onClick={() => setOpen(true)}>Open a cohort</Button>} />
      ) : (
        ['active', 'upcoming', 'past'].map((k) => grouped[k].length ? (
          <section key={k} className="mb-6">
            <h2 className="mb-2 text-lg capitalize">{k}</h2>
            <ul className="space-y-3">
              {grouped[k].map((c) => (
                <li key={c.id}>
                  <Card className="flex flex-wrap items-center justify-between gap-3">
                    <div>
                      <Link to={`/tutor/cohorts/${c.id}`} className="text-base font-bold text-navy hover:underline">{c.name}</Link>
                      <div className="text-sm text-muted">{c.courseTitle ? `${c.courseTitle} · ` : ''}{formatDate(c.startsAt)} – {formatDate(c.endsAt)}{c.capacity ? ` · ${c.capacity} seats` : ''}</div>
                    </div>
                    <div className="flex items-center gap-2">
                      <StatusPill status={k} tone={k === 'active' ? 'success' : k === 'upcoming' ? 'warn' : 'neutral'} />
                      <Button to={`/tutor/cohorts/${c.id}`} size="sm" variant="secondary">Manage</Button>
                      <Button size="sm" variant="ghost" onClick={() => forget(c.id)}>Forget</Button>
                    </div>
                  </Card>
                </li>
              ))}
            </ul>
          </section>
        ) : null)
      )}

      <CreateCohortModal open={open} onClose={() => setOpen(false)} onCreated={(c) => { remember(c); navigate(`/tutor/cohorts/${c.id}`); }} />
    </div>
  );
}

/** Own courses plus organisation courses with an active engagement, as one picker list. */
function useTeachableCourses(enabled) {
  const mine = useQuery({ queryKey: tutorKeys.myCourses, queryFn: courseApi.listMine, enabled });
  const engagements = useQuery({ queryKey: tutorKeys.engagements, queryFn: engagementApi.mine, enabled });
  const items = useMemo(() => {
    const own = (mine.data || []).map((c) => ({ id: c.courseId, slug: c.slug, title: c.title, status: String(c.status || '').toLowerCase(), engaged: false }));
    const engaged = (engagements.data || [])
      .filter((e) => String(e.status).toLowerCase() === 'active' && !own.some((c) => c.id === e.courseId))
      .map((e) => ({ id: e.courseId, slug: e.courseSlug, title: e.courseTitle, organisationName: e.organisationName, status: 'published', engaged: true }));
    return [...own, ...engaged];
  }, [mine.data, engagements.data]);
  return { items, isPending: mine.isPending || engagements.isPending, error: mine.error || engagements.error };
}

function CreateCohortModal({ open, onClose, onCreated }) {
  const auth = useAuth();
  const { values, bind, set, reset } = useForm({ courseId: '', offeringId: '', name: '', startsAt: '', endsAt: '', capacity: 30, programmeId: '' });
  const { items: courses, isPending, error } = useTeachableCourses(open);
  const course = courses.find((c) => c.id === values.courseId);
  const manualCourse = !course;

  // Offerings come from the public course read (published or unpublished); drafts have no readable offerings yet.
  const detail = useQuery({ queryKey: tutorKeys.course(course?.slug), queryFn: () => courseApi.bySlug(course.slug), enabled: Boolean(course?.slug), retry: false });
  const cohortOfferings = (detail.data?.offerings || []).filter((o) => String(o.format).toLowerCase() === 'cohort');

  const create = useAction(
    () => lmsApi.createCohort({
      courseId: values.courseId.trim(),
      offeringId: values.offeringId.trim(),
      name: values.name.trim(),
      startsAt: toIso(values.startsAt),
      endsAt: toIso(values.endsAt),
      capacity: Number(values.capacity),
      programmeId: values.programmeId.trim() || null,
      tutorUserId: course?.engaged ? auth.me?.id : undefined,
    }),
    {
      success: 'Cohort opened. Schedule its live classes next.',
      onSuccess: (res) => {
        onCreated({ id: res.cohortId, courseId: res.courseId, offeringId: res.offeringId, name: res.name, startsAt: res.startsAt, endsAt: res.endsAt, capacity: res.capacity, courseTitle: course?.title });
        reset();
        onClose();
      },
    },
  );
  const valid = /^[0-9a-f-]{36}$/i.test(values.courseId.trim()) && /^[0-9a-f-]{36}$/i.test(values.offeringId.trim()) && values.name.trim() && values.startsAt && values.endsAt && new Date(values.endsAt) > new Date(values.startsAt) && Number(values.capacity) > 0;

  return (
    <Modal open={open} onClose={onClose} title="Open a cohort" description="Pick a published course you own or teach for an organisation, and its cohort-format offering." footer={<><Button variant="secondary" onClick={onClose}>Cancel</Button><Button loading={create.isPending} disabled={!valid} onClick={() => create.mutate()}>Open cohort</Button></>}>
      <ErrorAlert error={create.error || error} className="mb-3" />
      <div className="space-y-4">
        <Field label="Course" hint={isPending ? 'Loading your courses…' : courses.length ? 'Your own courses and organisation courses you are engaged on.' : 'No courses yet: paste ids below.'}>
          {(p) => (
            <Select {...p} value={course ? values.courseId : '__manual'} onChange={(e) => { set('courseId', e.target.value === '__manual' ? '' : e.target.value); set('offeringId', ''); }}>
              <option value="__manual">Enter ids manually…</option>
              {courses.map((c) => <option key={c.id} value={c.id}>{c.title}{c.engaged ? ` (${c.organisationName || 'organisation'})` : c.status !== 'published' ? ` (${c.status})` : ''}</option>)}
            </Select>
          )}
        </Field>
        {manualCourse ? (
          <FormGrid>
            <Field label="Course id" required>{(p) => <Input {...p} {...bind('courseId')} className="font-mono text-sm" />}</Field>
            <Field label="Offering id" hint="A cohort-format offering." required>{(p) => <Input {...p} {...bind('offeringId')} className="font-mono text-sm" />}</Field>
          </FormGrid>
        ) : (
          <Field label="Cohort offering" required hint={detail.isPending ? 'Loading offerings…' : cohortOfferings.length ? undefined : detail.isError ? 'Offerings are readable once the course is published; enter the offering id manually until then.' : 'This course has no cohort-format offering; add one in the builder.'}>
            {(p) => (
              <Select {...p} {...bind('offeringId')}>
                <option value="">Select…</option>
                {cohortOfferings.map((o) => <option key={o.offeringId} value={o.offeringId}>Cohort · {o.capacity ? `${o.capacity} seats` : 'open'}{o.startAt ? ` · from ${formatDate(o.startAt)}` : ''}</option>)}
              </Select>
            )}
          </Field>
        )}
        {course && !cohortOfferings.length && !detail.isPending ? <Field label="Offering id" required>{(p) => <Input {...p} {...bind('offeringId')} className="font-mono text-sm" placeholder="Paste the cohort offering id" />}</Field> : null}
        {course?.engaged ? <Alert tone="info">You will be named as the teaching tutor on this cohort ({course.organisationName || 'organisation'} course).</Alert> : null}
        <Field label="Cohort name" required>{(p) => <Input {...p} {...bind('name')} placeholder="March 2027 evening cohort" />}</Field>
        <FormGrid cols={3}>
          <Field label="Starts" required>{(p) => <Input {...p} type="datetime-local" {...bind('startsAt')} />}</Field>
          <Field label="Ends" required>{(p) => <Input {...p} type="datetime-local" {...bind('endsAt')} />}</Field>
          <Field label="Capacity" required>{(p) => <Input {...p} type="number" min={1} {...bind('capacity', 'number')} />}</Field>
        </FormGrid>
        <Field label="Sponsored programme id (optional)" hint="Links the cohort to a sponsored programme so seats and attendance report to the sponsor.">{(p) => <Input {...p} {...bind('programmeId')} className="font-mono text-sm" />}</Field>
      </div>
    </Modal>
  );
}
