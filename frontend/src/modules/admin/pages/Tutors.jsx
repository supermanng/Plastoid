import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { GraduationCap } from 'lucide-react';
import { Alert, Button, Card, EmptyState, Field, Input, Loading, PageHeader, Select, StatusPill, Table } from '../../../shared/components/ui';
import { formatDateTime } from '../../../shared/lib/format';
import { useAction, useQueryParams } from '../../../shared/lib/hooks';
import { adminApi, adminKeys } from '../api/admin';
import { AdminError, DecisionDialog, Id, IdLookup, LimitSelect, UserLink } from './_shared';
import { VerificationQueue } from './Queues';

const DEFAULTS = { status: '', limit: '100', view: 'tutors' };
const STATUSES = ['', 'applied', 'in_review', 'verified', 'suspended', 'rejected'];

/** GET /admin/backoffice/tutors?status=&limit= → TutorAdminSummary[]; reviews via /admin/tutors/*. */
export default function Tutors() {
  const [params, setParam] = useQueryParams(DEFAULTS);
  const limit = Number(params.limit) || 100;
  const q = useQuery({ queryKey: adminKeys.tutors(`${params.status}|${limit}`), queryFn: () => adminApi.listTutors(params.status || undefined, limit), placeholderData: (prev) => prev });

  const [subject, setSubject] = useState(null); // tutor row for subject review
  const [score, setScore] = useState('');
  const [credentialId, setCredentialId] = useState(null);
  const reviewSubject = useAction(({ decision, note }) => adminApi.reviewSubject(subject.tutorId, decision, score === '' ? null : Number(score), note), {
    success: 'Subject review recorded',
    invalidate: [['admin', 'tutors'], adminKeys.verificationQueue],
    onSuccess: () => setSubject(null),
  });
  const reviewCredential = useAction(({ decision, note }) => adminApi.reviewCredential(credentialId, decision, note), {
    success: 'Credential review recorded',
    invalidate: [['admin', 'tutors'], adminKeys.verificationQueue],
    onSuccess: () => setCredentialId(null),
  });

  return (
    <div>
      <PageHeader title="Tutors" description="Verification stages (identity → credentials → demo lesson/subject → payout account), quality score and sessions delivered." />
      <div className="mb-4 flex gap-2">
        <Button size="sm" variant={params.view === 'tutors' ? 'primary' : 'secondary'} onClick={() => setParam('view', 'tutors')}>All tutors</Button>
        <Button size="sm" variant={params.view === 'queue' ? 'primary' : 'secondary'} onClick={() => setParam('view', 'queue')}>Pending checks</Button>
      </div>

      {params.view === 'queue' ? <VerificationQueue /> : (
        <>
          <Card className="mb-6">
            <div className="grid gap-3 md:grid-cols-[1fr_auto_2fr]">
              <Field label="Status">
                {(p) => <Select {...p} value={params.status} onChange={(e) => setParam('status', e.target.value)}>{STATUSES.map((s) => <option key={s} value={s}>{s ? s.replace('_', ' ') : 'Any status'}</option>)}</Select>}
              </Field>
              <Field label="Rows">{() => <LimitSelect value={limit} onChange={(v) => setParam('limit', String(v))} />}</Field>
              <IdLookup label="Review a credential by id" placeholder="credential GUID" buttonLabel="Review" onOpen={(id) => setCredentialId(id)} />
            </div>
          </Card>
          {q.isPending ? <Loading /> : null}
          {q.isError ? <AdminError error={q.error} /> : null}
          {q.data ? (
            q.data.length ? (
              <Table
                rows={q.data}
                rowKey={(r) => r.tutorId}
                columns={[
                  { key: 'userId', header: 'Tutor', render: (r) => <><UserLink id={r.userId} /><div className="text-[11px] text-muted">profile <Id value={r.tutorId} /></div></> },
                  { key: 'status', header: 'Status', render: (r) => <StatusPill status={r.status} /> },
                  { key: 'level', header: 'Level', render: (r) => <span className="capitalize">{r.level}</span> },
                  { key: 'identityPassed', header: 'Identity', render: (r) => (r.identityPassed ? <StatusPill tone="success" status="passed" /> : <StatusPill tone="warn" status="pending" />) },
                  { key: 'qualityScore', header: 'Quality', align: 'right', render: (r) => Number(r.qualityScore).toFixed(1) },
                  { key: 'sessionsDelivered', header: 'Sessions', align: 'right' },
                  { key: 'verifiedAt', header: 'Verified', render: (r) => <span className="text-xs text-muted">{r.verifiedAt ? formatDateTime(r.verifiedAt) : '—'}</span> },
                  { key: 'act', header: '', align: 'right', render: (r) => <Button size="sm" variant="secondary" onClick={() => { setScore(''); setSubject(r); }}>Subject review</Button> },
                ]}
              />
            ) : (
              <EmptyState icon={GraduationCap} title="No tutors" description="Tutors appear once they apply to teach." />
            )
          ) : null}
          <Alert tone="info" className="mt-4">Subject review approves the demo lesson. Credential decisions are made per upload from the Pending checks tab, or by id above.</Alert>
        </>
      )}

      <DecisionDialog
        open={Boolean(subject)}
        onClose={() => setSubject(null)}
        title="Subject review"
        description={subject ? `Tutor profile ${subject.tutorId}` : ''}
        options={[{ value: 'approved', label: 'Approve', tone: 'teal' }, { value: 'rejected', label: 'Reject', tone: 'danger' }]}
        loading={reviewSubject.isPending}
        error={reviewSubject.error}
        onSubmit={(decision, note) => reviewSubject.mutate({ decision, note })}
        extra={() => (
          <Field label="Score (0–100)" hint="Optional.">
            {(p) => <Input {...p} type="number" min={0} max={100} value={score} onChange={(e) => setScore(e.target.value)} />}
          </Field>
        )}
      />
      <DecisionDialog
        open={Boolean(credentialId)}
        onClose={() => setCredentialId(null)}
        title="Review credential"
        description={credentialId ? `Credential ${credentialId}` : ''}
        options={[{ value: 'approved', label: 'Approve', tone: 'teal' }, { value: 'rejected', label: 'Reject', tone: 'danger' }]}
        loading={reviewCredential.isPending}
        error={reviewCredential.error}
        onSubmit={(decision, note) => reviewCredential.mutate({ decision, note })}
      />
    </div>
  );
}
