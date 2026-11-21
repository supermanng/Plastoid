import { useState } from 'react';
import { Plus, Trash2 } from 'lucide-react';
import { Button, Card, CardHeader, Checkbox, ErrorAlert, Field, FormGrid, Input, Modal, Select, StatusPill, Textarea } from '../../components/ui';
import { useAction, useForm } from '../../lib/hooks';
import { formatDate, formatMoney, nairaToKobo } from '../../lib/format';
import { courseApi } from '../../api/tutor';
import { FUNDING_TYPES, OFFERING_FORMATS, toIso } from './shared';

/* ---------------------------------------------------------------- Lesson (video | doc | assignment | live) */

export function LessonModal({ open, onClose, courseId, moduleId, nextOrder, onCreated }) {
  const { values, bind, set, reset } = useForm({ type: 'video', title: '', durationMinutes: 10, isPreview: false, body: '', url: '', instructions: '', passMarkPercent: 70, maxPoints: 100 });

  const contentJson = () => {
    if (values.type === 'doc') return JSON.stringify({ body: values.body, url: values.url || undefined });
    if (values.type === 'assignment') return JSON.stringify({ instructions: values.instructions, passMarkPercent: Number(values.passMarkPercent) || 70, maxPoints: Number(values.maxPoints) || 100 });
    if (values.type === 'live') return JSON.stringify({ note: values.body || undefined });
    return null;
  };

  const create = useAction(
    () => courseApi.addLesson(courseId, moduleId, { type: values.type, title: values.title.trim(), order: nextOrder, durationMinutes: Number(values.durationMinutes) || 1, isPreview: values.isPreview, contentJson: contentJson() }),
    {
      success: (res) => (res.type === 'video' ? 'Lesson added. Upload its video from the module list.' : 'Lesson added.'),
      onSuccess: (res) => {
        onCreated({ id: res.lessonId, type: res.type, title: values.title.trim(), order: nextOrder, durationMinutes: Number(values.durationMinutes) || 1, isPreview: values.isPreview });
        reset();
        onClose();
      },
    },
  );

  return (
    <Modal
      open={open}
      onClose={onClose}
      title="Add a lesson"
      description="Quizzes have their own button. Live lessons are scheduled per cohort later."
      footer={<><Button variant="secondary" onClick={onClose}>Cancel</Button><Button loading={create.isPending} disabled={!values.title.trim()} onClick={() => create.mutate()}>Add lesson</Button></>}
    >
      <ErrorAlert error={create.error} className="mb-3" />
      <div className="space-y-4">
        <FormGrid>
          <Field label="Type">{(p) => (
            <Select {...p} {...bind('type')}>
              <option value="video">Video</option>
              <option value="doc">Document / reading</option>
              <option value="assignment">Assignment</option>
              <option value="live">Live class placeholder</option>
            </Select>
          )}</Field>
          <Field label="Duration (minutes)">{(p) => <Input {...p} type="number" min={1} max={600} {...bind('durationMinutes', 'number')} />}</Field>
        </FormGrid>
        <Field label="Title" required>{(p) => <Input {...p} {...bind('title')} placeholder="Introduction to pivot tables" />}</Field>
        {values.type === 'doc' ? (
          <>
            <Field label="Reading text" hint="Markdown or plain text shown to the learner.">{(p) => <Textarea {...p} {...bind('body')} rows={5} />}</Field>
            <Field label="External link (optional)">{(p) => <Input {...p} {...bind('url')} type="url" placeholder="https://" />}</Field>
          </>
        ) : null}
        {values.type === 'assignment' ? (
          <>
            <Field label="Instructions" required>{(p) => <Textarea {...p} {...bind('instructions')} rows={4} />}</Field>
            <FormGrid>
              <Field label="Pass mark (%)" hint="Used when you grade submissions (default 70).">{(p) => <Input {...p} type="number" min={1} max={100} {...bind('passMarkPercent', 'number')} />}</Field>
              <Field label="Max points">{(p) => <Input {...p} type="number" min={1} {...bind('maxPoints', 'number')} />}</Field>
            </FormGrid>
          </>
        ) : null}
        {values.type === 'live' ? <Field label="Note for learners (optional)">{(p) => <Input {...p} {...bind('body')} placeholder="Weekly Q&A, Thursdays 6pm" />}</Field> : null}
        <Checkbox label="Free preview (visible before enrolling)" checked={values.isPreview} onChange={(e) => set('isPreview', e.target.checked)} />
      </div>
    </Modal>
  );
}

/* ---------------------------------------------------------------- Quiz */

const emptyQuestion = () => ({ prompt: '', options: ['', ''], correctIndex: 0 });

