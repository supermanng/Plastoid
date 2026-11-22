import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { ArrowRight, Award, BookOpen, Briefcase, Building2, GraduationCap, Search, ShieldCheck, Sparkles, Star, Users } from 'lucide-react';
import { Button, Card, Skeleton, StatusPill } from '../../../../shared/components/ui';
import { api } from '../../../../shared/lib/api';
import { formatDate, koboOf } from '../../../../shared/lib/format';
import { UnitPrice } from '../../../../shared/lib/units.jsx';

/*
 * Public homepage (blueprint §14 core screens): hero with search, sponsored opportunities strip, categories,
 * top courses, how it works for learners · tutors · sponsors.
 */
export default function Home() {
  const navigate = useNavigate();
  const [q, setQ] = useState('');

  const opportunities = useQuery({
    queryKey: ['opportunities', 'home'],
    queryFn: () => api.get('/sponsorship/opportunities', { limit: 6 }, { auth: false }),
  });
  const courses = useQuery({
    queryKey: ['courses', 'home'],
    queryFn: () => api.get('/catalogue/courses', { limit: 6 }, { auth: false }),
  });

  const categories = courses.data?.suggestions?.categories?.length
    ? courses.data.suggestions.categories
    : ['Software', 'Data', 'Design', 'Business', 'Digital marketing', 'Trades'];

  return (
    <div>
      {/* Hero */}
      <section className="relative overflow-hidden bg-navy text-white">
        <div className="brand-gradient absolute inset-x-0 bottom-0 h-1" aria-hidden />
        <div className="mx-auto grid max-w-[1280px] gap-10 px-4 py-16 sm:px-6 lg:grid-cols-[1.2fr_1fr] lg:py-24">
          <div>
            <span className="inline-flex items-center gap-2 rounded-chip bg-white/10 px-3 py-1 text-xs font-semibold text-teal">
              <Sparkles className="h-3.5 w-3.5" /> Sponsored seats open now
            </span>
            <h1 className="mt-4 text-4xl leading-tight sm:text-5xl">
              Skills training for Nigeria, <span className="text-teal">funded by people who need your talent.</span>
            </h1>
            <p className="mt-4 max-w-xl text-base text-white/80 sm:text-lg">
              Apply for a sponsored seat, enrol in a paid course, or book a verified tutor. Learn on any phone, earn a certificate anyone can verify.
            </p>
            <form
              className="mt-8 flex max-w-xl flex-col gap-2 sm:flex-row"
              onSubmit={(e) => {
                e.preventDefault();
                navigate(q.trim() ? `/courses?q=${encodeURIComponent(q.trim())}` : '/courses');
              }}
              role="search"
            >
              <label className="sr-only" htmlFor="home-search">Search courses</label>
              <div className="relative flex-1">
                <Search className="pointer-events-none absolute left-3 top-1/2 h-5 w-5 -translate-y-1/2 text-muted" aria-hidden />
                <input id="home-search" className="input h-12 pl-10 text-navy" placeholder="What do you want to learn? e.g. data analysis" value={q} onChange={(e) => setQ(e.target.value)} />
              </div>
              <Button type="submit" size="lg" variant="teal">Search</Button>
            </form>
            <div className="mt-6 flex flex-wrap gap-3 text-sm">
              <Button to="/opportunities" variant="secondary" size="sm">Apply for a sponsored seat</Button>
              <Button to="/become-a-tutor" variant="ghost" size="sm" className="text-white hover:bg-white/10">Become a tutor</Button>
              <Button to="/for-sponsors" variant="ghost" size="sm" className="text-white hover:bg-white/10">Partner as a sponsor</Button>
            </div>
          </div>
          <ul className="grid gap-3 self-center sm:grid-cols-3 lg:grid-cols-1">
            {[
              [ShieldCheck, 'Verified tutors', 'Identity, credentials and a reviewed demo lesson before anyone teaches.'],
              [Award, 'Verifiable certificates', 'Every certificate has a public code and a tamper-evident hash.'],
              [Users, 'Sponsors see impact', 'Funnel, attendance and completion, live, with the money on a ledger.'],
            ].map(([Icon, title, text]) => (
              <li key={title} className="flex gap-3 rounded-card bg-white/5 p-4">
                <Icon className="h-6 w-6 shrink-0 text-teal" aria-hidden />
                <div>
                  <div className="font-semibold">{title}</div>
                  <div className="text-sm text-white/70">{text}</div>
                </div>
              </li>
            ))}
          </ul>
        </div>
      </section>

      {/* Sponsored opportunities */}
      <Section
        title="Sponsored opportunities"
        description="Seats paid for by sponsors. Check your eligibility in seconds; apply in under ten minutes."
        action={<Button to="/opportunities" variant="ghost" size="sm">See all <ArrowRight className="h-4 w-4" /></Button>}
      >
        {opportunities.isPending ? <CardSkeletons /> : null}
        {opportunities.isError ? <p className="text-sm text-muted">Opportunities are unavailable right now.</p> : null}
        {opportunities.data ? (
          itemsOf(opportunities.data).length ? (
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
              {itemsOf(opportunities.data).map((o) => <OpportunityCard key={o.programmeId} o={o} />)}
            </div>
          ) : (
            <Card className="text-sm text-muted">No sponsored programmes are open for applications right now. Browse paid courses below or <Link to="/for-sponsors">sponsor one</Link>.</Card>
          )
        ) : null}
      </Section>

      {/* Categories */}
      <section className="bg-surface">
        <div className="mx-auto max-w-[1280px] px-4 py-10 sm:px-6">
          <h2 className="text-xl">Browse by category</h2>
          <div className="mt-4 flex flex-wrap gap-2">
            {categories.map((c) => (
              <Link key={c} to={`/courses?category=${encodeURIComponent(c)}`} className="inline-flex min-h-10 items-center rounded-chip border border-line bg-white px-4 text-sm font-semibold text-navy hover:border-royal hover:text-royal">
                {c}
              </Link>
            ))}
          </div>
        </div>
      </section>

      {/* Top courses */}
      <Section title="Top courses" description="Published by verified tutors. Self-paced, cohort or one-to-one." action={<Button to="/courses" variant="ghost" size="sm">Browse all <ArrowRight className="h-4 w-4" /></Button>}>
        {courses.isPending ? <CardSkeletons /> : null}
        {courses.isError ? <p className="text-sm text-muted">Courses are unavailable right now.</p> : null}
        {courses.data ? (
          itemsOf(courses.data).length ? (
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
              {itemsOf(courses.data).map((c) => <CourseCard key={c.courseId} c={c} />)}
            </div>
          ) : (
            <Card className="text-sm text-muted">No courses are published yet. <Link to="/become-a-tutor">Be the first tutor.</Link></Card>
          )
        ) : null}
      </Section>

      {/* How it works */}
      <section className="border-t border-line bg-surface">
        <div className="mx-auto max-w-[1280px] px-4 py-14 sm:px-6">
          <h2 className="text-2xl">How Plastoids works</h2>
          <div className="mt-8 grid gap-6 md:grid-cols-3">
            <HowCard icon={GraduationCap} title="Learners" steps={['Sign up with your phone', 'Check eligibility and apply, or enrol', 'Learn live and on demand', 'Earn a verifiable certificate']} cta={['/opportunities', 'Find a sponsored seat']} />
            <HowCard icon={BookOpen} title="Tutors" steps={['Apply and verify your identity', 'Pass a short subject review', 'Publish courses and set your rates', 'Get paid weekly to your bank']} cta={['/become-a-tutor', 'Apply to teach']} />
            <HowCard icon={Briefcase} title="Sponsors" steps={['Verify your organisation', 'Build a campaign and approve the quote', 'Review the shortlist or delegate', 'Download the impact report']} cta={['/for-sponsors', 'Sponsor a programme']} />
          </div>
        </div>
      </section>
    </div>
  );
}

