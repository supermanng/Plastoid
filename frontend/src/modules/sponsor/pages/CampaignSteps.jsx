import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Plus, Trash2 } from 'lucide-react';
import { Alert, Button, Checkbox, Chip, Field, FormGrid, Input, Loading, Select, Textarea } from '../../../shared/components/ui';
import { formatMoney, koboOf, nairaToKobo } from '../../../shared/lib/format';
import { useDebounce } from '../../../shared/lib/hooks';
import { sponsorApi, sponsorKeys } from '../api/sponsor';
import { QuotePreview } from './QuotePanel';

/* Step forms for the campaign builder. Each receives `draft` (local edits), `set(key, value)` and the saved `programme`. */

export const STEPS = ['Objectives', 'Course & offering', 'Seats & budget', 'Eligibility', 'Dates', 'Selection', 'Review & quote'];

export function ObjectivesStep({ draft, set, error }) {
  return (
    <div className="space-y-4">
      <Field label="Campaign name" required error={error?.field?.('step.name') || error?.field?.('name')} hint="Shown to learners on the opportunity page.">
        {(p) => <Input {...p} value={draft.name || ''} onChange={(e) => set('name', e.target.value)} placeholder="Lagos Data Analysts Cohort 2026" maxLength={200} />}
      </Field>
      <Field label="Objectives" hint="What should beneficiaries be able to do afterwards? Up to 2,000 characters." error={error?.field?.('step.objectives')}>
        {(p) => <Textarea {...p} value={draft.objectives || ''} onChange={(e) => set('objectives', e.target.value)} rows={5} maxLength={2000} placeholder="Train 150 young graduates in data analysis and place 60% in jobs within six months." />}
      </Field>
    </div>
  );
}

