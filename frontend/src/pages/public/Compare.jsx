import { Link, useSearchParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { GitCompare, Star } from 'lucide-react';
import { Button, EmptyState, ErrorAlert, Loading, PageHeader, StatusPill } from '../../components/ui';
import { formatMoney, titleCase } from '../../lib/format';
import { compareCourses, queryKeys } from '../../api/public';

/** /courses/compare?ids=a,b,c → side-by-side table of up to three published courses. */
export default function Compare() {
  const [params] = useSearchParams();
  const ids = (params.get('ids') || '').split(',').map((s) => s.trim()).filter(Boolean).slice(0, 3);

  const query = useQuery({ queryKey: queryKeys.compare(ids), queryFn: () => compareCourses(ids), enabled: ids.length >= 1 });

  if (ids.length < 1) {
    return (
      <div className="mx-auto max-w-3xl px-4 py-10 sm:px-6">
        <EmptyState icon={GitCompare} title="Nothing to compare yet" description="Pick two or three courses from the marketplace and come back." action={<Button to="/courses">Browse courses</Button>} />
      </div>
    );
  }

  const courses = query.data?.courses || [];
  const rows = [
    ['Level', (c) => titleCase(c.level)],
    ['From price', (c) => (c.fromPrice ? formatMoney(c.fromPrice) : 'Free')],
    ['Formats', (c) => (c.formats || []).map(titleCase).join(', ') || '—'],
    ['Sponsored seats', (c) => (c.sponsoredSeats ? <StatusPill tone="success" status="sponsored" label="Available" /> : <StatusPill tone="neutral" status="none" label="None" />)],
    ['Lessons', (c) => c.lessonCount],
    ['Estimated time', (c) => `${Math.round((c.estimatedMinutes || 0) / 60)} h`],
    ['Rating', (c) => (c.ratingCount ? <span className="inline-flex items-center gap-1"><Star className="h-4 w-4 text-warn" aria-hidden />{Number(c.ratingAverage).toFixed(1)} ({c.ratingCount})</span> : 'New course')],
    ['Skills', (c) => (c.skills || []).join(', ') || '—'],
  ];

  return (
    <div className="mx-auto max-w-[1280px] px-4 py-8 sm:px-6">
      <PageHeader title="Compare courses" description="Up to three published courses side by side." actions={<Button to="/courses" variant="secondary">Back to courses</Button>} />
      {query.isPending ? <Loading /> : null}
      <ErrorAlert error={query.isError ? query.error : null} />
      {query.data && courses.length === 0 ? <EmptyState icon={GitCompare} title="Those courses are not published" description="Only published courses can be compared." action={<Button to="/courses">Browse courses</Button>} /> : null}
      {courses.length ? (
        <div className="overflow-x-auto rounded-card border border-line bg-white">
          <table className="w-full min-w-[640px] text-sm">
            <thead>
              <tr className="bg-surface text-left">
                <th scope="col" className="w-40 px-4 py-3 text-xs uppercase tracking-wide text-muted">Course</th>
                {courses.map((c) => (
                  <th key={c.courseId} scope="col" className="px-4 py-3">
                    <Link to={`/courses/${c.slug}`} className="font-bold text-navy hover:text-royal">{c.title}</Link>
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {rows.map(([label, render]) => (
                <tr key={label} className="border-t border-line">
                  <th scope="row" className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-muted">{label}</th>
                  {courses.map((c) => <td key={c.courseId} className="px-4 py-3 text-navy">{render(c)}</td>)}
                </tr>
              ))}
              <tr className="border-t border-line">
                <td className="px-4 py-3" />
                {courses.map((c) => <td key={c.courseId} className="px-4 py-3"><Button to={`/courses/${c.slug}`} size="sm">View course</Button></td>)}
              </tr>
            </tbody>
          </table>
        </div>
      ) : null}
    </div>
  );
}
