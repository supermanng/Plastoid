import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Download, KeyRound, ShieldCheck, Target, Trash2, UserRound } from 'lucide-react';
import { Alert, Button, Card, CardHeader, Chip, ConfirmDialog, ErrorAlert, Field, FormGrid, Input, KeyValue, PageHeader, StatusPill } from '../../components/ui';
import { useAuth } from '../../lib/auth';
import { useAction } from '../../lib/hooks';
import { formatDate, formatDateTime, titleCase } from '../../lib/format';
import { identity, keys } from '../../api/learn';

const INTERESTS = ['Software development', 'Data analysis', 'Product design', 'Digital marketing', 'Business & finance', 'Cybersecurity', 'Cloud & DevOps', 'Content creation', 'Project management', 'Trades & technical', 'Health & care', 'Teaching'];

export default function Profile() {
  const auth = useAuth();
  const me = auth.me;
  const navigate = useNavigate();
  const qc = useQueryClient();
  const [deleteOpen, setDeleteOpen] = useState(false);

  const del = useAction(() => identity.deleteAccount(), {
    success: 'Your account has been erased.',
    onSuccess: () => {
      auth.logout();
      qc.clear();
      navigate('/');
    },
  });

  if (!me) return null;

  return (
    <div className="mx-auto max-w-3xl">
      <PageHeader title="Profile & settings" description="Your account, goals, sign-in methods and your data rights under the NDPA." />

      <Card className="mb-6">
        <CardHeader title="Account" />
        <KeyValue items={[
          ['Phone', me.phoneMasked],
          ['Email', me.email ? `${me.email}${me.emailVerified ? '' : ' (unverified)'}` : 'Not added'],
          ['Member since', formatDate(me.createdAt)],
          ['Roles', <div className="flex flex-wrap gap-1">{me.roles.map((r) => <span key={r} className="rounded-chip bg-surface px-2 py-0.5 text-xs font-semibold capitalize">{r}</span>)}</div>],
          ['Identity (NIN)', me.ninVerified ? <StatusPill status="verified" /> : <span className="text-muted">Not verified (tutors only)</span>],
          ['Two-step verification', me.mfaEnabled ? <StatusPill status="enabled" /> : <StatusPill status="off" tone="neutral" label="Off" />],
        ]} />
        {me.organisations?.length ? (
          <div className="mt-4">
            <div className="text-xs font-medium uppercase tracking-wide text-muted">Organisations</div>
            <ul className="mt-1 space-y-1 text-sm">
              {me.organisations.map((o) => <li key={o.id} className="flex flex-wrap items-center gap-2"><span className="font-semibold text-navy">{o.name}</span><span className="text-muted">{titleCase(o.type)} · {titleCase(o.role)}</span><StatusPill status={o.verificationStatus} /></li>)}
            </ul>
          </div>
        ) : null}
      </Card>

      <DetailsCard me={me} onSaved={auth.refreshMe} />
      <GoalsCard me={me} onSaved={auth.refreshMe} />

      <Card className="mb-6">
        <CardHeader title={<span className="inline-flex items-center gap-2"><KeyRound className="h-4 w-4 text-royal" /> Sign-in methods</span>} description="Phone + SMS code always works. Add email + password to sign in without a phone, or to unlock the staff flow." />
        <EmailSignIn me={me} onSaved={auth.refreshMe} />
        <div className="mt-4 flex flex-wrap items-center justify-between gap-3 rounded-card border border-line p-3">
          <div className="flex items-center gap-3 text-sm">
            <ShieldCheck className={`h-5 w-5 ${auth.mfa ? 'text-teal' : 'text-muted'}`} />
            <div>
              <div className="font-semibold text-navy">Two-step verification {me.mfaEnabled ? 'is on' : 'is off'}</div>
              <div className="text-xs text-muted">Required for tutors, sponsors and staff; recommended for everyone.</div>
            </div>
          </div>
          <Button to="/auth/mfa" size="sm" variant={me.mfaEnabled ? 'secondary' : 'primary'}>{me.mfaEnabled ? (auth.mfa ? 'Manage' : 'Verify now') : 'Set up'}</Button>
        </div>
      </Card>

      <DataExportCard />

      <Card className="border-danger/30">
        <CardHeader title={<span className="inline-flex items-center gap-2 text-danger"><Trash2 className="h-4 w-4" /> Delete account</span>} description="Erases your personal data. Certificates, anonymised learning records and ledger entries are kept as the law requires. This cannot be undone." />
        <Button variant="danger" onClick={() => setDeleteOpen(true)}>Delete my account</Button>
      </Card>

      <ConfirmDialog open={deleteOpen} onClose={() => setDeleteOpen(false)} onConfirm={() => del.mutate()} loading={del.isPending} error={del.error} title="Delete your account?" description="You will be signed out immediately. Active enrolments and bookings are forfeited; paid balances follow the refund policy." confirmLabel="Delete permanently" tone="danger" />
    </div>
  );
}