export function CourseStep({ draft, set }) {
  const [q, setQ] = useState('');
  // For a saved draft only courseId/courseTitle are known: search by title to recover the slug and offerings.
  const term = useDebounce(q || (!draft.courseSlug && draft.courseTitle) || '', 300);
  const search = useQuery({ queryKey: sponsorKeys.courses({ q: term }), queryFn: () => sponsorApi.searchCourses({ q: term || undefined }) });
  const selectedCard = search.data?.items?.find((c) => c.courseId === draft.courseId);
  const detail = useQuery({
    queryKey: sponsorKeys.course(selectedCard?.slug || draft.courseSlug),
    queryFn: () => sponsorApi.getCourse(selectedCard?.slug || draft.courseSlug),
    enabled: Boolean(selectedCard?.slug || draft.courseSlug),
  });
  const offerings = (detail.data?.offerings || []).filter((o) => (o.fundingTypes || []).map((f) => String(f).toLowerCase()).includes('sponsored'));

  return (
    <div className="space-y-4">
      <Field label="Find a course" hint="Only published courses with an offering that accepts sponsorship are listed.">
        {(p) => <Input {...p} value={q} onChange={(e) => setQ(e.target.value)} placeholder="Search by title or skill" />}
      </Field>
      {search.isPending ? <Loading /> : null}
      {search.isError ? <Alert tone="danger">Could not load courses.</Alert> : null}
      {search.data ? (
        search.data.items.length ? (
          <ul className="grid gap-2 sm:grid-cols-2">
            {search.data.items.map((c) => {
              const active = c.courseId === draft.courseId;
              return (
                <li key={c.courseId}>
                  <button
                    type="button"
                    onClick={() => { set('courseId', c.courseId); set('courseSlug', c.slug); set('courseTitle', c.title); set('offeringId', null); }}
                    aria-pressed={active}
                    className={`w-full rounded-card border p-3 text-left text-sm transition-colors ${active ? 'border-royal bg-royal-50' : 'border-line bg-white hover:bg-surface'}`}
                  >
                    <div className="font-semibold text-navy">{c.title}</div>
                    <div className="mt-0.5 line-clamp-2 text-xs text-muted">{c.summary}</div>
                    <div className="mt-1 text-xs text-muted">{c.level} · {c.estimatedMinutes ? `${Math.round(c.estimatedMinutes / 60)} h` : ''} {c.fromPrice ? `· from ${formatMoney(c.fromPrice)}` : ''}</div>
                  </button>
                </li>
              );
            })}
          </ul>
        ) : (
          <Alert tone="info">No sponsorable courses match. Tutors enable sponsorship per offering; ask Plastoids to source a course for your brief.</Alert>
        )
      ) : null}

      {draft.courseId && !selectedCard && draft.courseTitle ? <Alert tone="info">Selected: <span className="font-semibold">{draft.courseTitle}</span>. Search again to change it.</Alert> : null}

      {draft.courseId ? (
        <Field label="Offering" required hint="The format and price the quote is based on. Tuition per seat defaults to the offering price.">
          {(p) => (
            detail.isPending && (selectedCard?.slug || draft.courseSlug) ? <Loading /> : offerings.length ? (
              <div className="grid gap-2 sm:grid-cols-2" id={p.id}>
                {offerings.map((o) => {
                  const active = o.offeringId === draft.offeringId;
                  return (
                    <button
                      key={o.offeringId}
                      type="button"
                      aria-pressed={active}
                      onClick={() => { set('offeringId', o.offeringId); set('tuitionPerSeatKobo', koboOf(o.price)); set('durationWeeks', o.durationWeeks); }}
                      className={`rounded-card border p-3 text-left text-sm ${active ? 'border-royal bg-royal-50' : 'border-line bg-white hover:bg-surface'}`}
                    >
                      <div className="font-semibold capitalize text-navy">{String(o.format).replace(/_/g, ' ')}</div>
                      <div className="text-xs text-muted">{formatMoney(o.price)} per seat{o.durationWeeks ? ` · ${o.durationWeeks} weeks` : ''}{o.capacity ? ` · capacity ${o.capacity}` : ''}</div>
                    </button>
                  );
                })}
              </div>
            ) : (
              <Alert tone="warn">This course has no offering that accepts sponsorship{detail.isError ? ' (or the course could not be loaded)' : ''}.</Alert>
            )
          )}
        </Field>
      ) : null}
    </div>
  );
}

export function SeatsBudgetStep({ draft, set, error }) {
  const months = draft.startsAt && draft.endsAt ? Math.max(1, Math.ceil((new Date(draft.endsAt) - new Date(draft.startsAt)) / (30 * 86400000))) : null;
  return (
    <div className="grid gap-6 lg:grid-cols-[1fr_1fr]">
      <div className="space-y-4">
        <FormGrid>
          <Field label="Seats" required hint="1 to 100,000." error={error?.field?.('step.seats')}>
            {(p) => <Input {...p} type="number" min={1} max={100000} inputMode="numeric" value={draft.seats ?? ''} onChange={(e) => set('seats', e.target.value === '' ? '' : Number(e.target.value))} />}
          </Field>
          <Field label="Sponsor share %" required hint="100% = fully sponsored; less means learners co-pay." error={error?.field?.('step.sponsorSharePercent')}>
            {(p) => <Input {...p} type="number" min={1} max={100} inputMode="numeric" value={draft.sponsorSharePercent ?? ''} onChange={(e) => set('sponsorSharePercent', e.target.value === '' ? '' : Number(e.target.value))} />}
          </Field>
        </FormGrid>
        <FormGrid>
          <Field label="Tuition per seat (₦)" required hint="Defaults to the offering price." error={error?.field?.('step.tuitionPerSeat')}>
            {(p) => <NairaInput {...p} kobo={draft.tuitionPerSeatKobo} onKobo={(k) => set('tuitionPerSeatKobo', k)} />}
          </Field>
          <Field label="Budget (₦, ex VAT)" required hint="The quote before VAT must fit inside this." error={error?.field?.('step.budget')}>
            {(p) => <NairaInput {...p} kobo={draft.budgetKobo} onKobo={(k) => set('budgetKobo', k)} />}
          </Field>
        </FormGrid>
        <Field label="Unused funds">
          {(p) => (
            <Select {...p} value={draft.unusedFundsPolicy || 'refund'} onChange={(e) => set('unusedFundsPolicy', e.target.value)}>
              <option value="refund">Refund unfilled seats when the programme closes</option>
              <option value="rollover">Roll unused funds into the next programme</option>
            </Select>
          )}
        </Field>
      </div>
      <QuotePreview seats={draft.seats} tuitionPerSeatKobo={draft.tuitionPerSeatKobo} sponsorSharePercent={draft.sponsorSharePercent} budgetKobo={draft.budgetKobo || 0} durationMonths={months} />
    </div>
  );
}

