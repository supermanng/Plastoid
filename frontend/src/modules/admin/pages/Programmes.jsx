import { useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { FolderKanban } from 'lucide-react';
import { Alert, Card, CardHeader, EmptyState, Loading, PageHeader, Stat, StatusPill, Table } from '../../../shared/components/ui';
import { formatDate, formatMoney } from '../../../shared/lib/format';
import { adminApi, adminKeys } from '../api/admin';
import { AdminError, Id, IdLookup } from './_shared';

/**
 * Staff view of programmes. The API has no staff list of programmes: published ones are read from the public
 * GET /sponsorship/opportunities, counts from the dashboard, and any programme (draft, quoted, funded…) opens by id.
 */
export default function Programmes() {
  const navigate = useNavigate();
  const dash = useQuery({ queryKey: adminKeys.dashboard, queryFn: adminApi.dashboard });
  const open = useQuery({ queryKey: adminKeys.opportunities, queryFn: () => adminApi.publishedOpportunities(100) });
  const prog = dash.data?.programmes || {};

  return (
    <div>
      <PageHeader title="Programmes" description="Review funded campaigns, publish on a sponsor's behalf, manage seats and run the application review." />
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <Stat label="Funded" value={prog.funded ?? '—'} />
        <Stat label="Published" value={prog.published ?? '—'} tone="teal" />
        <Stat label="Enrolments" value={prog.enrolmentsCreated ?? '—'} />
        <Stat label="Completed" value={prog.enrolmentsCompleted ?? '—'} />
      </div>

      <Card className="mt-6">
        <CardHeader title="Open a programme" description="Paste the programme id from the sponsor's notification, the review request or the audit log." />
        <IdLookup label="Programme id" placeholder="programme GUID" onOpen={(id) => navigate(`/programmes/${id}`)} />
      </Card>

      <Card className="mt-6">
        <CardHeader title="Published, accepting applications" description="From the public opportunities listing." />
        {open.isPending ? <Loading /> : null}
        {open.isError ? <AdminError error={open.error} /> : null}
        {open.data ? (
          (open.data.items || []).length ? (
            <Table
              rows={open.data.items}
              rowKey={(r) => r.programmeId}
              onRowClick={(r) => navigate(`/programmes/${r.programmeId}`, { state: { opportunity: r } })}
              columns={[
                { key: 'name', header: 'Programme', render: (r) => <><div className="font-semibold">{r.name}</div><div className="text-[11px] text-muted">{r.sponsorName} · {r.courseTitle || 'custom'} · <Id value={r.programmeId} /></div></> },
                { key: 'seats', header: 'Seats', render: (r) => <span className="tabular">{r.seats - r.seatsOpen}/{r.seats} filled</span> },
                { key: 'share', header: 'Funding', render: (r) => <StatusPill tone="success" status="sponsored" label={`${r.sponsorSharePercent}% · co-pay ${formatMoney(r.coPayPerSeat)}`} /> },
                { key: 'assessment', header: 'Entry test', render: (r) => (r.hasEntryAssessment ? 'Yes' : 'No') },
                { key: 'applicationsCloseAt', header: 'Closes', render: (r) => <span className="text-xs text-muted">{formatDate(r.applicationsCloseAt)}</span> },
                { key: 'startsAt', header: 'Runs', render: (r) => <span className="text-xs text-muted">{formatDate(r.startsAt)} → {formatDate(r.endsAt)}</span> },
              ]}
            />
          ) : (
            <EmptyState icon={FolderKanban} title="Nothing published" description="Programmes appear here once a sponsor (or staff) publishes them." />
          )
        ) : null}
      </Card>

      <Alert tone="info" className="mt-6" title="Backend gap">
        Draft, quoted and funded programmes awaiting review are not listed by any staff endpoint; the dashboard only counts them. Open them by id above.
      </Alert>
    </div>
  );
}