function DetailsCard({ me, onSaved }) {
  const [displayName, setName] = useState(me.displayName || '');
  const [email, setEmail] = useState(me.email || '');
  const save = useAction(() => identity.updateMe({ displayName: displayName.trim() || null, email: email.trim() || null }), { success: 'Details updated', onSuccess: onSaved });
  return (
    <Card className="mb-6">
      <CardHeader title={<span className="inline-flex items-center gap-2"><UserRound className="h-4 w-4 text-royal" /> Your details</span>} />
      <ErrorAlert error={save.error} className="mb-3" />
      <form onSubmit={(e) => { e.preventDefault(); save.mutate(); }}>
        <FormGrid>
          <Field label="Display name" error={save.error?.field?.('displayName')}>{(p) => <Input {...p} value={displayName} onChange={(e) => setName(e.target.value)} maxLength={120} />}</Field>
          <Field label="Email" hint="Used for receipts and as a backup sign-in." error={save.error?.field?.('email')}>{(p) => <Input {...p} type="email" value={email} onChange={(e) => setEmail(e.target.value)} />}</Field>
        </FormGrid>
        <div className="mt-4 flex justify-end"><Button type="submit" loading={save.isPending}>Save</Button></div>
      </form>
    </Card>
  );
}

function GoalsCard({ me, onSaved }) {
  const [goal, setGoal] = useState(me.careerGoal || '');
  const [interests, setInterests] = useState((me.interests || []).filter((i) => !i.startsWith('level:')));
  const level = (me.interests || []).find((i) => i.startsWith('level:'));
  const save = useAction(() => identity.updateGoals({ careerGoal: goal.trim() || null, interests: level ? [...interests, level] : interests }), { success: 'Goals updated', onSuccess: onSaved });
  const toggle = (i) => setInterests((l) => (l.includes(i) ? l.filter((x) => x !== i) : l.length < 5 ? [...l, i] : l));
  return (
    <Card className="mb-6">
      <CardHeader title={<span className="inline-flex items-center gap-2"><Target className="h-4 w-4 text-teal" /> Career goal & interests</span>} description="Drives your recommendations and which sponsored seats we surface." action={<Link to="/learn/onboarding" className="text-sm font-semibold text-royal">Redo setup</Link>} />
      <ErrorAlert error={save.error} className="mb-3" />
      <Field label="Career goal">{(p) => <Input {...p} value={goal} onChange={(e) => setGoal(e.target.value)} maxLength={200} placeholder="e.g. Become a frontend developer" />}</Field>
      <div className="mt-3 flex flex-wrap gap-2">
        {[...new Set([...INTERESTS, ...interests])].map((i) => <Chip key={i} active={interests.includes(i)} onClick={() => toggle(i)}>{i}</Chip>)}
      </div>
      <div className="mt-4 flex justify-end"><Button loading={save.isPending} onClick={() => save.mutate()}>Save goals</Button></div>
    </Card>
  );
}

