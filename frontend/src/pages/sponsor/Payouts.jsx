import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Landmark } from 'lucide-react';
import { Alert, Button, Card, CardHeader, ErrorAlert, Field, FormGrid, Input, Loading, PageHeader } from '../../components/ui';
import { DisputeHoldAlert, EarningsBreakdown, EarningsStats, RecentEarnings, RecentPayouts } from '../../components/EarningsStatement';
import { useAction, useForm } from '../../lib/hooks';
import { providerApi, providerKeys } from '../../api/provider';
import { useOrganisation } from './useOrganisation';
import { NoOrganisation, OrganisationSwitcher, RoleHint, VerificationBanner } from './OrganisationSwitcher';

/*
 * /sponsor/payouts — the organisation's earnings from its courses (ADR 0019).
 *   GET /payouts/organisations/{id}/earnings        statement as /payouts/me + payoutAccount
 *   PUT /payouts/organisations/{id}/payout-account  { bankCode, accountNumber } (owner or finance; account name must match the organisation)
 * Sales of organisation courses: platform commission → tutor share (from the engagement terms) → organisation net,
 * held 7 days, then paid in the weekly batch from the provider payable ledger (2060).
 */
export default function SponsorPayouts() {
  const org = useOrganisation();
  const orgId = org.organisationId;
  const earnings = useQuery({ queryKey: providerKeys.earnings(orgId), queryFn: () => providerApi.earnings(orgId), enabled: Boolean(orgId) });

  if (!org.organisation) {
    return (
      <div>
        <PageHeader title="Payouts" />
        <NoOrganisation />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <PageHeader className="mb-0" title="Payouts" description="What the organisation has earned from course sales and what has been paid to its bank." actions={<OrganisationSwitcher org={org} />} />
      <VerificationBanner org={org} />

      {earnings.isPending ? <Loading /> : null}
      {earnings.isError ? <ErrorAlert error={earnings.error} /> : null}
      {earnings.data ? (
        <>
          <DisputeHoldAlert data={earnings.data} />
          <EarningsStats data={earnings.data} />
          <div className="grid gap-6 lg:grid-cols-3">
            <EarningsBreakdown
              data={earnings.data}
              note="Each sale pays platform commission first, then the engaged tutor's share; the organisation keeps the rest. Course-sale earnings clear after 7 days (refunds in that window reduce them) and are paid in the weekly run to the payout account below."
            />
            <PayoutAccountCard org={org} current={earnings.data.payoutAccount} className="lg:col-span-2" />
          </div>
          <RecentEarnings data={earnings.data} />
          <RecentPayouts data={earnings.data} emptyHint="Earnings appear here once learners buy the organisation's courses." />
        </>
      ) : null}
    </div>
  );
}

function PayoutAccountCard({ org, current, className }) {
  const { values, bind, reset } = useForm({ bankCode: '', accountNumber: '' });
  const [result, setResult] = useState(null);
  const save = useAction(() => providerApi.setPayoutAccount(org.organisationId, { bankCode: values.bankCode.trim(), accountNumber: values.accountNumber.trim() }), {
    silent: true,
    invalidate: [providerKeys.earnings(org.organisationId)],
    onSuccess: (res) => { setResult(res); reset(); },
  });
  const shown = result || current;

  return (
    <Card className={className}>
      <CardHeader title="Payout account" description="Weekly payouts go here once earnings clear. The bank account name must match the registered organisation name." />
      {shown ? (
        <Alert tone={shown.nameMatched === false ? 'warn' : 'success'} className="mb-4" title={shown.nameMatched === false ? `Name mismatch: ${shown.accountName || 'unknown'}` : `Account ${shown.accountNumberMasked || ''}`}>
          <span className="inline-flex items-center gap-1"><Landmark className="h-4 w-4" aria-hidden /> Bank {shown.bankCode}{shown.accountName ? ` · ${shown.accountName}` : ''}</span>
          {shown.nameMatched === false ? <div className="mt-1">The account name does not match the organisation, so payouts are skipped until a matching account is saved.</div> : null}
        </Alert>
      ) : <p className="mb-4 text-sm text-muted">No payout account yet. Earnings accrue but cannot be paid until one is saved.</p>}
      <form onSubmit={(e) => { e.preventDefault(); if (org.isFinance) save.mutate(); }} className="space-y-3">
        <ErrorAlert error={save.error} />
        <fieldset disabled={!org.isFinance}>
          <FormGrid>
            <Field label="Bank code" hint="CBN/NIP bank code, e.g. 058 for GTBank." required>{(p) => <Input {...p} {...bind('bankCode')} inputMode="numeric" />}</Field>
            <Field label="Account number" hint="10-digit NUBAN." required>{(p) => <Input {...p} {...bind('accountNumber')} inputMode="numeric" maxLength={10} />}</Field>
          </FormGrid>
        </fieldset>
        <div className="flex flex-wrap items-center justify-between gap-3">
          <RoleHint org={org} needs="finance" />
          <Button type="submit" loading={save.isPending} disabled={!org.isFinance || !values.bankCode.trim() || !/^\d{10}$/.test(values.accountNumber.trim())}>Resolve and save</Button>
        </div>
      </form>
    </Card>
  );
}
