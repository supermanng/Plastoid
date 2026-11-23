import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Briefcase } from 'lucide-react';
import { Card, EmptyState, Field, Loading, PageHeader, Select } from '../../../shared/components/ui';
import { useAction, useQueryParams } from '../../../shared/lib/hooks';
import { adminApi, adminKeys } from '../api/admin';
import { AdminError, DecisionDialog, LimitSelect } from './_shared';
import { OrganisationTable } from './Queues';

const DEFAULTS = { status: '', limit: '100' };
const STATUSES = ['', 'pending', 'verified', 'rejected'];

/** GET /admin/backoffice/sponsors?verificationStatus=&limit= → OrganisationAdminSummary[]. Seat blocks per sponsor live under Seat blocks. */
export default function Sponsors() {
  const [params, setParam] = useQueryParams(DEFAULTS);
  const limit = Number(params.limit) || 100;
  const q = useQuery({ queryKey: adminKeys.sponsors(`${params.status}|${limit}`), queryFn: () => adminApi.listSponsors(params.status || undefined, limit), placeholderData: (prev) => prev });
  const [row, setRow] = useState(null);
  const decide = useAction(({ decision, note }) => adminApi.verifyOrganisation(row.organisationId, decision, note), {
    success: 'Verification decision recorded',
    invalidate: [['admin', 'sponsors'], adminKeys.verificationQueue],
    onSuccess: () => setRow(null),
  });

  return (
    <div>
      <PageHeader title="Sponsors" description="Organisations that registered to sponsor seats. Verification checks the CAC number and the authorised signatory." />
      <Card className="mb-6">
        <div className="grid gap-3 sm:grid-cols-[1fr_auto]">
          <Field label="Verification status">
            {(p) => <Select {...p} value={params.status} onChange={(e) => setParam('status', e.target.value)}>{STATUSES.map((s) => <option key={s} value={s}>{s ? s[0].toUpperCase() + s.slice(1) : 'Any'}</option>)}</Select>}
          </Field>
          <Field label="Rows">{() => <LimitSelect value={limit} onChange={(v) => setParam('limit', String(v))} />}</Field>
        </div>
      </Card>
      {q.isPending ? <Loading /> : null}
      {q.isError ? <AdminError error={q.error} /> : null}
      {q.data ? (
        q.data.length ? (
          <OrganisationTable rows={q.data} onDecide={(r) => setRow(r)} />
        ) : (
          <EmptyState icon={Briefcase} title="No organisations" description="Sponsors register from the public 'Sponsor' page." />
        )
      ) : null}
      <p className="mt-3 text-xs text-muted">The seats each organisation has bought are listed under <Link to="/seat-blocks" className="font-semibold text-royal">Seat blocks</Link>.</p>
      <DecisionDialog
        open={Boolean(row)}
        onClose={() => setRow(null)}
        title="Verify organisation"
        description={row ? `${row.name} · CAC ${row.cacNumber} · signatory ${row.signatoryName}` : ''}
        options={[{ value: 'verified', label: 'Verified', tone: 'teal' }, { value: 'rejected', label: 'Reject', tone: 'danger' }]}
        noteLabel="Reason"
        loading={decide.isPending}
        error={decide.error}
        onSubmit={(decision, note) => decide.mutate({ decision, note })}
      />
    </div>
  );
}
