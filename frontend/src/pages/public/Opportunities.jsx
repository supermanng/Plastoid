import { useQuery } from '@tanstack/react-query';
import { Sparkles } from 'lucide-react';
import { Alert, Button, EmptyState, ErrorAlert, PageHeader, Pagination, Skeleton } from '../../components/ui';
import { useCursor } from '../../lib/hooks';
import { listOpportunities, queryKeys } from '../../api/public';
import { OpportunityCard } from './Home';

/** /opportunities — sponsored programmes open for applications (public). */
export default function Opportunities() {
  const cursor = useCursor();
  const query = useQuery({ queryKey: queryKeys.opportunities(cursor.cursor), queryFn: () => listOpportunities(cursor.cursor), placeholderData: (prev) => prev });
  const items = query.data?.items || [];

  return (
    <div className="mx-auto max-w-[1280px] px-4 py-8 sm:px-6">
      <PageHeader
        title="Sponsored opportunities"
        description="Seats paid for by sponsors. Check whether you qualify in seconds, then apply in under ten minutes. Most programmes need no payment; some ask for a small co-payment."
      />
      <Alert tone="info" className="mb-6" title="How selection works">
        Applications are scored on the sponsor's rubric and, where set, a short entry assessment. Offers must be accepted within five days or the seat goes to the waitlist.
      </Alert>

      {query.isPending ? <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">{[0, 1, 2].map((i) => <Skeleton key={i} className="h-44" />)}</div> : null}
      <ErrorAlert error={query.isError ? query.error : null} />
      {query.data && items.length === 0 ? (
        <EmptyState
          icon={Sparkles}
          title="No programmes are open right now"
          description="Sponsored seats open in waves. Browse paid courses meanwhile, or sign in so we can notify you when one matches your goal."
          action={<div className="flex gap-2"><Button to="/courses" variant="secondary">Browse courses</Button><Button to="/auth/sign-in">Sign in</Button></div>}
        />
      ) : null}
      {items.length ? <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">{items.map((o) => <OpportunityCard key={o.programmeId} o={o} />)}</div> : null}
      <Pagination hasMore={Boolean(query.data?.nextCursor)} onMore={() => cursor.next(query.data.nextCursor)} canBack={cursor.canBack} onBack={cursor.back} loading={query.isFetching} />
    </div>
  );
}
