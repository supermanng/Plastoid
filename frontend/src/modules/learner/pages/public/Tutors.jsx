import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { BadgeCheck, Search, Star, Users, X } from 'lucide-react';
import { Avatar, Button, Card, Chip, EmptyState, ErrorAlert, Field, Input, PageHeader, Pagination, Select, Skeleton } from '../../../../shared/components/ui';
import { useCursor, useDebounce, useQueryParams } from '../../../../shared/lib/hooks';
import { formatMoney, nairaToKobo, titleCase } from '../../../../shared/lib/format';
import { UnitPrice } from '../../../../shared/lib/units.jsx';
import { LANGUAGES, listTutors, queryKeys, TEACHING_FORMATS } from '../../../../shared/api/public';

const DEFAULTS = Object.freeze({ q: '', skill: '', maxRate: '', lang: '', format: '' });

/** /tutors — verified tutor discovery with filters in the URL and cursor paging. */
export default function Tutors() {
  const [filters, setFilter, setMany] = useQueryParams(DEFAULTS);
  const [text, setText] = useState(filters.q);
  const debounced = useDebounce(text, 350);
  const cursor = useCursor();

  useEffect(() => {
    if (debounced !== filters.q) {
      setFilter('q', debounced);
      cursor.reset();
    }
  }, [debounced]); // eslint-disable-line react-hooks/exhaustive-deps

  const params = useMemo(() => ({ ...filters, cursor: cursor.cursor }), [filters, cursor.cursor]);
  const query = useQuery({ queryKey: queryKeys.tutors(params), queryFn: () => listTutors(params), placeholderData: (prev) => prev });
  const items = query.data?.items || [];
  const active = Object.entries(filters).filter(([k, v]) => k !== 'q' && v);

  function change(key, value) {
    setFilter(key, value);
    cursor.reset();
  }

  return (
    <div className="mx-auto max-w-[1280px] px-4 py-8 sm:px-6">
      <PageHeader title="Find a tutor" description="Every tutor passed identity, credential and subject checks. Book a one-hour slot or a discounted package." />
      <Card className="grid gap-3 sm:grid-cols-2 lg:grid-cols-[2fr_1fr_1fr_1fr_1fr]">
        <Field label="Search">{(p) => (
          <div className="relative"><Search className="pointer-events-none absolute left-3 top-1/2 h-5 w-5 -translate-y-1/2 text-muted" aria-hidden /><Input {...p} className="pl-10" placeholder="Subject, headline or keyword" value={text} onChange={(e) => setText(e.target.value)} /></div>
        )}</Field>
        <Field label="Subject / skill">{(p) => <Input {...p} placeholder="e.g. Mathematics" value={filters.skill} onChange={(e) => change('skill', e.target.value)} />}</Field>
        <Field label="Max hourly rate (₦)">{(p) => <Input {...p} type="number" inputMode="numeric" min={0} step={500} placeholder="e.g. 10000" value={filters.maxRate ? Number(filters.maxRate) / 100 : ''} onChange={(e) => change('maxRate', e.target.value ? String(nairaToKobo(e.target.value)) : '')} />}</Field>
        <Field label="Language">{(p) => <Select {...p} value={filters.lang} onChange={(e) => change('lang', e.target.value)}><option value="">Any</option>{LANGUAGES.map(([v, l]) => <option key={v} value={v}>{l}</option>)}</Select>}</Field>
        <Field label="Format">{(p) => <Select {...p} value={filters.format} onChange={(e) => change('format', e.target.value)}><option value="">Any</option>{TEACHING_FORMATS.map(([v, l]) => <option key={v} value={v}>{l}</option>)}</Select>}</Field>
      </Card>
      {active.length ? (
        <div className="mt-3 flex flex-wrap items-center gap-2 text-sm">
          {active.map(([k, v]) => <Chip key={k} active onClick={() => change(k, '')}>{titleCase(k)}: {k === 'maxRate' ? `≤ ${formatMoney(Number(v))}` : v} <X className="h-3.5 w-3.5" /></Chip>)}
          <button type="button" className="font-semibold text-royal" onClick={() => { setMany({ ...DEFAULTS, q: filters.q }); cursor.reset(); }}>Clear all</button>
        </div>
      ) : null}

      {query.isPending ? <div className="mt-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">{[0, 1, 2, 3, 4, 5].map((i) => <Skeleton key={i} className="h-44" />)}</div> : null}
      <ErrorAlert error={query.isError ? query.error : null} className="mt-6" />
      {query.data && items.length === 0 ? (
        <EmptyState
          className="mt-6"
          icon={Users}
          title="No tutors match"
          description="Try relaxing a filter: raise the maximum rate, allow any language, or search a broader subject."
          action={<div className="flex flex-wrap justify-center gap-2">
            {filters.maxRate ? <Button variant="secondary" size="sm" onClick={() => change('maxRate', '')}>Any rate</Button> : null}
            {filters.lang ? <Button variant="secondary" size="sm" onClick={() => change('lang', '')}>Any language</Button> : null}
            {filters.format ? <Button variant="secondary" size="sm" onClick={() => change('format', '')}>Any format</Button> : null}
            {filters.skill || filters.q ? <Button variant="secondary" size="sm" onClick={() => { setMany({ ...DEFAULTS }); setText(''); cursor.reset(); }}>Clear search</Button> : null}
          </div>}
        />
      ) : null}
      {items.length ? <div className="mt-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">{items.map((t) => <TutorCard key={t.tutorId} t={t} />)}</div> : null}
      <Pagination hasMore={Boolean(query.data?.nextCursor)} onMore={() => cursor.next(query.data.nextCursor)} canBack={cursor.canBack} onBack={cursor.back} loading={query.isFetching} />
    </div>
  );
}

export function TutorCard({ t }) {
  return (
    <Card as={Link} to={`/tutors/${t.tutorId}`} className="flex flex-col gap-3 transition-shadow hover:shadow-md">
      <div className="flex items-start gap-3">
        <Avatar name={t.headline} size={44} />
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-1 font-bold text-navy"><span className="truncate">{t.headline}</span><BadgeCheck className="h-4 w-4 shrink-0 text-teal" aria-label="Verified tutor" /></div>
          <div className="text-xs capitalize text-muted">{titleCase(t.level)} · {(t.languages || []).map((l) => l.toUpperCase()).join(', ')}</div>
        </div>
      </div>
      <div className="flex flex-wrap gap-1">{(t.subjects || []).slice(0, 4).map((s) => <span key={s} className="rounded-chip bg-surface px-2 py-0.5 text-xs font-semibold text-muted">{s}</span>)}</div>
      <div className="mt-auto flex items-center justify-between text-sm">
        <span className="inline-flex items-baseline gap-1"><UnitPrice money={t.hourlyRate} size="sm" /><span className="text-xs text-muted">/hour</span></span>
        <span className="flex items-center gap-1 text-muted"><Star className="h-4 w-4 text-warn" aria-hidden />{t.ratingCount ? `${Number(t.ratingAverage).toFixed(1)} (${t.ratingCount})` : 'New tutor'}</span>
      </div>
      <div className="text-xs text-muted">{(t.formats || []).map(titleCase).join(' · ')}</div>
    </Card>
  );
}
