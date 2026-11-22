import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Building2, Check, Handshake, X } from 'lucide-react';
import { Alert, Button, Card, EmptyState, ErrorAlert, Field, Modal, PageHeader, QueryState, StatusPill, Textarea } from '../../../../shared/components/ui';
import { useAction } from '../../../../shared/lib/hooks';
import { formatDateTime } from '../../../../shared/lib/format';
import { engagementApi, tutorKeys } from '../../api/tutor';
import { describeTerms } from '../../../../shared/lib/engagementTerms';

/*
 * /tutor/offers — engagements offered to the signed-in tutor by organisations (ADR 0019).
 *   GET  /catalogue/engagements/mine                → EngagementView[]
 *   POST /catalogue/engagements/{id}/respond        { accept, note? }
 * offered → active | declined; active → ended (by the organisation).
 */
const GROUPS = [
  ['offered', 'Waiting for your answer', 'Accept to teach the course on the organisation\'s terms, or decline.'],
  ['active', 'Active', 'You can open cohorts on these courses and are paid your share of each sale.'],
  ['ended', 'Ended', 'The organisation ended these engagements.'],
  ['declined', 'Declined', 'Offers you turned down.'],
];

export default function Offers() {
  const offers = useQuery({ queryKey: tutorKeys.engagements, queryFn: engagementApi.mine });
  const [responding, setResponding] = useState(null); // { engagement, accept }

  return (
    <div className="space-y-6">
      <PageHeader title="Offers" description="Organisations that want you to teach their courses. Terms are a share of each sale or a fixed fee per enrolment." className="mb-0" />
      <QueryState
        query={offers}
        empty={<EmptyState icon={Handshake} title="No offers yet" description="When a verified organisation engages you to teach one of its courses, the offer appears here." />}
      >
        {(items) => {
          const byStatus = (s) => items.filter((e) => String(e.status).toLowerCase() === s);
          return GROUPS.map(([status, title, description]) => {
            const rows = byStatus(status);
            if (!rows.length) return null;
            return (
              <section key={status}>
                <h2 className="text-lg">{title}</h2>
                <p className="mb-3 text-sm text-muted">{description}</p>
                <ul className="space-y-3">
                  {rows.map((e) => <OfferCard key={e.engagementId} e={e} onRespond={(accept) => setResponding({ engagement: e, accept })} />)}
                </ul>
              </section>
            );
          });
        }}
      </QueryState>

      <RespondDialog state={responding} onClose={() => setResponding(null)} />
    </div>
  );
}

function OfferCard({ e, onRespond }) {
  const status = String(e.status).toLowerCase();
  return (
    <li>
      <Card className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <span className="text-base font-bold text-navy">{e.courseTitle}</span>
            <StatusPill status={status} />
          </div>
          <div className="mt-1 flex items-center gap-1.5 text-sm text-muted"><Building2 className="h-4 w-4" aria-hidden /> {e.organisationName || 'Organisation'}</div>
          <div className="mt-2 text-sm text-navy"><span className="font-semibold">Terms:</span> {describeTerms(e.terms)}</div>
          {e.note ? <p className="mt-1 text-sm text-muted">“{e.note}”</p> : null}
          <div className="mt-2 text-xs text-muted">
            Offered {formatDateTime(e.offeredAt)}
            {e.respondedAt ? ` · responded ${formatDateTime(e.respondedAt)}` : ''}
            {e.endedAt ? ` · ended ${formatDateTime(e.endedAt)}` : ''}
          </div>
        </div>
        <div className="flex flex-wrap gap-2 sm:justify-end">
          {status === 'offered' ? (
            <>
              <Button icon={Check} onClick={() => onRespond(true)}>Accept</Button>
              <Button variant="secondary" icon={X} onClick={() => onRespond(false)}>Decline</Button>
            </>
          ) : null}
          {status === 'active' ? <Button to="/tutor/cohorts" variant="secondary">Open a cohort</Button> : null}
          {e.courseSlug ? <Button to={`/courses/${e.courseSlug}`} variant="ghost" size="sm">View course</Button> : null}
        </div>
      </Card>
    </li>
  );
}

function RespondDialog({ state, onClose }) {
  const [note, setNote] = useState('');
  const open = Boolean(state);
  const e = state?.engagement;
  const accept = Boolean(state?.accept);
  const respond = useAction(() => engagementApi.respond(e.engagementId, { accept, note: note.trim() }), {
    success: accept ? 'Offer accepted. You can open cohorts on this course.' : 'Offer declined.',
    invalidate: [tutorKeys.engagements, tutorKeys.myCourses],
    onSuccess: () => { setNote(''); onClose(); },
  });
  if (!open) return null;
  return (
    <Modal
      open={open}
      onClose={onClose}
      size="sm"
      title={accept ? 'Accept this offer?' : 'Decline this offer?'}
      description={`${e.courseTitle} · ${e.organisationName || 'Organisation'}`}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>Cancel</Button>
          <Button variant={accept ? 'primary' : 'danger'} loading={respond.isPending} onClick={() => respond.mutate()}>{accept ? 'Accept' : 'Decline'}</Button>
        </>
      }
    >
      <ErrorAlert error={respond.error} className="mb-3" />
      <Alert tone={accept ? 'info' : 'warn'} className="mb-3">
        {accept
          ? <>You will teach on these terms: <span className="font-semibold">{describeTerms(e.terms)}</span>, paid from the sale net of platform commission after the 7-day hold. The organisation can end the engagement later.</>
          : 'The organisation will be told you declined. They can make a new offer later.'}
      </Alert>
      <Field label="Note to the organisation (optional)">
        {(p) => <Textarea {...p} rows={3} value={note} onChange={(ev) => setNote(ev.target.value)} placeholder={accept ? 'Looking forward to it.' : 'Why you are declining, if you want to say.'} />}
      </Field>
      {accept && e.courseSlug ? <p className="mt-3 text-xs text-muted">Course page: <Link to={`/courses/${e.courseSlug}`} className="font-semibold text-royal">/courses/{e.courseSlug}</Link></p> : null}
    </Modal>
  );
}
