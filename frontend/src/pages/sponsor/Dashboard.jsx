import { useQueries } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { ArrowRight, FolderKanban, Plus, Receipt, Sparkles, Users } from 'lucide-react';
import { Alert, Button, Card, CardHeader, EmptyState, ProgressBar, Skeleton, Stat, StatusPill } from '../../components/ui';
import { formatMoney, formatPercent, koboOf, relativeTime } from '../../lib/format';
import { programmeMemory, sponsorApi, sponsorKeys } from '../../api/sponsor';
import { useOrganisation } from './useOrganisation';
import { NoOrganisation, OrganisationSwitcher, VerificationBanner } from './OrganisationSwitcher';
import { ProgrammeSummary, statusLabel } from './ProgrammeCard';

/*
 * /sponsor — programme cards, funnel + budget per programme, alerts and a first-time CTA.
 * Programme ids come from the browser memory (see Programmes.jsx: no list endpoint exists yet).
 */
export default function Dashboard() {
  const org = useOrganisation();
  const orgId = org.organisationId;
  const ids = orgId ? programmeMemory.list(orgId) : [];

  const programmes = useQueries({
    queries: ids.map((id) => ({ queryKey: sponsorKeys.programme(orgId, id), queryFn: () => sponsorApi.getProgramme(orgId, id), retry: false })),
  });
  const loaded = programmes.filter((q) => q.data).map((q) => q.data);
  const published = loaded.filter((p) => p.status === 'published');

  const funnels = useQueries({
    queries: published.map((p) => ({ queryKey: sponsorKeys.funnel(orgId, p.programmeId), queryFn: () => sponsorApi.getFunnel(orgId, p.programmeId) })),
  });
  const budgets = useQueries({
    queries: loaded.filter((p) => p.quote).map((p) => ({ queryKey: sponsorKeys.budget(orgId, p.programmeId), queryFn: () => sponsorApi.getBudget(orgId, p.programmeId) })),
  });

  if (!org.organisation) {
    return (
      <div>
        <h1 className="mb-6 text-2xl sm:text-3xl">Sponsor overview</h1>
        <NoOrganisation />
      </div>
    );
  }

  const unfunded = loaded.filter((p) => p.quote?.approvedAt && !p.isFunded && !p.paymentPlanApproved);
  const awaitingQuoteApproval = loaded.filter((p) => p.status === 'quoted');
  const totals = {
    seats: loaded.reduce((n, p) => n + (p.seats || 0), 0),
    unlocked: loaded.reduce((n, p) => n + (p.seatsUnlocked || 0), 0),
    budgetKobo: loaded.reduce((n, p) => n + (koboOf(p.budget) || 0), 0),
    received: budgets.reduce((n, q) => n + (koboOf(q.data?.received) || 0), 0),
    filled: funnels.reduce((n, q) => n + (q.data?.seatsFilled || 0), 0),
    certified: funnels.reduce((n, q) => n + (q.data?.stages?.find((s) => s.stage === 'certified')?.count || 0), 0),
  };
  const loading = programmes.some((q) => q.isPending);

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h1 className="text-2xl sm:text-3xl">{org.organisation.name}</h1>
          <p className="mt-1 text-sm text-muted">Programmes, funding and impact at a glance.</p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <OrganisationSwitcher org={org} />
          <Button to="/sponsor/programmes/new" icon={Plus} disabled={!org.verified}>Create campaign</Button>
        </div>
      </div>

      <VerificationBanner org={org} />
      {unfunded.map((p) => (
        <Alert key={p.programmeId} tone="warn" title={`"${p.name}" is blocked until funded`}>
          The quote is approved but the invoice has not been paid. Pay by transfer to the invoice's virtual account; the campaign publishes once funds arrive.
          <div className="mt-2 flex gap-2">
            {p.invoiceId ? <Button size="sm" variant="secondary" to={`/sponsor/invoices/${p.invoiceId}`} icon={Receipt}>View invoice</Button> : null}
            <Button size="sm" variant="ghost" to={`/sponsor/programmes/${p.programmeId}`}>Open programme</Button>
          </div>
        </Alert>
      ))}
      {awaitingQuoteApproval.map((p) => (
        <Alert key={p.programmeId} tone="info" title={`Quote ready for "${p.name}"`}>
          {org.isFinance ? 'Approve the quote to receive the invoice.' : 'An owner or finance member must approve the quote.'}
          <div className="mt-2"><Button size="sm" variant="secondary" to={`/sponsor/programmes/${p.programmeId}`}>Review quote</Button></div>
        </Alert>
      ))}

      {ids.length === 0 ? (
        <Card className="brand-gradient p-0">
          <div className="m-[2px] rounded-[10px] bg-white p-6">
            <div className="flex items-center gap-2 text-sm font-semibold text-teal-600"><Sparkles className="h-4 w-4" /> First campaign</div>
            <h2 className="mt-2 text-xl">Fund seats in seven steps</h2>
            <ol className="mt-3 grid gap-2 text-sm text-navy sm:grid-cols-2 lg:grid-cols-4">
              {['Objectives', 'Course & offering', 'Seats & budget', 'Eligibility rules', 'Dates', 'Selection mode', 'Review & quote'].map((s, i) => (
                <li key={s} className="flex items-center gap-2"><span className="grid h-6 w-6 place-items-center rounded-full bg-surface text-xs font-bold text-muted">{i + 1}</span>{s}</li>
              ))}
            </ol>
            <div className="mt-4 flex flex-wrap gap-2">
              <Button to="/sponsor/programmes/new" disabled={!org.verified}>Start the guided builder</Button>
              <Button variant="ghost" to="/for-sponsors">How pricing works <ArrowRight className="h-4 w-4" /></Button>
            </div>
          </div>
        </Card>
      ) : null}

      {ids.length ? (
        <>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            <Stat label="Programmes" value={loading ? '…' : loaded.length} hint={`${published.length} published`} icon={FolderKanban} />
            <Stat label="Seats funded" value={loading ? '…' : `${totals.unlocked} / ${totals.seats}`} hint={`${totals.filled} filled`} icon={Users} tone="teal" />
            <Stat label="Received vs budget" value={loading ? '…' : formatMoney(totals.received)} hint={`of ${formatMoney(totals.budgetKobo)} budgeted`} icon={Receipt} tone="royal" />
          </div>

          <section>
            <div className="mb-3 flex items-center justify-between">
              <h2 className="text-lg">Programmes</h2>
              <Link to="/sponsor/programmes" className="text-sm font-semibold">All programmes</Link>
            </div>
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
              {programmes.map((q, i) => (q.isPending ? <Skeleton key={ids[i]} className="h-48" /> : q.data ? <ProgrammeSummary key={ids[i]} p={q.data} /> : null))}
            </div>
          </section>

          {published.length ? (
            <section className="grid gap-4 lg:grid-cols-2">
              {published.map((p, i) => <FunnelCard key={p.programmeId} p={p} funnel={funnels[i]} budget={budgets.find((b) => b.data?.programmeId === p.programmeId)} />)}
            </section>
          ) : null}
        </>
      ) : null}

      {ids.length && !loaded.length && !loading ? (
        <EmptyState title="Nothing to show" description="None of the remembered programmes could be loaded." action={<Button to="/sponsor/programmes" variant="secondary">Manage list</Button>} />
      ) : null}
    </div>
  );
}

