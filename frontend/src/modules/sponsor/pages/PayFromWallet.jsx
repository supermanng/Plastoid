import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Coins } from 'lucide-react';
import { Alert, Button, Card, CardHeader, ConfirmDialog, ErrorAlert, Loading, useToast } from '../../../shared/components/ui';
import { formatMoney } from '../../../shared/lib/format';
import { formatUnits, unitsFor } from '../../../shared/lib/units.jsx';
import { walletApi, walletKeys } from '../../../shared/api/wallet';
import { BuyUnitsForm } from '../../../shared/components/WalletPanels';
import { sponsorKeys } from '../api/sponsor';
import { RoleHint } from './OrganisationSwitcher';

/**
 * "Pay from wallet" for a programme invoice: POST /sponsorship/organisations/{orgId}/programmes/{programmeId}/fund-from-wallet.
 * Shows the units needed (ceil(outstanding / rate)) against the organisation balance; owner/finance only.
 */
export function PayFromWallet({ org, programmeId, invoiceId, outstanding, compact = false }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [confirm, setConfirm] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const [done, setDone] = useState(null);
  const wallet = useQuery({ queryKey: walletKeys.organisation(org.organisationId), queryFn: () => walletApi.organisation(org.organisationId), enabled: Boolean(org.organisationId) });

  const rateKobo = wallet.data?.rateKobo;
  const needed = unitsFor(outstanding, rateKobo);
  const balance = wallet.data?.balance ?? null;
  const short = needed !== null && balance !== null && balance < needed;

  async function pay() {
    setBusy(true);
    setError(null);
    try {
      const res = await walletApi.fundProgramme(org.organisationId, programmeId);
      setDone(res);
      setConfirm(false);
      toast.success(`Paid with ${formatUnits(res.unitsRedeemed)}`, res.invoiceStatus === 'paid' ? 'The programme is funded.' : `Invoice ${res.invoiceStatus}; ${formatMoney(res.outstanding)} outstanding.`);
      qc.invalidateQueries({ queryKey: walletKeys.organisation(org.organisationId) });
      qc.invalidateQueries({ queryKey: sponsorKeys.programme(org.organisationId, programmeId) });
      if (invoiceId) qc.invalidateQueries({ queryKey: sponsorKeys.invoice(org.organisationId, invoiceId) });
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  if (done) {
    return <Alert tone="success" title={`Paid from wallet: ${formatUnits(done.unitsRedeemed)}`}>{formatMoney(done.amountApplied)} applied; balance now {formatUnits(done.walletBalanceAfter)}.</Alert>;
  }

  return (
    <Card>
      <CardHeader title="Pay from wallet" description="Use the organisation's units instead of a bank transfer; the programme is funded immediately." action={<Coins className="h-5 w-5 text-cyan" aria-hidden />} />
      {wallet.isPending ? <Loading /> : wallet.isError ? <ErrorAlert error={wallet.error} /> : (
        <div className="space-y-3 text-sm">
          <div className={`grid gap-3 ${compact ? '' : 'sm:grid-cols-2'}`}>
            <div className="rounded-card bg-surface p-3"><div className="text-xs uppercase tracking-wide text-muted">Needed</div><div className="font-display text-xl font-extrabold tabular">{needed === null ? '—' : formatUnits(needed)}</div><div className="text-xs text-muted">for {formatMoney(outstanding)}</div></div>
            <div className="rounded-card bg-surface p-3"><div className="text-xs uppercase tracking-wide text-muted">Organisation balance</div><div className={`font-display text-xl font-extrabold tabular ${short ? 'text-warn' : ''}`}>{formatUnits(balance)}</div><div className="text-xs text-muted">{formatMoney(wallet.data.balanceValue)}</div></div>
          </div>
          {short ? <Alert tone="warn">Short by {formatUnits(needed - balance)}. <Link to="/wallet" className="font-semibold">Buy units</Link> or pay by transfer.</Alert> : null}
          <Button icon={Coins} className="w-full" disabled={!org.isFinance || short || needed === null || needed === 0} onClick={() => setConfirm(true)}>Pay {needed === null ? '' : formatUnits(needed)} from wallet</Button>
          <RoleHint org={org} />
        </div>
      )}
      <ConfirmDialog
        open={confirm}
        onClose={() => setConfirm(false)}
        onConfirm={pay}
        loading={busy}
        error={error}
        title="Pay the invoice from the wallet?"
        description={`${formatUnits(needed)} will be taken from the organisation's wallet to settle ${formatMoney(outstanding)}. This cannot be undone.`}
        confirmLabel="Pay from wallet"
      />
    </Card>
  );
}

/** Organisation page card: balance plus a buy form for owners and finance. */
export function OrganisationWalletCard({ org }) {
  const wallet = useQuery({ queryKey: walletKeys.organisation(org.organisationId), queryFn: () => walletApi.organisation(org.organisationId), enabled: Boolean(org.organisationId) });
  if (wallet.isPending) return <Loading />;
  if (wallet.isError) return <ErrorAlert error={wallet.error} />;
  const w = wallet.data;
  return (
    <div className="grid gap-6 lg:grid-cols-[1fr_1.4fr]">
      <Card>
        <CardHeader title="Units" description="Prepaid balance used to fund programmes." action={<Coins className="h-5 w-5 text-cyan" aria-hidden />} />
        <div className="font-display text-3xl font-extrabold tabular">{formatUnits(w.balance)}</div>
        <div className="text-sm text-muted">Worth {formatMoney(w.balanceValue)} at {formatMoney({ amount: w.rateKobo, currency: 'NGN' })} per unit.</div>
        {w.pendingPurchases?.length ? <p className="mt-2 text-xs text-muted">{w.pendingPurchases.length} purchase(s) awaiting payment confirmation.</p> : null}
        <Button variant="secondary" size="sm" className="mt-4" to="/wallet">Open wallet</Button>
      </Card>
      <BuyUnitsForm rateKobo={w.rateKobo} organisationId={org.organisationId} disabled={!org.isFinance} disabledHint="Only an owner or finance member can buy units for the organisation." compact />
    </div>
  );
}