export function NairaInput({ kobo, onKobo, ...rest }) {
  const [text, setText] = useState(kobo ? String(kobo / 100) : '');
  const [last, setLast] = useState(kobo);
  if (kobo !== last) {
    setLast(kobo);
    setText(kobo ? String(kobo / 100) : '');
  }
  return (
    <div className="relative">
      <span className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-sm text-muted">₦</span>
      <Input {...rest} className="pl-7" inputMode="decimal" value={text} onChange={(e) => { setText(e.target.value); const k = nairaToKobo(e.target.value); setLast(k); onKobo(k); }} />
    </div>
  );
}

const RULE_KEYS = ['minAge', 'maxAge', 'residency', 'states', 'education', 'employmentStatus', 'notPreviouslySponsored'];
const PROTECTED = ['gender', 'sex', 'religion', 'ethnicity', 'ethnicGroup', 'tribe', 'disability', 'maritalStatus', 'politicalOpinion', 'politicalAffiliation', 'sexualOrientation', 'pregnancy', 'hivStatus', 'placeOfOrigin'];
const EDUCATION = ['none', 'primary', 'secondary', 'ond', 'hnd', 'bsc', 'msc', 'phd'];
const EMPLOYMENT = ['student', 'unemployed', 'employed', 'self_employed'];
const NG_STATES = ['Abia', 'Adamawa', 'Akwa Ibom', 'Anambra', 'Bauchi', 'Bayelsa', 'Benue', 'Borno', 'Cross River', 'Delta', 'Ebonyi', 'Edo', 'Ekiti', 'Enugu', 'FCT', 'Gombe', 'Imo', 'Jigawa', 'Kaduna', 'Kano', 'Katsina', 'Kebbi', 'Kogi', 'Kwara', 'Lagos', 'Nasarawa', 'Niger', 'Ogun', 'Ondo', 'Osun', 'Oyo', 'Plateau', 'Rivers', 'Sokoto', 'Taraba', 'Yobe', 'Zamfara'];

