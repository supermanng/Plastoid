import { useState } from 'react';
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { BookOpen, Building2, Clock, FileText, Globe, GraduationCap, Layers, Lock, PlayCircle, Star, Video, ClipboardCheck, Radio } from 'lucide-react';
import { Alert, Button, Card, CardHeader, EmptyState, ErrorAlert, Field, Loading, Select, StatusPill, Textarea, useToast } from '../../../../shared/components/ui';
import { useAuth } from '../../../../shared/lib/auth';
import { useAction } from '../../../../shared/lib/hooks';
import { api } from '../../../../shared/lib/api';
import { formatDate, formatDateTime, formatMoney, pluralise, titleCase } from '../../../../shared/lib/format';
import { UnitPrice, formatUnits, unitsFor, useUnitRate } from '../../../../shared/lib/units.jsx';
import { walletApi, walletKeys } from '../../../../shared/api/wallet';
import { createCourseCheckout, getCourse, goToCheckout, postReview, queryKeys } from '../../../../shared/api/public';

const LESSON_ICON = { video: Video, doc: FileText, document: FileText, assignment: ClipboardCheck, live: Radio, quiz: ClipboardCheck };

/** /courses/:slug — outline, offerings with 30-minute quotes, reviews, sponsored-seats panel and enrolment. */
export default function CourseDetail() {
  const { slug } = useParams();
  const query = useQuery({ queryKey: queryKeys.course(slug), queryFn: () => getCourse(slug) });

  if (query.isPending) return <Loading className="min-h-[50vh]" />;
  if (query.isError) {
    return (
      <div className="mx-auto max-w-3xl px-4 py-10 sm:px-6">
        {query.error?.status === 404 ? <EmptyState icon={BookOpen} title="Course not found" description="It may be unpublished or the link is old." action={<Button to="/courses">Browse courses</Button>} /> : <ErrorAlert error={query.error} />}
      </div>
    );
  }
  const c = query.data;
  const sponsored = (c.offerings || []).some((o) => (o.fundingTypes || []).some((f) => /sponsor/i.test(f)));
  const hours = Math.round((c.estimatedMinutes || 0) / 60);

  return (
    <div className="mx-auto max-w-[1280px] px-4 py-8 sm:px-6">
      <nav className="text-sm text-muted" aria-label="Breadcrumb"><Link to="/courses" className="hover:text-navy">Courses</Link> / <span className="text-navy">{c.category}</span></nav>
      <div className="mt-3 grid gap-8 lg:grid-cols-[1fr_380px]">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <span className="rounded-chip bg-surface px-2.5 py-0.5 text-xs font-semibold capitalize text-muted">{c.level}</span>
            {sponsored ? <StatusPill tone="success" status="sponsored" label="Sponsored seats" /> : null}
            {c.publishedAt ? <span className="text-xs text-muted">Version {c.versionNo} · published {formatDate(c.publishedAt)}</span> : null}
          </div>
          <h1 className="mt-3 text-3xl sm:text-4xl">{c.title}</h1>
          <p className="mt-3 max-w-2xl text-base text-muted">{c.summary}</p>
          <p className="mt-3 flex items-center gap-1.5 text-sm text-navy">
            {c.provider ? <><Building2 className="h-4 w-4 text-cyan" aria-hidden /> Offered by <span className="font-semibold">{c.provider.name}</span><span className="text-muted"> · taught by engaged verified tutors</span></> : <><GraduationCap className="h-4 w-4 text-cyan" aria-hidden /> Taught by a <Link to="/tutors" className="font-semibold hover:underline">verified tutor</Link></>}
          </p>
          <ul className="mt-5 flex flex-wrap gap-x-6 gap-y-2 text-sm text-navy">
            <li className="flex items-center gap-1.5"><Layers className="h-4 w-4 text-cyan" /> {pluralise(c.lessonCount || 0, 'lesson')}</li>
            <li className="flex items-center gap-1.5"><Clock className="h-4 w-4 text-cyan" /> about {hours || '<1'} h</li>
            <li className="flex items-center gap-1.5"><Globe className="h-4 w-4 text-cyan" /> {c.language?.toUpperCase()}</li>
            <li className="flex items-center gap-1.5"><Star className="h-4 w-4 text-warn" /> {c.ratingCount ? `${Number(c.ratingAverage).toFixed(1)} (${c.ratingCount} reviews)` : 'New course'}</li>
          </ul>
          {c.skills?.length ? <div className="mt-4 flex flex-wrap gap-1.5">{c.skills.map((s) => <Link key={s} to={`/courses?skill=${encodeURIComponent(s)}`} className="rounded-chip border border-line px-2.5 py-0.5 text-xs font-semibold text-navy hover:border-royal">{s}</Link>)}</div> : null}

          <div className="lg:hidden"><OfferingsPanel course={c} className="mt-6" /></div>

          <section className="mt-10">
            <h2 className="text-xl">Syllabus</h2>
            <Outline modules={c.outline || []} />
          </section>

          <section className="mt-10">
            <h2 className="text-xl">Reviews</h2>
            <Reviews course={c} />
          </section>
        </div>
        <aside className="hidden lg:block"><div className="sticky top-20"><OfferingsPanel course={c} /></div></aside>
      </div>
    </div>
  );
}

