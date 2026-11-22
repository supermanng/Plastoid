import { useQuery } from '@tanstack/react-query';
import { Award, Copy, ExternalLink, Share2 } from 'lucide-react';
import { Button, Card, EmptyState, ErrorAlert, Loading, PageHeader, StatusPill, useToast } from '../../../../shared/components/ui';
import { formatDate } from '../../../../shared/lib/format';
import { certificates, keys } from '../../api/learn';

/** GET /certificates/me → verifiable certificates with public codes. */
export default function Certificates() {
  const query = useQuery({ queryKey: keys.certificates, queryFn: certificates.mine });
  const toast = useToast();

  const verifyLink = (c) => `${window.location.origin}/verify/${encodeURIComponent(c.publicCode)}`;
  const copy = (c) => navigator.clipboard?.writeText(verifyLink(c)).then(() => toast.success('Link copied'));
  const share = async (c) => {
    const data = { title: `${c.courseTitle} — Plastoids certificate`, text: `I completed ${c.courseTitle} on Plastoids. Verify: ${c.publicCode}`, url: verifyLink(c) };
    if (navigator.share) {
      try { await navigator.share(data); } catch { /* cancelled */ }
    } else copy(c);
  };

  return (
    <div>
      <PageHeader title="My certificates" description="Each certificate carries a public code and a tamper-evident hash. Anyone can verify it without signing in." />
      {query.isPending ? <Loading /> : null}
      <ErrorAlert error={query.error} />
      {query.data && !query.data.length ? <EmptyState icon={Award} title="No certificates yet" description="Complete every required lesson and pass the assessments to earn one." action={<Button to="/learn/courses" size="sm">My courses</Button>} /> : null}
      <ul className="grid gap-4 md:grid-cols-2">
        {(query.data || []).map((c) => (
          <Card key={c.certificateId} as="li" className="relative overflow-hidden">
            <div className="brand-gradient absolute inset-x-0 top-0 h-1" aria-hidden />
            <div className="flex items-start justify-between gap-3">
              <span className="grid h-12 w-12 shrink-0 place-items-center rounded-full bg-teal-50 text-teal-600"><Award className="h-6 w-6" /></span>
              <StatusPill status={c.revokedAt ? 'revoked' : 'valid'} />
            </div>
            <h3 className="mt-3 text-lg font-bold text-navy">{c.courseTitle}</h3>
            <p className="text-sm text-muted">Awarded to {c.learnerName} · {formatDate(c.issuedAt)} · score {Number(c.finalScore).toFixed(0)}%</p>
            <div className="mt-3 rounded-input bg-surface px-3 py-2 font-mono text-sm tracking-wider text-navy">{c.publicCode}</div>
            {c.revokedAt ? <p className="mt-2 text-xs text-danger">Revoked {formatDate(c.revokedAt)}.</p> : null}
            <div className="mt-4 flex flex-wrap gap-2">
              <Button to={`/verify/${encodeURIComponent(c.publicCode)}`} size="sm" variant="secondary" icon={ExternalLink}>Verification page</Button>
              <Button size="sm" variant="ghost" icon={Copy} onClick={() => copy(c)}>Copy link</Button>
              <Button size="sm" variant="ghost" icon={Share2} onClick={() => share(c)}>Share</Button>
            </div>
            <p className="mt-3 break-all text-[10px] text-muted">Hash {c.hash}</p>
          </Card>
        ))}
      </ul>
    </div>
  );
}
