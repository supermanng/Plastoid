import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Copy, Search, ShieldAlert, UserCheck } from 'lucide-react';
import { Alert, Button, ErrorAlert, Field, Input, Modal, Select, Textarea } from '../../../shared/components/ui';
import { useAuth } from '../../../shared/lib/auth';
import { formatDateTime } from '../../../shared/lib/format';

/** Short, copyable identifier (GUIDs are long; show the first 8 chars). */
export function Id({ value, className = '' }) {
  const [copied, setCopied] = useState(false);
  if (!value) return <span className="text-muted">—</span>;
  const text = String(value);
  return (
    <button
      type="button"
      title={`${text} (click to copy)`}
      onClick={() => navigator.clipboard?.writeText(text).then(() => { setCopied(true); setTimeout(() => setCopied(false), 1200); })}
      className={`inline-flex items-center gap-1 rounded bg-surface px-1.5 py-0.5 font-mono text-xs text-navy hover:bg-line ${className}`}
    >
      {copied ? 'copied' : text.slice(0, 8)}
      <Copy className="h-3 w-3 text-muted" aria-hidden />
    </button>
  );
}

/** Link to a user's admin page with a short id. */
export function UserLink({ id, label }) {
  if (!id) return <span className="text-muted">—</span>;
  return (
    <Link to={`/users/${id}`} className="inline-flex items-center gap-1 font-mono text-xs text-royal hover:underline" title={id}>
      {label || id.slice(0, 8)}
    </Link>
  );
}

/** A payout payee (ADR 0019): a tutor links to the user page; an organisation shows its id with a label. */
export function PayeeLink({ row }) {
  const type = String(row?.payeeType || 'tutor').toLowerCase();
  const id = row?.payeeId || row?.tutorUserId;
  if (type === 'organisation') {
    return <span className="inline-flex items-center gap-1 text-xs"><span className="rounded bg-surface px-1.5 py-0.5 font-semibold text-navy">Organisation</span><Id value={id} /></span>;
  }
  return <UserLink id={id} />;
}

/** Is the signed-in staff member the maker of this record? */
export function useIsMaker(makerId) {
  const auth = useAuth();
  return Boolean(makerId && auth.me?.id && String(makerId).toLowerCase() === String(auth.me.id).toLowerCase());
}

/** Explains why the approve button is disabled for the maker (four-eyes). */
export function FourEyesNote({ makerId, what = 'this' }) {
  const isMaker = useIsMaker(makerId);
  return (
    <div className="flex items-start gap-2 text-xs text-muted">
      <UserCheck className="mt-0.5 h-3.5 w-3.5 shrink-0 text-cyan" aria-hidden />
      <span>
        Proposed by <UserLink id={makerId} />.{' '}
        {isMaker ? <strong className="text-warn">You proposed {what}; a different staff member must approve it.</strong> : 'Four-eyes: the checker must differ from the maker.'}
      </span>
    </div>
  );
}

/** Surfaces the admin IP allow-list refusal distinctly from other errors. */
export function AdminError({ error, className }) {
  if (!error) return null;
  if (error?.code === 'admin_ip_not_allowed' || error?.status === 403) {
    return (
      <Alert tone="danger" title={error.code === 'admin_ip_not_allowed' ? 'Your IP address is not on the admin allow-list' : 'Not permitted'} className={className}>
        {error.detail || 'Admin routes are restricted to known office or VPN addresses. Connect to the VPN or ask operations to add your address.'}
      </Alert>
    );
  }
  return <ErrorAlert error={error} className={className} />;
}

/** Pretty-printed JSON (strings that hold JSON are parsed first). */
export function JsonView({ value, className = '' }) {
  if (value === null || value === undefined || value === '') return <span className="text-muted">—</span>;
  let obj = value;
  if (typeof value === 'string') {
    try {
      obj = JSON.parse(value);
    } catch {
      obj = value;
    }
  }
  return <pre className={`max-h-72 overflow-auto rounded-input bg-surface p-3 text-xs leading-relaxed text-navy ${className}`}>{typeof obj === 'string' ? obj : JSON.stringify(obj, null, 2)}</pre>;
}