export function EligibilityStep({ draft, set, error }) {
  const rules = draft.eligibilityRules || {};
  const update = (key, value) => {
    const next = { ...rules };
    if (value === undefined || value === '' || value === null || (Array.isArray(value) && value.length === 0) || value === false) delete next[key];
    else next[key] = value;
    set('eligibilityRules', next);
  };
  const toggle = (key, item) => {
    const list = rules[key] || [];
    update(key, list.includes(item) ? list.filter((x) => x !== item) : [...list, item]);
  };
  const unknown = Object.keys(rules).filter((k) => !RULE_KEYS.includes(k));

  return (
    <div className="space-y-5">
      <Alert tone="warn" title="Lawful, non-discriminatory rules only">
        Plastoids reviews every programme. Rules on {PROTECTED.slice(0, 5).join(', ')} and other protected characteristics are refused. Allowed: age range, residency, state, education, employment status, and "not previously sponsored".
      </Alert>
      {unknown.length ? <Alert tone="danger">Unknown rules will be rejected: {unknown.join(', ')}</Alert> : null}
      <FormGrid>
        <Field label="Minimum age" hint="Learners must be 18+ anyway." error={error?.field?.('step.eligibilityRules')}>
          {(p) => <Input {...p} type="number" min={18} max={99} value={rules.minAge ?? ''} onChange={(e) => update('minAge', e.target.value === '' ? undefined : Number(e.target.value))} />}
        </Field>
        <Field label="Maximum age">
          {(p) => <Input {...p} type="number" min={18} max={99} value={rules.maxAge ?? ''} onChange={(e) => update('maxAge', e.target.value === '' ? undefined : Number(e.target.value))} />}
        </Field>
      </FormGrid>
      <Field label="Residency (country codes)" hint="Comma-separated ISO codes, e.g. NG">
        {(p) => <Input {...p} value={(rules.residency || []).join(', ')} onChange={(e) => update('residency', e.target.value.split(',').map((s) => s.trim().toUpperCase()).filter(Boolean))} placeholder="NG" />}
      </Field>
      <Field label="States" hint="Leave empty for all states.">
        {() => (
          <div className="flex flex-wrap gap-1.5">
            {NG_STATES.map((s) => <Chip key={s} active={(rules.states || []).includes(s)} onClick={() => toggle('states', s)}>{s}</Chip>)}
          </div>
        )}
      </Field>
      <FormGrid>
        <Field label="Education level" hint="Any of the selected.">
          {() => <div className="flex flex-wrap gap-1.5">{EDUCATION.map((s) => <Chip key={s} active={(rules.education || []).includes(s)} onClick={() => toggle('education', s)}>{s.toUpperCase()}</Chip>)}</div>}
        </Field>
        <Field label="Employment status">
          {() => <div className="flex flex-wrap gap-1.5">{EMPLOYMENT.map((s) => <Chip key={s} active={(rules.employmentStatus || []).includes(s)} onClick={() => toggle('employmentStatus', s)}>{s.replace('_', ' ')}</Chip>)}</div>}
        </Field>
      </FormGrid>
      <Checkbox label="Only learners who have not been sponsored on Plastoids before" checked={Boolean(rules.notPreviouslySponsored)} onChange={(e) => update('notPreviouslySponsored', e.target.checked)} />

      <EntryAssessmentEditor draft={draft} set={set} />
    </div>
  );
}

