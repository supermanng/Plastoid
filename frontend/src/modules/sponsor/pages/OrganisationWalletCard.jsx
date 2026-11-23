import { useQuery } from '@tanstack/react-query';
import { Coins } from 'lucide-react';
import { Button, Card, CardHeader, ErrorAlert, Loading } from '../../../shared/components/ui';
import { formatMoney } from '../../../shared/lib/format';
import { formatUnits } from '../../../shared/lib/units.jsx';
import { walletApi, walletKeys } from '../../../shared/api/wallet';
import { BuyUnitsForm } from '../../../shared/components/WalletPanels';

/** Organisation page card: balance plus a buy form for owners and finance. Units pay for seats (ADR 0021). */
export function OrganisationWalletCard({ org }) {
  const wallet = useQuery({ queryKey: walletKeys.organisation(org.organisationId), queryFn: () => walletApi.organisation(org.organisationId), enabled: Boolean(org.organisationId) });
  if (wallet.isPending) return <Loading />;
  if (wallet.isError) return <ErrorAlert error={wallet.error} />;
  const w = wallet.data;
  return (
    <div className="grid gap-6 lg:grid-cols-[1fr_1.4fr]">
      <Card>
        <CardHeader title="Units" description="Prepaid balance used to buy seats in cohorts." action={<Coins className="h-5 w-5 text-cyan" aria-hidden />} />
        <div className="font-display text-3xl font-extrabold tabular">{formatUnits(w.balance)}</div>
        <div className="text-sm text-muted">Worth {formatMoney(w.balanceValue)} at {formatMoney({ amount: w.rateKobo, currency: 'NGN' })} per unit.</div>
        {w.pendingPurchases?.length ? <p className="mt-2 text-xs text-muted">{w.pendingPurchases.length} purchase(s) awaiting payment confirmation.</p> : null}
        <Button variant="secondary" size="sm" className="mt-4" to="/wallet">Open wallet</Button>
      </Card>
      <BuyUnitsForm rateKobo={w.rateKobo} organisationId={org.organisationId} disabled={!org.isFinance} disabledHint="Only an owner or finance member can buy units for the organisation." compact />
    </div>
  );
}