function Section({ title, description, action, children }) {
  return (
    <section className="mx-auto max-w-[1280px] px-4 py-12 sm:px-6">
      <div className="mb-6 flex flex-wrap items-end justify-between gap-3">
        <div>
          <h2 className="text-2xl">{title}</h2>
          {description ? <p className="mt-1 text-sm text-muted">{description}</p> : null}
        </div>
        {action}
      </div>
      {children}
    </section>
  );
}

function CardSkeletons() {
  return (
    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
      {[0, 1, 2].map((i) => <Skeleton key={i} className="h-44" />)}
    </div>
  );
}

function itemsOf(data) {
  return Array.isArray(data) ? data : data?.items || [];
}

export function OpportunityCard({ o }) {
  const free = !koboOf(o.coPayPerSeat);
  return (
    <Card as={Link} to={`/opportunities/${o.programmeId}`} className="flex flex-col gap-3 transition-shadow hover:shadow-md">
      <div className="flex items-start justify-between gap-2">
        <StatusPill tone="success" status="sponsored" label={free ? 'Fully sponsored' : `${o.sponsorSharePercent}% sponsored`} />
        <span className="text-xs text-muted">{o.seatsOpen} of {o.seats} seats open</span>
      </div>
      <div>
        <h3 className="text-base font-bold leading-snug text-navy">{o.name}</h3>
        <p className="mt-0.5 text-sm text-muted">{o.courseTitle || 'Custom programme'} · by {o.sponsorName}</p>
      </div>
      <div className="mt-auto flex flex-wrap items-center justify-between gap-2 text-xs text-muted">
        <span>{free ? 'No co-payment' : <>You pay <UnitPrice money={o.coPayPerSeat} size="sm" inline /></>}</span>
        {o.applicationsCloseAt ? <span>Closes {formatDate(o.applicationsCloseAt)}</span> : null}
      </div>
    </Card>
  );
}

