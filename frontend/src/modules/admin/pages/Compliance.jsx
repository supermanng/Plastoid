import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Award, ShieldCheck } from 'lucide-react';
import { Alert, Button, Card, CardHeader, ConfirmDialog, EmptyState, Field, KeyValue, Loading, PageHeader, Select, StatusPill, Table, Tabs } from '../../../shared/components/ui';
import { formatDate, formatDateTime } from '../../../shared/lib/format';
import { useAction, useQueryParams } from '../../../shared/lib/hooks';
import { adminApi, adminKeys } from '../api/admin';
import { AdminError, Id, IdLookup, SeverityPill, UserLink } from './_shared';

const DEFAULTS = { tab: 'data-requests', status: 'pending' };
const TABS = [
  { value: 'data-requests', label: 'Data requests (NDPA)' },
  { value: 'certificates', label: 'Certificates' },
  { value: 'risk', label: 'Risk alerts' },
];

export default function Compliance() {
  const [params, setParam] = useQueryParams(DEFAULTS);
  return (
    <div>
      <PageHeader title="Compliance" description="Data-subject requests, certificate revocation and open risk alerts." />
      <Tabs tabs={TABS} value={params.tab} onChange={(v) => setParam('tab', v)} className="mb-6" />
      {params.tab === 'data-requests' ? <DataRequests status={params.status} setStatus={(s) => setParam('status', s)} /> : null}
      {params.tab === 'certificates' ? <Certificates /> : null}
      {params.tab === 'risk' ? <RiskAlerts /> : null}
    </div>
  );
}

/** GET /admin/backoffice/data-requests?status= → DataRequestSummary[] (exports and erasures run as background jobs). */
function DataRequests({ status, setStatus }) {
  const q = useQuery({ queryKey: adminKeys.dataRequests(status), queryFn: () => adminApi.dataRequests(status === 'all' ? undefined : status), placeholderData: (prev) => prev });
  return (
    <div>
      <Card className="mb-4">
        <Field label="Status" className="max-w-xs">{(p) => <Select {...p} value={status} onChange={(e) => setStatus(e.target.value)}>{['pending', 'processing', 'completed', 'failed', 'all'].map((s) => <option key={s} value={s}>{s[0].toUpperCase() + s.slice(1)}</option>)}</Select>}</Field>
      </Card>
      {q.isPending ? <Loading /> : null}
      {q.isError ? <AdminError error={q.error} /> : null}
      {q.data ? (
        q.data.length ? (
          <Table
            rows={q.data}
            rowKey={(r) => r.requestId}
            columns={[
              { key: 'requestId', header: 'Request', render: (r) => <Id value={r.requestId} /> },
              { key: 'userId', header: 'User', render: (r) => <UserLink id={r.userId} /> },
              { key: 'status', header: 'Status', render: (r) => <StatusPill status={r.status} /> },
              { key: 'requestedAt', header: 'Requested', render: (r) => <span className="text-xs text-muted">{formatDateTime(r.requestedAt)}</span> },
              { key: 'completedAt', header: 'Completed', render: (r) => <span className="text-xs text-muted">{r.completedAt ? formatDateTime(r.completedAt) : '—'}</span> },
              { key: 'attempts', header: 'Attempts', align: 'right' },
              { key: 'failureReason', header: 'Failure', render: (r) => <span className="text-xs text-danger">{r.failureReason || ''}</span> },
            ]}
          />
        ) : (
          <EmptyState icon={ShieldCheck} title="No data requests" description="Exports (JSON + PDF within 24 h) and erasures requested by users appear here." />
        )
      ) : null}
      <Alert tone="info" className="mt-4">Requests are fulfilled by the background job; failed ones retry automatically. There is no manual retry endpoint yet.</Alert>
    </div>
  );
}