/** Field-by-field before/after view for audit rows. */
export function DiffView({ before, after }) {
  const b = parse(before);
  const a = parse(after);
  if (!b && !a) return <span className="text-muted">No snapshot</span>;
  if (typeof b !== 'object' || typeof a !== 'object' || !b || !a) {
    return (
      <div className="grid gap-3 sm:grid-cols-2">
        <div><div className="mb-1 text-xs font-semibold uppercase text-muted">Before</div><JsonView value={before} /></div>
        <div><div className="mb-1 text-xs font-semibold uppercase text-muted">After</div><JsonView value={after} /></div>
      </div>
    );
  }
  const keys = Array.from(new Set([...Object.keys(b), ...Object.keys(a)])).sort();
  return (
    <div className="overflow-x-auto rounded-input border border-line">
      <table className="w-full text-xs">
        <thead className="bg-surface text-left uppercase text-muted">
          <tr><th className="px-3 py-2">Field</th><th className="px-3 py-2">Before</th><th className="px-3 py-2">After</th></tr>
        </thead>
        <tbody>
          {keys.map((k) => {
            const changed = JSON.stringify(b[k]) !== JSON.stringify(a[k]);
            return (
              <tr key={k} className={`border-t border-line ${changed ? 'bg-warn-50/60' : ''}`}>
                <td className="px-3 py-1.5 font-mono">{k}</td>
                <td className="px-3 py-1.5 font-mono text-muted">{fmt(b[k])}</td>
                <td className={`px-3 py-1.5 font-mono ${changed ? 'font-semibold text-navy' : 'text-muted'}`}>{fmt(a[k])}</td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

function parse(v) {
  if (v === null || v === undefined || v === '') return null;
  if (typeof v !== 'string') return v;
  try {
    return JSON.parse(v);
  } catch {
    return v;
  }
}

function fmt(v) {
  if (v === undefined) return '';
  if (v === null) return 'null';
  return typeof v === 'object' ? JSON.stringify(v) : String(v);
}

/**
 * Generic decision dialog: approve/reject (or custom options) with an optional note.
 * options: [{ value, label, tone }] ; noteRequiredFor: array of values that need a note.
 */
export function DecisionDialog({ open, onClose, onSubmit, title, description, options, noteLabel = 'Note', noteRequiredFor = ['rejected'], loading, error, children, extra }) {
  const [decision, setDecision] = useState(options?.[0]?.value || '');
  const [note, setNote] = useState('');
  useEffect(() => {
    if (open) {
      setDecision(options?.[0]?.value || '');
      setNote('');
    }
  }, [open, options]);
  const needsNote = noteRequiredFor.includes(decision);
  const selected = options?.find((o) => o.value === decision);
  return (
    <Modal
      open={open}
      onClose={onClose}
      title={title}
      description={description}
      size="sm"
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>Cancel</Button>
          <Button variant={selected?.tone || 'primary'} loading={loading} disabled={!decision || (needsNote && !note.trim())} onClick={() => onSubmit(decision, note.trim() || null)}>
            {selected?.label || 'Confirm'}
          </Button>
        </>
      }
    >
      <AdminError error={error} className="mb-3" />
      {children}
      <div className="flex flex-wrap gap-2">
        {options.map((o) => (
          <button
            key={o.value}
            type="button"
            onClick={() => setDecision(o.value)}
            aria-pressed={decision === o.value}
            className={`min-h-10 rounded-chip border px-4 text-sm font-semibold ${decision === o.value ? 'border-royal bg-royal text-white' : 'border-line bg-white text-navy hover:bg-surface'}`}
          >
            {o.label}
          </button>
        ))}
      </div>
      {extra ? <div className="mt-3">{extra(decision)}</div> : null}
      <Field label={noteLabel} required={needsNote} className="mt-3" hint={needsNote ? 'Required for this decision.' : 'Optional.'}>
        {(p) => <Textarea {...p} rows={3} value={note} onChange={(e) => setNote(e.target.value)} />}
      </Field>
    </Modal>
  );
}

/** "Open by id" box for entities without a staff list. */
export function IdLookup({ label, placeholder = 'Paste an id', onOpen, buttonLabel = 'Open' }) {
  const [value, setValue] = useState('');
  return (
    <form
      className="flex flex-col gap-2 sm:flex-row sm:items-end"
      onSubmit={(e) => {
        e.preventDefault();
        if (value.trim()) onOpen(value.trim());
      }}
    >
      <Field label={label} className="flex-1">
        {(p) => <Input {...p} value={value} onChange={(e) => setValue(e.target.value)} placeholder={placeholder} className="font-mono text-sm" />}
      </Field>
      <Button type="submit" icon={Search} variant="secondary" disabled={!value.trim()}>{buttonLabel}</Button>
    </form>
  );
}

export function SeverityPill({ severity }) {
  const s = String(severity || '').toLowerCase();
  const cls = s === 'critical' || s === 'high' ? 'bg-danger-50 text-danger border-danger/30' : s === 'medium' ? 'bg-warn-50 text-warn border-warn/30' : 'bg-cyan-50 text-royal border-cyan/30';
  return (
    <span className={`inline-flex items-center gap-1 rounded-chip border px-2 py-0.5 text-xs font-semibold capitalize ${cls}`}>
      <ShieldAlert className="h-3 w-3" aria-hidden /> {s || 'info'}
    </span>
  );
}

export function Freshness({ at }) {
  if (!at) return null;
  const stale = Date.now() - new Date(at).getTime() > 15 * 60_000;
  return (
    <span className={`text-xs ${stale ? 'text-warn' : 'text-muted'}`}>
      Data as of {formatDateTime(at)}{stale ? ' · stale feed' : ''}
    </span>
  );
}

export function LimitSelect({ value, onChange }) {
  return (
    <Select value={value} onChange={(e) => onChange(Number(e.target.value))} aria-label="Rows" className="w-28">
      {[25, 50, 100, 200].map((n) => <option key={n} value={n}>{n} rows</option>)}
    </Select>
  );
}

/** Row of key→value chips used in queues. */
export function Meta({ items }) {
  return (
    <dl className="flex flex-wrap gap-x-4 gap-y-1 text-xs text-muted">
      {items.filter(Boolean).map(([k, v]) => (
        <div key={k} className="flex gap-1"><dt>{k}:</dt><dd className="text-navy">{v}</dd></div>
      ))}
    </dl>
  );
}
