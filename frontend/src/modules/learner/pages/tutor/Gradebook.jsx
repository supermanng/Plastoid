import { useMemo, useState } from 'react';
import { useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { AlertTriangle, ClipboardCheck, Plus, Trash2, Users } from 'lucide-react';
import { Alert, Breadcrumb, Button, Chip, EmptyState, ErrorAlert, Field, FormGrid, Input, Loading, Modal, ProgressBar, StatusPill, Textarea } from '../../../../shared/components/ui';
import { useAction } from '../../../../shared/lib/hooks';
import { lmsApi, tutorKeys } from '../../api/tutor';

/*
 * /tutor/courses/:courseId/gradebook — GET /lms/courses/{courseId}/gradebook
 * rows[]: enrolmentId, learnerId, progressPct, status, finalScore, items[]: lessonId, title, type, status, bestScore, passed, attempts
 * Grading: POST /lms/submissions/{submissionId}/grade { rubric: [{ criterion, points, maxPoints, comment }], feedback }.
 * The gradebook does not expose submission ids (backend gap), so the grading form takes the id from the submission
 * notification / learner record.
 */
const AT_RISK_BELOW = 30;

export default function Gradebook() {
  const { courseId } = useParams();
  const gradebook = useQuery({ queryKey: tutorKeys.gradebook(courseId), queryFn: () => lmsApi.gradebook(courseId) });
  const [filter, setFilter] = useState('all');
  const [grading, setGrading] = useState(false);

  const rows = useMemo(() => {
    const all = gradebook.data?.rows || [];
    if (filter === 'at_risk') return all.filter((r) => Number(r.progressPct) < AT_RISK_BELOW && r.status !== 'completed');
    if (filter === 'completed') return all.filter((r) => r.status === 'completed');
    if (filter === 'to_grade') return all.filter((r) => r.items.some((i) => i.type === 'assignment' && i.status !== 'notstarted' && i.bestScore == null));
    return all;
  }, [gradebook.data, filter]);

  if (gradebook.isPending) return <Loading />;
  if (gradebook.isError) return <ErrorAlert error={gradebook.error} />;
  const g = gradebook.data;
  const all = g.rows || [];
  const atRisk = all.filter((r) => Number(r.progressPct) < AT_RISK_BELOW && r.status !== 'completed').length;
  const lessons = all[0]?.items || [];

  return (
    <div>
      <Breadcrumb items={[{ to: '/tutor/courses', label: 'My courses' }, { to: `/tutor/courses/${courseId}`, label: g.courseTitle }, { label: 'Gradebook' }]} />
      <header className="mb-6 mt-2 flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h1 className="text-2xl sm:text-3xl">Gradebook</h1>
          <p className="mt-1 text-sm text-muted">{all.length} learner{all.length === 1 ? '' : 's'} · {g.courseTitle}</p>
        </div>
        <Button icon={ClipboardCheck} onClick={() => setGrading(true)}>Grade a submission</Button>
      </header>

      {atRisk > 0 ? (
        <Alert tone="warn" className="mb-4" title={`${atRisk} learner${atRisk === 1 ? '' : 's'} at risk`}>
          Progress below {AT_RISK_BELOW}% and not completed. Weeks 2–4 are where drop-out peaks: a message or announcement now helps.
        </Alert>
      ) : null}

      <div className="mb-4 flex flex-wrap gap-2">
        {[['all', 'All', all.length], ['at_risk', 'At risk', atRisk], ['to_grade', 'Awaiting grade', all.filter((r) => r.items.some((i) => i.type === 'assignment' && i.status !== 'notstarted' && i.bestScore == null)).length], ['completed', 'Completed', all.filter((r) => r.status === 'completed').length]].map(([v, l, n]) => (
          <Chip key={v} active={filter === v} onClick={() => setFilter(v)}>{l} <span className="text-xs opacity-70">{n}</span></Chip>
        ))}
      </div>

      {all.length === 0 ? (
        <EmptyState icon={Users} title="No learners yet" description="Enrolments appear here as soon as someone buys or is sponsored into the course." />
      ) : rows.length === 0 ? (
        <EmptyState icon={Users} title="Nobody matches this filter" />
      ) : (
        <div className="overflow-x-auto rounded-card border border-line">
          <table className="w-full min-w-[720px] text-sm">
            <thead className="bg-surface text-left text-xs uppercase tracking-wide text-muted">
              <tr>
                <th className="px-4 py-3 font-semibold">Learner</th>
                <th className="px-4 py-3 font-semibold">Progress</th>
                <th className="px-4 py-3 font-semibold">Status</th>
                {lessons.map((l) => <th key={l.lessonId} className="px-2 py-3 text-center font-semibold" title={l.title}><span className="block max-w-24 truncate">{l.title}</span><span className="font-normal normal-case">{l.type}</span></th>)}
                <th className="px-4 py-3 text-right font-semibold">Final</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((r) => {
                const risk = Number(r.progressPct) < AT_RISK_BELOW && r.status !== 'completed';
                return (
                  <tr key={r.enrolmentId} className={`border-t border-line ${risk ? 'bg-warn-50/40' : ''}`}>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2 font-medium text-navy">
                        {risk ? <AlertTriangle className="h-4 w-4 text-warn" aria-label="At risk" /> : null}
                        <span className="font-mono text-xs">{String(r.learnerId).slice(0, 8)}</span>
                      </div>
                      <div className="font-mono text-[10px] text-muted">enrolment {String(r.enrolmentId).slice(0, 8)}</div>
                    </td>
                    <td className="w-40 px-4 py-3"><ProgressBar value={Number(r.progressPct)} /></td>
                    <td className="px-4 py-3"><StatusPill status={r.status} /></td>
                    {r.items.map((i) => (
                      <td key={i.lessonId} className="px-2 py-3 text-center">
                        <Cell item={i} />
                      </td>
                    ))}
                    <td className="px-4 py-3 text-right tabular font-semibold text-navy">{r.finalScore != null ? `${Number(r.finalScore).toFixed(0)}%` : '—'}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      <GradeModal open={grading} onClose={() => setGrading(false)} courseId={courseId} />
    </div>
  );
}

function Cell({ item }) {
  if (item.type === 'quiz' || item.type === 'assignment') {
    if (item.bestScore == null) return <span className="text-xs text-muted">{item.status === 'notstarted' ? '—' : item.type === 'assignment' ? 'to grade' : `${item.attempts} att.`}</span>;
    return <span className={`tabular text-xs font-semibold ${item.passed ? 'text-teal-600' : 'text-danger'}`}>{Number(item.bestScore).toFixed(0)}%</span>;
  }
  return <span className={`inline-block h-2.5 w-2.5 rounded-full ${item.status === 'completed' ? 'bg-teal' : item.status === 'inprogress' ? 'bg-cyan' : 'bg-line'}`} aria-label={item.status} />;
}

/* ---------------------------------------------------------------- Grading */

function GradeModal({ open, onClose, courseId }) {
  const [submissionId, setSubmissionId] = useState('');
  const [feedback, setFeedback] = useState('');
  const [rubric, setRubric] = useState([{ criterion: 'Correctness', points: '', maxPoints: 50, comment: '' }, { criterion: 'Presentation', points: '', maxPoints: 50, comment: '' }]);
  const [result, setResult] = useState(null);

  const grade = useAction(
    () => lmsApi.grade(submissionId.trim(), { rubric: rubric.map((r) => ({ criterion: r.criterion.trim(), points: Number(r.points), maxPoints: Number(r.maxPoints), comment: r.comment.trim() || null })), feedback: feedback.trim() || null }),
    { invalidate: [tutorKeys.gradebook(courseId)], onSuccess: setResult, silent: true },
  );
  const update = (i, patch) => setRubric((rs) => rs.map((r, j) => (j === i ? { ...r, ...patch } : r)));
  const valid = /^[0-9a-f-]{36}$/i.test(submissionId.trim()) && rubric.length > 0 && rubric.every((r) => r.criterion.trim() && r.points !== '' && Number(r.points) >= 0 && Number(r.points) <= Number(r.maxPoints) && Number(r.maxPoints) > 0);
  const total = rubric.reduce((s, r) => s + (Number(r.points) || 0), 0);
  const max = rubric.reduce((s, r) => s + (Number(r.maxPoints) || 0), 0);

  return (
    <Modal open={open} onClose={onClose} size="lg" title="Grade a submission" description="Score each rubric criterion; passing marks the assignment complete for the learner." footer={<><Button variant="secondary" onClick={onClose}>Close</Button><Button loading={grade.isPending} disabled={!valid} onClick={() => grade.mutate()}>Submit grade</Button></>}>
      <ErrorAlert error={grade.error} className="mb-3" />
      {result ? (
        <Alert tone={result.passed ? 'success' : 'warn'} className="mb-3" title={`${Number(result.scorePercent).toFixed(0)}% — ${result.passed ? 'passed' : 'not passed'}`}>
          {result.score} of {result.maxScore} points.{result.enrolmentCompleted ? ' This completed the learner’s enrolment.' : ''}
        </Alert>
      ) : null}
      <Field label="Submission id" hint="From the submission notification. The gradebook does not list submission ids yet." required>
        {(p) => <Input {...p} value={submissionId} onChange={(e) => setSubmissionId(e.target.value)} placeholder="00000000-0000-0000-0000-000000000000" className="font-mono text-sm" />}
      </Field>
      <h4 className="mt-4 text-sm font-bold">Rubric <span className="font-normal text-muted">({total}/{max} points)</span></h4>
      <ul className="mt-2 space-y-2">
        {rubric.map((r, i) => (
          <li key={i} className="grid grid-cols-2 items-end gap-2 sm:grid-cols-[1fr_auto_auto_1fr_auto]">
            <Field label="Criterion">{(p) => <Input {...p} value={r.criterion} onChange={(e) => update(i, { criterion: e.target.value })} />}</Field>
            <Field label="Points">{(p) => <Input {...p} type="number" min={0} max={r.maxPoints} value={r.points} onChange={(e) => update(i, { points: e.target.value })} className="w-20" />}</Field>
            <Field label="Max">{(p) => <Input {...p} type="number" min={1} value={r.maxPoints} onChange={(e) => update(i, { maxPoints: e.target.value })} className="w-20" />}</Field>
            <Field label="Comment">{(p) => <Input {...p} value={r.comment} onChange={(e) => update(i, { comment: e.target.value })} />}</Field>
            <Button variant="ghost" size="sm" aria-label="Remove criterion" disabled={rubric.length === 1} onClick={() => setRubric((rs) => rs.filter((_, j) => j !== i))}><Trash2 className="h-4 w-4" /></Button>
          </li>
        ))}
      </ul>
      <Button variant="secondary" size="sm" icon={Plus} className="mt-2" disabled={rubric.length >= 30} onClick={() => setRubric((rs) => [...rs, { criterion: '', points: '', maxPoints: 10, comment: '' }])}>Add criterion</Button>
      <FormGrid cols={1} className="mt-4">
        <Field label="Feedback to the learner" hint="AI-drafted feedback is a Phase 2 feature; write your own for now.">{(p) => <Textarea {...p} value={feedback} onChange={(e) => setFeedback(e.target.value)} rows={3} />}</Field>
      </FormGrid>
    </Modal>
  );
}
