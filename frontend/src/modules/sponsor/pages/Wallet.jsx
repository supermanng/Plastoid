import { ErrorAlert, Loading, PageHeader } from '../../../shared/components/ui';
import { BalanceHero, BuyUnitsForm, OpenLots, PendingPurchases, TransactionsList, useWallet } from '../../../shared/components/WalletPanels';
import { useOrganisation } from './useOrganisation';
import { NoOrganisation, OrganisationSwitcher, RoleHint } from './OrganisationSwitcher';

/** /sponsor/wallet — the selected organisation's units: GET /wallet/organisations/{id}; owner/finance can buy. */
export default function SponsorWallet() {
  const org = useOrganisation();
  const q = useWallet(org.organisationId, { enabled: Boolean(org.organisationId) });

  if (!org.organisation) return <div><PageHeader title="Organisation wallet" /><NoOrganisation /></div>;

  return (
    <div className="space-y-6">
      <PageHeader title="Organisation wallet" description="Units pay for seats: buy units here, then buy seats in a cohort from the Seats page. Released seats refund units." actions={<OrganisationSwitcher org={org} />} />
      {q.isPending ? <Loading /> : q.isError ? <ErrorAlert error={q.error} /> : (
        <>
          <BalanceHero wallet={q.data} title={`${org.organisation.name} units`} />
          <div className="grid gap-6 lg:grid-cols-[1.1fr_1fr]">
            <div className="space-y-3">
              <BuyUnitsForm rateKobo={q.data.rateKobo} organisationId={org.organisationId} disabled={!org.isFinance} disabledHint="Only an owner or finance member can buy units for the organisation." />
              <RoleHint org={org} />
            </div>
            <div className="space-y-6">
              <PendingPurchases purchases={q.data.pendingPurchases} />
              <OpenLots lots={q.data.openLots} />
            </div>
          </div>
          <TransactionsList organisationId={org.organisationId} />
        </>
      )}
    </div>
  );
}
