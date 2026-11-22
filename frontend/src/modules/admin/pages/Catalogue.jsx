import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Button, Card, CardHeader, KeyValue, Loading, PageHeader, StatusPill } from '../../../shared/components/ui';
import { formatMoney } from '../../../shared/lib/format';
import { hrefFor } from '../../../shared/lib/navigation';
import { adminApi, adminKeys } from '../api/admin';
import { AdminError, IdLookup } from './_shared';
import { ModerationQueue } from './Queues';

/** Moderation queue plus a read-only preview of any published course (GET /catalogue/courses/{slug}). */
export default function Catalogue() {
  const [slug, setSlug] = useState('');
  const preview = useQuery({ queryKey: adminKeys.course(slug), queryFn: () => adminApi.course(slug), enabled: Boolean(slug), retry: false });

  return (
    <div>
      <PageHeader title="Course moderation" description="Courses queued for content review. Approve to let the tutor publish an immutable version; reject with a note they can act on." />
      <ModerationQueue />
      <Card className="mt-6">
        <CardHeader title="Preview a course" description="Published courses only; drafts in review are previewed from the tutor's builder." />
        <IdLookup label="Course slug" placeholder="e.g. data-analysis-with-excel" buttonLabel="Load" onOpen={setSlug} />
        {slug && preview.isPending ? <Loading /> : null}
        {slug && preview.isError ? <AdminError error={preview.error} className="mt-4" /> : null}
        {preview.data ? <CoursePreview c={preview.data} /> : null}
      </Card>
    </div>
  );
}

function CoursePreview({ c }) {
  return (
    <div className="mt-4">
      <div className="flex flex-wrap items-center gap-2">
        <h3 className="text-lg">{c.title}</h3>
        {c.status ? <StatusPill status={c.status} /> : null}
        {c.version ? <span className="text-xs text-muted">v{c.version}</span> : null}
      </div>
      <p className="mt-1 text-sm text-muted">{c.summary}</p>
      <KeyValue className="mt-4" cols={3} items={[['Level', c.level], ['Language', c.language], ['Category', c.category], ['Skills', (c.skills || []).join(', ')], ['Rating', c.ratingCount ? `${Number(c.ratingAverage).toFixed(1)} (${c.ratingCount})` : 'new'], ['Tutor', c.tutor?.displayName || c.tutorName]]} />
      {c.offerings?.length ? (
        <>
          <h4 className="mt-5 text-xs font-semibold uppercase tracking-wide text-muted">Offerings</h4>
          <ul className="mt-2 divide-y divide-line text-sm">
            {c.offerings.map((o, i) => (
              <li key={o.offeringId || i} className="flex items-center justify-between py-1.5">
                <span className="capitalize">{String(o.format || '').replace('_', ' ')}</span>
                <span className="tabular">{formatMoney(o.price)} {o.acceptsSponsorship ? <span className="ml-2 text-xs text-teal-600">sponsorable</span> : null}</span>
              </li>
            ))}
          </ul>
        </>
      ) : null}
      {c.modules?.length || c.outline?.length ? (
        <>
          <h4 className="mt-5 text-xs font-semibold uppercase tracking-wide text-muted">Outline</h4>
          <ol className="mt-2 space-y-2 text-sm">
            {(c.modules || c.outline).map((m, i) => (
              <li key={m.moduleId || i}>
                <div className="font-semibold">{i + 1}. {m.title}</div>
                {m.lessons?.length ? <ul className="ml-4 mt-1 list-disc text-muted">{m.lessons.map((l, j) => <li key={l.lessonId || j}>{l.title} <span className="text-[11px] capitalize">({l.type || l.kind})</span></li>)}</ul> : null}
              </li>
            ))}
          </ol>
        </>
      ) : null}
      <Button variant="ghost" size="sm" className="mt-4" href={hrefFor(`/courses/${c.slug}`)} target="_blank" rel="noreferrer">Open public page</Button>
    </div>
  );
}
