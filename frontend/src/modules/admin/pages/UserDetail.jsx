import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Ban, RotateCcw } from 'lucide-react';
import { Alert, Breadcrumb, Button, Card, CardHeader, ConfirmDialog, KeyValue, Loading, PageHeader, StatusPill } from '../../../shared/components/ui';
import { formatDateTime } from '../../../shared/lib/format';
import { useAction } from '../../../shared/lib/hooks';
import { useAuth } from '../../../shared/lib/auth';
import { adminApi, adminKeys } from '../api/admin';
import { AdminError, Id } from './_shared';
import { AuditTable } from './Audit';

/** GET /admin/backoffice/users/{userId} → UserAdminSummary; suspend/reinstate; audit trail for and by the user. */
export default function UserDetail() {
  const { userId } = useParams();
  const auth = useAuth();
  const q = useQuery({ queryKey: adminKeys.user(userId), queryFn: () => adminApi.getUser(userId) });
  const [suspending, setSuspending] = useState(false);
  const [reinstating, setReinstating] = useState(false);

  const invalidate = [adminKeys.user(userId), ['admin', 'users'], ['admin', 'audit-log']];
  const suspend = useAction((reason) => adminApi.suspendUser(userId, reason), { success: 'Account suspended; sessions revoked', invalidate, onSuccess: () => setSuspending(false) });
  const reinstate = useAction(() => adminApi.reinstateUser(userId), { success: 'Account reinstated', invalidate, onSuccess: () => setReinstating(false) });

  const asActor = useQuery({ queryKey: adminKeys.auditLog({ actorId: userId, limit: 10 }), queryFn: () => adminApi.auditLog({ actorId: userId, limit: 10 }) });
  const asEntity = useQuery({ queryKey: adminKeys.auditLog({ entityId: userId, limit: 10 }), queryFn: () => adminApi.auditLog({ entityId: userId, limit: 10 }) });

  if (q.isPending) return <Loading />;
  if (q.isError) return <AdminError error={q.error} />;
  const u = q.data;
  const suspended = String(u.status).toLowerCase() === 'suspended';
  const isSelf = auth.me?.id && String(auth.me.id).toLowerCase() === String(u.userId).toLowerCase();

  return (
    <div>
      <PageHeader
        breadcrumb={<Breadcrumb items={[{ to: '/users', label: 'Users' }, { label: u.displayName }]} />}
        title={u.displayName}
        description={u.email || 'No email on file'}
        actions={
          suspended ? (
            <Button icon={RotateCcw} variant="teal" onClick={() => setReinstating(true)}>Reinstate</Button>
          ) : (
            <Button icon={Ban} variant="danger" disabled={isSelf} title={isSelf ? 'You cannot suspend yourself' : undefined} onClick={() => setSuspending(true)}>Suspend</Button>
          )
        }
      />

      {suspended ? <Alert tone="danger" title="This account is suspended" className="mb-6">Sign-ins are refused and existing sessions were revoked. Reinstating restores access immediately.</Alert> : null}

      <div className="grid gap-6 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader title="Account" />
          <KeyValue
            cols={3}
            items={[
              ['User id', <Id value={u.userId} />],
              ['Status', <StatusPill status={u.status} />],
              ['MFA', u.mfaEnabled ? 'Enabled' : 'Not enrolled'],
              ['Roles', <div className="flex flex-wrap gap-1">{(u.roles || []).map((r) => <span key={r} className="rounded-chip bg-surface px-2 py-0.5 text-[11px] font-semibold capitalize text-muted">{r}</span>)}{!u.roles?.length ? '—' : null}</div>],
              ['Joined', formatDateTime(u.createdAt)],
              ['Last sign-in', u.lastLoginAt ? formatDateTime(u.lastLoginAt) : 'never'],
            ]}
          />
          <p className="mt-4 text-xs text-muted">
            Phone, date of birth and identity numbers are encrypted and intentionally not shown here. Grant platform roles through <Link to="/role-grants" className="font-semibold text-royal">Role grants</Link> (maker/checker).
          </p>
        </Card>
        <Card>
          <CardHeader title="Quick links" />
          <ul className="space-y-2 text-sm">
            <li><Link className="text-royal hover:underline" to={`/audit?actorId=${u.userId}`}>Full audit trail as actor</Link></li>
            <li><Link className="text-royal hover:underline" to={`/audit?entityId=${u.userId}`}>Changes made to this account</Link></li>
            {u.roles?.includes('tutor') ? <li><Link className="text-royal hover:underline" to="/tutors">Tutor verification status</Link></li> : null}
            {u.roles?.includes('tutor') ? <li><Link className="text-royal hover:underline" to="/payouts">Payout queue (holds)</Link></li> : null}
          </ul>
        </Card>
      </div>

      <div className="mt-6 grid gap-6 xl:grid-cols-2">
        <Card>
          <CardHeader title="Recent actions by this user" />
          <AuditTable query={asActor} compact />
        </Card>
        <Card>
          <CardHeader title="Recent changes to this account" />
          <AuditTable query={asEntity} compact />
        </Card>
      </div>

      <ConfirmDialog open={suspending} onClose={() => setSuspending(false)} title={`Suspend ${u.displayName}?`} description="Their sessions are revoked at once. The reason is kept in the audit log." confirmLabel="Suspend" tone="danger" reason loading={suspend.isPending} error={suspend.error} onConfirm={(reason) => suspend.mutate(reason)} />
      <ConfirmDialog open={reinstating} onClose={() => setReinstating(false)} title={`Reinstate ${u.displayName}?`} description="Access is restored immediately." confirmLabel="Reinstate" tone="teal" loading={reinstate.isPending} error={reinstate.error} onConfirm={() => reinstate.mutate()} />
    </div>
  );
}
