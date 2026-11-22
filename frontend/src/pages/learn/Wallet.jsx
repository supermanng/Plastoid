import { ErrorAlert, Loading, PageHeader } from '../../components/ui';
import { BalanceHero, BuyUnitsForm, OpenLots, PendingPurchases, TransactionsList, useWallet } from '../../components/WalletPanels';

/** /learn/wallet — GET /wallet/me, POST /wallet/purchases, GET /wallet/me/transactions. */
export default function Wallet() {
  const q = useWallet(null);
  return (
    <div className="space-y-6">
      <PageHeader title="Wallet" description="Units pay for courses, tutor sessions and co-payments. Buy them once at today's rate; they never expire and refunds come back as units." />
      {q.isPending ? <Loading /> : q.isError ? <ErrorAlert error={q.error} /> : (
        <>
          <BalanceHero wallet={q.data} />
          <div className="grid gap-6 lg:grid-cols-[1.1fr_1fr]">
            <BuyUnitsForm rateKobo={q.data.rateKobo} />
            <div className="space-y-6">
              <PendingPurchases purchases={q.data.pendingPurchases} />
              <OpenLots lots={q.data.openLots} />
            </div>
          </div>
          <TransactionsList />
        </>
      )}
    </div>
  );
}