export function QuizModal({ open, onClose, courseId, moduleId, nextOrder, onCreated }) {
  const [title, setTitle] = useState('');
  const [passMark, setPassMark] = useState(70);
  const [questions, setQuestions] = useState([emptyQuestion()]);

  const create = useAction(
    () => courseApi.addQuiz(courseId, moduleId, { title: title.trim(), order: nextOrder, passMarkPercent: Number(passMark), questions: questions.map((q) => ({ prompt: q.prompt.trim(), options: q.options.map((o) => o.trim()), correctIndex: Number(q.correctIndex) })) }),
    {
      success: (res) => `Quiz added with ${res.questionCount} question${res.questionCount === 1 ? '' : 's'}.`,
      onSuccess: (res) => {
        onCreated({ id: res.lessonId, type: 'quiz', title: title.trim(), order: nextOrder, durationMinutes: questions.length * 2, isPreview: false });
        setTitle('');
        setQuestions([emptyQuestion()]);
        onClose();
      },
    },
  );

  const updateQ = (i, patch) => setQuestions((qs) => qs.map((q, j) => (j === i ? { ...q, ...patch } : q)));
  const valid = title.trim() && questions.length > 0 && questions.every((q) => q.prompt.trim() && q.options.length >= 2 && q.options.every((o) => o.trim()) && q.correctIndex < q.options.length);

  return (
    <Modal
      open={open}
      onClose={onClose}
      size="lg"
      title="Add a quiz"
      description="Multiple choice, one correct answer per question. Learners must reach the pass mark to complete the lesson."
      footer={<><Button variant="secondary" onClick={onClose}>Cancel</Button><Button loading={create.isPending} disabled={!valid} onClick={() => create.mutate()}>Add quiz</Button></>}
    >
      <ErrorAlert error={create.error} className="mb-3" />
      <FormGrid>
        <Field label="Title" required>{(p) => <Input {...p} value={title} onChange={(e) => setTitle(e.target.value)} placeholder="Module 1 check" />}</Field>
        <Field label="Pass mark (%)">{(p) => <Input {...p} type="number" min={1} max={100} value={passMark} onChange={(e) => setPassMark(e.target.value)} />}</Field>
      </FormGrid>
      <ol className="mt-4 space-y-4">
        {questions.map((q, i) => (
          <li key={i} className="rounded-card border border-line p-3">
            <div className="flex items-start gap-2">
              <span className="mt-2 text-sm font-bold text-muted">Q{i + 1}</span>
              <Textarea aria-label={`Question ${i + 1}`} value={q.prompt} onChange={(e) => updateQ(i, { prompt: e.target.value })} rows={2} placeholder="What does a pivot table do?" className="flex-1" />
              <Button variant="ghost" size="sm" aria-label="Remove question" disabled={questions.length === 1} onClick={() => setQuestions((qs) => qs.filter((_, j) => j !== i))}><Trash2 className="h-4 w-4" /></Button>
            </div>
            <ul className="mt-2 space-y-2 pl-7">
              {q.options.map((o, k) => (
                <li key={k} className="flex items-center gap-2">
                  <input type="radio" name={`correct-${i}`} aria-label={`Mark option ${k + 1} correct`} className="h-5 w-5 accent-teal" checked={q.correctIndex === k} onChange={() => updateQ(i, { correctIndex: k })} />
                  <Input aria-label={`Option ${k + 1}`} value={o} onChange={(e) => updateQ(i, { options: q.options.map((x, m) => (m === k ? e.target.value : x)) })} className="min-h-9" />
                  <Button variant="ghost" size="sm" aria-label="Remove option" disabled={q.options.length <= 2} onClick={() => updateQ(i, { options: q.options.filter((_, m) => m !== k), correctIndex: q.correctIndex >= k && q.correctIndex > 0 ? q.correctIndex - 1 : q.correctIndex })}><Trash2 className="h-4 w-4" /></Button>
                </li>
              ))}
            </ul>
            <Button variant="ghost" size="sm" icon={Plus} className="ml-7 mt-1" disabled={q.options.length >= 6} onClick={() => updateQ(i, { options: [...q.options, ''] })}>Option</Button>
          </li>
        ))}
      </ol>
      <Button variant="secondary" size="sm" icon={Plus} className="mt-3" onClick={() => setQuestions((qs) => [...qs, emptyQuestion()])}>Add question</Button>
      <p className="mt-2 text-xs text-muted">Tick the radio next to the correct option.</p>
    </Modal>
  );
}

/* ---------------------------------------------------------------- Offerings */

