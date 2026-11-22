import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Video } from 'lucide-react';
import { Alert, Button, StatusPill } from '../../../../shared/components/ui';
import { formatDateTime } from '../../../../shared/lib/format';

/* Course-authoring helpers moved to shared (the organisation portal uses the same builder); re-exported for the tutor pages. */
export { FUNDING_TYPES, OFFERING_FORMATS, splitList, toIso, uploadToPresignedUrl } from '../../../../shared/lib/authoring';

/* ---------------------------------------------------------------- Local memory
 * The Lms module has no "list my cohorts" read, so cohorts this UI opens are remembered per browser. They are only a
 * convenience index; the server stays the source of truth. (Courses come from GET /catalogue/courses/mine since ADR 0019.) */

const COHORTS_KEY = 'plastoids.tutor.cohorts';

function readList(key) {
  try {
    const raw = localStorage.getItem(key);
    const list = raw ? JSON.parse(raw) : [];
    return Array.isArray(list) ? list : [];
  } catch {
    return [];
  }
}

function writeList(key, list) {
  try {
    localStorage.setItem(key, JSON.stringify(list));
  } catch {
    /* private mode: nothing to do */
  }
}

/** Shared hook for a remembered list keyed by `id`; `remember` upserts, `forget` removes. */
function useRemembered(key) {
  const [items, setItems] = useState(() => readList(key));
  useEffect(() => {
    const onStorage = (e) => e.key === key && setItems(readList(key));
    window.addEventListener('storage', onStorage);
    return () => window.removeEventListener('storage', onStorage);
  }, [key]);
  const remember = useCallback((item) => {
    setItems((prev) => {
      const next = [{ ...prev.find((x) => x.id === item.id), ...item, updatedAt: new Date().toISOString() }, ...prev.filter((x) => x.id !== item.id)];
      writeList(key, next);
      return next;
    });
  }, [key]);
  const forget = useCallback((id) => {
    setItems((prev) => {
      const next = prev.filter((x) => x.id !== id);
      writeList(key, next);
      return next;
    });
  }, [key]);
  return { items, remember, forget };
}

export function useRememberedCohorts() {
  return useRemembered(COHORTS_KEY);
}

/* ---------------------------------------------------------------- Uploads */

/** Small file picker + progress strip used by credentials, demo lesson and lesson videos. */
export function UploadButton({ accept, label = 'Choose file', onFile, progress, busy, disabled, variant = 'secondary' }) {
  return (
    <label className="inline-flex flex-col gap-2">
      <span className={`inline-flex min-h-11 cursor-pointer items-center justify-center gap-2 rounded-input border px-4 text-sm font-semibold ${disabled || busy ? 'cursor-not-allowed border-line text-muted' : variant === 'primary' ? 'border-royal bg-royal text-white hover:bg-royal-600' : 'border-line bg-white text-navy hover:bg-surface'}`}>
        <Video className="h-4 w-4" aria-hidden /> {busy ? `Uploading ${progress ?? 0}%` : label}
        <input type="file" accept={accept} className="sr-only" disabled={disabled || busy} onChange={(e) => e.target.files?.[0] && onFile(e.target.files[0])} />
      </span>
      {busy ? (
        <span className="h-1.5 w-full overflow-hidden rounded-chip bg-surface" role="progressbar" aria-valuenow={progress ?? 0} aria-valuemin={0} aria-valuemax={100}>
          <span className="brand-gradient block h-full" style={{ width: `${progress ?? 0}%` }} />
        </span>
      ) : null}
    </label>
  );
}

/* ---------------------------------------------------------------- Verification stages */

export const STAGES = [
  { name: 'identity', label: 'Identity check', next: 'NIN and BVN are verified instantly through the KYC provider.' },
  { name: 'qualifications', label: 'Credentials', next: 'A reviewer checks each uploaded credential within the SLA shown.' },
  { name: 'subject', label: 'Demo lesson', next: 'A subject reviewer watches your 5-minute demo and approves you to teach.' },
  { name: 'payout_account', label: 'Payout account', next: 'Your bank account is resolved and the name must match your identity.' },
];

