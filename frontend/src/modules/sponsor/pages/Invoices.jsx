import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useQueries } from '@tanstack/react-query';
import { Plus, Receipt, Search } from 'lucide-react';
import { Alert, Button, Card, EmptyState, ErrorAlert, Field, FormGrid, Input, Modal, PageHeader, Select, StatusPill, Table, useToast } from '../../../shared/components/ui';
import { formatDate, formatMoney, nairaToKobo } from '../../../shared/lib/format';
import { invoiceMemory, money, programmeMemory, sponsorApi, sponsorKeys } from '../api/sponsor';
import { useOrganisation } from './useOrganisation';
import { NoOrganisation, OrganisationSwitcher, RoleHint } from './OrganisationSwitcher';

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const LINE_KINDS = ['tuition', 'management_fee', 'seat_fee', 'other'];

/*
 * /sponsor/invoices — invoices this browser has seen (no list endpoint; ids are remembered in
 * localStorage plastoids.sponsor.invoices.<orgId>, and programme invoiceIds are picked up too).
 * Invoices are normally issued by approving a quote; finance members can also raise an ad-hoc invoice.
 */
export default function Invoices() {
  const org = useOrganisation();
  const navigate = useNavigate();
  const orgId = org.organisationId;
  const [openId, setOpenId] = useState('');
  const [creating, setCreating] = useState(false);
  const [version, setVersion] = useState(0);

  const programmeIds = orgId ? programmeMemory.list(orgId) : [];
  const programmes = useQueries({
    queries: programmeIds.map((id) => ({ queryKey: sponsorKeys.programme(orgId, id), queryFn: () => sponsorApi.getProgramme(orgId, id), retry: false })),
  });
  const fromProgrammes = programmes.map((q) => q.data?.invoiceId).filter(Boolean);
  const ids = orgId ? Array.from(new Set([...invoiceMemory.list(orgId), ...fromProgrammes])) : [];

  const invoices = useQueries({
    queries: ids.map((id) => ({ queryKey: sponsorKeys.invoice(orgId, id), queryFn: () => sponsorApi.getInvoice(orgId, id), retry: false })),
  });

  if (!org.organisation) return <div><PageHeader title="Invoices" /><NoOrganisation /></div>;

  const rows = invoices.map((q, i) => ({ id: ids[i], q })).filter((r) => r.q.data).map((r) => r.q.data);
  const loading = invoices.some((q) => q.isPending) || programmes.some((q) => q.isPending);
  const programmeName = (programmeId) => programmes.find((q) => q.data?.programmeId === programmeId)?.data?.name || '—';

  function open(e) {
    e.preventDefault();
    const id = openId.trim();
    if (!GUID.test(id)) return;
    invoiceMemory.remember(orgId, id);
    navigate(`/invoices/${id}`);
  }

  return (
    <div className="space-y-6" key={version}>
      <PageHeader
        title="Invoices"
        description="Pay by bank transfer to the virtual account on each invoice; funds are matched automatically."
        actions={
          <>
            <OrganisationSwitcher org={org} />
            <Button icon={Plus} variant="secondary" disabled={!org.isFinance} onClick={() => setCreating(true)}>New invoice</Button>
          </>
        }
      />
      <RoleHint org={org} needs="finance" />

      {rows.length || loading ? (
        <Table
          columns={[
            { key: 'number', header: 'Invoice', render: (r) => <span className="font-semibold">{r.number}</span> },
            { key: 'programmeId', header: 'Programme', render: (r) => programmeName(r.programmeId) },
            { key: 'issuedAt', header: 'Issued', render: (r) => formatDate(r.issuedAt) },
            { key: 'dueAt', header: 'Due', render: (r) => formatDate(r.dueAt) },
            { key: 'total', header: 'Total', align: 'right', render: (r) => formatMoney(r.total) },
            { key: 'outstanding', header: 'Outstanding', align: 'right', render: (r) => formatMoney(r.outstanding) },
            { key: 'status', header: 'Status', render: (r) => <StatusPill status={r.status} /> },
          ]}
          rows={rows}
          rowKey={(r) => r.invoiceId}
          onRowClick={(r) => navigate(`/invoices/${r.invoiceId}`)}
          empty={<p className="py-6 text-center text-sm text-muted">Loading invoices…</p>}
        />
      ) : (
        <EmptyState icon={Receipt} title="No invoices yet" description="Approving a campaign quote issues its invoice automatically." action={<Button to="/programmes" variant="secondary">Go to programmes</Button>} />
      )}
      {invoices.some((q) => q.isError) ? <ErrorAlert error={invoices.find((q) => q.isError)?.error} title="Some invoices could not be loaded" /> : null}

      <Card>
        <form onSubmit={open} className="flex flex-col gap-3 sm:flex-row sm:items-end">
          <Field label="Open an invoice by id" hint="Invoices issued on another device are not listed automatically." className="flex-1">
            {(p) => <Input {...p} value={openId} onChange={(e) => setOpenId(e.target.value)} placeholder="00000000-0000-0000-0000-000000000000" />}
          </Field>
          <Button type="submit" variant="secondary" icon={Search} disabled={!GUID.test(openId.trim())}>Open</Button>
        </form>
        <Alert tone="info" className="mt-4">The API has no "list invoices" endpoint yet, so this list is remembered in your browser.</Alert>
      </Card>

      <NewInvoiceModal open={creating} onClose={() => setCreating(false)} org={org} programmes={programmes.map((q) => q.data).filter(Boolean)} onCreated={(inv) => { invoiceMemory.remember(orgId, inv.invoiceId); setCreating(false); setVersion((v) => v + 1); navigate(`/invoices/${inv.invoiceId}`); }} />
    </div>
  );
}

