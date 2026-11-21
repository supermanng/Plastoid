import { createContext, useCallback, useContext, useEffect, useId, useMemo, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import clsx from 'clsx';
import { AlertCircle, CheckCircle2, ChevronLeft, ChevronRight, Info, Loader2, X, XCircle, Inbox, Clock, Ban, Check } from 'lucide-react';
import { errorMessage } from '../lib/api';
import { koboOf } from '../lib/format';

/* ---------------------------------------------------------------- Buttons */

const buttonVariants = {
  primary: 'bg-royal text-white hover:bg-royal-600 disabled:bg-royal/50',
  secondary: 'bg-white text-navy border border-line hover:bg-surface disabled:text-muted',
  ghost: 'bg-transparent text-royal hover:bg-royal-50',
  danger: 'bg-danger text-white hover:bg-danger/90 disabled:bg-danger/50',
  teal: 'bg-teal text-navy hover:bg-teal-600 hover:text-white',
  navy: 'bg-navy text-white hover:bg-navy-700',
};

const buttonSizes = {
  sm: 'min-h-9 px-3 text-sm',
  md: 'min-h-11 px-4 text-sm',
  lg: 'min-h-12 px-6 text-base',
};

export function Button({ as, to, href, variant = 'primary', size = 'md', loading = false, icon: Icon, className, children, disabled, ...rest }) {
  const classes = clsx(
    'inline-flex items-center justify-center gap-2 rounded-input font-semibold transition-colors select-none whitespace-nowrap',
    'disabled:cursor-not-allowed focus-visible:outline-2',
    buttonVariants[variant],
    buttonSizes[size],
    className,
  );
  const content = (
    <>
      {loading ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden /> : Icon ? <Icon className="h-4 w-4" aria-hidden /> : null}
      {children}
    </>
  );
  if (to) return <Link to={to} className={classes} aria-disabled={disabled || loading} {...rest}>{content}</Link>;
  if (href) return <a href={href} className={classes} {...rest}>{content}</a>;
  const Comp = as || 'button';
  return (
    <Comp type={Comp === 'button' ? rest.type || 'button' : undefined} className={classes} disabled={disabled || loading} {...rest}>
      {content}
    </Comp>
  );
}

/* ---------------------------------------------------------------- Surfaces */

export function Card({ className, children, as: Comp = 'div', padded = true, ...rest }) {
  return (
    <Comp className={clsx('card', padded && 'p-5', className)} {...rest}>
      {children}
    </Comp>
  );
}

export function CardHeader({ title, description, action, className }) {
  return (
    <div className={clsx('mb-4 flex flex-wrap items-start justify-between gap-3', className)}>
      <div>
        <h3 className="text-base font-bold text-navy">{title}</h3>
        {description ? <p className="mt-0.5 text-sm text-muted">{description}</p> : null}
      </div>
      {action}
    </div>
  );
}

export function PageHeader({ title, description, actions, breadcrumb, className }) {
  return (
    <header className={clsx('mb-6 flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between', className)}>
      <div className="min-w-0">
        {breadcrumb ? <div className="mb-1 text-sm text-muted">{breadcrumb}</div> : null}
        <h1 className="text-2xl sm:text-3xl text-navy">{title}</h1>
        {description ? <p className="mt-1 max-w-2xl text-sm sm:text-base text-muted">{description}</p> : null}
      </div>
      {actions ? <div className="flex flex-wrap gap-2 shrink-0">{actions}</div> : null}
    </header>
  );
}

export function Stat({ label, value, hint, tone = 'default', icon: Icon, className }) {
  const tones = {
    default: 'text-navy',
    teal: 'text-teal-600',
    royal: 'text-royal',
    danger: 'text-danger',
    warn: 'text-warn',
  };
  return (
    <Card className={clsx('flex items-start justify-between gap-3', className)}>
      <div className="min-w-0">
        <div className="text-xs font-medium uppercase tracking-wide text-muted">{label}</div>
        <div className={clsx('mt-1 text-2xl font-extrabold font-display tabular truncate', tones[tone])}>{value}</div>
        {hint ? <div className="mt-1 text-xs text-muted">{hint}</div> : null}
      </div>
      {Icon ? <Icon className="h-5 w-5 shrink-0 text-cyan" aria-hidden /> : null}
    </Card>
  );
}

/* ---------------------------------------------------------------- Status */

const statusTones = {
  success: ['bg-teal-50 text-teal-600 border-teal/30', CheckCircle2],
  info: ['bg-cyan-50 text-royal border-cyan/30', Info],
  warn: ['bg-warn-50 text-warn border-warn/30', Clock],
  danger: ['bg-danger-50 text-danger border-danger/30', XCircle],
  neutral: ['bg-surface text-muted border-line', Ban],
  navy: ['bg-navy text-white border-navy', Check],
};

/** Map any API status string to a tone. Extend in pages via the `tone` prop when a status is domain-specific. */
export function toneFor(status) {
  const s = String(status || '').toLowerCase();
  if (/(active|approved|published|verified|completed|complete|settled|paid|succeeded|success|accepted|funded|valid|passed|released|confirmed|open|granted|resolved|enrolled|issued|certified|filled)/.test(s)) return 'success';
  if (/(pending|review|queued|quoted|submitted|draft|awaiting|processing|in_review|scheduled|reserved|offer|offered|shortlisted|waitlist|waitlisted|hold|held|locked|in_progress|requires|proposed|sent)/.test(s)) return 'warn';
  if (/(rejected|failed|revoked|cancelled|canceled|expired|suspended|frozen|declined|withdrawn|refunded|error|unknown|invalid|dismissed|reversed|blocked|ineligible)/.test(s)) return 'danger';
  if (/(closed|archived|inactive|none)/.test(s)) return 'neutral';
  return 'info';
}

export function StatusPill({ status, tone, label, className }) {
  const t = tone || toneFor(status);
  const [classes, Icon] = statusTones[t] || statusTones.info;
  return (
    <span className={clsx('inline-flex items-center gap-1 rounded-chip border px-2.5 py-0.5 text-xs font-semibold capitalize', classes, className)}>
      <Icon className="h-3.5 w-3.5" aria-hidden />
      {label || String(status || '—').replace(/[_-]+/g, ' ')}
    </span>
  );
}

export function Chip({ children, active = false, onClick, className, as: Comp = onClick ? 'button' : 'span' }) {
  return (
    <Comp
      type={Comp === 'button' ? 'button' : undefined}
      onClick={onClick}
      aria-pressed={Comp === 'button' ? active : undefined}
      className={clsx(
        'inline-flex min-h-8 items-center gap-1 rounded-chip border px-3 text-sm font-medium transition-colors',
        active ? 'border-royal bg-royal text-white' : 'border-line bg-white text-navy hover:bg-surface',
        className,
      )}
    >
      {children}
    </Comp>
  );
}

export function Alert({ tone = 'info', title, children, className, onClose }) {
  const [classes, Icon] = {
    info: ['border-cyan/30 bg-cyan-50 text-navy', Info],
    success: ['border-teal/30 bg-teal-50 text-navy', CheckCircle2],
    warn: ['border-warn/30 bg-warn-50 text-navy', AlertCircle],
    danger: ['border-danger/30 bg-danger-50 text-navy', XCircle],
  }[tone];
  return (
    <div role={tone === 'danger' ? 'alert' : 'status'} className={clsx('flex gap-3 rounded-card border p-4 text-sm', classes, className)}>
      <Icon className={clsx('mt-0.5 h-5 w-5 shrink-0', tone === 'danger' ? 'text-danger' : tone === 'warn' ? 'text-warn' : tone === 'success' ? 'text-teal-600' : 'text-cyan')} aria-hidden />
      <div className="min-w-0 flex-1">
        {title ? <div className="font-semibold">{title}</div> : null}
        {children ? <div className={clsx(title && 'mt-0.5', 'text-muted')}>{children}</div> : null}
      </div>
      {onClose ? (
        <button type="button" onClick={onClose} className="-m-1 rounded p-1 text-muted hover:text-navy" aria-label="Dismiss">
          <X className="h-4 w-4" />
        </button>
      ) : null}
    </div>
  );
}

/** Shows an API error (or nothing when error is falsy). */
export function ErrorAlert({ error, className, title }) {
  if (!error) return null;
  return (
    <Alert tone="danger" title={title} className={className}>
      {errorMessage(error)}
      {error?.correlationId ? <span className="mt-1 block text-xs text-muted">Ref {error.correlationId}</span> : null}
    </Alert>
  );
}

/* ---------------------------------------------------------------- Forms */

export function Field({ label, hint, error, required, children, className, id: idProp }) {
  const autoId = useId();
  const id = idProp || autoId;
  return (
    <div className={clsx('flex flex-col gap-1.5', className)}>
      {label ? (
        <label htmlFor={id} className="text-sm font-semibold text-navy">
          {label}
          {required ? <span className="text-danger"> *</span> : null}
        </label>
      ) : null}
      {typeof children === 'function' ? children({ id, 'aria-invalid': error ? 'true' : undefined, 'aria-describedby': hint || error ? `${id}-hint` : undefined }) : children}
      {error ? (
        <p id={`${id}-hint`} className="text-xs font-medium text-danger">{error}</p>
      ) : hint ? (
        <p id={`${id}-hint`} className="text-xs text-muted">{hint}</p>
      ) : null}
    </div>
  );
}

export function Input({ className, ...rest }) {
  return <input className={clsx('input', className)} {...rest} />;
}

export function Textarea({ className, ...rest }) {
  return <textarea className={clsx('input', className)} {...rest} />;
}

export function Select({ className, children, ...rest }) {
  return (
    <select className={clsx('input appearance-none bg-[url("data:image/svg+xml,%3Csvg xmlns=%27http://www.w3.org/2000/svg%27 width=%2716%27 height=%2716%27 viewBox=%270 0 24 24%27 fill=%27none%27 stroke=%27%235B6B7F%27 stroke-width=%272%27%3E%3Cpath d=%27m6 9 6 6 6-6%27/%3E%3C/svg%3E")] bg-[length:16px] bg-[right_12px_center] bg-no-repeat pr-9', className)} {...rest}>
      {children}
    </select>
  );
}

export function Checkbox({ label, className, ...rest }) {
  const id = useId();
  return (
    <label htmlFor={id} className={clsx('flex min-h-11 cursor-pointer items-center gap-3 text-sm text-navy', className)}>
      <input id={id} type="checkbox" className="h-5 w-5 rounded border-line accent-royal" {...rest} />
      <span>{label}</span>
    </label>
  );
}

export function Switch({ checked, onChange, label, className }) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      onClick={() => onChange(!checked)}
      className={clsx('flex min-h-11 items-center justify-between gap-3 text-sm text-navy', className)}
    >
      <span>{label}</span>
      <span className={clsx('relative inline-flex h-6 w-11 shrink-0 rounded-chip transition-colors', checked ? 'bg-teal' : 'bg-line')}>
        <span className={clsx('absolute top-0.5 h-5 w-5 rounded-full bg-white shadow transition-transform', checked ? 'translate-x-5' : 'translate-x-0.5')} />
      </span>
    </button>
  );
}

