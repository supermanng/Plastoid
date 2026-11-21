import { Fragment, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { ChevronDown, ChevronRight, History } from 'lucide-react';
import { Button, Card, EmptyState, Field, Input, Loading, PageHeader, Pagination } from '../../components/ui';
import { formatDateTime } from '../../lib/format';
import { useCursor, useDebounce, useQueryParams } from '../../lib/hooks';
import { adminApi, adminKeys } from '../../api/admin';
import { AdminError, DiffView, Id, UserLink } from './_shared';

const DEFAULTS = { actorId: '', entity: '', entityId: '', from: '', to: '' };

/**
 * Immutable audit log. Uses GET /admin/backoffice/audit-log (BackOffice view over Audit.Contracts.IAuditLogReader:
 * AuditLogPage { items: AuditLogEntry[], nextCursor }). /admin/audit-events returns the same entry shape via Page<AuditEventItem>
 * but lacks the from/to filters, so the back-office route is used.
 */
export default function Audit() {
  const [params, setParam] = useQueryParams(DEFAULTS);
  const debounced = useDebounce(params, 350);
  const cursor = useCursor();
  const filters = {
    actorId: debounced.actorId || undefined,
    entity: debounced.entity || undefined,
    entityId: debounced.entityId || undefined,
    from: debounced.from ? new Date(debounced.from).toISOString() : undefined,
    to: debounced.to ? new Date(`${debounced.to}T23:59:59`).toISOString() : undefined,
    cursor: cursor.cursor || undefined,
    limit: 50,
  };
  const q = useQuery({ queryKey: adminKeys.auditLog(filters), queryFn: () => adminApi.auditLog(filters), placeholderData: (prev) => prev });

  return (
    <div>
      <PageHeader title="Audit log" description="Every audited command, newest first, with before/after snapshots. Rows cannot be edited or deleted." />
      <Card className="mb-6">
        <div className="grid gap-3 md:grid-cols-5">
          <Field label="Actor id">{(p) => <Input {...p} className="font-mono text-xs" value={params.actorId} onChange={(e) => { setParam('actorId', e.target.value); cursor.reset(); }} placeholder="user GUID" />}</Field>
          <Field label="Entity type">{(p) => <Input {...p} value={params.entity} onChange={(e) => { setParam('entity', e.target.value); cursor.reset(); }} placeholder="e.g. User, PayoutBatch" />}</Field>
          <Field label="Entity id">{(p) => <Input {...p} className="font-mono text-xs" value={params.entityId} onChange={(e) => { setParam('entityId', e.target.value); cursor.reset(); }} placeholder="GUID" />}</Field>
          <Field label="From">{(p) => <Input {...p} type="date" value={params.from} onChange={(e) => { setParam('from', e.target.value); cursor.reset(); }} />}</Field>
          <Field label="To">{(p) => <Input {...p} type="date" value={params.to} onChange={(e) => { setParam('to', e.target.value); cursor.reset(); }} />}</Field>
        </div>
      </Card>
      <AuditTable query={q} />
      <Pagination hasMore={Boolean(q.data?.nextCursor)} canBack={cursor.canBack} onMore={() => cursor.next(q.data?.nextCursor)} onBack={cursor.back} loading={q.isFetching} />
    </div>
  );
}

/** Reusable table; `query` is a React Query result holding AuditLogPage. */
export function AuditTable({ query, compact = false }) {
  const [open, setOpen] = useState(null);
  if (query.isPending) return <Loading />;
  if (query.isError) return <AdminError error={query.error} />;
  const items = query.data?.items || [];
  if (!items.length) return <EmptyState icon={History} title="No audit entries" description="Nothing matches these filters." className={compact ? 'py-6' : ''} />;
  return (
    <div className="overflow-x-auto rounded-card border border-line">
      <table className="w-full min-w-[640px] text-sm">
        <thead className="bg-surface text-left text-xs uppercase tracking-wide text-muted">
          <tr>
            <th className="w-8 px-2 py-2" />
            <th className="px-3 py-2 font-semibold">When</th>
            <th className="px-3 py-2 font-semibold">Action</th>
            <th className="px-3 py-2 font-semibold">Entity</th>
            <th className="px-3 py-2 font-semibold">Actor</th>
            {!compact ? <th className="px-3 py-2 font-semibold">IP / correlation</th> : null}
          </tr>
        </thead>
        <tbody>
          {items.map((e) => {
            const expanded = open === e.id;
            return (
              <Fragment key={e.id}>
                <tr className="border-t border-line hover:bg-surface/60">
                  <td className="px-2 py-2">
                    <Button variant="ghost" size="sm" className="h-8 w-8 p-0" aria-expanded={expanded} aria-label={expanded ? 'Collapse' : 'Expand'} onClick={() => setOpen(expanded ? null : e.id)}>
                      {expanded ? <ChevronDown className="h-4 w-4" /> : <ChevronRight className="h-4 w-4" />}
                    </Button>
                  </td>
                  <td className="whitespace-nowrap px-3 py-2 text-xs text-muted">{formatDateTime(e.occurredAt)}</td>
                  <td className="px-3 py-2 font-mono text-xs">{e.action}</td>
                  <td className="px-3 py-2"><span className="text-xs">{e.entity}</span> <Id value={e.entityId} /></td>
                  <td className="px-3 py-2">{e.actorId ? <UserLink id={e.actorId} /> : <span className="text-xs text-muted">system</span>}</td>
                  {!compact ? <td className="px-3 py-2 text-[11px] text-muted">{e.ip || '—'}<br />{e.correlationId || ''}</td> : null}
                </tr>
                {expanded ? (
                  <tr className="border-t border-line bg-surface/40">
                    <td colSpan={compact ? 5 : 6} className="px-4 py-3">
                      <DiffView before={e.before} after={e.after} />
                    </td>
                  </tr>
                ) : null}
              </Fragment>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
