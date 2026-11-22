import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Flag, PlusCircle } from 'lucide-react';
import { Button, Card, EmptyState, Field, Input, Loading, Modal, PageHeader, Switch } from '../../../shared/components/ui';
import { formatDateTime } from '../../../shared/lib/format';
import { useAction, useForm } from '../../../shared/lib/hooks';
import { adminApi, adminKeys } from '../api/admin';
import { AdminError, UserLink } from './_shared';

/** GET /admin/backoffice/feature-flags → FeatureFlagView[]; PUT /{key} { enabled, description }. */
export default function FeatureFlags() {
  const q = useQuery({ queryKey: adminKeys.featureFlags, queryFn: adminApi.featureFlags });
  const [creating, setCreating] = useState(false);
  const toggle = useAction(({ key, enabled, description }) => adminApi.setFeatureFlag(key, enabled, description), { success: (f) => `${f.key} ${f.enabled ? 'enabled' : 'disabled'}`, invalidate: [adminKeys.featureFlags] });

  return (
    <div>
      <PageHeader title="Feature flags" description="Runtime switches read by the API. Changes apply immediately and are audited with who flipped them." actions={<Button icon={PlusCircle} onClick={() => setCreating(true)}>New flag</Button>} />
      {q.isPending ? <Loading /> : null}
      {q.isError ? <AdminError error={q.error} /> : null}
      <AdminError error={toggle.error} className="mb-4" />
      {q.data ? (
        q.data.length ? (
          <div className="grid gap-3 md:grid-cols-2">
            {q.data.map((f) => (
              <Card key={f.key} className="flex items-start justify-between gap-4">
                <div className="min-w-0">
                  <div className="font-mono text-sm font-semibold">{f.key}</div>
                  <p className="mt-0.5 text-sm text-muted">{f.description || 'No description'}</p>
                  <div className="mt-2 text-[11px] text-muted">Updated {formatDateTime(f.updatedAt)} by <UserLink id={f.updatedBy} /></div>
                </div>
                <Switch label="" checked={f.enabled} onChange={(v) => toggle.mutate({ key: f.key, enabled: v, description: f.description })} className="shrink-0" />
              </Card>
            ))}
          </div>
        ) : (
          <EmptyState icon={Flag} title="No feature flags" description="Create one to gate a feature at runtime." />
        )
      ) : null}
      <CreateDialog open={creating} onClose={() => setCreating(false)} />
    </div>
  );
}

function CreateDialog({ open, onClose }) {
  const form = useForm({ key: '', description: '', enabled: false });
  const save = useAction(() => adminApi.setFeatureFlag(form.values.key.trim(), form.values.enabled, form.values.description.trim()), { success: 'Flag saved', invalidate: [adminKeys.featureFlags], onSuccess: () => { form.reset(); onClose(); } });
  const valid = /^[a-z0-9_.:-]{2,64}$/i.test(form.values.key.trim());
  return (
    <Modal open={open} onClose={onClose} title="New feature flag" size="sm" footer={<><Button variant="secondary" onClick={onClose}>Cancel</Button><Button loading={save.isPending} disabled={!valid} onClick={() => save.mutate()}>Save</Button></>}>
      <AdminError error={save.error} className="mb-3" />
      <div className="space-y-3">
        <Field label="Key" required hint="Letters, digits, dots, dashes, underscores.">{(p) => <Input {...p} className="font-mono text-xs" {...form.bind('key')} placeholder="e.g. lms.offline_packages" />}</Field>
        <Field label="Description">{(p) => <Input {...p} {...form.bind('description')} />}</Field>
        <Switch label="Enabled" checked={form.values.enabled} onChange={(v) => form.set('enabled', v)} />
      </div>
    </Modal>
  );
}
