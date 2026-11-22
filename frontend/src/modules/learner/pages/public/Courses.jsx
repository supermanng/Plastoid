import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Filter, GitCompare, Search, X } from 'lucide-react';
import { Button, Card, Checkbox, Chip, EmptyState, ErrorAlert, Field, Input, PageHeader, Pagination, Select, Skeleton } from '../../../../shared/components/ui';
import { useCursor, useDebounce, useQueryParams } from '../../../../shared/lib/hooks';
import { nairaToKobo } from '../../../../shared/lib/format';
import { COURSE_FORMATS, LANGUAGES, LEVELS, queryKeys, searchCourses } from '../../../../shared/api/public';
import { CourseCard } from './Home';

const DEFAULTS = Object.freeze({ q: '', skill: '', level: '', funding: '', format: '', maxPriceKobo: '', lang: '', category: '' });

/** /courses — marketplace search with URL-backed filters, cursor paging and a compare tray (max 3). */
export default function Courses() {
  const [filters, setFilter, setMany] = useQueryParams(DEFAULTS);
  const [text, setText] = useState(filters.q);
  const debounced = useDebounce(text, 350);
  const cursor = useCursor();
  const [compare, setCompare] = useState([]);
  const [showFilters, setShowFilters] = useState(false);

  useEffect(() => {
    if (debounced !== filters.q) {
      setFilter('q', debounced);
      cursor.reset();
    }
  }, [debounced]); // eslint-disable-line react-hooks/exhaustive-deps

  const params = useMemo(() => ({ ...filters, cursor: cursor.cursor }), [filters, cursor.cursor]);
  const query = useQuery({ queryKey: queryKeys.courses(params), queryFn: () => searchCourses(params), placeholderData: (prev) => prev });

  const items = query.data?.items || [];
  const suggestions = query.data?.suggestions;
  const activeCount = Object.entries(filters).filter(([k, v]) => k !== 'q' && v).length;

  function change(key, value) {
    setFilter(key, value);
    cursor.reset();
  }

  function toggleCompare(course) {
    setCompare((list) => {
      if (list.some((c) => c.courseId === course.courseId)) return list.filter((c) => c.courseId !== course.courseId);
      if (list.length >= 3) return list;
      return [...list, course];
    });
  }

  return (
    <div className="mx-auto max-w-[1280px] px-4 py-8 sm:px-6">
      <PageHeader title="Courses" description="Published by verified tutors. Filter by level, format, price and language, or look for sponsored seats." />

      <form className="flex flex-col gap-2 sm:flex-row" role="search" onSubmit={(e) => { e.preventDefault(); setFilter('q', text); cursor.reset(); }}>
        <div className="relative flex-1">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-5 w-5 -translate-y-1/2 text-muted" aria-hidden />
          <Input aria-label="Search courses" className="pl-10" placeholder="Search by title, skill or keyword" value={text} onChange={(e) => setText(e.target.value)} />
        </div>
        <Button type="button" variant="secondary" icon={Filter} onClick={() => setShowFilters((v) => !v)} aria-expanded={showFilters}>
          Filters{activeCount ? ` (${activeCount})` : ''}
        </Button>
      </form>

      <div className="mt-3 flex flex-wrap gap-2">
        <Chip active={filters.funding === 'sponsored'} onClick={() => change('funding', filters.funding === 'sponsored' ? '' : 'sponsored')}>Sponsored seats</Chip>
        <Chip active={filters.funding === 'paid'} onClick={() => change('funding', filters.funding === 'paid' ? '' : 'paid')}>Paid</Chip>
        {COURSE_FORMATS.map(([v, label]) => (
          <Chip key={v} active={filters.format === v} onClick={() => change('format', filters.format === v ? '' : v)}>{label}</Chip>
        ))}
        {activeCount ? (
          <button type="button" className="inline-flex min-h-8 items-center gap-1 text-sm font-semibold text-royal" onClick={() => { setMany({ ...DEFAULTS, q: filters.q }); cursor.reset(); }}>
            <X className="h-4 w-4" /> Clear filters
          </button>
        ) : null}
      </div>

      {showFilters ? (
        <Card className="mt-4 grid gap-4 sm:grid-cols-2 lg:grid-cols-5">
          <Field label="Level">{(p) => (
            <Select {...p} value={filters.level} onChange={(e) => change('level', e.target.value)}>
              <option value="">Any</option>
              {LEVELS.map((l) => <option key={l} value={l} className="capitalize">{l[0].toUpperCase() + l.slice(1)}</option>)}
            </Select>
          )}</Field>
          <Field label="Language">{(p) => (
            <Select {...p} value={filters.lang} onChange={(e) => change('lang', e.target.value)}>
              <option value="">Any</option>
              {LANGUAGES.map(([v, label]) => <option key={v} value={v}>{label}</option>)}
            </Select>
          )}</Field>
          <Field label="Max price (₦)">{(p) => (
            <Input {...p} type="number" inputMode="numeric" min={0} step={1000} placeholder="e.g. 50000" value={filters.maxPriceKobo ? Number(filters.maxPriceKobo) / 100 : ''} onChange={(e) => change('maxPriceKobo', e.target.value ? String(nairaToKobo(e.target.value)) : '')} />
          )}</Field>
          <Field label="Skill">{(p) => (
            <Input {...p} list="skill-suggestions" placeholder="e.g. Excel" value={filters.skill} onChange={(e) => change('skill', e.target.value)} />
          )}</Field>
          <Field label="Category">{(p) => (
            <Input {...p} list="category-suggestions" placeholder="e.g. Data" value={filters.category} onChange={(e) => change('category', e.target.value)} />
          )}</Field>
          <datalist id="skill-suggestions">{(suggestions?.skills || []).map((s) => <option key={s} value={s} />)}</datalist>
          <datalist id="category-suggestions">{(suggestions?.categories || []).map((s) => <option key={s} value={s} />)}</datalist>
        </Card>
      ) : null}

      <div className="mt-6 flex items-center justify-between text-sm text-muted">
        <span>{query.data ? `${query.data.total ?? items.length} course${(query.data.total ?? items.length) === 1 ? '' : 's'}` : ' '}</span>
        {compare.length ? <span>{compare.length}/3 selected to compare</span> : <span className="hidden sm:inline">Tick up to three courses to compare them.</span>}
      </div>

      {query.isPending ? (
        <div className="mt-4 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">{[0, 1, 2, 3, 4, 5].map((i) => <Skeleton key={i} className="h-48" />)}</div>
      ) : null}
      <ErrorAlert error={query.isError ? query.error : null} className="mt-4" />

      {query.data && items.length === 0 ? (
        <NoResults filters={filters} suggestions={suggestions} onPick={(k, v) => { setMany({ ...DEFAULTS, [k]: v }); setText(''); cursor.reset(); }} />
      ) : null}

      {items.length ? (
        <div className="mt-4 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {items.map((c) => (
            <div key={c.courseId} className="relative">
              <CourseCard c={c} />
              <div className="absolute right-3 top-3 rounded-chip bg-white/90 px-2">
                <Checkbox
                  label={<span className="sr-only sm:not-sr-only sm:text-xs">Compare</span>}
                  className="min-h-9"
                  checked={compare.some((x) => x.courseId === c.courseId)}
                  disabled={compare.length >= 3 && !compare.some((x) => x.courseId === c.courseId)}
                  onChange={() => toggleCompare(c)}
                />
              </div>
            </div>
          ))}
        </div>
      ) : null}

      <Pagination hasMore={Boolean(query.data?.nextCursor)} onMore={() => cursor.next(query.data.nextCursor)} canBack={cursor.canBack} onBack={cursor.back} loading={query.isFetching} />

      {compare.length ? (
        <div className="fixed inset-x-0 bottom-0 z-40 border-t border-line bg-white p-3 shadow-[0_-4px_16px_rgb(0_59_122/0.08)]">
          <div className="mx-auto flex max-w-[1280px] flex-wrap items-center gap-2">
            <GitCompare className="h-5 w-5 text-royal" aria-hidden />
            <div className="flex flex-1 flex-wrap gap-1">
              {compare.map((c) => (
                <span key={c.courseId} className="inline-flex items-center gap-1 rounded-chip bg-surface px-2.5 py-1 text-xs font-semibold text-navy">
                  {c.title}
                  <button type="button" aria-label={`Remove ${c.title}`} onClick={() => toggleCompare(c)}><X className="h-3.5 w-3.5" /></button>
                </span>
              ))}
            </div>
            <Button variant="ghost" size="sm" onClick={() => setCompare([])}>Clear</Button>
            <Button to={`/courses/compare?ids=${compare.map((c) => c.courseId).join(',')}`} size="sm" disabled={compare.length < 2}>Compare {compare.length}</Button>
          </div>
        </div>
      ) : null}
    </div>
  );
}