export function OfferingsPanel({ course, onChange }) {
  const [open, setOpen] = useState(false);
  const { values, bind, set, reset } = useForm({ format: 'self_paced', priceNaira: '', fundingTypes: ['self'], capacity: '', startAt: '', durationWeeks: '' });

  const create = useAction(
    () => courseApi.addOffering(course.id, {
      format: values.format,
      price: { amount: nairaToKobo(values.priceNaira), currency: 'NGN' },
      fundingTypes: values.fundingTypes,
      capacity: values.format === 'cohort' && values.capacity ? Number(values.capacity) : null,
      startAt: values.format === 'cohort' ? toIso(values.startAt) : null,
      durationWeeks: values.format === 'cohort' && values.durationWeeks ? Number(values.durationWeeks) : null,
    }),
    {
      success: 'Offering added.',
      onSuccess: (res) => {
        onChange({ offerings: [...(course.offerings || []), { id: res.offeringId, format: res.format, price: res.price, fundingTypes: values.fundingTypes, capacity: values.capacity ? Number(values.capacity) : null, startAt: toIso(values.startAt), durationWeeks: values.durationWeeks ? Number(values.durationWeeks) : null }] });
        reset();
        setOpen(false);
      },
    },
  );

  const toggleFunding = (f) => set('fundingTypes', values.fundingTypes.includes(f) ? values.fundingTypes.filter((x) => x !== f) : [...values.fundingTypes, f]);
  const offerings = course.offerings || [];

  return (
    <Card>
      <CardHeader title="Offerings" description="How the course is sold: self-paced access, a dated cohort with live classes, or one-to-one delivery. Sponsors can only fund offerings that accept sponsorship." action={<Button size="sm" variant="secondary" icon={Plus} onClick={() => setOpen(true)}>Add offering</Button>} />
      {offerings.length === 0 ? <p className="text-sm text-muted">No offerings yet. A course needs at least one before it can be published.</p> : (
        <ul className="divide-y divide-line">
          {offerings.map((o) => (
            <li key={o.id} className="flex flex-wrap items-center justify-between gap-2 py-2 text-sm">
              <div>
                <span className="font-semibold text-navy">{(OFFERING_FORMATS.find(([v]) => v === o.format) || [])[1] || o.format}</span>
                <span className="text-muted"> · {formatMoney(o.price)}{o.capacity ? ` · ${o.capacity} seats` : ''}{o.startAt ? ` · starts ${formatDate(o.startAt)}` : ''}{o.durationWeeks ? ` · ${o.durationWeeks} weeks` : ''}</span>
              </div>
              <div className="flex gap-1">{(o.fundingTypes || []).map((f) => <StatusPill key={f} status={f} tone={f === 'sponsored' ? 'success' : 'neutral'} label={(FUNDING_TYPES.find(([v]) => v === f) || [])[1] || f} />)}</div>
            </li>
          ))}
        </ul>
      )}

      <Modal open={open} onClose={() => setOpen(false)} title="Add an offering" footer={<><Button variant="secondary" onClick={() => setOpen(false)}>Cancel</Button><Button loading={create.isPending} disabled={values.priceNaira === '' || values.fundingTypes.length === 0 || (values.format === 'cohort' && !values.startAt)} onClick={() => create.mutate()}>Add offering</Button></>}>
        <ErrorAlert error={create.error} className="mb-3" />
        <div className="space-y-4">
          <FormGrid>
            <Field label="Format">{(p) => <Select {...p} {...bind('format')}>{OFFERING_FORMATS.map(([v, l]) => <option key={v} value={v}>{l}</option>)}</Select>}</Field>
            <Field label="Price (₦)" hint="0 for free." required>{(p) => <Input {...p} inputMode="numeric" {...bind('priceNaira')} />}</Field>
          </FormGrid>
          {values.format === 'cohort' ? (
            <FormGrid cols={3}>
              <Field label="Starts" required>{(p) => <Input {...p} type="datetime-local" {...bind('startAt')} />}</Field>
              <Field label="Duration (weeks)">{(p) => <Input {...p} type="number" min={1} {...bind('durationWeeks')} />}</Field>
              <Field label="Capacity">{(p) => <Input {...p} type="number" min={1} {...bind('capacity')} />}</Field>
            </FormGrid>
          ) : null}
          <Field label="Who can pay" required>
            <div className="flex flex-wrap gap-2">
              {FUNDING_TYPES.map(([v, l]) => (
                <button key={v} type="button" aria-pressed={values.fundingTypes.includes(v)} onClick={() => toggleFunding(v)} className={`min-h-10 rounded-chip border px-3 text-sm font-semibold ${values.fundingTypes.includes(v) ? 'border-royal bg-royal text-white' : 'border-line bg-white text-navy'}`}>{l}</button>
              ))}
            </div>
          </Field>
        </div>
      </Modal>
    </Card>
  );
}