function Outline({ modules }) {
  if (!modules.length) return <p className="mt-2 text-sm text-muted">No outline has been published yet.</p>;
  return (
    <ol className="mt-4 space-y-3">
      {[...modules].sort((a, b) => a.order - b.order).map((m, i) => (
        <li key={m.id}>
          <Card padded={false}>
            <div className="flex items-center justify-between gap-3 px-4 py-3"><h3 className="text-base">{i + 1}. {m.title}</h3><span className="text-xs text-muted">{pluralise(m.lessons?.length || 0, 'lesson')}</span></div>
            <ul className="divide-y divide-line border-t border-line">
              {[...(m.lessons || [])].sort((a, b) => a.order - b.order).map((l) => {
                const Icon = LESSON_ICON[String(l.type).toLowerCase()] || FileText;
                return (
                  <li key={l.id} className="flex items-center gap-3 px-4 py-2.5 text-sm">
                    <Icon className="h-4 w-4 shrink-0 text-cyan" aria-hidden />
                    <span className="flex-1 text-navy">{l.title}</span>
                    <span className="text-xs capitalize text-muted">{titleCase(l.type)} · {l.durationMinutes} min</span>
                    {l.isPreview && l.previewPlaybackUrl ? <a href={l.previewPlaybackUrl} target="_blank" rel="noreferrer" className="inline-flex items-center gap-1 text-xs font-semibold text-royal"><PlayCircle className="h-4 w-4" /> Preview</a> : l.isPreview ? <span className="text-xs font-semibold text-teal-600">Preview</span> : <Lock className="h-3.5 w-3.5 text-line" aria-label="Locked" />}
                  </li>
                );
              })}
            </ul>
          </Card>
        </li>
      ))}
    </ol>
  );
}