function NoResults({ filters, suggestions, onPick }) {
  const subject = filters.q || filters.skill || filters.category || 'that';
  return (
    <div className="mt-4 space-y-4">
      <EmptyState
        icon={Search}
        title={`No courses match ${filters.q ? `“${filters.q}”` : 'these filters'}`}
        description="Try a broader search, or pick one of the suggestions below. You can also ask us to add a course."
        action={<Button href={`mailto:hello@plastoids.ng?subject=${encodeURIComponent(`Course request: ${subject}`)}`} variant="secondary">Request a course</Button>}
      />
      {suggestions?.skills?.length || suggestions?.categories?.length ? (
        <Card>
          <div className="text-sm font-semibold">Try one of these</div>
          <div className="mt-2 flex flex-wrap gap-2">
            {(suggestions.skills || []).map((s) => <Chip key={`s-${s}`} onClick={() => onPick('skill', s)}>{s}</Chip>)}
            {(suggestions.categories || []).map((c) => <Chip key={`c-${c}`} onClick={() => onPick('category', c)}>{c}</Chip>)}
          </div>
        </Card>
      ) : null}
      {suggestions?.popular?.length ? (
        <div>
          <h2 className="text-lg">Popular right now</h2>
          <div className="mt-3 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">{suggestions.popular.map((c) => <CourseCard key={c.courseId} c={c} />)}</div>
        </div>
      ) : null}
      <p className="text-sm text-muted">Looking for a tutor instead? <Link to="/tutors">Find a tutor</Link>.</p>
    </div>
  );
}