export function CourseCard({ c }) {
  return (
    <Card as={Link} to={`/courses/${c.slug}`} className="flex flex-col gap-3 transition-shadow hover:shadow-md">
      <div className="flex items-start justify-between gap-2">
        <span className="rounded-chip bg-surface px-2.5 py-0.5 text-xs font-semibold capitalize text-muted">{c.level}</span>
        {c.sponsoredSeats ? <StatusPill tone="success" status="sponsored" label="Sponsored seats" /> : null}
      </div>
      <div>
        <h3 className="text-base font-bold leading-snug text-navy">{c.title}</h3>
        {c.providerName ? <p className="mt-0.5 flex items-center gap-1 text-xs font-semibold text-muted"><Building2 className="h-3.5 w-3.5 text-cyan" aria-hidden /> {c.providerName}</p> : null}
        <p className="mt-0.5 line-clamp-2 text-sm text-muted">{c.summary}</p>
      </div>
      <div className="mt-auto flex items-center justify-between text-sm">
        {c.fromPrice ? <UnitPrice money={c.fromPrice} size="sm" prefix="From " /> : <span className="font-bold text-navy">Free</span>}
        <span className="flex items-center gap-1 text-muted">
          <Star className="h-4 w-4 text-warn" aria-hidden />
          {c.ratingCount ? `${Number(c.ratingAverage).toFixed(1)} (${c.ratingCount})` : 'New course'}
        </span>
      </div>
    </Card>
  );
}

function HowCard({ icon: Icon, title, steps, cta }) {
  return (
    <Card className="flex flex-col">
      <div className="flex items-center gap-3">
        <span className="grid h-10 w-10 place-items-center rounded-full bg-royal-50 text-royal"><Icon className="h-5 w-5" /></span>
        <h3 className="text-lg">{title}</h3>
      </div>
      <ol className="mt-4 space-y-2 text-sm text-navy">
        {steps.map((s, i) => (
          <li key={s} className="flex gap-3">
            <span className="grid h-6 w-6 shrink-0 place-items-center rounded-full bg-surface text-xs font-bold text-muted">{i + 1}</span>
            {s}
          </li>
        ))}
      </ol>
      <Button to={cta[0]} variant="secondary" size="sm" className="mt-5 self-start">{cta[1]}</Button>
    </Card>
  );
}
