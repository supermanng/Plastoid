/*
 * Query functions for the public discovery pages (courses, cohorts, tutors) and the
 * learner-side actions those pages trigger (checkout, bookings).
 * Shapes come from the C# records; see each comment.
 */
import { api } from '../lib/api';

const anon = { auth: false };

export const queryKeys = {
  courses: (filters) => ['public', 'courses', filters],
  course: (slug) => ['public', 'course', slug],
  compare: (ids) => ['public', 'compare', ids],
  cohorts: (filters) => ['public', 'cohorts', filters ?? null],
  cohort: (cohortId) => ['public', 'cohort', cohortId],
  tutors: (filters) => ['public', 'tutors', filters],
  tutor: (tutorId) => ['public', 'tutor', tutorId],
  availability: (tutorId, days) => ['public', 'availability', tutorId, days],
};

/* ---- Catalogue ---------------------------------------------------------- */

/** GET /catalogue/courses → SearchCoursesResponse { items: CourseCard[], nextCursor, total, suggestions? { skills, categories, popular } } */
export function searchCourses(filters = {}) {
  const { q, skill, level, funding, format, maxPriceKobo, lang, category, cursor, limit } = filters;
  return api.get('/catalogue/courses', { q, skill, level, funding, format, maxPriceKobo, lang, category, cursor, limit: limit ?? 12 }, anon);
}

/** GET /catalogue/courses/{slug} → CourseDetail (outline[], offerings[], recentReviews[]) */
export function getCourse(slug) {
  return api.get(`/catalogue/courses/${encodeURIComponent(slug)}`, undefined, anon);
}

/** GET /catalogue/courses/compare?ids=a,b,c → { courses: CompareRow[] } */
export function compareCourses(ids) {
  return api.get('/catalogue/courses/compare', { ids: ids.join(',') }, anon);
}

/** POST /catalogue/courses/{courseId}/reviews { enrolmentId, rating, body } → { reviewId, courseRatingAverage, courseRatingCount } */
export function postReview(courseId, body) {
  return api.post(`/catalogue/courses/${courseId}/reviews`, body);
}

/* ---- Payments ----------------------------------------------------------- */

/**
 * POST /payments/intents { purpose: 'course', quoteId } → { paymentIntentId, gateway, gatewayReference, checkoutUrl, amount, commissionBps, status }
 * purpose=booking|co_pay take { amount, referenceId, payeeUserId }.
 */
/** payWithUnits=true settles from the signed-in user's unit wallet at once (status "succeeded", checkoutUrl null, unitsCharged). */
export function createCourseCheckout(quoteId, learnerSource, payWithUnits = false) {
  return api.post('/payments/intents', { purpose: 'course', quoteId, learnerSource: learnerSource ?? null, payWithUnits: Boolean(payWithUnits) });
}

/* ---- Cohorts (ADR 0021) ---------------------------------------------- */

/**
 * GET /lms/cohorts?courseId=&from=&limit= → CohortListing[] (upcoming, endsAt ≥ now, ordered by startsAt)
 * CohortListing { cohortId, courseId, courseSlug, courseTitle, offeringId, name, startsAt, endsAt, capacity, enrolled,
 *                 reserved, seatsLeft, pricePerSeat: Money, tutorUserId, ownerType, providerName? }
 */
export function listCohorts({ courseId, from, limit } = {}) {
  return api.get('/lms/cohorts', { courseId, from, limit }, anon);
}

/** GET /lms/cohorts/{cohortId} → CohortListing */
export function getCohort(cohortId) {
  return api.get(`/lms/cohorts/${cohortId}`, undefined, anon);
}

/** Lists come back as a bare array per the ADR; tolerate a paged `{ items }` wrapper too. */
export function cohortItems(data) {
  return Array.isArray(data) ? data : data?.items || [];
}

/* ---- Tutors ------------------------------------------------------------- */

/** GET /tutors → Page<TutorCard> { items: [{ tutorId, headline, level, subjects[], languages[], formats[], hourlyRate, ratingAverage, ratingCount, timeZone }], nextCursor } */
export function listTutors(filters = {}) {
  const { q, skill, maxRate, lang, format, cursor, limit } = filters;
  return api.get('/tutors', { q, skill, maxRate, lang, format, cursor, limit: limit ?? 12 }, anon);
}

/** Finds one tutor card by id by walking the public list. */
export async function findTutor(tutorId, maxPages = 20) {
  let cursor = null;
  for (let i = 0; i < maxPages; i += 1) {
    const page = await listTutors({ cursor, limit: 50 });
    const hit = (page?.items || []).find((t) => t.tutorId === tutorId);
    if (hit) return hit;
    cursor = page?.nextCursor;
    if (!cursor) break;
  }
  return null;
}

/** GET /tutors/{tutorId}/availability?days=7 → { tutorId, timeZone, from, days, slots: [{ startUtc, endUtc }] } */
export function getAvailability(tutorId, days = 7, from) {
  return api.get(`/tutors/${tutorId}/availability`, { days, from }, anon);
}

/* ---- Bookings ----------------------------------------------------------- */

/** GET /bookings/quote → SlotQuote { tutorId, tutorUserId, format, slotStart, slotEnd, durationMinutes, price, timeZone } */
export function quoteBooking({ tutorId, slotStart, durationMinutes, format }) {
  return api.get('/bookings/quote', { tutorId, slotStart, durationMinutes, format });
}

/** POST /bookings/locks { tutorId, slotStart, durationMinutes, format } → { tutorId, slotStart, lockedUntil, quote } (409 slot_locked) */
export function lockSlot(body) {
  return api.post('/bookings/locks', body);
}

/** POST /bookings { tutorId, slotStart, durationMinutes, format, packageId?, note? } → { bookingId, status, price, paymentIntentId?, checkoutUrl?, slotStart, slotEnd } */
export function createBooking(body) {
  return api.post('/bookings', body);
}

/** POST /bookings/packages { tutorId, sessions: 5|10, durationMinutes, format } → { packageId, sessions, pricePerSession, total, paymentIntentId, checkoutUrl } */
export function createPackage(body) {
  return api.post('/bookings/packages', body);
}

/* ---- Shared helpers ----------------------------------------------------- */

/**
 * Send the browser to the gateway. In development the fake gateway's URL (checkout.plastoids.local) does not resolve,
 * so pages also link to /learn/payments/{id}; this helper returns false when it decides not to navigate.
 */
export function goToCheckout(checkoutUrl) {
  if (!checkoutUrl) return false;
  try {
    const url = new URL(checkoutUrl);
    if (url.hostname.endsWith('.local')) return false;
  } catch {
    return false;
  }
  window.location.assign(checkoutUrl);
  return true;
}

export const LEVELS = ['beginner', 'intermediate', 'advanced'];
export const COURSE_FORMATS = [
  ['self_paced', 'Self-paced'],
  ['cohort', 'Cohort'],
  ['one_to_one', 'One-to-one'],
];
export const TEACHING_FORMATS = [
  ['one_to_one', 'One-to-one'],
  ['group', 'Group'],
  ['course', 'Course'],
];
export const LANGUAGES = [
  ['en', 'English'],
  ['yo', 'Yoruba'],
  ['ig', 'Igbo'],
  ['ha', 'Hausa'],
  ['pcm', 'Pidgin'],
];
