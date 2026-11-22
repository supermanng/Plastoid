import { useMemo, useState } from 'react';
import { useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { BadgeCheck, Handshake, Search, UserCheck } from 'lucide-react';
import { Alert, Avatar, Breadcrumb, Button, Card, CardHeader, EmptyState, ErrorAlert, Field, FormGrid, Input, Modal, PageHeader, QueryState, Select, StatusPill, Table, Textarea } from '../../../shared/components/ui';
import { useAction, useDebounce, useForm } from '../../../shared/lib/hooks';
import { formatDate, formatMoney, nairaToKobo, titleCase } from '../../../shared/lib/format';
import { UnitPrice } from '../../../shared/lib/units.jsx';
import { AppLink } from '../../../shared/lib/navigation';
import { listTutors, queryKeys } from '../../../shared/api/public';
import { COURSE_PROVIDER_COMMISSION_BPS, TERM_KINDS, describeTerms, providerApi, providerKeys, splitSale } from '../api/provider';
import { useOrganisation } from './useOrganisation';
import { NoOrganisation } from './OrganisationSwitcher';

/*
 * /sponsor/courses/:courseId/tutors — engagements on one organisation course (ADR 0019).
 *   GET  /catalogue/organisations/{orgId}/courses/{courseId}/engagements  → EngagementView[]
 *   POST /catalogue/organisations/{orgId}/courses/{courseId}/engagements  { tutorId, terms { kind, shareBps? | fixedFee? }, note? }
 *   POST /catalogue/engagements/{id}/end                                   { reason? }
 * Terms: revenue_share = basis points of the sale net of platform commission; per_enrolment = a fixed fee per paid
 * enrolment, capped at that net. One open-or-active engagement per course and tutor.
 */
export default function CourseTutors() {
  const { courseId } = useParams();
  const org = useOrganisation();
  const orgId = org.organisationId;
  const courses = useQuery({ queryKey: providerKeys.courses(orgId), queryFn: () => providerApi.listCourses(orgId), enabled: Boolean(orgId) });
  const engagements = useQuery({ queryKey: providerKeys.engagements(orgId, courseId), queryFn: () => providerApi.listEngagements(orgId, courseId), enabled: Boolean(orgId) });
  const [ending, setEnding] = useState(null);

  if (!org.organisation) {
    return (
      <div>
        <PageHeader title="Course tutors" />
        <NoOrganisation />
      </div>
    );
  }

  const course = (courses.data || []).find((c) => c.courseId === courseId);
  const title = course?.title || 'Course';
  const openOrActive = (engagements.data || []).filter((e) => ['offered', 'active'].includes(String(e.status).toLowerCase())).map((e) => e.tutorId);

  return (
    <div className="space-y-6">
      <Breadcrumb items={[{ to: '/courses', label: 'Courses' }, { to: `/courses/${courseId}`, label: title }, { label: 'Tutors' }]} />
      <PageHeader
        className="mb-0"
        title={`Tutors · ${title}`}
        description="Verified tutors you engage to teach this course. A tutor accepts or declines the offer; you can end an engagement at any time."
        actions={<Button to={`/courses/${courseId}`} variant="secondary">Back to course</Button>}
      />

      <Card>
        <CardHeader title="Engagements" description="Offered, active and past engagements on this course." />
        <QueryState query={engagements} empty={<EmptyState icon={Handshake} title="No tutors engaged yet" description="Search for a verified tutor below and offer them terms." />}>
          {(rows) => (
            <Table
              rows={rows}
              rowKey={(e) => e.engagementId}
              columns={[
                { key: 'tutor', header: 'Tutor', render: (e) => <AppLink to={`/tutors/${e.tutorId}`} className="font-semibold text-royal hover:underline">Tutor {String(e.tutorId).slice(0, 8)}</AppLink> },
                { key: 'terms', header: 'Terms', render: (e) => describeTerms(e.terms) },
                { key: 'status', header: 'Status', render: (e) => <StatusPill status={e.status} /> },
                { key: 'offeredAt', header: 'Offered', render: (e) => formatDate(e.offeredAt) },
                { key: 'respondedAt', header: 'Responded', render: (e) => (e.respondedAt ? formatDate(e.respondedAt) : e.endedAt ? `ended ${formatDate(e.endedAt)}` : '—') },
                { key: 'note', header: 'Note', render: (e) => e.note || '—' },
                {
                  key: 'actions',
                  header: '',
                  align: 'right',
                  render: (e) => (['offered', 'active'].includes(String(e.status).toLowerCase()) && org.isAuthor ? <Button size="sm" variant="ghost" onClick={() => setEnding(e)}>End</Button> : null),
                },
              ]}
            />
          )}
        </QueryState>
      </Card>

      {org.isAuthor ? <EngageTutor orgId={orgId} courseId={courseId} alreadyEngaged={openOrActive} /> : <Alert tone="info">Only an organisation owner or author can engage or end tutors. Your role: <span className="font-semibold capitalize">{org.role || 'member'}</span>.</Alert>}

      <EndDialog engagement={ending} orgId={orgId} courseId={courseId} onClose={() => setEnding(null)} />
    </div>
  );
}

function EndDialog({ engagement, orgId, courseId, onClose }) {
  const [reason, setReason] = useState('');
  const end = useAction(() => providerApi.endEngagement(engagement.engagementId, reason.trim()), {
    success: 'Engagement ended.',
    invalidate: [providerKeys.engagements(orgId, courseId), providerKeys.courses(orgId)],
    onSuccess: () => { setReason(''); onClose(); },
  });
  if (!engagement) return null;
  const offered = String(engagement.status).toLowerCase() === 'offered';
  return (
    <Modal
      open
      onClose={onClose}
      size="sm"
      title={offered ? 'Withdraw this offer?' : 'End this engagement?'}
      description={offered ? 'The tutor will no longer be able to accept it.' : 'The tutor keeps earnings already made. Offerings that name this tutor need a new teaching tutor before new cohorts can open.'}
      footer={<><Button variant="secondary" onClick={onClose}>Cancel</Button><Button variant="danger" loading={end.isPending} onClick={() => end.mutate()}>{offered ? 'Withdraw' : 'End engagement'}</Button></>}
    >
      <ErrorAlert error={end.error} className="mb-3" />
      <Field label="Reason (optional)" hint="Shared with the tutor.">
        {(p) => <Textarea {...p} rows={3} value={reason} onChange={(e) => setReason(e.target.value)} />}
      </Field>
    </Modal>
  );
}

/* ---------------------------------------------------------------- Engage a tutor */

function EngageTutor({ orgId, courseId, alreadyEngaged }) {
  const [q, setQ] = useState('');
  const [skill, setSkill] = useState('');
  const [chosen, setChosen] = useState(null);
  const debouncedQ = useDebounce(q, 350);
  const debouncedSkill = useDebounce(skill, 350);
  const params = useMemo(() => ({ q: debouncedQ, skill: debouncedSkill, limit: 9 }), [debouncedQ, debouncedSkill]);
  const search = useQuery({ queryKey: queryKeys.tutors(params), queryFn: () => listTutors(params), placeholderData: (prev) => prev });

  const { values, bind, set, reset } = useForm({ kind: 'revenue_share', sharePercent: '30', feeNaira: '', note: '', samplePriceNaira: '50000' });
  const terms = values.kind === 'revenue_share'
    ? { kind: 'revenue_share', shareBps: Math.round((Number(values.sharePercent) || 0) * 100) }
    : { kind: 'per_enrolment', fixedFee: { amount: nairaToKobo(values.feeNaira), currency: 'NGN' } };
  const termsValid = values.kind === 'revenue_share' ? terms.shareBps > 0 && terms.shareBps <= 10_000 : terms.fixedFee.amount > 0;

  const offer = useAction(() => providerApi.offerEngagement(orgId, courseId, { tutorId: chosen.tutorId, terms, note: values.note.trim() || null }), {
    success: 'Offer sent. The tutor is notified and can accept or decline.',
    invalidate: [providerKeys.engagements(orgId, courseId), providerKeys.courses(orgId)],
    onSuccess: () => { setChosen(null); reset(); },
  });

  const sample = splitSale(nairaToKobo(values.samplePriceNaira), terms);
  const items = search.data?.items || [];

  return (
    <Card>
      <CardHeader title="Engage a tutor" description="Search verified tutors, pick one, set the terms and send the offer." />
      <div className="grid gap-6 lg:grid-cols-[1.2fr_1fr]">
        <div>
          <FormGrid>
            <Field label="Search">{(p) => (
              <div className="relative"><Search className="pointer-events-none absolute left-3 top-1/2 h-5 w-5 -translate-y-1/2 text-muted" aria-hidden /><Input {...p} className="pl-10" placeholder="Headline or keyword" value={q} onChange={(e) => setQ(e.target.value)} /></div>
            )}</Field>
            <Field label="Subject / skill">{(p) => <Input {...p} placeholder="e.g. Data analysis" value={skill} onChange={(e) => setSkill(e.target.value)} />}</Field>
          </FormGrid>
          <ErrorAlert error={search.error} className="mt-3" />
          {search.isPending ? <p className="mt-3 text-sm text-muted">Searching…</p> : null}
          {search.data && !items.length ? <p className="mt-3 text-sm text-muted">No verified tutors match. Try a broader subject.</p> : null}
          <ul className="mt-3 space-y-2">
            {items.map((t) => {
              const engaged = alreadyEngaged.includes(t.tutorId);
              const selected = chosen?.tutorId === t.tutorId;
              return (
                <li key={t.tutorId} className={`flex flex-wrap items-center justify-between gap-3 rounded-card border p-3 ${selected ? 'border-royal bg-royal-50/40' : 'border-line'}`}>
                  <div className="flex min-w-0 items-center gap-3">
                    <Avatar name={t.headline} size={40} />
                    <div className="min-w-0">
                      <div className="flex items-center gap-1 text-sm font-bold text-navy"><span className="truncate">{t.headline}</span><BadgeCheck className="h-4 w-4 shrink-0 text-teal" aria-label="Verified tutor" /></div>
                      <div className="text-xs text-muted">{titleCase(t.level)} · {(t.subjects || []).slice(0, 3).join(', ')}{t.ratingCount ? ` · ${Number(t.ratingAverage).toFixed(1)} ★ (${t.ratingCount})` : ''}</div>
                      <div className="text-xs text-muted"><UnitPrice money={t.hourlyRate} size="sm" inline /> /hour</div>
                    </div>
                  </div>
                  <div className="flex items-center gap-2">
                    <AppLink to={`/tutors/${t.tutorId}`} className="text-xs font-semibold text-royal hover:underline">Profile</AppLink>
                    {engaged ? <StatusPill tone="info" status="engaged" label="Already engaged" /> : <Button size="sm" variant={selected ? 'primary' : 'secondary'} icon={UserCheck} onClick={() => setChosen(t)}>{selected ? 'Chosen' : 'Choose'}</Button>}
                  </div>
                </li>
              );
            })}
          </ul>
        </div>

        <form className="space-y-4" onSubmit={(e) => { e.preventDefault(); if (chosen && termsValid) offer.mutate(); }}>
          <ErrorAlert error={offer.error} />
          {chosen ? (
            <Alert tone="info" title={chosen.headline}>Offer goes to this tutor. <button type="button" className="font-semibold text-royal" onClick={() => setChosen(null)}>Change</button></Alert>
          ) : (
            <Alert tone="info">Choose a tutor from the search results first.</Alert>
          )}
          <Field label="Terms" required>{(p) => <Select {...p} {...bind('kind')}>{TERM_KINDS.map(([v, l]) => <option key={v} value={v}>{l}</option>)}</Select>}</Field>
          {values.kind === 'revenue_share' ? (
            <Field label="Tutor's share (%)" required hint="Of each sale after platform commission. Stored as basis points.">{(p) => <Input {...p} type="number" inputMode="decimal" min={0} max={100} step="any" {...bind('sharePercent')} />}</Field>
          ) : (
            <Field label="Fee per paid enrolment (₦)" required hint="Capped at the sale net of commission.">{(p) => <Input {...p} inputMode="numeric" {...bind('feeNaira')} />}</Field>
          )}
          <Field label="Note to the tutor (optional)">{(p) => <Textarea {...p} rows={2} maxLength={500} {...bind('note')} placeholder="Evening cohorts, two live classes a week." />}</Field>

          <div className="rounded-card border border-line bg-surface/60 p-3 text-sm">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <span className="font-semibold text-navy">Example split</span>
              <label className="flex items-center gap-2 text-xs text-muted">Sale price ₦<Input inputMode="numeric" className="min-h-9 w-28" value={values.samplePriceNaira} onChange={(e) => set('samplePriceNaira', e.target.value)} aria-label="Example sale price in naira" /></label>
            </div>
            <dl className="mt-2 space-y-1">
              <SplitRow label="Learner pays" value={<UnitPrice money={{ amount: sample.price, currency: 'NGN' }} size="sm" inline />} />
              <SplitRow label={`Platform commission (${COURSE_PROVIDER_COMMISSION_BPS / 100}%)`} value={`− ${formatMoney(sample.commission)}`} />
              <SplitRow label="Tutor's share" value={formatMoney(sample.tutor)} />
              <SplitRow label="Organisation keeps" value={formatMoney(sample.organisation)} strong />
            </dl>
            <p className="mt-2 text-xs text-muted">Commission uses the current `course_provider` fee rule (default {COURSE_PROVIDER_COMMISSION_BPS / 100}%); the exact figure is fixed per sale.</p>
          </div>

          <Button type="submit" icon={Handshake} loading={offer.isPending} disabled={!chosen || !termsValid}>Send offer</Button>
        </form>
      </div>
    </Card>
  );
}

function SplitRow({ label, value, strong }) {
  return (
    <div className="flex items-center justify-between gap-3">
      <dt className="text-muted">{label}</dt>
      <dd className={`tabular ${strong ? 'font-extrabold text-navy' : 'font-medium text-navy'}`}>{value}</dd>
    </div>
  );
}
