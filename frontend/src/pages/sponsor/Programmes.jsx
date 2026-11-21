import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { FolderKanban, Plus, Search } from 'lucide-react';
import { Alert, Button, Card, EmptyState, Field, Input, PageHeader } from '../../components/ui';
import { programmeMemory } from '../../api/sponsor';
import { useOrganisation } from './useOrganisation';
import { NoOrganisation, OrganisationSwitcher, VerificationBanner } from './OrganisationSwitcher';
import { ProgrammeCard } from './ProgrammeCard';

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/*
 * /sponsor/programmes — the organisation's programmes.
 * Backend gap: there is no "list programmes for an organisation" endpoint (only GET by id), so the ids this browser
 * created or opened are remembered in localStorage (plastoids.sponsor.programmes.<orgId>) and hydrated one by one.
 */
export default function Programmes() {
  const org = useOrganisation();
  const navigate = useNavigate();
  const [ids, setIds] = useState(() => (org.organisationId ? programmeMemory.list(org.organisationId) : []));
  const [openId, setOpenId] = useState('');
  const [orgKey, setOrgKey] = useState(org.organisationId);

  if (orgKey !== org.organisationId) {
    setOrgKey(org.organisationId);
    setIds(org.organisationId ? programmeMemory.list(org.organisationId) : []);
  }

  if (!org.organisation) {
    return (
      <div>
        <PageHeader title="Programmes" />
        <NoOrganisation />
      </div>
    );
  }

  function forget(programmeId) {
    programmeMemory.forget(org.organisationId, programmeId);
    setIds(programmeMemory.list(org.organisationId));
  }

  function open(e) {
    e.preventDefault();
    const id = openId.trim();
    if (!GUID.test(id)) return;
    programmeMemory.remember(org.organisationId, id);
    navigate(`/sponsor/programmes/${id}`);
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Programmes"
        description="Campaigns you have drafted, quoted, funded and published."
        actions={
          <>
            <OrganisationSwitcher org={org} />
            <Button to="/sponsor/programmes/new" icon={Plus} disabled={!org.verified} title={org.verified ? undefined : 'Organisation must be verified first'}>New campaign</Button>
          </>
        }
      />
      <VerificationBanner org={org} />

      {ids.length ? (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {ids.map((id) => <ProgrammeCard key={id} orgId={org.organisationId} programmeId={id} onMissing={forget} />)}
        </div>
      ) : (
        <EmptyState
          icon={FolderKanban}
          title="No programmes yet"
          description={org.verified ? 'Build your first campaign: pick a course, set seats and budget, define who qualifies, and request a quote.' : 'Once your organisation is verified you can build a campaign.'}
          action={org.verified ? <Button to="/sponsor/programmes/new" icon={Plus}>Create campaign</Button> : null}
        />
      )}

      <Card>
        <form onSubmit={open} className="flex flex-col gap-3 sm:flex-row sm:items-end">
          <Field label="Open a programme by id" hint="Programmes created on another device are not listed automatically." className="flex-1">
            {(p) => <Input {...p} value={openId} onChange={(e) => setOpenId(e.target.value)} placeholder="00000000-0000-0000-0000-000000000000" />}
          </Field>
          <Button type="submit" variant="secondary" icon={Search} disabled={!GUID.test(openId.trim())}>Open</Button>
        </form>
        <Alert tone="info" className="mt-4">This list is kept in your browser because the API does not yet expose a list of an organisation's programmes.</Alert>
      </Card>
    </div>
  );
}