/** Group of fields laid out responsively. */
export function FormGrid({ children, className, cols = 2 }) {
  return <div className={clsx('grid gap-4', cols === 2 ? 'sm:grid-cols-2' : cols === 3 ? 'sm:grid-cols-3' : '', className)}>{children}</div>;
}

/* ---------------------------------------------------------------- Feedback */

export function Spinner({ className, label = 'Loading' }) {
  return (
    <span role="status" aria-label={label} className={clsx('inline-block h-5 w-5 animate-spin rounded-full border-2 border-line border-t-royal', className)} />
  );
}

export function Loading({ label = 'Loading…', className }) {
  return (
    <div className={clsx('flex items-center justify-center gap-3 py-12 text-sm text-muted', className)} role="status">
      <Spinner /> {label}
    </div>
  );
}

export function Skeleton({ className }) {
  return <div className={clsx('animate-pulse rounded-input bg-surface', className)} aria-hidden />;
}

export function EmptyState({ icon: Icon = Inbox, title, description, action, className }) {
  return (
    <div className={clsx('flex flex-col items-center justify-center gap-2 rounded-card border border-dashed border-line px-6 py-12 text-center', className)}>
      <span className="grid h-12 w-12 place-items-center rounded-full bg-surface text-royal">
        <Icon className="h-6 w-6" aria-hidden />
      </span>
      <h3 className="text-base font-bold text-navy">{title}</h3>
      {description ? <p className="max-w-sm text-sm text-muted">{description}</p> : null}
      {action ? <div className="mt-2">{action}</div> : null}
    </div>
  );
}

