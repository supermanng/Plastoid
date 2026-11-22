/*
 * Tutor engagement terms on organisation courses (ADR 0019): shared by the organisation portal (sponsor module) and
 * the tutor's offers page (learner module). All amounts in kobo.
 */
import { formatMoney, koboOf } from './format';

/** Platform commission on organisation course sales: fee-rule code `course_provider`, default Fees:CourseProviderBps. */
export const COURSE_PROVIDER_COMMISSION_BPS = 1500;

export const TERM_KINDS = [
  ['revenue_share', 'Revenue share'],
  ['per_enrolment', 'Fixed fee per enrolment'],
];

export const ENGAGEMENT_STATUSES = ['offered', 'active', 'declined', 'ended'];

/** "30% of each sale" / "₦5,000 per enrolment". */
export function describeTerms(terms) {
  if (!terms) return '—';
  const kind = String(terms.kind || '').toLowerCase();
  if (kind === 'revenue_share') return `${(Number(terms.shareBps) || 0) / 100}% of each sale`;
  if (kind === 'per_enrolment') return `${formatMoney(terms.fixedFee)} per paid enrolment`;
  return kind.replace(/_/g, ' ');
}

/**
 * Where a sale of `priceKobo` goes under `terms`: platform commission first, the tutor share from the net, the
 * organisation keeps the rest. Mirrors the ADR §5 maths; all amounts in kobo.
 */
export function splitSale(priceKobo, terms, commissionBps = COURSE_PROVIDER_COMMISSION_BPS) {
  const price = Math.max(0, Math.round(Number(priceKobo) || 0));
  const commission = Math.round((price * commissionBps) / 10_000);
  const net = price - commission;
  let tutor = 0;
  const kind = String(terms?.kind || '').toLowerCase();
  if (kind === 'revenue_share') tutor = Math.round((net * (Number(terms.shareBps) || 0)) / 10_000);
  else if (kind === 'per_enrolment') tutor = Math.min(net, Math.max(0, koboOf(terms.fixedFee) || 0));
  return { price, commission, net, tutor, organisation: net - tutor };
}