export function stageTone(status) {
  const s = String(status || '').toLowerCase();
  if (s === 'passed') return 'success';
  if (s === 'in_review' || s === 'pending') return 'warn';
  if (s === 'failed' || s === 'rejected') return 'danger';
  return 'neutral';
}

export function stageLabel(status) {
  return { passed: 'Passed', in_review: 'In review', pending: 'Pending', not_started: 'Not started', failed: 'Failed' }[status] || status || '—';
}

/** Lists the tutor's verification stages compactly (dashboard) or in full (onboarding). */
export function VerificationSummary({ dashboard, compact = false }) {
  const stages = dashboard?.verification || [];
  const done = stages.filter((s) => s.status === 'passed').length;
  return (
    <div>
      <div className="mb-2 flex items-center justify-between text-sm">
        <span className="font-semibold text-navy">Verification</span>
        <span className="text-muted">{done} of {stages.length} stages</span>
      </div>
      <ul className="space-y-2">
        {STAGES.map((meta) => {
          const s = stages.find((x) => x.name === meta.name) || { status: 'not_started' };
          return (
            <li key={meta.name} className="flex items-center justify-between gap-3 text-sm">
              <span className="text-navy">{meta.label}</span>
              <div className="flex items-center gap-2">
                {!compact && s.reviewDueAt && s.status !== 'passed' ? <span className="hidden text-xs text-muted sm:inline">Due {formatDateTime(s.reviewDueAt)}</span> : null}
                <StatusPill status={s.status} tone={stageTone(s.status)} label={stageLabel(s.status)} />
              </div>
            </li>
          );
        })}
      </ul>
      {!compact && done < stages.length ? (
        <Button to="/tutor/onboarding" size="sm" variant="secondary" className="mt-3">Continue verification</Button>
      ) : null}
    </div>
  );
}

/** Banner shown across tutor pages until the account is verified or the payout account is missing. */
export function TutorStatusBanners({ dashboard }) {
  if (!dashboard) return null;
  const banners = [];
  const payout = dashboard.verification?.find((s) => s.name === 'payout_account');
  if (dashboard.status && dashboard.status !== 'verified') {
    banners.push(
      <Alert key="status" tone={dashboard.status === 'rejected' || dashboard.status === 'suspended' ? 'danger' : 'warn'} title={dashboard.status === 'rejected' ? 'Your application was not approved' : dashboard.status === 'suspended' ? 'Your tutor account is suspended' : 'Your tutor account is not fully verified yet'}>
        {dashboard.status === 'rejected' || dashboard.status === 'suspended' ? 'Contact support if you believe this is a mistake.' : (
          <>Complete every verification stage before you can be booked or publish courses. <Link to="/tutor/onboarding" className="font-semibold">Continue verification</Link></>
        )}
      </Alert>,
    );
  }
  if (!dashboard.payoutAccountMasked || payout?.status !== 'passed') {
    banners.push(
      <Alert key="payout" tone="warn" title="No payout account">
        Earnings cannot be paid until a bank account in your name is resolved. <Link to="/tutor/profile#payout" className="font-semibold">Add payout account</Link>
      </Alert>,
    );
  }
  return banners.length ? <div className="mb-6 space-y-3">{banners}</div> : null;
}

/* ---------------------------------------------------------------- Misc */

export const WEEKDAYS = ['monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday', 'sunday'];
export const TEACHING_FORMATS = [
  ['one_to_one', 'One-to-one'],
  ['group', 'Small group'],
  ['course', 'Course (cohort / self-paced)'],
];

/** "HH:mm" → "HH:mm:00" for TimeOnly fields. */
export function toTimeOnly(value) {
  if (!value) return null;
  return value.length === 5 ? `${value}:00` : value;
}

/** Browser's IANA time zone, defaulting to Lagos. */
export function localTimeZone() {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || 'Africa/Lagos';
  } catch {
    return 'Africa/Lagos';
  }
}

export function groupByDay(items, key) {
  const groups = new Map();
  for (const item of items) {
    const day = new Date(item[key]).toLocaleDateString('en-NG', { weekday: 'long', day: 'numeric', month: 'short', year: 'numeric' });
    if (!groups.has(day)) groups.set(day, []);
    groups.get(day).push(item);
  }
  return [...groups.entries()];
}