/** Generic data-state wrapper for React Query results. */
export function QueryState({ query, empty, children, loading, emptyWhen }) {
  if (query.isPending) return loading || <Loading />;
  if (query.isError) return <ErrorAlert error={query.error} />;
  const data = query.data;
  const isEmpty = emptyWhen ? emptyWhen(data) : data == null || (Array.isArray(data) && data.length === 0) || (Array.isArray(data?.items) && data.items.length === 0);
  if (isEmpty && empty) return empty;
  return typeof children === 'function' ? children(data) : children;
}

/* ---------------------------------------------------------------- Tables */

export function Table({ columns, rows, rowKey = (r, i) => r.id ?? i, onRowClick, empty, className, dense = false }) {
  if (!rows?.length && empty) return empty;
  return (
    <div className={clsx('overflow-x-auto rounded-card border border-line', className)}>
      <table className="w-full min-w-[560px] text-sm">
        <thead className="bg-surface text-left text-xs uppercase tracking-wide text-muted">
          <tr>
            {columns.map((c) => (
              <th key={c.key} scope="col" className={clsx('px-4 font-semibold', dense ? 'py-2' : 'py-3', c.align === 'right' && 'text-right', c.className)}>
                {c.header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows?.map((row, i) => (
            <tr
              key={rowKey(row, i)}
              onClick={onRowClick ? () => onRowClick(row) : undefined}
              className={clsx('border-t border-line', onRowClick && 'cursor-pointer hover:bg-surface/70')}
            >
              {columns.map((c) => (
                <td key={c.key} className={clsx('px-4 align-middle', dense ? 'py-2' : 'py-3', c.align === 'right' && 'text-right tabular', c.cellClassName)}>
                  {c.render ? c.render(row, i) : row[c.key] ?? '—'}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export function KeyValue({ items, className, cols = 2 }) {
  return (
    <dl className={clsx('grid gap-x-6 gap-y-3 text-sm', cols === 2 ? 'sm:grid-cols-2' : cols === 3 ? 'sm:grid-cols-3' : '', className)}>
      {items.filter(Boolean).map(([k, v]) => (
        <div key={k} className="min-w-0">
          <dt className="text-xs font-medium uppercase tracking-wide text-muted">{k}</dt>
          <dd className="mt-0.5 break-words text-navy">{v ?? '—'}</dd>
        </div>
      ))}
    </dl>
  );
}

/* ---------------------------------------------------------------- Navigation bits */

export function Tabs({ tabs, value, onChange, className }) {
  return (
    <div role="tablist" className={clsx('flex gap-1 overflow-x-auto border-b border-line', className)}>
      {tabs.map((t) => (
        <button
          key={t.value}
          role="tab"
          type="button"
          aria-selected={value === t.value}
          onClick={() => onChange(t.value)}
          className={clsx(
            'min-h-11 shrink-0 border-b-2 px-3 text-sm font-semibold transition-colors',
            value === t.value ? 'border-royal text-royal' : 'border-transparent text-muted hover:text-navy',
          )}
        >
          {t.label}
          {t.count !== undefined ? <span className="ml-1.5 rounded-chip bg-surface px-1.5 py-0.5 text-xs text-muted">{t.count}</span> : null}
        </button>
      ))}
    </div>
  );
}

export function Stepper({ steps, current, onSelect, className }) {
  return (
    <ol className={clsx('flex flex-wrap gap-2', className)}>
      {steps.map((s, i) => {
        const state = i < current ? 'done' : i === current ? 'current' : 'todo';
        const Comp = onSelect ? 'button' : 'div';
        return (
          <li key={s} className="flex items-center gap-2">
            <Comp
              type={onSelect ? 'button' : undefined}
              onClick={onSelect ? () => onSelect(i) : undefined}
              aria-current={state === 'current' ? 'step' : undefined}
              className={clsx(
                'flex min-h-9 items-center gap-2 rounded-chip border px-3 text-sm font-semibold',
                state === 'done' && 'border-teal/40 bg-teal-50 text-teal-600',
                state === 'current' && 'border-royal bg-royal text-white',
                state === 'todo' && 'border-line bg-white text-muted',
              )}
            >
              <span className="grid h-5 w-5 place-items-center rounded-full bg-white/80 text-xs text-navy">{state === 'done' ? <Check className="h-3 w-3" /> : i + 1}</span>
              {s}
            </Comp>
            {i < steps.length - 1 ? <ChevronRight className="h-4 w-4 text-line" aria-hidden /> : null}
          </li>
        );
      })}
    </ol>
  );
}

export function Pagination({ hasMore, onMore, onBack, canBack = false, loading = false, className }) {
  if (!hasMore && !canBack) return null;
  return (
    <div className={clsx('mt-4 flex items-center justify-between gap-2', className)}>
      <Button variant="secondary" size="sm" icon={ChevronLeft} disabled={!canBack} onClick={onBack}>Previous</Button>
      <Button variant="secondary" size="sm" disabled={!hasMore} loading={loading} onClick={onMore}>Next <ChevronRight className="h-4 w-4" /></Button>
    </div>
  );
}

/* ---------------------------------------------------------------- Progress */

export function ProgressBar({ value = 0, label, className, showValue = true }) {
  const pct = Math.max(0, Math.min(100, Number(value) <= 1 && Number(value) > 0 && !Number.isInteger(Number(value)) ? Number(value) * 100 : Number(value) || 0));
  return (
    <div className={className}>
      {label || showValue ? (
        <div className="mb-1 flex items-center justify-between text-xs text-muted">
          <span>{label}</span>
          {showValue ? <span className="tabular">{Math.round(pct)}%</span> : null}
        </div>
      ) : null}
      <div className="h-2 w-full overflow-hidden rounded-chip bg-surface" role="progressbar" aria-valuenow={Math.round(pct)} aria-valuemin={0} aria-valuemax={100} aria-label={label}>
        <div className="brand-gradient h-full rounded-chip transition-[width]" style={{ width: `${pct}%` }} />
      </div>
    </div>
  );
}

export function ProgressRing({ value = 0, size = 56, stroke = 6, children, className }) {
  const pct = Math.max(0, Math.min(100, Number(value) || 0));
  const r = (size - stroke) / 2;
  const c = 2 * Math.PI * r;
  return (
    <div className={clsx('relative inline-grid place-items-center', className)} style={{ width: size, height: size }} role="progressbar" aria-valuenow={Math.round(pct)}>
      <svg width={size} height={size} className="-rotate-90">
        <circle cx={size / 2} cy={size / 2} r={r} stroke="#D5E0EC" strokeWidth={stroke} fill="none" />
        <circle cx={size / 2} cy={size / 2} r={r} stroke="#11CDB5" strokeWidth={stroke} fill="none" strokeLinecap="round" strokeDasharray={c} strokeDashoffset={c * (1 - pct / 100)} />
      </svg>
      <div className="absolute text-xs font-bold text-navy tabular">{children ?? `${Math.round(pct)}%`}</div>
    </div>
  );
}

/* ---------------------------------------------------------------- Overlay */

export function Modal({ open, onClose, title, description, children, footer, size = 'md' }) {
  const ref = useRef(null);
  useEffect(() => {
    if (!open) return undefined;
    const onKey = (e) => e.key === 'Escape' && onClose?.();
    document.addEventListener('keydown', onKey);
    const prev = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    return () => {
      document.removeEventListener('keydown', onKey);
      document.body.style.overflow = prev;
    };
  }, [open, onClose]);
  if (!open) return null;
  return (
    <div className="fixed inset-0 z-50 flex items-end justify-center bg-navy/40 p-0 sm:items-center sm:p-4" onMouseDown={(e) => e.target === e.currentTarget && onClose?.()}>
      <div
        ref={ref}
        role="dialog"
        aria-modal="true"
        aria-labelledby="modal-title"
        className={clsx('card max-h-[90dvh] w-full overflow-y-auto rounded-b-none p-5 sm:rounded-card', size === 'lg' ? 'sm:max-w-3xl' : size === 'sm' ? 'sm:max-w-md' : 'sm:max-w-xl')}
      >
        <div className="mb-4 flex items-start justify-between gap-4">
          <div>
            <h2 id="modal-title" className="text-lg text-navy">{title}</h2>
            {description ? <p className="mt-0.5 text-sm text-muted">{description}</p> : null}
          </div>
          <button type="button" onClick={onClose} className="-m-2 grid h-11 w-11 place-items-center rounded-full text-muted hover:bg-surface hover:text-navy" aria-label="Close">
            <X className="h-5 w-5" />
          </button>
        </div>
        <div>{children}</div>
        {footer ? <div className="mt-5 flex flex-wrap justify-end gap-2">{footer}</div> : null}
      </div>
    </div>
  );
}

/** Confirm dialog with an optional reason field. */
export function ConfirmDialog({ open, onClose, onConfirm, title, description, confirmLabel = 'Confirm', tone = 'primary', reason = false, reasonLabel = 'Reason', loading = false, error }) {
  const [text, setText] = useState('');
  useEffect(() => {
    if (open) setText('');
  }, [open]);
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
          <Button variant={tone} loading={loading} disabled={reason && !text.trim()} onClick={() => onConfirm(reason ? text.trim() : undefined)}>{confirmLabel}</Button>
        </>
      }
    >
      <ErrorAlert error={error} className="mb-3" />
      {reason ? (
        <Field label={reasonLabel} required>
          {(p) => <Textarea {...p} value={text} onChange={(e) => setText(e.target.value)} rows={3} />}
        </Field>
      ) : null}
    </Modal>
  );
}

/* ---------------------------------------------------------------- Toasts */

const ToastContext = createContext(null);

export function ToastProvider({ children }) {
  const [toasts, setToasts] = useState([]);
  const dismiss = useCallback((id) => setToasts((t) => t.filter((x) => x.id !== id)), []);
  const push = useCallback((toast) => {
    const id = crypto.randomUUID();
    setToasts((t) => [...t, { id, tone: 'info', ...toast }]);
    setTimeout(() => dismiss(id), toast.duration ?? 5000);
  }, [dismiss]);
  const value = useMemo(() => ({
    push,
    success: (title, description) => push({ tone: 'success', title, description }),
    error: (error, title = 'Something went wrong') => push({ tone: 'danger', title, description: typeof error === 'string' ? error : errorMessage(error), duration: 8000 }),
    info: (title, description) => push({ tone: 'info', title, description }),
  }), [push]);
  return (
    <ToastContext.Provider value={value}>
      {children}
      <div className="pointer-events-none fixed inset-x-0 bottom-20 z-[60] flex flex-col items-center gap-2 px-4 sm:bottom-6 sm:items-end sm:px-6" aria-live="polite">
        {toasts.map((t) => (
          <div key={t.id} className="pointer-events-auto w-full max-w-sm">
            <Alert tone={t.tone} title={t.title} onClose={() => dismiss(t.id)}>{t.description}</Alert>
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  );
}

export function useToast() {
  const ctx = useContext(ToastContext);
  if (!ctx) throw new Error('useToast must be used inside <ToastProvider>');
  return ctx;
}

/* ---------------------------------------------------------------- Misc */

export function Avatar({ name, src, size = 40, className }) {
  const letters = String(name || '?').split(/\s+/).slice(0, 2).map((p) => p[0]?.toUpperCase() || '').join('');
  return src ? (
    <img src={src} alt={name} width={size} height={size} className={clsx('rounded-full object-cover', className)} />
  ) : (
    <span className={clsx('inline-grid shrink-0 place-items-center rounded-full bg-royal-50 font-bold text-royal', className)} style={{ width: size, height: size, fontSize: size * 0.4 }} aria-hidden>
      {letters}
    </span>
  );
}

export function Divider({ className }) {
  return <hr className={clsx('border-line', className)} />;
}

export function Money({ value, className, precise }) {
  const kobo = koboOf(value);
  if (kobo === null || kobo === undefined) return <span className={className}>—</span>;
  const naira = kobo / 100;
  const text = new Intl.NumberFormat('en-NG', { style: 'currency', currency: 'NGN', minimumFractionDigits: precise || naira % 1 ? 2 : 0, maximumFractionDigits: 2 }).format(naira);
  return <span className={clsx('tabular', className)}>{text}</span>;
}

export function Breadcrumb({ items }) {
  return (
    <nav aria-label="Breadcrumb" className="flex flex-wrap items-center gap-1 text-sm text-muted">
      {items.map((item, i) => (
        <span key={i} className="flex items-center gap-1">
          {item.to ? <Link to={item.to} className="hover:text-navy">{item.label}</Link> : <span className="text-navy">{item.label}</span>}
          {i < items.length - 1 ? <ChevronRight className="h-3.5 w-3.5" aria-hidden /> : null}
        </span>
      ))}
    </nav>
  );
}
