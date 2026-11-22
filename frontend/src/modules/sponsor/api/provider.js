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
import { api } from '../../../shared/lib/api';

/* Engagement terms helpers live in shared (the tutor's offers page describes the same terms); re-exported for the sponsor pages. */
export { COURSE_PROVIDER_COMMISSION_BPS, ENGAGEMENT_STATUSES, TERM_KINDS, describeTerms, splitSale } from '../../../shared/lib/engagementTerms';

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
