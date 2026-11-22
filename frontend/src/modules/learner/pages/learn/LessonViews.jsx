import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Check, Clock, ExternalLink, Upload, Video } from 'lucide-react';
import { Alert, Button, Card, ErrorAlert, Field, Loading, StatusPill } from '../../../../shared/components/ui';
import { useAction } from '../../../../shared/lib/hooks';
import { formatDateTime } from '../../../../shared/lib/format';
import { keys, lms } from '../../api/learn';

/** Loads GET /lms/enrolments/{id}/lessons/{lessonId} and renders by type. */
export function LessonBody({ enrolmentId, lessonId, lesson, gradeItem, onProgress, onComplete, completing, onRefresh }) {
  const content = useQuery({ queryKey: keys.lesson(enrolmentId, lessonId), queryFn: () => lms.lesson(enrolmentId, lessonId) });
  const started = useRef(false);

  // First open → tell the API the lesson is in progress (idempotent; completion never regresses).
  useEffect(() => {
    if (content.data && !started.current && content.data.progressStatus === 'notStarted') {
      started.current = true;
      onProgress({ status: 'inProgress', percentComplete: 1, positionSeconds: content.data.positionSeconds || 0, page: content.data.page || 1 });
    }
  }, [content.data, onProgress]);

  if (content.isPending) return <Loading />;
  if (content.isError) return <ErrorAlert error={content.error} title={`Could not load “${lesson.title}”`} />;
  const c = content.data;
  const done = c.progressStatus === 'completed' || gradeItem?.status === 'completed';

  return (
    <Card>
      <div className="mb-4 flex flex-wrap items-start justify-between gap-3">
        <div>
          <div className="text-xs font-semibold uppercase tracking-wide text-muted">{lesson.moduleTitle} · <span className="capitalize">{c.type}</span>{c.durationMinutes ? ` · ${c.durationMinutes} min` : ''}</div>
          <h2 className="mt-1 text-xl">{c.title}</h2>
        </div>
        <StatusPill status={done ? 'completed' : c.progressStatus} />
      </div>

      {c.type === 'video' ? <VideoLesson c={c} onProgress={onProgress} onComplete={onComplete} /> : null}
      {c.type === 'doc' || c.type === 'document' ? <DocumentLesson c={c} /> : null}
      {c.type === 'quiz' ? <QuizLesson enrolmentId={enrolmentId} lessonId={lessonId} c={c} gradeItem={gradeItem} onRefresh={onRefresh} /> : null}
      {c.type === 'assignment' ? <AssignmentLesson enrolmentId={enrolmentId} lessonId={lessonId} c={c} gradeItem={gradeItem} onRefresh={onRefresh} /> : null}
      {c.type === 'live' ? <LiveLesson c={c} /> : null}
      {!['video', 'doc', 'document', 'quiz', 'assignment', 'live'].includes(c.type) ? <DocumentLesson c={c} /> : null}

      {['video', 'doc', 'document', 'live'].includes(c.type) && !done ? (
        <div className="mt-6 flex justify-end">
          <Button variant="teal" icon={Check} loading={completing} onClick={onComplete}>Mark as complete</Button>
        </div>
      ) : null}
    </Card>
  );
}

