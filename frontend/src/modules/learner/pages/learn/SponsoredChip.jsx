import { HandHeart } from 'lucide-react';

/**
 * "Sponsored by …" chip for an enrolment whose seat an organisation paid for (ADR 0021).
 * EnrolmentSummary.fundingLabel carries the sponsor's name; nothing renders without it.
 */
export function SponsoredChip({ enrolment, className = '' }) {
  const label = enrolment?.fundingLabel;
  if (!label) return null;
  return (
    <span className={`inline-flex max-w-full items-center gap-1 rounded-chip border border-teal/30 bg-teal-50 px-2.5 py-0.5 text-xs font-semibold text-teal-600 ${className}`} title={label}>
      <HandHeart className="h-3.5 w-3.5 shrink-0" aria-hidden />
      <span className="truncate">{label}</span>
    </span>
  );
}
