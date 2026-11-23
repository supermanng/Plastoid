import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { ArrowRight, Armchair, Coins, Plus, Sparkles, Users } from 'lucide-react';
import { Button, Card, CardHeader, EmptyState, ErrorAlert, Skeleton, Stat } from '../../../shared/components/ui';
import { formatMoney } from '../../../shared/lib/format';
import { formatUnits } from '../../../shared/lib/units.jsx';
import { walletApi, walletKeys } from '../../../shared/api/wallet';
import { seatsApi, seatsKeys } from '../api/seats';
import { useOrganisation } from './useOrganisation';
import { NoOrganisation, OrganisationSwitcher, VerificationBanner } from './OrganisationSwitcher';
import { SeatBlockCard, totalsOf } from './Seats';

/** /sponsor — seat-block totals, the wallet balance and the latest blocks (ADR 0021). */
export default function Dashboard() {
  const org = useOrganisation();
  const orgId = org.organisationId;
  const blocks = useQuery({ queryKey: seatsKeys.blocks(orgId), queryFn: () => seatsApi.listBlocks(orgId), enabled: Boolean(orgId) });
  const wallet = useQuery({ queryKey: walletKeys.organisation(orgId), queryFn: () => walletApi.organisation(orgId), enabled: Boolean(orgId) });

  if (!org.organisation) {
    return (
      <div>
        <h1 className="mb-6 text-2xl sm:text-3xl">Sponsor overview</h1>
        <NoOrganisation />
      </div>
    );
  }

  const list = blocks.data || [];
  const totals = totalsOf(list);
  const latest = [...list].sort((a, b) => new Date(b.purchasedAt) - new Date(a.purchasedAt)).slice(0, 3);
  const loading = blocks.isPending;

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h1 className="text-2xl sm:text-3xl">{org.organisation.name}</h1>
          <p className="mt-1 text-sm text-muted">Seats, learners and units at a glance.</p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <OrganisationSwitcher org={org} />
          <Button to="/seats/new" icon={Plus} disabled={!org.isFinance}>Buy seats</Button>
        </div>
      </div>

      <VerificationBanner org={org} />
      <ErrorAlert error={blocks.error} />

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-5">
        <Stat label="Seat blocks" value={loading ? '…' : list.length} hint={loading ? '' : `${totals.active} active`} icon={Armchair} />
        <Stat label="Seats bought" value={loading ? '…' : totals.seats} hint={loading ? '' : `${totals.released} released`} />
        <Stat label="Given out" value={loading ? '…' : totals.allocated} hint={loading ? '' : `${totals.enrolled} enrolled · ${totals.invited} invited`} icon={Users} tone="teal" />
        <Stat label="Open seats" value={loading ? '…' : totals.open} hint="Still to give out" tone={totals.open ? 'royal' : 'default'} />
        <Stat
          label="Wallet"
          value={wallet.isPending ? '…' : wallet.isError ? '—' : formatUnits(wallet.data.balance)}
          hint={wallet.data ? `Worth ${formatMoney(wallet.data.balanceValue)}` : wallet.isError ? 'Unavailable' : ''}
          icon={Coins}
          tone="royal"
        />
      </div>

      {!loading && !list.length ? (
        <Card className="brand-gradient p-0">
          <div className="m-[2px] rounded-[10px] bg-white p-6">
            <div className="flex items-center gap-2 text-sm font-semibold text-teal-600"><Sparkles className="h-4 w-4" /> First seats</div>
            <h2 className="mt-2 text-xl">Sponsor learners in three steps</h2>
            <ol className="mt-3 grid gap-2 text-sm text-navy sm:grid-cols-3">
              {['Top up the organisation wallet with units', 'Pick a course cohort and buy seats', 'Give each seat to a learner by phone or email'].map((s, i) => (
                <li key={s} className="flex items-center gap-2"><span className="grid h-6 w-6 shrink-0 place-items-center rounded-full bg-surface text-xs font-bold text-muted">{i + 1}</span>{s}</li>
              ))}
            </ol>
            <div className="mt-4 flex flex-wrap gap-2">
              <Button to="/seats/new" disabled={!org.isFinance}>Buy seats</Button>
              <Button variant="secondary" to="/wallet" icon={Coins}>Buy units</Button>
            </div>
          </div>
        </Card>
      ) : null}

      {loading || list.length ? (
        <section>
          <div className="mb-3 flex items-center justify-between">
            <h2 className="text-lg">Latest seat blocks</h2>
            <Link to="/seats" className="text-sm font-semibold">All seats <ArrowRight className="inline h-4 w-4" /></Link>
          </div>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {loading ? [0, 1, 2].map((i) => <Skeleton key={i} className="h-48" />) : latest.map((b) => <SeatBlockCard key={b.blockId} block={b} />)}
          </div>
        </section>
      ) : null}

      {blocks.isError ? <EmptyState title="Seats could not be loaded" description="Try again in a moment." action={<Button variant="secondary" onClick={() => blocks.refetch()}>Retry</Button>} /> : null}

      <Card>
        <CardHeader title="Organisation wallet" description="Seats are paid for in units at today's rate; unused seats refund units when released." action={<Button to="/wallet" variant="ghost" size="sm">Open wallet <ArrowRight className="h-4 w-4" /></Button>} />
        {wallet.isPending ? <Skeleton className="h-10" /> : wallet.isError ? <ErrorAlert error={wallet.error} /> : (
          <p className="text-sm text-navy"><span className="font-display text-2xl font-extrabold tabular">{formatUnits(wallet.data.balance)}</span> <span className="text-muted">worth {formatMoney(wallet.data.balanceValue)} at {formatMoney({ amount: wallet.data.rateKobo, currency: 'NGN' })} per unit.</span></p>
        )}
      </Card>
    </div>
  );
}