function EmailSignIn({ me, onSaved }) {
  const auth = useAuth();
  const [email, setEmail] = useState(me.email || '');
  const [password, setPassword] = useState('');
  const [open, setOpen] = useState(false);
  const add = useAction(() => auth.registerEmail(email.trim(), password), { success: 'Email sign-in added', onSuccess: () => { setOpen(false); setPassword(''); onSaved(); } });
  return (
    <div className="rounded-card border border-line p-3">
      <div className="flex flex-wrap items-center justify-between gap-3 text-sm">
        <div>
          <div className="font-semibold text-navy">Email + password</div>
          <div className="text-xs text-muted">{me.email && me.emailVerified ? `Sign in with ${me.email}` : 'Not set up'}</div>
        </div>
        <Button size="sm" variant="secondary" onClick={() => setOpen((v) => !v)}>{open ? 'Close' : me.email ? 'Set password' : 'Add'}</Button>
      </div>
      {open ? (
        <form className="mt-3 space-y-3" onSubmit={(e) => { e.preventDefault(); add.mutate(); }}>
          <ErrorAlert error={add.error} />
          <FormGrid>
            <Field label="Email" required error={add.error?.field?.('email')}>{(p) => <Input {...p} type="email" autoComplete="username" value={email} onChange={(e) => setEmail(e.target.value)} />}</Field>
            <Field label="Password" required hint="At least 12 characters." error={add.error?.field?.('password')}>{(p) => <Input {...p} type="password" autoComplete="new-password" value={password} onChange={(e) => setPassword(e.target.value)} minLength={12} />}</Field>
          </FormGrid>
          <div className="flex justify-end"><Button type="submit" size="sm" loading={add.isPending} disabled={!email || password.length < 12}>Save sign-in</Button></div>
        </form>
      ) : null}
    </div>
  );
}

function DataExportCard() {
  const [exportId, setExportId] = useState(() => localStorage.getItem('plastoids.dataExport') || null);
  const request = useAction(() => identity.requestDataExport(), { success: 'Export requested. It is usually ready within a few minutes (24 hours at most).', onSuccess: (r) => { setExportId(r.exportId); localStorage.setItem('plastoids.dataExport', r.exportId); } });
  const status = useQuery({
    queryKey: keys.dataExport(exportId),
    queryFn: () => identity.getDataExport(exportId),
    enabled: Boolean(exportId),
    refetchInterval: (q) => (q.state.data && q.state.data.status !== 'completed' && q.state.data.status !== 'failed' ? 10_000 : false),
  });
  useEffect(() => {
    if (status.error?.status === 404) {
      setExportId(null);
      localStorage.removeItem('plastoids.dataExport');
    }
  }, [status.error]);
  const d = status.data;
  return (
    <Card className="mb-6">
      <CardHeader title={<span className="inline-flex items-center gap-2"><Download className="h-4 w-4 text-royal" /> Your data (NDPA)</span>} description="Request a copy of everything we hold about you as JSON and PDF. Links are valid for one hour once ready." />
      <ErrorAlert error={request.error} className="mb-3" />
      {d ? (
        <div className="rounded-card border border-line p-3 text-sm">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <span>Requested {formatDateTime(d.requestedAt)}{d.completedAt ? ` · ready ${formatDateTime(d.completedAt)}` : ''}</span>
            <StatusPill status={d.status} />
          </div>
          {d.status === 'completed' ? (
            <div className="mt-3 flex flex-wrap gap-2">
              {d.jsonUrl ? <Button href={d.jsonUrl} size="sm" variant="secondary" icon={Download}>Download JSON</Button> : null}
              {d.pdfUrl ? <Button href={d.pdfUrl} size="sm" variant="secondary" icon={Download}>Download PDF</Button> : null}
              {!d.jsonUrl && !d.pdfUrl ? <Alert tone="info">Links expired. Request a new export.</Alert> : null}
            </div>
          ) : d.status === 'failed' ? <Alert tone="danger" className="mt-3">The export failed. Request it again or contact support.</Alert> : <p className="mt-2 text-xs text-muted">Checking every 10 seconds…</p>}
        </div>
      ) : null}
      <div className="mt-3"><Button variant={d ? 'secondary' : 'primary'} size="sm" loading={request.isPending} onClick={() => request.mutate()}>{d ? 'Request a new export' : 'Request my data'}</Button></div>
    </Card>
  );
}