function OfferingsPanel({ course, className }) {
  const auth = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const toast = useToast();
  const [result, setResult] = useState(null);
  const offerings = course.offerings || [];
  const rate = useUnitRate();
  const wallet = useQuery({ queryKey: walletKeys.me, queryFn: () => walletApi.me(), enabled: auth.isAuthenticated, staleTime: 15_000 });
  const balance = wallet.data?.balance ?? null;

  const checkout = useAction(({ quoteId, payWithUnits }) => createCourseCheckout(quoteId, undefined, payWithUnits), {
    silent: true,
    invalidate: [walletKeys.me],
    onSuccess: (res, vars) => {
      if (vars.payWithUnits) {
        toast.success(`Enrolled: ${formatUnits(res.unitsCharged)} spent`, 'Your course is ready in My courses.');
        navigate('/learn/courses');
        return;
      }
      setResult(res);
      if (!goToCheckout(res.checkoutUrl)) toast.info('Checkout created', 'The payment gateway is simulated in this environment; open the payment to continue.');
    },
    onError: (err) => toast.error(err, err?.code === 'insufficient_units' ? 'Not enough units' : 'Could not start checkout'),
  });

  function enrol(o, payWithUnits = false) {
    if (!auth.isAuthenticated) return navigate('/auth/sign-in', { state: { from: location.pathname } });
    if (new Date(o.priceValidUntil) < new Date()) return toast.error('This price quote has expired; reload the page for a fresh one.', 'Price changed');
    return checkout.mutate({ quoteId: o.quoteId, payWithUnits });
  }

  return (
    <Card className={className}>
      <CardHeader title="Enrol" description={offerings.length ? 'Prices are held for 30 minutes from loading this page.' : undefined} />
      {!offerings.length ? <p className="text-sm text-muted">Enrolment is not open for this course yet.</p> : null}
      <div className="space-y-3">
        {offerings.map((o) => {
          const sponsoredOnly = (o.fundingTypes || []).length > 0 && (o.fundingTypes || []).every((f) => /sponsor/i.test(f));
          const full = o.capacity !== null && o.capacity !== undefined && o.capacity <= 0;
          return (
            <div key={o.offeringId} className="rounded-card border border-line p-4">
              <div className="flex items-start justify-between gap-2">
                <div>
                  <div className="font-bold text-navy">{titleCase(o.format)}</div>
                  <div className="mt-0.5 text-xs text-muted">
                    {o.startAt ? `Starts ${formatDate(o.startAt)}` : 'Start any time'}
                    {o.durationWeeks ? ` · ${pluralise(o.durationWeeks, 'week')}` : ''}
                    {o.capacity !== null && o.capacity !== undefined ? ` · ${o.capacity} seats` : ''}
                  </div>
                </div>
                <div className="text-right"><UnitPrice money={o.price} size="lg" className="items-end" /></div>
              </div>
              {(o.fundingTypes || []).some((f) => /sponsor/i.test(f)) ? <div className="mt-2"><StatusPill tone="success" status="sponsored" label="Sponsored seats accepted" /></div> : null}
              <div className="mt-3 flex flex-col gap-2">
                {full ? <Alert tone="warn">Seats are full. Sponsored opportunities for this course may still be open.</Alert> : null}
                {!sponsoredOnly ? (
                  auth.isAuthenticated ? (
                    <PayButtons
                      offering={o}
                      full={full}
                      units={unitsFor(o.price, rate.data?.rateKobo)}
                      balance={balance}
                      busy={checkout.isPending && checkout.variables?.quoteId === o.quoteId}
                      busyWithUnits={checkout.isPending && checkout.variables?.quoteId === o.quoteId && checkout.variables?.payWithUnits}
                      onUnits={() => enrol(o, true)}
                      onCard={() => enrol(o, false)}
                    />
                  ) : (
                    <Button className="w-full" disabled={full} onClick={() => enrol(o)}>Sign in to enrol</Button>
                  )
                ) : null}
                <Button to={`/opportunities`} variant={sponsoredOnly ? 'primary' : 'ghost'} className="w-full" size="sm">Find a sponsored seat</Button>
              </div>
              <div className="mt-2 text-[11px] text-muted">Quote valid until {formatDateTime(o.priceValidUntil)}</div>
            </div>
          );
        })}
      </div>
      {result ? (
        <Alert tone="info" className="mt-4" title="Payment started">
          {formatMoney(result.amount)} via {result.gateway}. <Link to={`/learn/payments/${result.paymentIntentId}`} className="font-semibold">Open payment</Link>
          {result.checkoutUrl ? <> or <a href={result.checkoutUrl} className="font-semibold">go to the gateway</a>.</> : '.'}
        </Alert>
      ) : null}
    </Card>
  );
}

/** Primary: pay with units (disabled with a hint when short); secondary: hosted card checkout. */
function PayButtons({ offering, full, units, balance, busy, busyWithUnits, onUnits, onCard }) {
  const short = units !== null && balance !== null && balance < units;
  const unknown = units === null || balance === null;
  return (
    <>
      <Button className="w-full" loading={busyWithUnits} disabled={full || short || unknown || busy} onClick={onUnits} title={short ? `You have ${formatUnits(balance)}; this needs ${formatUnits(units)}.` : undefined}>
        {units === null ? 'Pay with units' : `Pay ${formatUnits(units)}`}
      </Button>
      {short ? (
        <p className="text-xs text-muted">
          You have {formatUnits(balance)}; this course needs {formatUnits(units)}. <Link to="/learn/wallet" className="font-semibold text-royal">Buy {formatUnits(units - balance)}</Link>
        </p>
      ) : balance !== null && units !== null ? (
        <p className="text-xs text-muted">Wallet balance {formatUnits(balance)}; {formatUnits(balance - units)} left after this. <Link to="/learn/wallet" className="font-semibold text-royal">Wallet</Link></p>
      ) : null}
      <Button variant="secondary" className="w-full" loading={busy && !busyWithUnits} disabled={full || busy} onClick={onCard}>Pay {formatMoney(offering.price)} by card</Button>
    </>
  );
}