function VideoLesson({ c, onProgress, onComplete }) {
  const ref = useRef(null);
  const lastSync = useRef(0);
  const url = c.playbackUrl;
  const canNative = typeof document !== 'undefined' && document.createElement('video').canPlayType('application/vnd.apple.mpegurl');
  const isHls = /\.m3u8(\?|$)/i.test(url || '');

  useEffect(() => {
    const el = ref.current;
    if (!el || !url) return undefined;
    if (c.positionSeconds) el.currentTime = c.positionSeconds;
    const onTime = () => {
      const now = Date.now();
      if (now - lastSync.current > 15000 && el.duration) {
        lastSync.current = now;
        onProgress({ status: 'inProgress', positionSeconds: Math.floor(el.currentTime), percentComplete: Math.min(99, Math.round((el.currentTime / el.duration) * 100)) });
      }
    };
    const onEnded = () => onComplete();
    el.addEventListener('timeupdate', onTime);
    el.addEventListener('ended', onEnded);
    return () => {
      el.removeEventListener('timeupdate', onTime);
      el.removeEventListener('ended', onEnded);
    };
  }, [url, c.positionSeconds, onProgress, onComplete]);

  if (!url) return <Alert tone="warn" title="Video not ready">The tutor's upload is still processing. Check back shortly.</Alert>;
  return (
    <div>
      <video ref={ref} controls playsInline preload="metadata" className="aspect-video w-full rounded-card bg-navy" src={url}>
        Your browser cannot play this video.
      </video>
      {isHls && !canNative ? (
        <Alert tone="info" className="mt-3" title="Playback in this browser">
          This lesson streams as HLS, which Safari and most mobile browsers play directly. If it does not start here, <a href={url} target="_blank" rel="noreferrer" className="font-semibold">open the stream</a> in your device's player.
        </Alert>
      ) : null}
      <div className="mt-2 text-xs text-muted">Progress saves automatically every few seconds; the lesson completes when the video ends.</div>
    </div>
  );
}

function DocumentLesson({ c }) {
  const doc = c.document;
  if (!doc) return <p className="text-sm text-muted">This lesson has no content yet.</p>;
  if (typeof doc === 'string') return <Prose text={doc} />;
  const text = doc.markdown || doc.body || doc.text || doc.content;
  const url = doc.url || doc.fileUrl || doc.downloadUrl;
  return (
    <div className="space-y-4">
      {doc.title && doc.title !== c.title ? <h3 className="text-lg">{doc.title}</h3> : null}
      {text ? <Prose text={String(text)} /> : null}
      {url ? <Button href={url} target="_blank" rel="noreferrer" variant="secondary" icon={ExternalLink}>Open document</Button> : null}
      {!text && !url ? <pre className="overflow-x-auto rounded-card bg-surface p-4 text-xs text-navy">{JSON.stringify(doc, null, 2)}</pre> : null}
    </div>
  );
}

/** Minimal markdown-ish rendering: headings, bullets, paragraphs. */
function Prose({ text }) {
  const blocks = text.split(/\n{2,}/);
  return (
    <div className="space-y-3 text-base leading-7 text-navy">
      {blocks.map((b, i) => {
        const lines = b.split('\n');
        if (/^#{1,3}\s/.test(lines[0])) return <h3 key={i} className="text-lg">{lines[0].replace(/^#+\s/, '')}</h3>;
        if (lines.every((l) => /^\s*[-*]\s/.test(l))) return <ul key={i} className="list-disc space-y-1 pl-6">{lines.map((l, j) => <li key={j}>{l.replace(/^\s*[-*]\s/, '')}</li>)}</ul>;
        if (lines.every((l) => /^\s*\d+[.)]\s/.test(l))) return <ol key={i} className="list-decimal space-y-1 pl-6">{lines.map((l, j) => <li key={j}>{l.replace(/^\s*\d+[.)]\s/, '')}</li>)}</ol>;
        return <p key={i}>{b}</p>;
      })}
    </div>
  );
}

function LiveLesson() {
  return (
    <div className="space-y-3">
      <p className="text-sm text-muted">This is a live class. Join from your upcoming sessions when the room opens (15 minutes before start).</p>
      <Button to="/learn" variant="secondary" icon={Video}>See upcoming classes</Button>
    </div>
  );
}

/* ------------------------------------------------------------------ Quiz */