function FunnelCard({ p, funnel, budget }) {
  const f = funnel?.data;
  const b = budget?.data;
  const applied = f?.stages?.[0]?.count || 0;
  return (
    <Card>
      <CardHeader
        title={p.name}
        description={f ? `Refreshed ${relativeTime(f.refreshedAt)}` : 'Loading funnel…'}
        action={<StatusPill status={p.status} label={statusLabel(p.status)} />}
      />
      {funnel?.isError ? <p className="text-sm text-danger">Funnel unavailable.</p> : null}
      {f ? (
        <div className="space-y-2">
          {f.stages.map((s) => (
            <ProgressBar key={s.stage} value={applied ? (100 * s.count) / applied : 0} label={`${s.stage} · ${s.count}`} showValue={false} />
          ))}
          <div className="mt-3 grid grid-cols-3 gap-2 text-center text-xs text-muted">
            <div><div className="text-base font-extrabold text-navy tabular">{f.seatsFilled}/{f.seats}</div>seats filled</div>
            <div><div className="text-base font-extrabold text-navy tabular">{f.attendancePerSeat}</div>sessions / seat</div>
            <div><div className="text-base font-extrabold text-navy tabular">{f.averageScore == null ? '—' : formatPercent(f.averageScore)}</div>avg score</div>
          </div>
        </div>
      ) : null}
      {b ? (
        <div className="mt-4 border-t border-line pt-3">
          <ProgressBar value={koboOf(b.quoteTotal) ? (100 * koboOf(b.received)) / koboOf(b.quoteTotal) : 0} label={`Received ${formatMoney(b.received)} of ${formatMoney(b.quoteTotal)}`} />
        </div>
      ) : null}
      <div className="mt-4"><Button size="sm" variant="secondary" to={`/sponsor/programmes/${p.programmeId}`}>Open programme</Button></div>
    </Card>
  );
}
