import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Armchair, CalendarDays, Coins, UserPlus, Undo2, Users } from 'lucide-react';
import { Alert, Breadcrumb, Button, Card, CardHeader, ConfirmDialog, EmptyState, ErrorAlert, Field, FormGrid, Input, KeyValue, PageHeader, QueryState, Stat, StatusPill, Table } from '../../../shared/components/ui';
import { useAction, useForm } from '../../../shared/lib/hooks';
import { formatDate, formatDateTime, formatMoney, pluralise } from '../../../shared/lib/format';
import { UnitPrice, formatUnits } from '../../../shared/lib/units.jsx';
import { walletKeys } from '../../../shared/api/wallet';
import { AppLink } from '../../../shared/lib/navigation';
import { seatsApi, seatsKeys, seatErrorText } from '../api/seats';
import { useOrganisation } from './useOrganisation';
import { NoOrganisation, RoleHint } from './OrganisationSwitcher';

const ALLOCATION_LABEL = { invited: 'Invited', enrolled: 'Enrolled' };
const PHONE_RE = /^\+?[0-9][0-9 ()-]{6,}$/;
const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/** /sponsor/seats/:blockId — GET …/seat-blocks/{blockId} (SeatBlockDetail): summary, allocations, give a seat, release seats. */
export default function SeatBlock() {
  const { blockId } = useParams();
  const org = useOrganisation();
  const orgId = org.organisationId;
  const q = useQuery({ queryKey: seatsKeys.block(orgId, blockId), queryFn: () => seatsApi.getBlock(orgId, blockId), enabled: Boolean(orgId) });

  if (!org.organisation) {
    return (
      <div>
        <PageHeader title="Seat block" />
        <NoOrganisation />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <Breadcrumb items={[{ to: '/seats', label: 'Seats' }, { label: q.data?.courseTitle || 'Seat block' }]} />
      <QueryState
        query={q}
        empty={<EmptyState icon={Armchair} title="Seat block not found" description="It may belong to another organisation." action={<Button to="/seats" variant="secondary">All seats</Button>} />}
      >
        {(b) => <BlockView org={org} b={b} />}
      </QueryState>
    </div>
  );
}

function BlockView({ org, b }) {
  const closed = b.status !== 'active';
  const isFinance = org.isFinance;
  const invalidate = [seatsKeys.all(org.organisationId), walletKeys.organisation(org.organisationId)];
  const resend = useAction((allocationId) => seatsApi.resendInvite(org.organisationId, b.blockId, allocationId), {
    success: (a) => `Invite sent again by ${a.contactKind === 'email' ? 'email' : 'SMS'} to ${a.contact}`,
    invalidate,
  });
  return (
    <>
      <PageHeader
        title={b.courseTitle || 'Course'}
        description={<span className="inline-flex flex-wrap items-center gap-x-3 gap-y-1"><span>{b.cohortName}</span><span className="inline-flex items-center gap-1"><CalendarDays className="h-4 w-4 text-cyan" aria-hidden /> {formatDate(b.startsAt)} → {formatDate(b.endsAt)}</span></span>}
        actions={
          <>
            <StatusPill status={b.status} label={closed ? 'Closed' : 'Active'} />
            {b.courseSlug ? <Button as={AppLink} to={`/courses/${b.courseSlug}`} variant="secondary" size="sm">Course page</Button> : null}
          </>
        }
        className="mb-0"
      />

      {closed ? <Alert tone="info" title="This block is closed">The cohort has ended or every seat was given out or released. {b.closedAt ? `Closed ${formatDateTime(b.closedAt)}.` : ''}</Alert> : null}

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <Stat label="Seats bought" value={b.seats} hint={`${formatUnits(b.unitsRedeemed)} redeemed`} icon={Armchair} />
        <Stat label="Given out" value={b.allocated} hint={`${b.enrolled} enrolled · ${b.invited} invited`} icon={Users} tone="teal" />
        <Stat label="Open" value={b.open} hint={closed ? 'No more seats to give' : 'Still to give out'} tone={b.open > 0 && !closed ? 'royal' : 'default'} />
        <Stat label="Released" value={b.released} hint="Units refunded to the wallet" />
      </div>

      <div className="grid gap-6 lg:grid-cols-[1fr_360px]">
        <div className="space-y-6">
          <Card>
            <CardHeader title="Who has a seat" description="One seat per learner. Invited people are enrolled automatically when they sign up with the same phone number or email." />
            <Table
              rows={b.allocations || []}
              rowKey={(r) => r.allocationId}
              empty={<EmptyState icon={UserPlus} title="No seats given out yet" description={b.open > 0 && !closed ? 'Use the form to give the first seat.' : 'Nothing to show.'} />}
              columns={[
                { key: 'seatNo', header: '#', render: (r) => <span className="tabular text-muted">{r.seatNo}</span> },
                { key: 'displayName', header: 'Learner', render: (r) => <span className="font-semibold text-navy">{r.displayName || '—'}</span> },
                { key: 'contact', header: 'Contact', render: (r) => <span className="font-mono text-xs">{r.contact || '—'}</span> },
                { key: 'status', header: 'Status', render: (r) => <StatusPill status={r.status} tone={r.status === 'enrolled' ? 'success' : 'warn'} label={ALLOCATION_LABEL[r.status] || r.status} /> },
                { key: 'dates', header: 'When', render: (r) => <span className="text-xs text-muted">Invited {formatDate(r.invitedAt)}{r.enrolledAt ? <><br />Enrolled {formatDate(r.enrolledAt)}</> : null}{r.status === 'invited' ? <><br />{r.inviteSentAt ? `${r.contactKind === 'email' ? 'Email' : 'SMS'} sent ${formatDate(r.inviteSentAt)}${r.inviteSendCount > 1 ? ` (×${r.inviteSendCount})` : ''}` : 'Invite not sent yet'}</> : null}</span> },
                { key: 'act', header: '', align: 'right', render: (r) => (r.status === 'invited' && isFinance && !closed ? <Button size="sm" variant="ghost" loading={resend.isPending && resend.variables === r.allocationId} onClick={() => resend.mutate(r.allocationId)}>Resend invite</Button> : null) },
              ]}
            />
          </Card>

          <Card>
            <CardHeader title="Purchase" action={<Coins className="h-5 w-5 text-cyan" aria-hidden />} />
            <KeyValue
              cols={2}
              items={[
                ['Price per seat', <UnitPrice money={b.pricePerSeat} size="sm" inline />],
                ['Units redeemed', `${formatUnits(b.unitsRedeemed)} at ${formatMoney({ amount: b.unitRateKobo, currency: 'NGN' })} per unit`],
                ['Bought', formatDateTime(b.purchasedAt)],
                ['Sponsor name shown to learners', b.sponsorName || org.organisation.name],
                b.walletBalance !== undefined && b.walletBalance !== null ? ['Organisation wallet now', formatUnits(b.walletBalance)] : null,
                ['Block id', <code className="text-xs">{b.blockId}</code>],
              ]}
            />
          </Card>
        </div>

        <div className="space-y-6">
          <GiveSeat org={org} b={b} disabled={closed || b.open <= 0} invalidate={invalidate} />
          <ReleaseSeats org={org} b={b} disabled={closed || b.open <= 0} invalidate={invalidate} />
        </div>
      </div>
    </>
  );
}

function GiveSeat({ org, b, disabled, invalidate }) {
  const { values, bind, reset } = useForm({ phone: '', email: '', displayName: '' });
  const phone = values.phone.trim();
  const email = values.email.trim();
  const hasContact = Boolean(phone || email);
  const phoneBad = phone && !PHONE_RE.test(phone);
  const emailBad = email && !EMAIL_RE.test(email);

  const give = useAction(() => seatsApi.allocate(org.organisationId, b.blockId, { phone: phone || null, email: email || null, displayName: values.displayName.trim() || null }), {
    success: (a) => (a.status === 'enrolled' ? `${a.displayName || 'The learner'} is enrolled` : `${a.displayName || 'The learner'} has been invited; the seat is theirs when they sign up`),
    invalidate,
    onSuccess: () => reset(),
  });

  const cannot = !org.isFinance || disabled;
  return (
    <Card as="form" onSubmit={(e) => { e.preventDefault(); if (!cannot && hasContact && !phoneBad && !emailBad) give.mutate(); }}>
      <CardHeader title="Give a seat" description={disabled ? 'No open seats in this block.' : `${pluralise(b.open, 'open seat')}. Enter a phone number or an email address.`} action={<UserPlus className="h-5 w-5 text-cyan" aria-hidden />} />
      <ErrorAlert error={give.error} title={seatErrorText(give.error) || undefined} className="mb-3" />
      <FormGrid cols={1}>
        <Field label="Phone number" hint="The number they sign in with, e.g. +2348031234567." error={phoneBad ? 'That does not look like a phone number.' : undefined}>
          {(p) => <Input {...p} {...bind('phone')} type="tel" inputMode="tel" autoComplete="off" disabled={cannot} placeholder="+234…" />}
        </Field>
        <Field label="Email address" hint="Use this instead of a phone number if that is how they sign in." error={emailBad ? 'That does not look like an email address.' : undefined}>
          {(p) => <Input {...p} {...bind('email')} type="email" inputMode="email" autoComplete="off" disabled={cannot} placeholder="name@example.com" />}
        </Field>
        <Field label="Name" hint="Shown on this page so you know who has which seat.">
          {(p) => <Input {...p} {...bind('displayName')} maxLength={120} disabled={cannot} placeholder="Full name" />}
        </Field>
      </FormGrid>
      <Button type="submit" className="mt-4 w-full" icon={UserPlus} loading={give.isPending} disabled={cannot || !hasContact || Boolean(phoneBad || emailBad)}>Give this seat</Button>
      <div className="mt-2"><RoleHint org={org} needs="finance" /></div>
    </Card>
  );
}

function ReleaseSeats({ org, b, disabled, invalidate }) {
  const [seats, setSeats] = useState(1);
  const [confirm, setConfirm] = useState(false);
  const n = Math.max(0, Math.floor(Number(seats) || 0));
  const tooMany = n > b.open;
  const perSeatUnits = b.seats ? (b.unitsRedeemed || 0) / b.seats : 0;
  const refund = Math.floor(perSeatUnits * n);

  const release = useAction(() => seatsApi.release(org.organisationId, b.blockId, n), {
    success: (block) => `Released ${pluralise(n, 'seat')}; ${pluralise(block.open, 'seat')} still open`,
    invalidate,
    onSuccess: () => {
      setConfirm(false);
      setSeats(1);
    },
    onError: () => setConfirm(false),
  });

  const cannot = !org.isFinance || disabled;
  return (
    <Card>
      <CardHeader title="Release seats" description="Give back seats you will not use. The units return to the organisation wallet pro rata." action={<Undo2 className="h-5 w-5 text-cyan" aria-hidden />} />
      <ErrorAlert error={release.error} title={seatErrorText(release.error) || undefined} className="mb-3" />
      <Field label="Seats to release" hint={disabled ? 'No open seats to release.' : `Up to ${b.open}.`} error={tooMany ? `Only ${pluralise(b.open, 'open seat')}.` : undefined}>
        {(p) => <Input {...p} type="number" inputMode="numeric" min={1} max={b.open} step={1} value={seats} onChange={(e) => setSeats(e.target.value === '' ? '' : Number(e.target.value))} disabled={cannot} className="max-w-[160px]" />}
      </Field>
      <Button variant="secondary" className="mt-4 w-full" icon={Undo2} disabled={cannot || n < 1 || tooMany} onClick={() => setConfirm(true)}>Release {n >= 1 ? pluralise(n, 'seat') : 'seats'}</Button>
      <ConfirmDialog
        open={confirm}
        onClose={() => setConfirm(false)}
        onConfirm={() => release.mutate()}
        loading={release.isPending}
        error={release.error}
        tone="danger"
        title={`Release ${pluralise(n, 'seat')}?`}
        description={`The cohort reservation is freed and about ${formatUnits(refund)} come back to the wallet. You can buy seats again later while the cohort is open.`}
        confirmLabel="Release"
      />
    </Card>
  );
}
