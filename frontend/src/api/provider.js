/*
 * Organisation-owned courses (ADR 0019): a verified organisation authors courses, engages verified tutors to teach
 * them and is paid its share of each sale. Routes are relative to /api/v1 (api.js adds the Idempotency-Key).
 *
 * Shapes (from the ADR):
 *   CourseSummary  { courseId, slug, title, status, level, category, language, ownerType, organisationId?, currentVersionNo,
 *                    publishedAt, updatedAt, offeringCount, activeEngagements }
 *   EngagementView { engagementId, courseId, courseTitle, courseSlug, organisationId, organisationName, tutorId, tutorUserId,
 *                    terms { kind: revenue_share|per_enrolment, shareBps?, fixedFee? }, status: offered|active|declined|ended,
 *                    note, offeredBy, offeredAt, respondedAt, endedAt }
 *   Earnings       statement as GET /payouts/me (gross, commission, net, held, available|payable, pending, paid, recentPayouts[])
 *                  + payoutAccount { bankCode, accountNumberMasked, accountName, nameMatched }?
 *   Money          { amount: <kobo>, currency: 'NGN' }
 */
import { api } from '../lib/api';
import { formatMoney, koboOf } from '../lib/format';

export const providerKeys = {
  courses: (orgId) => ['provider', 'courses', orgId],
  engagements: (orgId, courseId) => ['provider', 'engagements', orgId, courseId],
  earnings: (orgId) => ['provider', 'earnings', orgId],
};

const base = (orgId) => `/catalogue/organisations/${orgId}`;

export const providerApi = {
  /** [CourseSummary] (any organisation member). */
  listCourses: (orgId) => api.get(`${base(orgId)}/courses`),

  /** { title, summary, level, language, category, skills[] } → { courseId, slug, status } (owner or author; organisation must be verified). */
  createCourse: (orgId, body) => api.post(`${base(orgId)}/courses`, body),

  /** Draft outline for the builder: course fields, status, modules[lessons], offerings. */
  authoring: (courseId) => api.get(`/catalogue/courses/${courseId}/authoring`),

  /** { tutorId, terms: { kind, shareBps?, fixedFee? }, note? } → EngagementView */
  offerEngagement: (orgId, courseId, body) => api.post(`${base(orgId)}/courses/${courseId}/engagements`, body),

  /** [EngagementView] */
  listEngagements: (orgId, courseId) => api.get(`${base(orgId)}/courses/${courseId}/engagements`),

  /** { reason? } */
  endEngagement: (engagementId, reason) => api.post(`/catalogue/engagements/${engagementId}/end`, { reason: reason || null }),

  /** Statement + payoutAccount (any organisation member). */
  earnings: (orgId) => api.get(`/payouts/organisations/${orgId}/earnings`),

  /** { bankCode, accountNumber } → { bankCode, accountNumberMasked, accountName, nameMatched } (owner or finance). */
  setPayoutAccount: (orgId, body) => api.put(`/payouts/organisations/${orgId}/payout-account`, body),
};

/* ---------------------------------------------------------------- Engagement terms */

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
