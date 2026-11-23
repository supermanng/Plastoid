import { useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Armchair, Building2, CalendarDays, Check, Coins, Search, Star } from 'lucide-react';
import { Alert, Breadcrumb, Button, Card, CardHeader, ConfirmDialog, EmptyState, ErrorAlert, Field, Input, Loading, PageHeader, Skeleton, Stepper } from '../../../shared/components/ui';
import { useAction, useDebounce } from '../../../shared/lib/hooks';
import { formatDate, formatMoney, koboOf, pluralise } from '../../../shared/lib/format';
import { UnitPrice, formatUnits, unitsFor } from '../../../shared/lib/units.jsx';
import { cohortItems, getCohort, listCohorts, queryKeys } from '../../../shared/api/public';
import { walletApi, walletKeys } from '../../../shared/api/wallet';
import { AppLink } from '../../../shared/lib/navigation';
import { seatsApi, seatsKeys, seatErrorText } from '../api/seats';
import { sponsorApi, sponsorKeys } from '../api/sponsor';
import { useOrganisation } from './useOrganisation';
import { NoOrganisation, OrganisationSwitcher, RoleHint } from './OrganisationSwitcher';

const STEPS = ['Course', 'Cohort', 'Seats'];

/*
 * /sponsor/seats/new — three steps: find a course, pick one of its upcoming cohorts, choose how many seats.
 * POST /sponsorship/organisations/{orgId}/seat-blocks { cohortId, seats } redeems seats × pricePerSeat from the
 * organisation wallet and reserves the seats in the cohort. `?cohortId=` (from a course page) skips to step 3.
 */