export function QuizLesson({ enrolmentId, lessonId, c, gradeItem, onRefresh }) {
  const [attempt, setAttempt] = useState(null);
  const [answers, setAnswers] = useState({});
  const [result, setResult] = useState(null);
  const [remaining, setRemaining] = useState(null);
  const saveTimer = useRef(null);

  const start = useAction(() => lms.startAttempt(enrolmentId, lessonId), { silent: true, onSuccess: (a) => { setAttempt(a); setResult(null); setRemaining(a.secondsRemaining); } });
  const save = useAction(() => lms.saveAnswers(attempt.attemptId, answers), { silent: true });
  const submit = useAction(() => lms.submitAttempt(attempt.attemptId, answers), { success: (r) => (r.passed ? `Passed: ${r.correct}/${r.total}` : `Not passed: ${r.correct}/${r.total}`), onSuccess: (r) => { setResult(r); setAttempt(null); onRefresh(); } });

  useEffect(() => {
    if (!attempt) return undefined;
    const t = setInterval(() => setRemaining((s) => (s > 0 ? s - 1 : 0)), 1000);
    return () => clearInterval(t);
  }, [attempt]);

  useEffect(() => {
    if (attempt && remaining === 0 && !submit.isPending && !result) submit.mutate();
  }, [remaining, attempt, submit, result]);

  const setAnswer = (idx, value) => {
    setAnswers((a) => {
      const next = { ...a, [String(idx)]: value };
      clearTimeout(saveTimer.current);
      saveTimer.current = setTimeout(() => attempt && lms.saveAnswers(attempt.attemptId, next).catch(() => {}), 1200);
      return next;
    });
  };

  const questions = c.questions || [];
  const passMark = c.passMarkPercent ?? 70;

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap gap-4 text-sm text-muted">
        <span>{questions.length} questions</span>
        <span>Pass mark {passMark}%</span>
        {c.timeLimitMinutes ? <span className="inline-flex items-center gap-1"><Clock className="h-4 w-4" /> {c.timeLimitMinutes} min</span> : null}
        {gradeItem?.attempts ? <span>{gradeItem.attempts} attempt{gradeItem.attempts === 1 ? '' : 's'} · best {gradeItem.bestScore != null ? `${Number(gradeItem.bestScore).toFixed(0)}%` : '—'}</span> : null}
      </div>

      {result ? (
        <Alert tone={result.passed ? 'success' : 'warn'} title={result.passed ? 'Passed' : 'Not passed yet'}>
          You scored {Number(result.score).toFixed(0)} of {Number(result.maxScore).toFixed(0)} ({result.correct}/{result.total} correct).{result.enrolmentCompleted ? ' That completed the course.' : ''}
        </Alert>
      ) : null}
      {gradeItem?.passed && !attempt ? <Alert tone="success" title="Quiz passed">Best score {Number(gradeItem.bestScore).toFixed(0)}%. You can retake it to improve.</Alert> : null}

      {!attempt ? (
        <div>
          <ErrorAlert error={start.error} className="mb-3" />
          <Button loading={start.isPending} onClick={() => { setAnswers({}); start.mutate(); }}>{gradeItem?.attempts ? 'Start another attempt' : 'Start quiz'}</Button>
          {c.timeLimitMinutes ? <p className="mt-2 text-xs text-muted">The timer starts when you begin. Late submissions are not accepted.</p> : null}
        </div>
      ) : (
        <form
          className="space-y-5"
          onSubmit={(e) => {
            e.preventDefault();
            submit.mutate();
          }}
        >
          <div className="sticky top-16 z-10 -mx-5 flex items-center justify-between border-y border-line bg-white/95 px-5 py-2 text-sm backdrop-blur">
            <span className="text-muted">Attempt {attempt.number} · answered {Object.keys(answers).length}/{questions.length}</span>
            <span className={`inline-flex items-center gap-1 font-semibold tabular ${remaining != null && remaining < 60 ? 'text-danger' : 'text-navy'}`}><Clock className="h-4 w-4" /> {formatSeconds(remaining)}</span>
          </div>
          {questions.map((q) => (
            <fieldset key={q.index} className="rounded-card border border-line p-4">
              <legend className="px-1 text-sm font-semibold text-navy">{q.index + 1}. {q.prompt}</legend>
              {q.shortAnswer ? (
                <input className="input mt-2" value={answers[String(q.index)] ?? ''} onChange={(e) => setAnswer(q.index, e.target.value)} placeholder="Type your answer" />
              ) : (
                <div className="mt-2 grid gap-2">
                  {(q.options || []).map((opt, oi) => {
                    const chosen = q.multiSelect ? (answers[String(q.index)] || []).includes(oi) : answers[String(q.index)] === oi;
                    return (
                      <label key={oi} className={`flex min-h-11 cursor-pointer items-center gap-3 rounded-input border px-3 text-sm ${chosen ? 'border-royal bg-royal-50' : 'border-line hover:bg-surface'}`}>
                        <input
                          type={q.multiSelect ? 'checkbox' : 'radio'}
                          name={`q-${q.index}`}
                          className="h-4 w-4 accent-royal"
                          checked={chosen}
                          onChange={() => {
                            if (q.multiSelect) {
                              const cur = answers[String(q.index)] || [];
                              setAnswer(q.index, chosen ? cur.filter((x) => x !== oi) : [...cur, oi].sort());
                            } else setAnswer(q.index, oi);
                          }}
                        />
                        {opt}
                      </label>
                    );
                  })}
                </div>
              )}
            </fieldset>
          ))}
          <ErrorAlert error={submit.error || save.error} />
          <div className="flex flex-wrap justify-end gap-2">
            <Button type="button" variant="secondary" loading={save.isPending} onClick={() => save.mutate()}>Save progress</Button>
            <Button type="submit" loading={submit.isPending}>Submit answers</Button>
          </div>
        </form>
      )}
    </div>
  );
}