function EntryAssessmentEditor({ draft, set }) {
  const enabled = Boolean(draft.entryAssessment);
  const ea = draft.entryAssessment || { timeLimitMinutes: 30, passMarkPercent: 50, questions: [] };
  const setEa = (next) => set('entryAssessment', next);
  const setQ = (i, patch) => setEa({ ...ea, questions: ea.questions.map((q, j) => (j === i ? { ...q, ...patch } : q)) });
  const rubric = draft.scoringRubric || null;

  return (
    <div className="space-y-4 border-t border-line pt-4">
      <Checkbox
        label="Add a timed entry assessment (multiple choice; graded automatically)"
        checked={enabled}
        onChange={(e) => (e.target.checked ? setEa({ timeLimitMinutes: 30, passMarkPercent: 50, questions: [{ prompt: '', options: ['', ''], correctIndex: 0 }] }) : set('entryAssessment', null))}
      />
      {enabled ? (
        <div className="space-y-4 rounded-card border border-line p-4">
          <FormGrid>
            <Field label="Time limit (minutes)">
              {(p) => <Input {...p} type="number" min={5} max={600} value={ea.timeLimitMinutes} onChange={(e) => setEa({ ...ea, timeLimitMinutes: Number(e.target.value) })} />}
            </Field>
            <Field label="Pass mark %">
              {(p) => <Input {...p} type="number" min={0} max={100} value={ea.passMarkPercent} onChange={(e) => setEa({ ...ea, passMarkPercent: Number(e.target.value) })} />}
            </Field>
          </FormGrid>
          {ea.questions.map((q, i) => (
            <div key={i} className="space-y-2 rounded-card bg-surface p-3">
              <div className="flex items-start gap-2">
                <Field label={`Question ${i + 1}`} className="flex-1">
                  {(p) => <Input {...p} value={q.prompt} onChange={(e) => setQ(i, { prompt: e.target.value })} placeholder="What does a pivot table do?" />}
                </Field>
                <Button variant="ghost" size="sm" icon={Trash2} aria-label="Remove question" className="mt-6" onClick={() => setEa({ ...ea, questions: ea.questions.filter((_, j) => j !== i) })} />
              </div>
              <div className="grid gap-2 sm:grid-cols-2">
                {q.options.map((opt, k) => (
                  <label key={k} className="flex items-center gap-2 text-sm">
                    <input type="radio" name={`correct-${i}`} checked={q.correctIndex === k} onChange={() => setQ(i, { correctIndex: k })} aria-label={`Option ${k + 1} is correct`} className="accent-royal" />
                    <Input value={opt} onChange={(e) => setQ(i, { options: q.options.map((o, m) => (m === k ? e.target.value : o)) })} placeholder={`Option ${k + 1}`} />
                  </label>
                ))}
              </div>
              <div className="flex gap-2">
                <Button variant="ghost" size="sm" icon={Plus} onClick={() => setQ(i, { options: [...q.options, ''] })} disabled={q.options.length >= 6}>Option</Button>
              </div>
            </div>
          ))}
          <Button variant="secondary" size="sm" icon={Plus} onClick={() => setEa({ ...ea, questions: [...ea.questions, { prompt: '', options: ['', ''], correctIndex: 0 }] })}>Add question</Button>
          <p className="text-xs text-muted">The marked option is the correct answer; it is never sent to applicants.</p>
        </div>
      ) : null}

      <Checkbox
        label="Add a reviewer scoring rubric (weighted criteria scored by Plastoids reviewers)"
        checked={Boolean(rubric)}
        onChange={(e) => set('scoringRubric', e.target.checked ? { assessmentWeight: 50, criteria: [{ key: 'motivation', weight: 1, max: 5 }] } : null)}
      />
      {rubric ? (
        <div className="space-y-3 rounded-card border border-line p-4">
          <Field label="Assessment weight %" hint="Share of the total score from the entry assessment; the rest comes from the rubric.">
            {(p) => <Input {...p} type="number" min={0} max={100} value={rubric.assessmentWeight} onChange={(e) => set('scoringRubric', { ...rubric, assessmentWeight: Number(e.target.value) })} />}
          </Field>
          {rubric.criteria.map((c, i) => (
            <div key={i} className="grid grid-cols-[1fr_80px_80px_auto] items-end gap-2">
              <Field label={i === 0 ? 'Criterion key' : undefined}>{(p) => <Input {...p} value={c.key} onChange={(e) => set('scoringRubric', { ...rubric, criteria: rubric.criteria.map((x, j) => (j === i ? { ...x, key: e.target.value.replace(/\s+/g, '_') } : x)) })} placeholder="motivation" />}</Field>
              <Field label={i === 0 ? 'Weight' : undefined}>{(p) => <Input {...p} type="number" min={0} value={c.weight} onChange={(e) => set('scoringRubric', { ...rubric, criteria: rubric.criteria.map((x, j) => (j === i ? { ...x, weight: Number(e.target.value) } : x)) })} />}</Field>
              <Field label={i === 0 ? 'Max' : undefined}>{(p) => <Input {...p} type="number" min={1} value={c.max} onChange={(e) => set('scoringRubric', { ...rubric, criteria: rubric.criteria.map((x, j) => (j === i ? { ...x, max: Number(e.target.value) } : x)) })} />}</Field>
              <Button variant="ghost" size="sm" icon={Trash2} aria-label="Remove criterion" onClick={() => set('scoringRubric', { ...rubric, criteria: rubric.criteria.filter((_, j) => j !== i) })} />
            </div>
          ))}
          {rubric.criteria.some((c) => PROTECTED.includes(c.key)) ? <Alert tone="danger">A criterion uses a protected characteristic and will be rejected.</Alert> : null}
          <Button variant="secondary" size="sm" icon={Plus} onClick={() => set('scoringRubric', { ...rubric, criteria: [...rubric.criteria, { key: '', weight: 1, max: 5 }] })}>Add criterion</Button>
        </div>
      ) : null}
    </div>
  );
}