function NewInvoiceModal({ open, onClose, org, programmes, onCreated }) {
  const toast = useToast();
  const [programmeId, setProgrammeId] = useState('');
  const [customerName, setCustomerName] = useState(org.organisation?.name || '');
  const [dueDays, setDueDays] = useState(14);
  const [lines, setLines] = useState([{ kind: 'tuition', description: '', quantity: 1, unitNaira: '' }]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  const valid = customerName.trim() && lines.every((l) => l.description.trim() && Number(l.quantity) > 0 && nairaToKobo(l.unitNaira) > 0);

  async function submit(e) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const inv = await sponsorApi.createInvoice(org.organisationId, {
        programmeId: programmeId || null,
        customerName: customerName.trim(),
        dueDays: Number(dueDays) || 14,
        lines: lines.map((l) => ({ kind: l.kind, description: l.description.trim(), quantity: Number(l.quantity), unitAmount: money(nairaToKobo(l.unitNaira)) })),
      });
      toast.success(`Invoice ${inv.number} issued`);
      onCreated(inv);
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  const setLine = (i, patch) => setLines((ls) => ls.map((l, j) => (j === i ? { ...l, ...patch } : l)));

  return (
    <Modal
      open={open}
      onClose={onClose}
      title="New invoice"
      description="VAT (7.5%) is added to fee lines automatically; tuition is VAT-exempt."
      size="lg"
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>Cancel</Button>
          <Button type="submit" form="new-invoice" loading={busy} disabled={!valid}>Issue invoice</Button>
        </>
      }
    >
      <form id="new-invoice" onSubmit={submit} className="space-y-4">
        <ErrorAlert error={error} />
        <FormGrid cols={3}>
          <Field label="Programme (optional)">
            {(p) => (
              <Select {...p} value={programmeId} onChange={(e) => setProgrammeId(e.target.value)}>
                <option value="">None</option>
                {programmes.map((pr) => <option key={pr.programmeId} value={pr.programmeId}>{pr.name}</option>)}
              </Select>
            )}
          </Field>
          <Field label="Bill to" required>{(p) => <Input {...p} value={customerName} onChange={(e) => setCustomerName(e.target.value)} />}</Field>
          <Field label="Due in (days)">{(p) => <Input {...p} type="number" min={1} max={90} value={dueDays} onChange={(e) => setDueDays(e.target.value)} />}</Field>
        </FormGrid>
        <div className="space-y-2">
          {lines.map((l, i) => (
            <div key={i} className="grid gap-2 sm:grid-cols-[130px_1fr_80px_140px_auto] sm:items-end">
              <Field label={i === 0 ? 'Kind' : undefined}>{(p) => <Select {...p} value={l.kind} onChange={(e) => setLine(i, { kind: e.target.value })}>{LINE_KINDS.map((k) => <option key={k} value={k}>{k.replace('_', ' ')}</option>)}</Select>}</Field>
              <Field label={i === 0 ? 'Description' : undefined}>{(p) => <Input {...p} value={l.description} onChange={(e) => setLine(i, { description: e.target.value })} />}</Field>
              <Field label={i === 0 ? 'Qty' : undefined}>{(p) => <Input {...p} type="number" min={1} value={l.quantity} onChange={(e) => setLine(i, { quantity: e.target.value })} />}</Field>
              <Field label={i === 0 ? 'Unit (₦)' : undefined}>{(p) => <Input {...p} inputMode="decimal" value={l.unitNaira} onChange={(e) => setLine(i, { unitNaira: e.target.value })} />}</Field>
              <Button variant="ghost" size="sm" disabled={lines.length === 1} onClick={() => setLines((ls) => ls.filter((_, j) => j !== i))} aria-label="Remove line">×</Button>
            </div>
          ))}
          <Button variant="secondary" size="sm" icon={Plus} onClick={() => setLines((ls) => [...ls, { kind: 'other', description: '', quantity: 1, unitNaira: '' }])}>Add line</Button>
        </div>
      </form>
    </Modal>
  );
}
