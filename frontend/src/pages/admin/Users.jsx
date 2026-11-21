import { useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Search, Users as UsersIcon } from 'lucide-react';
import { Card, EmptyState, Field, Input, Loading, PageHeader, Select, StatusPill, Table } from '../../components/ui';
import { formatDate, formatDateTime } from '../../lib/format';
import { useDebounce, useQueryParams } from '../../lib/hooks';
import { adminApi, adminKeys } from '../../api/admin';
import { AdminError, Id, LimitSelect } from './_shared';

const DEFAULTS = { query: '', role: '', status: '', limit: '50' };
const ROLES = ['', 'learner', 'tutor', 'sponsor', 'staff'];
const STATUSES = ['', 'active', 'suspended', 'inactive', 'deleted'];

/** GET /admin/backoffice/users?query=&role=&status=&limit= → UserAdminSummary[] (no cursor; limit caps the result). */
export default function Users() {
  const navigate = useNavigate();
  const [params, setParam] = useQueryParams(DEFAULTS);
  const query = useDebounce(params.query, 350);
  const limit = Number(params.limit) || 50;

  const q = useQuery({
    queryKey: adminKeys.users({ query, role: params.role, status: params.status, limit }),
    queryFn: () => adminApi.searchUsers({ query: query || undefined, role: params.role || undefined, status: params.status || undefined, limit }),
    placeholderData: (prev) => prev,
  });

  return (
    <div>
      <PageHeader title="Users" description="Search accounts by name, email or id. Suspending revokes sessions and is audited with a before/after snapshot." />
      <Card className="mb-6">
        <div className="grid gap-3 md:grid-cols-[2fr_1fr_1fr_auto]">
          <Field label="Search">
            {(p) => (
              <div className="relative">
                <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted" aria-hidden />
                <Input {...p} className="pl-9" placeholder="Name, email or user id" value={params.query} onChange={(e) => setParam('query', e.target.value)} />
              </div>
            )}
          </Field>
          <Field label="Role">
            {(p) => <Select {...p} value={params.role} onChange={(e) => setParam('role', e.target.value)}>{ROLES.map((r) => <option key={r} value={r}>{r ? r[0].toUpperCase() + r.slice(1) : 'Any role'}</option>)}</Select>}
          </Field>
          <Field label="Status">
            {(p) => <Select {...p} value={params.status} onChange={(e) => setParam('status', e.target.value)}>{STATUSES.map((s) => <option key={s} value={s}>{s ? s[0].toUpperCase() + s.slice(1) : 'Any status'}</option>)}</Select>}
          </Field>
          <Field label="Rows">{() => <LimitSelect value={limit} onChange={(v) => setParam('limit', String(v))} />}</Field>
        </div>
      </Card>

      {q.isPending ? <Loading /> : null}
      {q.isError ? <AdminError error={q.error} /> : null}
      {q.data ? (
        q.data.length ? (
          <>
            <Table
              rows={q.data}
              rowKey={(r) => r.userId}
              onRowClick={(r) => navigate(`/admin/users/${r.userId}`)}
              columns={[
                { key: 'displayName', header: 'Name', render: (r) => <><div className="font-semibold">{r.displayName}</div><div className="text-[11px] text-muted">{r.email || 'no email'} · <Id value={r.userId} /></div></> },
                { key: 'roles', header: 'Roles', render: (r) => <div className="flex flex-wrap gap-1">{(r.roles || []).map((role) => <span key={role} className="rounded-chip bg-surface px-2 py-0.5 text-[11px] font-semibold capitalize text-muted">{role}</span>)}</div> },
                { key: 'status', header: 'Status', render: (r) => <StatusPill status={r.status} /> },
                { key: 'mfaEnabled', header: 'MFA', render: (r) => (r.mfaEnabled ? <StatusPill tone="success" status="on" label="Enabled" /> : <StatusPill tone="neutral" status="off" label="Off" />) },
                { key: 'createdAt', header: 'Joined', render: (r) => <span className="text-xs text-muted">{formatDate(r.createdAt)}</span> },
                { key: 'lastLoginAt', header: 'Last sign-in', render: (r) => <span className="text-xs text-muted">{r.lastLoginAt ? formatDateTime(r.lastLoginAt) : 'never'}</span> },
              ]}
            />
            {q.data.length >= limit ? <p className="mt-2 text-xs text-muted">Showing the first {limit}. Narrow the search or raise the row count.</p> : null}
          </>
        ) : (
          <EmptyState icon={UsersIcon} title="No accounts match" description="Try a different name, email or id, or clear the filters." />
        )
      ) : null}
    </div>
  );
}