function Reviews({ course }) {
  const auth = useAuth();
  const reviews = course.recentReviews || [];
  return (
    <div className="mt-4 space-y-4">
      {reviews.length === 0 ? (
        <Card className="flex items-center gap-3 text-sm text-muted"><StatusPill tone="info" status="new" label="New course" /> No reviews yet.</Card>
      ) : (
        <ul className="space-y-3">
          {reviews.map((r, i) => (
            <li key={i}>
              <Card>
                <div className="flex items-center justify-between gap-2 text-sm"><Stars value={r.rating} /><span className="text-xs text-muted">{formatDate(r.createdAt)}</span></div>
                <p className="mt-2 text-sm text-navy">{r.body}</p>
              </Card>
            </li>
          ))}
        </ul>
      )}
      {auth.isAuthenticated ? <ReviewForm course={course} /> : <p className="text-sm text-muted"><Link to="/auth/sign-in">Sign in</Link> to review once you have completed 30% of the course.</p>}
    </div>
  );
}

function ReviewForm({ course }) {
  const [rating, setRating] = useState(5);
  const [body, setBody] = useState('');
  const [done, setDone] = useState(null);
  const enrolments = useQuery({ queryKey: ['lms', 'enrolments', 'me'], queryFn: () => api.get('/lms/enrolments/me'), retry: false });
  const mine = (enrolments.data || []).filter((e) => e.courseId === course.courseId);
  const enrolment = mine.find((e) => Number(e.progressPct) >= 30) || mine[0];

  const submit = useAction(() => postReview(course.courseId, { enrolmentId: enrolment.enrolmentId, rating: Number(rating), body: body.trim() }), {
    invalidate: [queryKeys.course(course.slug)],
    success: 'Thanks for your review',
    onSuccess: (res) => setDone(res),
  });

  if (enrolments.isPending) return null;
  if (!enrolment) return <p className="text-sm text-muted">Enrolled learners can leave a review after completing 30% of the course.</p>;
  if (done) return <Alert tone="success" title="Review published">The course now averages {Number(done.courseRatingAverage).toFixed(1)} from {done.courseRatingCount} reviews.</Alert>;

  return (
    <Card as="form" onSubmit={(e) => { e.preventDefault(); submit.mutate(); }}>
      <CardHeader title="Write a review" description={Number(enrolment.progressPct) < 30 ? `You are at ${Math.round(enrolment.progressPct)}%; reviews open at 30%.` : 'Verified enrolment.'} />
      <ErrorAlert error={submit.error} className="mb-3" />
      <div className="grid gap-3 sm:grid-cols-[160px_1fr]">
        <Field label="Rating">{(p) => <Select {...p} value={rating} onChange={(e) => setRating(e.target.value)}>{[5, 4, 3, 2, 1].map((n) => <option key={n} value={n}>{n} star{n > 1 ? 's' : ''}</option>)}</Select>}</Field>
        <Field label="Your review">{(p) => <Textarea {...p} rows={3} maxLength={2000} value={body} onChange={(e) => setBody(e.target.value)} placeholder="What did you learn? What would you change?" />}</Field>
      </div>
      <Button type="submit" className="mt-3" loading={submit.isPending} disabled={body.trim().length < 10 || Number(enrolment.progressPct) < 30}>Publish review</Button>
    </Card>
  );
}

function Stars({ value }) {
  return (
    <span className="inline-flex items-center gap-0.5" aria-label={`${value} out of 5`}>
      {[1, 2, 3, 4, 5].map((n) => <Star key={n} className={`h-4 w-4 ${n <= value ? 'fill-warn text-warn' : 'text-line'}`} aria-hidden />)}
      <span className="ml-1 text-xs text-muted">{value}/5</span>
    </span>
  );
}