/** Verify by public code (GET /verify/{code}) and revoke by certificate id (POST /admin/certificates/{id}/revoke { reason }). */
function Certificates() {
  const [code, setCode] = useState('');
  const [revokeId, setRevokeId] = useState(null);
  const [last, setLast] = useState(null);
  const verify = useQuery({ queryKey: adminKeys.verify(code), queryFn: () => adminApi.verifyCertificate(code), enabled: Boolean(code), retry: false });
  const revoke = useAction((reason) => adminApi.revokeCertificate(revokeId, reason), { success: 'Certificate revoked', invalidate: [['verify']], onSuccess: (r) => { setLast(r); setRevokeId(null); } });
  const v = verify.data;
  return (
    <div className="grid gap-6 lg:grid-cols-2">
      <Card>
        <CardHeader title="Look up a certificate" description="Same check the public verification page runs." />
        <IdLookup label="Public code" placeholder="e.g. PLT-7F3K-9Q2M" buttonLabel="Verify" onOpen={setCode} />
        {code && verify.isPending ? <Loading /> : null}
        {code && verify.isError ? (verify.error?.status === 404 ? <Alert tone="warn" className="mt-4">No certificate with that code.</Alert> : <AdminError error={verify.error} className="mt-4" />) : null}
        {v ? (
          <div className="mt-4">
            <StatusPill status={v.status} />
            <KeyValue className="mt-3" cols={2} items={[['Holder', v.learnerName], ['Course', v.courseTitle], ['Issued', formatDate(v.issuedAt)], ['Final score', v.finalScore != null ? `${v.finalScore}%` : '—'], ['Hash matches', v.hashMatches ? 'Yes' : 'No'], v.revokedAt ? ['Revoked', formatDateTime(v.revokedAt)] : null, ['Hash', <code className="break-all text-xs">{v.hash}</code>]]} />
          </div>
        ) : null}
      </Card>
      <Card>
        <CardHeader title="Revoke a certificate" description="The record is kept; the public page shows it as revoked. Use the certificate id (not the public code) from the learner's record or the audit log." action={<Award className="h-5 w-5 text-cyan" />} />
        <IdLookup label="Certificate id" placeholder="GUID" buttonLabel="Revoke…" onOpen={setRevokeId} />
        {last ? <KeyValue className="mt-4" cols={3} items={[['Certificate', <Id value={last.certificateId} />], ['Public code', last.publicCode], ['Revoked at', formatDateTime(last.revokedAt)]]} /> : null}
      </Card>
      <ConfirmDialog open={Boolean(revokeId)} onClose={() => setRevokeId(null)} title="Revoke this certificate?" description={`Certificate ${revokeId}. Verification will show it as revoked from now on.`} confirmLabel="Revoke" tone="danger" reason loading={revoke.isPending} error={revoke.error} onConfirm={(reason) => revoke.mutate(reason)} />
    </div>
  );
}

/** Risk alerts come from the dashboard payload; resolving posts to /admin/backoffice/risk-alerts/{id}/resolve. */
function RiskAlerts() {
  const q = useQuery({ queryKey: adminKeys.dashboard, queryFn: adminApi.dashboard });
  const [alert, setAlert] = useState(null);
  const resolve = useAction((reason) => adminApi.resolveRiskAlert(alert.alertId, reason), { success: 'Risk alert resolved', invalidate: [adminKeys.dashboard], onSuccess: () => setAlert(null) });
  if (q.isPending) return <Loading />;
  if (q.isError) return <AdminError error={q.error} />;
  const alerts = q.data?.riskAlerts || [];
  return (
    <Card>
      <CardHeader title={`Open risk alerts (${alerts.length})`} />
      {alerts.length ? (
        <Table
          rows={alerts}
          rowKey={(r) => r.alertId}
          columns={[
            { key: 'severity', header: 'Severity', render: (r) => <SeverityPill severity={r.severity} /> },
            { key: 'kind', header: 'Kind', render: (r) => <span className="font-mono text-xs">{r.kind}</span> },
            { key: 'message', header: 'Message' },
            { key: 'referenceId', header: 'Ref', render: (r) => <Id value={r.referenceId} /> },
            { key: 'raisedAt', header: 'Raised', render: (r) => <span className="text-xs text-muted">{formatDateTime(r.raisedAt)}</span> },
            { key: 'act', header: '', align: 'right', render: (r) => <Button size="sm" variant="secondary" onClick={() => setAlert(r)}>Resolve</Button> },
          ]}
        />
      ) : (
        <EmptyState icon={ShieldCheck} title="No open risk alerts" />
      )}
      <ConfirmDialog open={Boolean(alert)} onClose={() => setAlert(null)} title="Resolve risk alert" description={alert ? `${alert.kind}: ${alert.message}` : ''} confirmLabel="Resolve" reason reasonLabel="Resolution" loading={resolve.isPending} error={resolve.error} onConfirm={(reason) => resolve.mutate(reason)} />
    </Card>
  );
}
