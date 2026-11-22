/*
 * Learner-area API functions and query keys. Field names mirror the C# response records
 * (camelCase); Money is { amount: <kobo>, currency }.
 */
import { api } from '../../../shared/lib/api';

export const keys = {
  me: ['identity', 'me'],
  enrolments: ['lms', 'enrolments', 'me'],
  gradebook: (enrolmentId) => ['lms', 'gradebook', enrolmentId],
  lesson: (enrolmentId, lessonId) => ['lms', 'lesson', enrolmentId, lessonId],
  course: (slug) => ['catalogue', 'course', slug],
  courses: (params) => ['catalogue', 'courses', params],
  upcoming: (days) => ['lms', 'live', 'upcoming', days],
  threads: (cohortId) => ['lms', 'threads', cohortId],
  announcements: (cohortId) => ['notifications', 'announcements', cohortId],
  applications: ['applications', 'me'],
  opportunities: ['sponsorship', 'opportunities'],
  bookings: (params) => ['bookings', 'me', params],
  booking: (id) => ['bookings', id],
  availability: (tutorId, from, days) => ['tutors', tutorId, 'availability', from, days],
  certificates: ['certificates', 'me'],
  payments: (cursor) => ['payments', 'me', cursor],
  payment: (id) => ['payments', 'intent', id],
  receipt: (id) => ['payments', 'receipt', id],
  messageThreads: ['notifications', 'threads'],
  messageThread: (id) => ['notifications', 'threads', id],
  notifications: (params) => ['notifications', 'me', params],
  unread: ['notifications', 'unread-count'],
  preferences: ['notifications', 'preferences'],
  dataExport: (id) => ['identity', 'data-export', id],
};

// ----- Identity
export const identity = {
  updateMe: (body) => api.patch('/identity/me', body),
  updateGoals: (body) => api.put('/identity/me/goals', body),
  requestDataExport: () => api.post('/identity/me/data-export', {}),
  getDataExport: (exportId) => api.get(`/identity/me/data-export/${exportId}`),
  deleteAccount: () => api.delete('/identity/me', { confirmation: 'DELETE' }),
};

// ----- Catalogue (public)
export const catalogue = {
  search: (params) => api.get('/catalogue/courses', params, { auth: false }),
  course: (slug) => api.get(`/catalogue/courses/${encodeURIComponent(slug)}`, undefined, { auth: false }),
};

// ----- LMS
export const lms = {
  enrolments: () => api.get('/lms/enrolments/me'),
  gradebook: (enrolmentId) => api.get(`/lms/enrolments/${enrolmentId}/gradebook`),
  lesson: (enrolmentId, lessonId) => api.get(`/lms/enrolments/${enrolmentId}/lessons/${lessonId}`),
  complete: (enrolmentId) => api.post(`/lms/enrolments/${enrolmentId}/complete`, {}),
  offlinePackage: (enrolmentId) => api.post(`/lms/enrolments/${enrolmentId}/offline-package`, {}),
  /** items: [{ lessonId, status: 'notStarted'|'inProgress'|'completed', positionSeconds, page, percentComplete, clientUpdatedAt }] */
  progress: (enrolmentId, items) => api.post(`/lms/enrolments/${enrolmentId}/progress`, { items }),
  startAttempt: (enrolmentId, lessonId) => api.post(`/lms/enrolments/${enrolmentId}/lessons/${lessonId}/attempts`, {}),
  saveAnswers: (attemptId, answers) => api.put(`/lms/attempts/${attemptId}/answers`, { answers }),
  submitAttempt: (attemptId, answers) => api.post(`/lms/attempts/${attemptId}/submit`, { answers }),
  submitAssignment: (enrolmentId, lessonId, contentType) => api.post(`/lms/enrolments/${enrolmentId}/lessons/${lessonId}/assignment`, { contentType }),
  upcoming: (days = 7) => api.get('/lms/live-sessions/upcoming', { days }),
  joinToken: (liveSessionId) => api.post(`/lms/live-sessions/${liveSessionId}/join-token`, {}),
  threads: (cohortId) => api.get(`/lms/cohorts/${cohortId}/threads`),
  createThread: (cohortId, body) => api.post(`/lms/cohorts/${cohortId}/threads`, body),
  reply: (threadId, body) => api.post(`/lms/threads/${threadId}/posts`, { body }),
  announcements: (cohortId) => api.get(`/notifications/cohorts/${cohortId}/announcements`),
};

// ----- Applications & opportunities
export const applications = {
  mine: () => api.get('/applications/me'),
  opportunities: (params) => api.get('/sponsorship/opportunities', params, { auth: false }),
  eligibility: (programmeId, facts) => api.post(`/sponsorship/opportunities/${programmeId}/eligibility`, facts),
  submit: (programmeId, answers, submit) => api.post(`/applications/programmes/${programmeId}/applications`, { answers, submit }),
  startAssessment: (applicationId) => api.post(`/applications/${applicationId}/assessment`, {}),
  submitAssessment: (applicationId, answers) => api.put(`/applications/${applicationId}/assessment`, { answers }),
  acceptOffer: (applicationId, withdrawFromApplicationId = null) => api.post(`/applications/${applicationId}/offer/accept`, { withdrawFromApplicationId }),
  declineOffer: (applicationId) => api.post(`/applications/${applicationId}/offer/decline`, {}),
  appeal: (applicationId, reason) => api.post(`/applications/${applicationId}/appeals`, { reason }),
};

// ----- Bookings
export const bookings = {
  mine: (params) => api.get('/bookings/me', params),
  get: (bookingId) => api.get(`/bookings/${bookingId}`),
  cancel: (bookingId, body) => api.post(`/bookings/${bookingId}/cancel`, body),
  reschedule: (bookingId, newSlotStart) => api.post(`/bookings/${bookingId}/reschedule`, { newSlotStart }),
  availability: (tutorId, params) => api.get(`/tutors/${tutorId}/availability`, params, { auth: false }),
};

// ----- Certificates
export const certificates = {
  mine: () => api.get('/certificates/me'),
};

// ----- Payments
export const payments = {
  mine: (params) => api.get('/payments/me', params),
  intent: (paymentIntentId) => api.get(`/payments/intents/${paymentIntentId}`),
  verify: (paymentIntentId) => api.post(`/payments/intents/${paymentIntentId}/verify`, {}),
  receipt: (paymentIntentId) => api.get(`/payments/intents/${paymentIntentId}/receipt`),
  create: (body) => api.post('/payments/intents', body),
};

// ----- Notifications & messages
export const notifications = {
  inbox: (params) => api.get('/notifications/me', params),
  markRead: (notificationId) => api.post(`/notifications/${notificationId}/read`, {}),
  preferences: () => api.get('/notifications/preferences'),
  updatePreferences: (body) => api.put('/notifications/preferences', body),
  threads: () => api.get('/notifications/threads'),
  thread: (threadId) => api.get(`/notifications/threads/${threadId}`),
  openThread: (body) => api.post('/notifications/threads', body),
  postMessage: (threadId, body) => api.post(`/notifications/threads/${threadId}/messages`, { body }),
};

/** Opens the gateway checkout in the same tab; in development the fake gateway URL is not reachable, so callers show a fallback link. */
export function goToCheckout(checkoutUrl) {
  if (!checkoutUrl) return;
  window.location.assign(checkoutUrl);
}

export function isDevCheckout(checkoutUrl) {
  return typeof checkoutUrl === 'string' && checkoutUrl.includes('plastoids.local');
}
