import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Download, FileText, Pencil, Receipt, Rocket, Users } from 'lucide-react';
import { Alert, Breadcrumb, Button, Card, CardHeader, ConfirmDialog, ErrorAlert, KeyValue, Loading, PageHeader, ProgressBar, QueryState, Stat, StatusPill, Table, Tabs, useToast } from '../../components/ui';
import { saveBlob } from '../../lib/api';
import { formatDate, formatDateTime, formatMoney, formatPercent, koboOf, relativeTime, titleCase } from '../../lib/format';
import { useQueryParams } from '../../lib/hooks';
import { REVIEW_STATUS_LABEL, programmeMemory, sponsorApi, sponsorKeys } from '../../api/sponsor';
import { useOrganisation } from './useOrganisation';
import { NoOrganisation, RoleHint } from './OrganisationSwitcher';
import { QuoteLines, quoteViewLines } from './QuotePanel';
import { statusLabel } from './ProgrammeCard';
import { PayFromWallet } from './PayFromWallet';

const TABS = [
  { value: 'overview', label: 'Overview' },
  { value: 'budget', label: 'Budget' },
  { value: 'funnel', label: 'Funnel' },
  { value: 'applicants', label: 'Shortlist' },
  { value: 'reports', label: 'Reports' },
];

/** /sponsor/programmes/:programmeId */
export default function ProgrammeDetail() {
  const org = useOrganisation();
  const { programmeId } = useParams();
  const [{ tab }, setParam] = useQueryParams({ tab: 'overview' });
  const orgId = org.organisationId;

  const programme = useQuery({
    queryKey: sponsorKeys.programme(orgId, programmeId),
    queryFn: async () => {
      const p = await sponsorApi.getProgramme(orgId, programmeId);
      programmeMemory.remember(orgId, programmeId);
      return p;
    },
    enabled: Boolean(orgId),
  });

  if (!org.organisation) return <div><PageHeader title="Programme" /><NoOrganisation /></div>;
  if (programme.isPending) return <Loading />;
  if (programme.isError) return <ErrorAlert error={programme.error} />;
  const p = programme.data;
  const editable = ['draft', 'quoted'].includes(p.status);

  return (
    <div className="space-y-6">
      <PageHeader
        breadcrumb={<Breadcrumb items={[{ to: '/sponsor/programmes', label: 'Programmes' }, { label: p.name }]} />}
        title={p.name}
        description={p.courseTitle ? `${p.courseTitle} · ${p.seats} seats · ${p.sponsorSharePercent}% sponsored` : 'No course chosen yet'}
        actions={
          <>
            <StatusPill status={p.status} label={statusLabel(p.status)} />
            {editable ? <Button variant="secondary" size="sm" icon={Pencil} to={`/sponsor/programmes/${p.programmeId}/edit`}>Edit</Button> : null}
          </>
        }
      />

      <Tabs tabs={TABS} value={tab} onChange={(v) => setParam('tab', v)} />

      {tab === 'overview' ? <Overview p={p} org={org} /> : null}
      {tab === 'budget' ? <Budget p={p} orgId={orgId} /> : null}
      {tab === 'funnel' ? <Funnel p={p} orgId={orgId} /> : null}
      {tab === 'applicants' ? <Shortlist p={p} org={org} /> : null}
      {tab === 'reports' ? <Reports p={p} orgId={orgId} /> : null}
    </div>
  );
}