export function DatesStep({ draft, set, error }) {
  const toLocal = (v) => (v ? new Date(v).toISOString().slice(0, 10) : '');
  const fromLocal = (v, endOfDay = false) => (v ? new Date(`${v}T${endOfDay ? '23:59:59' : '09:00:00'}Z`).toISOString() : null);
  return (
    <div className="space-y-4">
      <FormGrid>
        <Field label="Applications open" hint="Defaults to when you publish." error={error?.field?.('step.applicationsOpenAt')}>
          {(p) => <Input {...p} type="date" value={toLocal(draft.applicationsOpenAt)} onChange={(e) => set('applicationsOpenAt', fromLocal(e.target.value))} />}
        </Field>
        <Field label="Applications close" required hint="Must be before the programme starts and in the future at publication." error={error?.field?.('step.applicationsCloseAt')}>
          {(p) => <Input {...p} type="date" value={toLocal(draft.applicationsCloseAt)} onChange={(e) => set('applicationsCloseAt', fromLocal(e.target.value, true))} />}
        </Field>
      </FormGrid>
      <FormGrid>
        <Field label="Programme starts" required error={error?.field?.('step.startsAt')}>
          {(p) => <Input {...p} type="date" value={toLocal(draft.startsAt)} onChange={(e) => set('startsAt', fromLocal(e.target.value))} />}
        </Field>
        <Field label="Programme ends" required hint="Fees and tutor earnings are posted per month between these dates." error={error?.field?.('step.endsAt') || error?.field?.('step')}>
          {(p) => <Input {...p} type="date" value={toLocal(draft.endsAt)} onChange={(e) => set('endsAt', fromLocal(e.target.value, true))} />}
        </Field>
      </FormGrid>
      {draft.startsAt && draft.endsAt && new Date(draft.endsAt) <= new Date(draft.startsAt) ? <Alert tone="danger">The end date must be after the start date.</Alert> : null}
      {draft.applicationsCloseAt && draft.startsAt && new Date(draft.applicationsCloseAt) >= new Date(draft.startsAt) ? <Alert tone="danger">Applications must close before the programme starts.</Alert> : null}
    </div>
  );
}

export function SelectionStep({ draft, set }) {
  const modes = [
    ['plastoids', 'Plastoids selects', 'Plastoids programme managers run screening (rules, assessment, review) and send offers.'],
    ['sponsor', 'We review the shortlist', 'Plastoids screens and scores; an organisation owner signs off the accepted shortlist before offers go out.'],
    ['delegated', 'Delegated to Plastoids', 'Same as "Plastoids selects" but recorded as a delegation from your organisation for the audit trail.'],
  ];
  return (
    <div className="space-y-3">
      {modes.map(([value, title, text]) => {
        const active = (draft.selectionMode || 'plastoids') === value;
        return (
          <button key={value} type="button" aria-pressed={active} onClick={() => set('selectionMode', value)} className={`w-full rounded-card border p-4 text-left ${active ? 'border-royal bg-royal-50' : 'border-line bg-white hover:bg-surface'}`}>
            <div className="font-semibold text-navy">{title}</div>
            <div className="mt-0.5 text-sm text-muted">{text}</div>
          </button>
        );
      })}
    </div>
  );
}