export function formatSeconds(s) {
  if (s == null) return '—';
  const m = Math.floor(s / 60);
  return `${m}:${String(s % 60).padStart(2, '0')}`;
}

/* ------------------------------------------------------------ Assignment */

function AssignmentLesson({ enrolmentId, lessonId, c, gradeItem, onRefresh }) {
  const [file, setFile] = useState(null);
  const [state, setState] = useState(null); // { submissionId, submittedAt }
  const [progress, setProgress] = useState(0);

  const submit = useAction(
    async () => {
      const res = await lms.submitAssignment(enrolmentId, lessonId, file.type || 'application/octet-stream');
      setProgress(50);
      const put = await fetch(res.uploadUrl, { method: 'PUT', body: file, headers: { 'Content-Type': file.type || 'application/octet-stream' } });
      if (!put.ok) throw new Error(`Upload failed (${put.status}). Try again.`);
      setProgress(100);
      return res;
    },
    { success: 'Assignment submitted. Your tutor will grade it.', onSuccess: (r) => { setState(r); setFile(null); onRefresh(); } },
  );

  return (
    <div className="space-y-4">
      <DocumentLesson c={c} />
      {c.passMarkPercent ? <p className="text-sm text-muted">Pass mark {c.passMarkPercent}%. Your tutor grades against a rubric.</p> : null}
      {gradeItem?.status && gradeItem.status !== 'notStarted' ? (
        <Alert tone={gradeItem.passed ? 'success' : 'info'} title={gradeItem.passed ? 'Graded: passed' : gradeItem.bestScore != null ? 'Graded' : 'Submitted'}>
          {gradeItem.bestScore != null ? `Score ${Number(gradeItem.bestScore).toFixed(0)}%.` : 'Waiting for your tutor to grade it.'} You can resubmit until you pass.
        </Alert>
      ) : null}
      {state ? <Alert tone="success" title="Uploaded">Submitted {formatDateTime(state.submittedAt)}.</Alert> : null}
      <Field label="Upload your work" hint="PDF, document, image or zip up to 50 MB.">
        {(p) => <input {...p} type="file" className="input py-2" onChange={(e) => setFile(e.target.files?.[0] || null)} />}
      </Field>
      <ErrorAlert error={submit.error} />
      {submit.isPending ? <div className="h-2 w-full overflow-hidden rounded-chip bg-surface"><div className="brand-gradient h-full transition-[width]" style={{ width: `${progress}%` }} /></div> : null}
      <Button icon={Upload} disabled={!file} loading={submit.isPending} onClick={() => submit.mutate()}>Submit assignment</Button>
      <p className="text-xs text-muted">Need help? <Link to="/learn/messages" className="font-semibold">Message your tutor</Link>.</p>
    </div>
  );
}