function Overview({ p, org }) {
  const qc = useQueryClient();
  const toast = useToast();
  const [confirm, setConfirm] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  const gates = [
    ['Quote approved by your organisation', Boolean(p.quote?.approvedAt)],
    ['Plastoids programme review approved', p.reviewStatus === 'approved'],
    ['Funded (invoice paid or payment plan approved)', p.isFunded || p.paymentPlanApproved],
    ['Application window still open', !p.applicationsCloseAt || new Date(p.applicationsCloseAt) > new Date()],
  ];
  const ready = gates.every(([, ok]) => ok) && p.status !== 'published' && p.status !== 'closed';

  async function publish() {
    setBusy(true);
    setError(null);
    try {
      const res = await sponsorApi.publish(org.organisationId, p.programmeId);
      toast.success(`Published: ${res.seatsUnlocked} seats unlocked`);
      setConfirm(false);
      qc.invalidateQueries({ queryKey: sponsorKeys.programme(org.organisationId, p.programmeId) });
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  const quoteLines = quoteViewLines(p.quote);

  return (
    <div className="grid gap-6 lg:grid-cols-3">
      <Card className="lg:col-span-2">
        <CardHeader title="Campaign" description={p.objectives || 'No objectives recorded.'} />
        <KeyValue
          cols={3}
          items={[
            ['Status', <StatusPill status={p.status} label={statusLabel(p.status)} />],
            ['Plastoids review', <StatusPill status={p.reviewStatus} label={REVIEW_STATUS_LABEL[p.reviewStatus] || titleCase(p.reviewStatus)} />],
            ['Funding', <StatusPill status={p.isFunded ? 'funded' : p.paymentPlanApproved ? 'payment plan' : 'unfunded'} label={p.isFunded ? 'Funded' : p.paymentPlanApproved ? 'Payment plan approved' : 'Not funded'} />],
            ['Seats', `${p.seatsUnlocked} unlocked of ${p.seats}`],
            ['Tuition per seat', formatMoney(p.tuitionPerSeat)],
            ['Learner co-pay', koboOf(p.coPayPerSeat) ? formatMoney(p.coPayPerSeat) : 'None'],
            ['Budget (ex VAT)', formatMoney(p.budget)],
            ['Funded amount', formatMoney(p.fundedAmount)],
            ['Selection', titleCase(p.selectionMode)],
            ['Applications', p.applicationsCloseAt ? `${p.applicationsOpenAt ? formatDate(p.applicationsOpenAt) : 'On publish'} → ${formatDate(p.applicationsCloseAt)}` : 'Dates not set'],
            ['Programme', p.startsAt ? `${formatDate(p.startsAt)} → ${formatDate(p.endsAt)}` : 'Dates not set'],
            ['Published', p.publishedAt ? formatDateTime(p.publishedAt) : '—'],
            ['Eligibility', Object.keys(p.eligibilityRules || {}).length ? Object.entries(p.eligibilityRules).map(([k, v]) => `${k}: ${Array.isArray(v) ? v.join('/') : String(v)}`).join('; ') : 'Open to all adults'],
            ['Entry assessment', p.hasEntryAssessment ? 'Yes' : 'No'],
            ['Scoring rubric', p.hasScoringRubric ? 'Yes' : 'No'],
          ]}
        />
      </Card>

      <div className="space-y-4">
        <Card>
          <CardHeader title="Publish" description="Opens the opportunity to learners once every gate is met." />
          <ul className="space-y-2 text-sm">
            {gates.map(([label, ok]) => (
              <li key={label} className="flex items-center gap-2">
                <StatusPill tone={ok ? 'success' : 'warn'} status={ok ? 'done' : 'pending'} label={ok ? 'Met' : 'Pending'} />
                <span className={ok ? 'text-navy' : 'text-muted'}>{label}</span>
              </li>
            ))}
          </ul>
          {p.status === 'published' ? (
            <Alert tone="success" className="mt-4">Published {relativeTime(p.publishedAt)}; {p.seatsUnlocked} seats unlocked.</Alert>
          ) : p.status === 'closed' ? (
            <Alert tone="info" className="mt-4">Closed {formatDate(p.closedAt)}.</Alert>
          ) : (
            <div className="mt-4 space-y-2">
              <Button icon={Rocket} className="w-full" disabled={!ready || !org.isOwner} onClick={() => setConfirm(true)}>Publish opportunity</Button>
              <RoleHint org={org} />
              {!p.isFunded && !p.paymentPlanApproved && p.invoiceId ? <Button variant="secondary" size="sm" className="w-full" icon={Receipt} to={`/sponsor/invoices/${p.invoiceId}`}>Pay the invoice by transfer</Button> : null}
              {p.reviewStatus === 'changesRequested' ? <Alert tone="warn">Plastoids asked for changes to the campaign; edit and request a new quote.</Alert> : null}
            </div>
          )}
        </Card>

        {!p.isFunded && !p.paymentPlanApproved && p.invoiceId && p.status !== 'closed' ? <PayFromWallet org={org} programmeId={p.programmeId} invoiceId={p.invoiceId} outstanding={p.quote?.total} compact /> : null}

        {quoteLines ? (
          <Card>
            <CardHeader title={`Quote v${p.quote.version}`} description={p.quote.approvedAt ? `Approved ${formatDate(p.quote.approvedAt)}` : `Requested ${formatDate(p.quote.quotedAt)}`} action={p.invoiceId ? <Button size="sm" variant="ghost" icon={Receipt} to={`/sponsor/invoices/${p.invoiceId}`}>Invoice</Button> : null} />
            <QuoteLines {...quoteLines} />
          </Card>
        ) : (
          <Card className="text-sm text-muted">No quote yet. <Button size="sm" variant="secondary" className="mt-2" to={`/sponsor/programmes/${p.programmeId}/edit`}>Continue the builder</Button></Card>
        )}
      </div>

      <ConfirmDialog
        open={confirm}
        onClose={() => setConfirm(false)}
        onConfirm={publish}
        loading={busy}
        error={error}
        title="Publish this opportunity?"
        description={`${p.seats} seats on "${p.courseTitle}" become visible to learners and applications open immediately.`}
        confirmLabel="Publish"
      />
    </div>
  );
}

function Budget({ p, orgId }) {
  const q = useQuery({ queryKey: sponsorKeys.budget(orgId, p.programmeId), queryFn: () => sponsorApi.getBudget(orgId, p.programmeId) });
  return (
    <QueryState query={q} emptyWhen={() => false}>
      {(b) => {
        const total = koboOf(b.quoteTotal) || 0;
        const spent = (koboOf(b.seatFeesEarned) || 0) + (koboOf(b.managementFeesEarned) || 0) + (koboOf(b.tutorEarnings) || 0);
        return (
          <div className="space-y-6">
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
              <Stat label="Budget (ex VAT)" value={formatMoney(b.budget)} hint={`Quote total ${formatMoney(b.quoteTotal)}`} />
              <Stat label="Received" value={formatMoney(b.received)} hint={`Invoiced ${formatMoney(b.invoiced)}`} tone={b.isFunded ? 'teal' : 'warn'} />
              <Stat label="Funds held for the programme" value={formatMoney(b.fundsHeld)} hint={`${b.seatsFilled} of ${b.seatsUnlocked} seats filled`} tone="royal" />
            </div>
            <Card>
              <CardHeader title="Budget vs spend" description="Read from the ledger's programme sub-accounts." />
              <ProgressBar value={total ? (100 * spent) / total : 0} label={`Spent ${formatMoney(spent)} of ${formatMoney(b.quoteTotal)}`} />
              <KeyValue
                className="mt-5"
                cols={3}
                items={[
                  ['Seat fees earned', formatMoney(b.seatFeesEarned)],
                  ['Management fees earned', formatMoney(b.managementFeesEarned)],
                  ['Tutor earnings', formatMoney(b.tutorEarnings)],
                  ['Close-out refund', formatMoney(b.closeoutRefund)],
                  ['Funded', b.isFunded ? 'Yes' : 'No'],
                ]}
              />
            </Card>
            <Card>
              <CardHeader title="Monthly postings" description="Management fee and tutor earnings are posted per programme month." />
              <Table
                columns={[
                  { key: 'month', header: 'Month', render: (r) => `Month ${r.month}` },
                  { key: 'filledSeats', header: 'Filled seats', align: 'right' },
                  { key: 'managementFee', header: 'Management fee', align: 'right', render: (r) => formatMoney(r.managementFee) },
                  { key: 'tutorEarnings', header: 'Tutor earnings', align: 'right', render: (r) => formatMoney(r.tutorEarnings) },
                  { key: 'postedAt', header: 'Posted', render: (r) => formatDate(r.postedAt) },
                ]}
                rows={b.months || []}
                rowKey={(r) => r.month}
                empty={<p className="text-sm text-muted">Nothing posted yet. Months post once the programme starts and seats are filled.</p>}
              />
            </Card>
          </div>
        );
      }}
    </QueryState>
  );
}

function Funnel({ p, orgId }) {
  const q = useQuery({ queryKey: sponsorKeys.funnel(orgId, p.programmeId), queryFn: () => sponsorApi.getFunnel(orgId, p.programmeId), refetchInterval: 60_000 });
  const toast = useToast();
  const [busy, setBusy] = useState(false);
  async function csv() {
    setBusy(true);
    try {
      const { blob, fileName } = await sponsorApi.downloadFunnelCsv(orgId, p.programmeId);
      saveBlob(blob, fileName?.endsWith('.csv') ? fileName : `${p.name}-funnel.csv`);
    } catch (err) {
      toast.error(err);
    } finally {
      setBusy(false);
    }
  }
  return (
    <QueryState query={q} emptyWhen={() => false}>
      {(f) => {
        const applied = f.stages?.[0]?.count || 0;
        return (
          <div className="space-y-6">
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
              <Stat label="Seats filled" value={`${f.seatsFilled} / ${f.seats}`} hint={`${f.seatsOpen} open`} icon={Users} tone="teal" />
              <Stat label="Attendance" value={f.attendancePerSeat} hint={`${f.sessionsAttended} sessions attended · per filled seat`} />
              <Stat label="Average score" value={f.averageScore == null ? '—' : formatPercent(f.averageScore)} hint="Latest assessment per seat" tone="royal" />
            </div>
            <Card>
              <CardHeader title="Funnel" description={`Refreshed ${relativeTime(f.refreshedAt)} · updates as events arrive`} action={<Button size="sm" variant="secondary" icon={Download} loading={busy} onClick={csv}>CSV</Button>} />
              <div className="space-y-3">
                {f.stages.map((s) => (
                  <div key={s.stage} className="grid grid-cols-[110px_1fr_60px] items-center gap-3 text-sm">
                    <span className="capitalize text-navy">{s.stage}</span>
                    <ProgressBar value={applied ? (100 * s.count) / applied : 0} showValue={false} />
                    <span className="text-right tabular text-muted">{s.count} <span className="text-xs">({s.percentOfApplied}%)</span></span>
                  </div>
                ))}
              </div>
              {!applied ? <Alert tone="info" className="mt-4">No applications yet{p.status !== 'published' ? ': the opportunity is not published' : ''}.</Alert> : null}
            </Card>
          </div>
        );
      }}
    </QueryState>
  );
}

function Shortlist({ p, org }) {
  const [result, setResult] = useState(null);
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);
  const [confirm, setConfirm] = useState(false);
  const sponsorReviews = p.selectionMode === 'sponsor';

  async function approve() {
    setBusy(true);
    setError(null);
    try {
      setResult(await sponsorApi.approveShortlist(p.programmeId));
      setConfirm(false);
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="grid gap-6 lg:grid-cols-3">
      <Card className="lg:col-span-2">
        <CardHeader title="Shortlist sign-off" description="Plastoids screens applications (eligibility, entry assessment, reviewer scores) and records accept / reject / waitlist decisions. Offers go out after sign-off." />
        <Alert tone="info" className="mb-4">
          Applicant identities are not shown to sponsors (blind review). Track the numbers on the Funnel tab; Plastoids staff see individual applications.
        </Alert>
        {sponsorReviews ? (
          <>
            <p className="text-sm text-navy">Selection mode <span className="font-semibold">sponsor reviews</span>: an organisation owner approves the accepted shortlist, then Plastoids sends offers (seat reserved, 5-day expiry).</p>
            <div className="mt-4 space-y-2">
              <Button icon={Users} disabled={!org.isOwner || p.status !== 'published'} onClick={() => setConfirm(true)}>Approve accepted shortlist</Button>
              <RoleHint org={org} />
              {p.status !== 'published' ? <p className="text-xs text-muted">Available once the programme is published.</p> : null}
            </div>
          </>
        ) : (
          <p className="text-sm text-navy">Selection mode <span className="font-semibold">{titleCase(p.selectionMode)}</span>: Plastoids signs off the shortlist on your behalf. Nothing to do here.</p>
        )}
        <ErrorAlert error={error} className="mt-4" />
        {result ? (
          <Alert tone="success" className="mt-4" title={`Shortlist approved: ${result.acceptedCount} accepted`}>
            {result.delegated ? 'Recorded as a delegated approval. ' : ''}Approved {formatDateTime(result.approvedAt)}. Offers are sent by Plastoids staff.
          </Alert>
        ) : null}
      </Card>
      <Card>
        <CardHeader title="Seats" />
        <KeyValue cols={1} items={[['Unlocked', `${p.seatsUnlocked} of ${p.seats}`], ['Co-pay per seat', koboOf(p.coPayPerSeat) ? formatMoney(p.coPayPerSeat) : 'None'], ['Applications close', formatDate(p.applicationsCloseAt)]]} />
      </Card>
      <ConfirmDialog open={confirm} onClose={() => setConfirm(false)} onConfirm={approve} loading={busy} error={error} title="Approve the accepted shortlist?" description="Every application currently marked accepted is confirmed; Plastoids then sends offers." confirmLabel="Approve" />
    </div>
  );
}

function Reports({ p, orgId }) {
  const toast = useToast();
  const [busy, setBusy] = useState(null);
  async function download(kind) {
    setBusy(kind);
    try {
      const { blob, fileName } = kind === 'pdf' ? await sponsorApi.downloadImpactReport(orgId, p.programmeId) : await sponsorApi.downloadFunnelCsv(orgId, p.programmeId);
      saveBlob(blob, fileName && fileName.includes('.') ? fileName : `${p.name}-${kind === 'pdf' ? 'impact-report.pdf' : 'funnel.csv'}`);
    } catch (err) {
      toast.error(err);
    } finally {
      setBusy(null);
    }
  }
  return (
    <div className="grid gap-4 sm:grid-cols-2">
      <Card>
        <CardHeader title="Impact report (PDF)" description="Funnel, attendance, completion and scores. Generated now from live data." />
        <Button icon={FileText} loading={busy === 'pdf'} onClick={() => download('pdf')}>Download PDF</Button>
      </Card>
      <Card>
        <CardHeader title="Funnel export (CSV)" description="Stage counts, seats, attendance and average score for your own analysis." />
        <Button variant="secondary" icon={Download} loading={busy === 'csv'} onClick={() => download('csv')}>Download CSV</Button>
      </Card>
      <Alert tone="info" className="sm:col-span-2">Beneficiary certificates are issued to learners and verifiable at /verify; monthly reports arrive by email while the programme runs.</Alert>
    </div>
  );
}
