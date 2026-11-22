import { Building2 } from 'lucide-react';
import { Alert, Button, Select, StatusPill } from '../../../shared/components/ui';

/** Header widget: pick the active organisation. Renders nothing when there is only one. */
export function OrganisationSwitcher({ org }) {
  if (!org.organisations.length) return null;
  if (org.organisations.length === 1) {
    return (
      <span className="inline-flex items-center gap-2 text-sm text-muted">
        <Building2 className="h-4 w-4" aria-hidden /> {org.organisation?.name}
        <StatusPill status={org.verificationStatus} />
      </span>
    );
  }
  return (
    <label className="flex items-center gap-2 text-sm">
      <Building2 className="h-4 w-4 text-muted" aria-hidden />
      <span className="sr-only">Organisation</span>
      <Select value={org.organisationId || ''} onChange={(e) => org.select(e.target.value)} className="min-w-[200px]">
        {org.organisations.map((o) => (
          <option key={o.id} value={o.id}>{o.name}</option>
        ))}
      </Select>
    </label>
  );
}

/** Shown on every sponsor page when the account has no organisation yet. */
export function NoOrganisation() {
  return (
    <Alert tone="info" title="No organisation yet">
      Register your organisation (CAC number and authorised signatory) to start sponsoring.
      <div className="mt-3">
        <Button to="/organisation/new" size="sm">Register organisation</Button>
      </div>
    </Alert>
  );
}

/** Verification banner: staff must verify the organisation before campaigns can be created. */
export function VerificationBanner({ org }) {
  if (!org.organisation || org.verified) return null;
  const status = String(org.verificationStatus || '').toLowerCase();
  return (
    <Alert tone={status === 'rejected' ? 'danger' : 'warn'} title={status === 'rejected' ? 'Organisation verification was rejected' : 'Organisation verification pending'}>
      {status === 'rejected'
        ? 'Contact Plastoids support to resubmit your CAC details. Campaigns cannot be created until the organisation is verified.'
        : 'Plastoids staff are checking your CAC number and authorised signatory. You can explore the sponsor area, but campaigns can only be created once verified.'}
    </Alert>
  );
}

/** Explains why an owner-only / finance-only control is disabled. */
export function RoleHint({ org, needs = 'owner' }) {
  const allowed = needs === 'finance' ? org.isFinance : needs === 'author' ? org.isAuthor : org.isOwner;
  if (allowed) return null;
  const who = { finance: 'owner or finance member', author: 'owner or author', owner: 'owner' }[needs] || 'owner';
  return (
    <p className="text-xs text-muted">
      Only an organisation {who} can do this. Your role: <span className="font-semibold capitalize">{org.role || 'member'}</span>.
    </p>
  );
}