export default function BuySeats() {
  const org = useOrganisation();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const presetCohortId = params.get('cohortId');
  const [step, setStep] = useState(presetCohortId ? 2 : 0);
  const [picked, setPicked] = useState({ course: null, cohort: null });

  // A preset cohort (from a course page) fills both choices until the sponsor picks something else.
  const preset = useQuery({ queryKey: queryKeys.cohort(presetCohortId), queryFn: () => getCohort(presetCohortId), enabled: Boolean(presetCohortId) && !picked.course && !picked.cohort });
  const cohort = picked.cohort ?? (presetCohortId && !picked.course ? preset.data ?? null : null);
  const course = picked.course ?? (cohort ? { courseId: cohort.courseId, title: cohort.courseTitle, slug: cohort.courseSlug, providerName: cohort.providerName } : null);
  const setCourse = (c) => setPicked({ course: c, cohort: null });
  const setCohort = (c) => setPicked((prev) => ({ course: prev.course ?? course, cohort: c }));

  if (!org.organisation) {
    return (
      <div>
        <PageHeader title="Buy seats" />
        <NoOrganisation />
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <Breadcrumb items={[{ to: '/seats', label: 'Seats' }, { label: 'Buy seats' }]} />
      <PageHeader
        title="Buy seats"
        description="Pay for seats in a cohort from the organisation wallet, then give them to your people."
        actions={<OrganisationSwitcher org={org} />}
        className="mb-0"
      />
      <Stepper steps={STEPS} current={step} onSelect={(i) => i < step && setStep(i)} />
      <RoleHint org={org} needs="finance" />

      {presetCohortId && preset.isPending ? <Loading /> : null}
      {presetCohortId && preset.isError ? <ErrorAlert error={preset.error} title="That cohort could not be loaded" /> : null}

      {step === 0 ? (
        <CourseStep
          selected={course}
          onPick={(c) => {
            setCourse(c);
            setStep(1);
          }}
        />
      ) : null}
      {step === 1 && course ? (
        <CohortStep
          course={course}
          selected={cohort}
          onBack={() => setStep(0)}
          onPick={(c) => {
            setCohort(c);
            setStep(2);
          }}
        />
      ) : null}
      {step === 2 && cohort ? (
        <SeatsStep org={org} cohort={cohort} onBack={() => setStep(1)} onBought={(block) => navigate(`/seats/${block.blockId}`, { replace: true })} />
      ) : null}
    </div>
  );
}

/* ---------------------------------------------------------------- Step 1: course */

function CourseStep({ selected, onPick }) {
  const [q, setQ] = useState('');
  const term = useDebounce(q.trim(), 300);
  const search = useQuery({ queryKey: sponsorKeys.courses({ q: term }), queryFn: () => sponsorApi.searchCourses({ q: term || undefined }), placeholderData: (prev) => prev });
  const items = search.data?.items || [];

  return (
    <Card>
      <CardHeader title="Which course?" description="Search the published catalogue. Only courses with a dated cohort can be sponsored." />
      <Field label="Search courses">
        {(p) => (
          <div className="relative">
            <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted" aria-hidden />
            <Input {...p} className="pl-9" value={q} onChange={(e) => setQ(e.target.value)} placeholder="e.g. data analysis" autoFocus />
          </div>
        )}
      </Field>
      <div className="mt-4">
        {search.isPending ? <div className="space-y-2"><Skeleton className="h-16" /><Skeleton className="h-16" /><Skeleton className="h-16" /></div> : null}
        <ErrorAlert error={search.error} />
        {search.data && !items.length ? <EmptyState title="No courses match" description="Try another word, or a skill such as “excel” or “python”." /> : null}
        <ul className="divide-y divide-line">
          {items.map((c) => {
            const active = selected?.courseId === c.courseId;
            return (
              <li key={c.courseId}>
                <button
                  type="button"
                  onClick={() => onPick(c)}
                  className={`flex w-full items-center gap-3 rounded-input px-2 py-3 text-left hover:bg-surface ${active ? 'bg-royal-50' : ''}`}
                  aria-pressed={active}
                >
                  <div className="min-w-0 flex-1">
                    <div className="truncate font-semibold text-navy">{c.title}</div>
                    <div className="mt-0.5 flex flex-wrap items-center gap-x-3 gap-y-0.5 text-xs text-muted">
                      <span className="capitalize">{c.level}</span>
                      {c.providerName ? <span className="inline-flex items-center gap-1"><Building2 className="h-3.5 w-3.5 text-cyan" aria-hidden /> {c.providerName}</span> : null}
                      {c.ratingCount ? <span className="inline-flex items-center gap-1"><Star className="h-3.5 w-3.5 text-warn" aria-hidden /> {Number(c.ratingAverage).toFixed(1)} ({c.ratingCount})</span> : null}
                      {c.fromPrice ? <span>From {formatMoney(c.fromPrice)}</span> : null}
                    </div>
                  </div>
                  {active ? <Check className="h-5 w-5 shrink-0 text-royal" aria-hidden /> : <span className="shrink-0 text-xs font-semibold text-royal">Choose</span>}
                </button>
              </li>
            );
          })}
        </ul>
      </div>
    </Card>
  );
}

/* ---------------------------------------------------------------- Step 2: cohort */

function CohortStep({ course, selected, onBack, onPick }) {
  const filters = { courseId: course.courseId };
  const q = useQuery({ queryKey: queryKeys.cohorts(filters), queryFn: () => listCohorts(filters) });
  const items = cohortItems(q.data);

  return (
    <Card>
      <CardHeader
        title="Which cohort?"
        description={<>Upcoming dated runs of <span className="font-semibold text-navy">{course.title}</span>. Seats can be bought until a cohort ends.</>}
        action={<Button variant="ghost" size="sm" onClick={onBack}>Change course</Button>}
      />
      {q.isPending ? <div className="space-y-2"><Skeleton className="h-20" /><Skeleton className="h-20" /></div> : null}
      <ErrorAlert error={q.error} />
      {q.data && !items.length ? (
        <EmptyState icon={CalendarDays} title="No upcoming cohorts" description="This course has no dated cohort open right now. Pick another course, or check back when the tutor schedules one." action={<Button variant="secondary" size="sm" onClick={onBack}>Choose another course</Button>} />
      ) : null}
      <ul className="space-y-3">
        {items.map((c) => {
          const active = selected?.cohortId === c.cohortId;
          const none = (c.seatsLeft ?? 0) <= 0;
          return (
            <li key={c.cohortId}>
              <button
                type="button"
                onClick={() => !none && onPick(c)}
                disabled={none}
                aria-pressed={active}
                className={`flex w-full flex-col gap-2 rounded-card border p-4 text-left sm:flex-row sm:items-center sm:justify-between ${active ? 'border-royal bg-royal-50' : 'border-line hover:bg-surface'} disabled:cursor-not-allowed disabled:opacity-60`}
              >
                <div className="min-w-0">
                  <div className="font-semibold text-navy">{c.name}</div>
                  <div className="mt-0.5 text-xs text-muted">{formatDate(c.startsAt)} → {formatDate(c.endsAt)} · {none ? 'Full' : `${pluralise(c.seatsLeft, 'seat')} left`} of {c.capacity}</div>
                  {c.providerName ? <div className="mt-0.5 text-xs text-muted">Offered by {c.providerName}</div> : null}
                </div>
                <div className="flex items-center gap-3 sm:text-right">
                  <span className="text-xs text-muted">per seat</span>
                  <UnitPrice money={c.pricePerSeat} className="sm:items-end" />
                  {active ? <Check className="h-5 w-5 text-royal" aria-hidden /> : null}
                </div>
              </button>
            </li>
          );
        })}
      </ul>
    </Card>
  );
}

/* ---------------------------------------------------------------- Step 3: seats */

function SeatsStep({ org, cohort, onBack, onBought }) {
  const [seats, setSeats] = useState(1);
  const [confirm, setConfirm] = useState(false);
  const wallet = useQuery({ queryKey: walletKeys.organisation(org.organisationId), queryFn: () => walletApi.organisation(org.organisationId), enabled: Boolean(org.organisationId) });

  const priceKobo = koboOf(cohort.pricePerSeat) || 0;
  const n = Math.max(0, Math.floor(Number(seats) || 0));
  const max = cohort.seatsLeft ?? null;
  const total = { amount: priceKobo * n, currency: cohort.pricePerSeat?.currency || 'NGN' };
  const rateKobo = wallet.data?.rateKobo;
  const units = unitsFor(total, rateKobo);
  const balance = wallet.data?.balance ?? null;
  const short = units !== null && balance !== null && balance < units;
  const tooMany = max !== null && n > max;
  const invalid = n < 1 || tooMany;

  const buy = useAction(() => seatsApi.buySeats(org.organisationId, { cohortId: cohort.cohortId, seats: n }), {
    success: (block) => `Bought ${pluralise(block.seats, 'seat')} in ${block.cohortName || cohort.name}`,
    invalidate: [seatsKeys.all(org.organisationId), walletKeys.organisation(org.organisationId), queryKeys.cohorts({ courseId: cohort.courseId }), queryKeys.cohort(cohort.cohortId)],
    onSuccess: (block) => {
      setConfirm(false);
      onBought(block);
    },
    onError: () => setConfirm(false),
  });

  return (
    <div className="grid gap-6 lg:grid-cols-[1fr_320px]">
      <Card>
        <CardHeader
          title="How many seats?"
          description={<><span className="font-semibold text-navy">{cohort.courseTitle}</span> · {cohort.name} · {formatDate(cohort.startsAt)} → {formatDate(cohort.endsAt)}</>}
          action={<Button variant="ghost" size="sm" onClick={onBack}>Change cohort</Button>}
        />
        <ErrorAlert error={buy.error} title={seatErrorText(buy.error) || undefined} className="mb-4" />
        <Field label="Seats" hint={max !== null ? `${pluralise(max, 'seat')} left in this cohort.` : undefined} error={tooMany ? `Only ${pluralise(max, 'seat')} left.` : n < 1 && seats !== '' ? 'Enter at least 1 seat.' : undefined} required>
          {(p) => <Input {...p} type="number" inputMode="numeric" min={1} max={max ?? undefined} step={1} value={seats} onChange={(e) => setSeats(e.target.value === '' ? '' : Number(e.target.value))} className="max-w-[160px]" />}
        </Field>
        <div className="mt-3 flex flex-wrap gap-2">
          {[5, 10, 20, 50].filter((v) => max === null || v <= max).map((v) => (
            <Button key={v} type="button" size="sm" variant={n === v ? 'navy' : 'secondary'} onClick={() => setSeats(v)}>{v}</Button>
          ))}
        </div>

        <dl className="mt-6 divide-y divide-line rounded-card border border-line text-sm">
          <Row k="Price per seat" v={<UnitPrice money={cohort.pricePerSeat} size="sm" inline />} />
          <Row k={`Total for ${pluralise(n, 'seat')}`} v={units === null ? formatMoney(total) : <><span className="font-bold text-navy">{formatUnits(units)}</span><span className="text-xs text-muted"> · {formatMoney(total)}</span></>} bold />
          <Row k="Organisation wallet" v={wallet.isPending ? '…' : wallet.isError ? 'Unavailable' : <>{formatUnits(balance)}<span className="text-xs text-muted"> · {formatMoney(wallet.data.balanceValue)}</span></>} />
          {units !== null && balance !== null && !short ? <Row k="Left after purchase" v={formatUnits(balance - units)} /> : null}
        </dl>
        <ErrorAlert error={wallet.error} className="mt-3" />
        {short ? (
          <Alert tone="warn" className="mt-4" title={`Short by ${formatUnits(units - balance)}`}>
            The organisation wallet holds {formatUnits(balance)}; this purchase needs {formatUnits(units)}. <AppLink to="/sponsor/wallet" className="font-semibold">Buy units in the wallet</AppLink>, then come back.
          </Alert>
        ) : null}

        <div className="mt-6 flex flex-wrap items-center gap-3">
          <Button icon={Coins} disabled={!org.isFinance || invalid || short || units === null || wallet.isPending} onClick={() => setConfirm(true)}>
            Buy {n >= 1 ? pluralise(n, 'seat') : 'seats'}{units ? ` for ${formatUnits(units)}` : ''}
          </Button>
          <RoleHint org={org} needs="finance" />
        </div>
        <p className="mt-3 text-xs text-muted">Seats you do not give out can be released at any time; the units come back to the wallet. Any seats still open when the cohort ends are released automatically.</p>
      </Card>

      <Card className="h-fit">
        <CardHeader title="What happens next" action={<Armchair className="h-5 w-5 text-cyan" aria-hidden />} />
        <ol className="space-y-3 text-sm text-navy">
          {[
            'The seats are reserved in the cohort and the units leave the wallet.',
            'Give each seat to a learner by phone number or email from the seat block page.',
            'People with an account are enrolled at once; everyone else is enrolled the moment they sign up.',
            'Enrolled learners see “Sponsored by ' + (org.organisation?.name || 'your organisation') + '” on the course.',
          ].map((s, i) => (
            <li key={s} className="flex gap-3"><span className="grid h-6 w-6 shrink-0 place-items-center rounded-full bg-surface text-xs font-bold text-muted">{i + 1}</span>{s}</li>
          ))}
        </ol>
      </Card>

      <ConfirmDialog
        open={confirm}
        onClose={() => setConfirm(false)}
        onConfirm={() => buy.mutate()}
        loading={buy.isPending}
        error={buy.error}
        title={`Buy ${pluralise(n, 'seat')} in ${cohort.name}?`}
        description={`${formatUnits(units)} (${formatMoney(total)}) will be taken from ${org.organisation?.name}'s wallet. Seats you do not give out can be released for a refund of units.`}
        confirmLabel="Buy seats"
      />
    </div>
  );
}

function Row({ k, v, bold = false }) {
  return (
    <div className={`flex items-center justify-between gap-4 px-4 py-2.5 ${bold ? 'font-semibold text-navy' : 'text-muted'}`}>
      <dt>{k}</dt>
      <dd className="text-right tabular text-navy">{v}</dd>
    </div>
  );
}
