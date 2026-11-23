import { useState } from 'react';
import { UserPlus } from 'lucide-react';
import { Alert, Button, Card, CardHeader, ErrorAlert, Field, FormGrid, Input, KeyValue, PageHeader, Select, StatusPill } from '../../../shared/components/ui';
import { useAuth } from '../../../shared/lib/auth';
import { useForm } from '../../../shared/lib/hooks';
import { sponsorApi } from '../api/sponsor';
import { useOrganisation } from './useOrganisation';
import { NoOrganisation, OrganisationSwitcher, RoleHint, VerificationBanner } from './OrganisationSwitcher';
import { OrganisationWalletCard } from './OrganisationWalletCard';

const ROLES = [
  ['owner', 'Owner: everything, including publishing and members'],
  ['finance', 'Finance: buy units, buy and release seats, give seats to learners, payout account'],
  ['author', 'Author: create and edit courses, engage tutors'],
  ['member', 'Member: view seats, courses and the wallet'],
];

/*
 * /sponsor/organisation — details, verification status and members.
 * There is no "list members" endpoint, so this page remembers the members it has added in this browser
 * (localStorage plastoids.sponsor.members.<orgId>) and always lists the signed-in user.
 */
export default function Organisation() {
  const auth = useAuth();
  const org = useOrganisation();

  if (!org.organisation) {
    return (
      <div>
        <PageHeader title="Organisation" />
        <NoOrganisation />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <PageHeader title={org.organisation.name} description="Organisation details, verification and members." actions={<OrganisationSwitcher org={org} />} />
      <VerificationBanner org={org} />

      <div className="grid gap-6 lg:grid-cols-[1fr_1.4fr]">
        <Card>
          <CardHeader title="Details" />
          <KeyValue
            cols={1}
            items={[
              ['Name', org.organisation.name],
              ['Type', <span className="capitalize">{org.organisation.type}</span>],
              ['Verification', <StatusPill status={org.verificationStatus} />],
              ['Your role', <span className="capitalize">{org.role}</span>],
              ['Organisation id', <code className="text-xs">{org.organisationId}</code>],
            ]}
          />
          <p className="mt-4 text-xs text-muted">CAC number and signatory are held by Plastoids staff for verification and are not shown here.</p>
        </Card>

        <Members org={org} me={auth.me} />
      </div>

      <OrganisationWalletCard org={org} />
    </div>
  );
}

function membersKey(orgId) {
  return `plastoids.sponsor.members.${orgId}`;
}

function readMembers(orgId) {
  try {
    const raw = localStorage.getItem(membersKey(orgId));
    const list = raw ? JSON.parse(raw) : [];
    return Array.isArray(list) ? list : [];
  } catch {
    return [];
  }
}

function writeMembers(orgId, list) {
  try {
    localStorage.setItem(membersKey(orgId), JSON.stringify(list));
  } catch {
    /* ignore */
  }
}

function Members({ org, me }) {
  const [members, setMembers] = useState(() => readMembers(org.organisationId));
  const { values, bind, reset } = useForm({ identifier: '', role: 'member' });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const [roleError, setRoleError] = useState(null);
  const [changing, setChanging] = useState(null);

  const all = [
    { userId: me?.id, label: me?.displayName || 'You', contact: me?.email || me?.phoneMasked, role: org.role, isMe: true },
    ...members.filter((m) => m.userId !== me?.id),
  ];

  function persist(next) {
    setMembers(next);
    writeMembers(org.organisationId, next);
  }

  async function add(e) {
    e.preventDefault();
    setError(null);
    const id = values.identifier.trim();
    const body = id.includes('@') ? { email: id, role: values.role } : { phone: id, role: values.role };
    setBusy(true);
    try {
      const res = await sponsorApi.addMember(org.organisationId, body);
      persist([...members.filter((m) => m.userId !== res.userId), { userId: res.userId, label: id, contact: id, role: res.role }]);
      reset();
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  async function changeRole(member, role) {
    setRoleError(null);
    setChanging(member.userId);
    try {
      await sponsorApi.changeMemberRole(org.organisationId, member.userId, role);
      if (member.isMe) {
        await org.refresh();
      } else {
        persist(members.map((m) => (m.userId === member.userId ? { ...m, role } : m)));
      }
    } catch (err) {
      setRoleError(err);
    } finally {
      setChanging(null);
    }
  }

  return (
    <Card>
      <CardHeader title="Members" description="Owners manage members. The last owner cannot be demoted." />
      <ErrorAlert error={roleError} className="mb-3" />
      <ul className="divide-y divide-line rounded-card border border-line">
        {all.map((m) => (
          <li key={m.userId || m.label} className="flex flex-wrap items-center justify-between gap-3 px-4 py-3 text-sm">
            <div className="min-w-0">
              <div className="truncate font-semibold text-navy">{m.label}{m.isMe ? ' (you)' : ''}</div>
              <div className="truncate text-xs text-muted">{m.contact}</div>
            </div>
            {org.isOwner ? (
              <Select aria-label={`Role for ${m.label}`} value={m.role} disabled={changing === m.userId} onChange={(e) => changeRole(m, e.target.value)} className="w-36">
                {ROLES.map(([v]) => <option key={v} value={v} className="capitalize">{v}</option>)}
              </Select>
            ) : (
              <StatusPill tone="info" status={m.role} />
            )}
          </li>
        ))}
      </ul>
      <p className="mt-2 text-xs text-muted">Members added from other devices are not listed here: the API has no member list yet.</p>

      <form onSubmit={add} className="mt-5 space-y-3 border-t border-line pt-4">
        <h4 className="text-sm font-bold">Add a member</h4>
        <ErrorAlert error={error} />
        <FormGrid>
          <Field label="Email or phone" hint="The person must already have a Plastoids account." required>
            {(p) => <Input {...p} {...bind('identifier')} placeholder="finance@foundation.org or +2348012345678" disabled={!org.isOwner} />}
          </Field>
          <Field label="Role" required>
            {(p) => (
              <Select {...p} {...bind('role')} disabled={!org.isOwner}>
                {ROLES.map(([v, l]) => <option key={v} value={v}>{l}</option>)}
              </Select>
            )}
          </Field>
        </FormGrid>
        <div className="flex items-center justify-between gap-3">
          <RoleHint org={org} />
          <Button type="submit" icon={UserPlus} loading={busy} disabled={!org.isOwner || !values.identifier.trim()}>Add member</Button>
        </div>
      </form>
      {!org.verified ? <Alert tone="info" className="mt-4">Members can be added before verification completes.</Alert> : null}
    </Card>
  );
}
