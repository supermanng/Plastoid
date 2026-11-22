import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Mail, PlusCircle } from 'lucide-react';
import { Alert, Button, Card, Checkbox, EmptyState, Field, Input, Loading, Modal, PageHeader, Select, StatusPill, Table, Textarea } from '../../../shared/components/ui';
import { formatDateTime } from '../../../shared/lib/format';
import { useAction, useDebounce, useForm, useQueryParams } from '../../../shared/lib/hooks';
import { adminApi, adminKeys } from '../api/admin';
import { AdminError, Id } from './_shared';

const DEFAULTS = { key: '' };
const CHANNELS = ['sms', 'whatsapp', 'email', 'push', 'in_app'];

/** GET /admin/notifications/templates?key= → TemplateView[]; POST (new version, approve flag); POST /{id}/approve. */
export default function Templates() {
  const [params, setParam] = useQueryParams(DEFAULTS);
  const key = useDebounce(params.key, 300);
  const q = useQuery({ queryKey: adminKeys.templates(key), queryFn: () => adminApi.templates(key || undefined), placeholderData: (prev) => prev });
  const [editing, setEditing] = useState(null); // null | {} (new) | TemplateView (new version)
  const [preview, setPreview] = useState(null);
  const approve = useAction((id) => adminApi.approveTemplate(id), { success: 'Template version approved and now in use', invalidate: [['admin', 'templates']] });

  return (
    <div>
      <PageHeader title="Notification templates" description="Versioned per key and channel. Placeholders use {{name}}. Only approved versions are sent; approving a version retires the previous one." actions={<Button icon={PlusCircle} onClick={() => setEditing({})}>New template</Button>} />
      <Card className="mb-4">
        <Field label="Filter by key" className="max-w-sm">{(p) => <Input {...p} value={params.key} onChange={(e) => setParam('key', e.target.value)} placeholder="e.g. applications.offer_sent" className="font-mono text-xs" />}</Field>
      </Card>
      {q.isPending ? <Loading /> : null}
      {q.isError ? <AdminError error={q.error} /> : null}
      {q.data ? (
        q.data.length ? (
          <Table
            rows={q.data}
            rowKey={(t) => t.templateId}
            columns={[
              { key: 'key', header: 'Key', render: (t) => <><span className="font-mono text-xs">{t.key}</span><div className="text-[11px] text-muted">v{t.version} · <Id value={t.templateId} /></div></> },
              { key: 'channel', header: 'Channel', render: (t) => <span className="capitalize">{String(t.channel).replace('_', ' ')}</span> },
              { key: 'subject', header: 'Subject / body', render: (t) => <>{t.subject ? <div className="font-semibold">{t.subject}</div> : null}<div className="line-clamp-2 max-w-md text-xs text-muted">{t.body}</div></> },
              { key: 'approved', header: 'Status', render: (t) => (t.approved ? <StatusPill tone="success" status="approved" label={`Approved ${formatDateTime(t.approvedAt)}`} /> : <StatusPill tone="warn" status="draft" />) },
              { key: 'act', header: '', align: 'right', render: (t) => (
                <div className="flex justify-end gap-1">
                  <Button size="sm" variant="ghost" onClick={() => setPreview(t)}>Preview</Button>
                  <Button size="sm" variant="secondary" onClick={() => setEditing(t)}>New version</Button>
                  {!t.approved ? <Button size="sm" variant="teal" loading={approve.isPending && approve.variables === t.templateId} onClick={() => approve.mutate(t.templateId)}>Approve</Button> : null}
                </div>
              ) },
            ]}
          />
        ) : (
          <EmptyState icon={Mail} title="No templates" description="Add the first version of a template for a notification key." />
        )
      ) : null}
      <AdminError error={approve.error} className="mt-3" />

      <EditDialog key={editing ? editing.templateId || 'new' : 'closed'} template={editing} onClose={() => setEditing(null)} />
      <Modal open={Boolean(preview)} onClose={() => setPreview(null)} title="Rendered preview" description={preview ? `${preview.key} · ${preview.channel} · v${preview.version}` : ''}>
        {preview?.subject ? <div className="mb-2 font-semibold">{preview.subject}</div> : null}
        <pre className="whitespace-pre-wrap rounded-input bg-surface p-3 text-sm">{preview?.preview || preview?.body}</pre>
      </Modal>
    </div>
  );
}

/** POST /admin/notifications/templates { key, channel, subject?, body, approve? } → new version. */
function EditDialog({ template, onClose }) {
  const open = template !== null;
  const base = template || {};
  const form = useForm({ key: base.key || '', channel: base.channel || 'sms', subject: base.subject || '', body: base.body || '', approve: false });
  const save = useAction(
    () => adminApi.upsertTemplate({ key: form.values.key.trim(), channel: form.values.channel, subject: form.values.subject.trim() || null, body: form.values.body, approve: form.values.approve }),
    { success: (t) => `Saved ${t.key} v${t.version}${t.approved ? ' (approved)' : ''}`, invalidate: [['admin', 'templates']], onSuccess: onClose },
  );
  const placeholders = Array.from(new Set((form.values.body.match(/{{\s*[\w.]+\s*}}/g) || []).map((p) => p.replace(/[{}\s]/g, ''))));
  const valid = form.values.key.trim() && form.values.body.trim();
  return (
    <Modal open={open} onClose={onClose} title={base.templateId ? `New version of ${base.key}` : 'New template'} description="Saving creates a new version; tick approve to put it live at once." footer={<><Button variant="secondary" onClick={onClose}>Cancel</Button><Button loading={save.isPending} disabled={!valid} onClick={() => save.mutate()}>Save version</Button></>}>
      <AdminError error={save.error} className="mb-3" />
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label="Key" required hint="Notification key, e.g. payouts.settled">{(p) => <Input {...p} className="font-mono text-xs" {...form.bind('key')} disabled={Boolean(base.templateId)} />}</Field>
        <Field label="Channel" required>{(p) => <Select {...p} {...form.bind('channel')} disabled={Boolean(base.templateId)}>{CHANNELS.map((c) => <option key={c} value={c}>{c.replace('_', ' ')}</option>)}</Select>}</Field>
      </div>
      {form.values.channel === 'email' || form.values.channel === 'push' ? <Field label="Subject / title" className="mt-3">{(p) => <Input {...p} {...form.bind('subject')} />}</Field> : null}
      <Field label="Body" required className="mt-3" hint={placeholders.length ? `Placeholders: ${placeholders.join(', ')}` : 'Use {{placeholder}} for dynamic values.'}>{(p) => <Textarea {...p} rows={6} {...form.bind('body')} />}</Field>
      <Checkbox className="mt-2" label="Approve immediately" checked={form.values.approve} onChange={(e) => form.set('approve', e.target.checked)} />
      {form.values.channel === 'sms' && form.values.body.length > 160 ? <Alert tone="warn" className="mt-2">SMS over 160 characters is sent as several segments.</Alert> : null}
    </Modal>
  );
}
