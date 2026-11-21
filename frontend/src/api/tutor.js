/*
 * Tutor-area API calls. Shapes come from the C# records:
 *   Tutors:   GetTutorDashboard (TutorDashboard), ApplyAsTutor, SubmitIdentityCheck, UploadCredential, SubmitDemoLesson,
 *             SetAvailability, SetPricing, SetPayoutAccount, ConnectGoogleCalendar
 *   Catalogue: CreateCourse, UpdateCourseOutline, AddLesson, AddQuiz, UploadLessonVideo, CreateOffering, SubmitForReview,
 *             PublishCourse, GetCourse (CourseDetail)
 *   Lms:      CreateCohort, ScheduleCohortSessions, CohortForum, CreateLiveSession, ListUpcomingSessions, IssueJoinToken,
 *             GetGradebook, GradeAssignment
 *   Booking:  ListMyBookings, GetBooking, CancelBooking, RescheduleBooking
 *   Payouts:  GetMyEarnings, DownloadStatement
 *   Notifications: cohort announcements
 */
import { api } from '../lib/api';

export const tutorKeys = {
  me: ['tutor', 'me'],
  earnings: ['tutor', 'earnings'],
  bookings: (upcoming) => ['tutor', 'bookings', upcoming ? 'upcoming' : 'all'],
  upcomingSessions: (days) => ['tutor', 'live-sessions', days ?? 7],
  course: (slug) => ['course', slug],
  gradebook: (courseId) => ['tutor', 'gradebook', courseId],
  threads: (cohortId) => ['tutor', 'cohort', cohortId, 'threads'],
  announcements: (cohortId) => ['tutor', 'cohort', cohortId, 'announcements'],
};

/* ---------------------------------------------------------------- Tutors module */

export const tutorApi = {
  /** TutorDashboard: tutorId, status, level, headline, subjects, formats, timeZone, hourlyRate, verification[], credentials[],
   *  availabilityRules, priceRules, calendarConnected, payoutAccountMasked, qualityScore, sessionsDelivered, enrolmentsTaught, ratingAverage */
  me: () => api.get('/tutors/me'),

  /** { headline, bio, subjects[], languages[], formats[] (one_to_one|group|course), timeZone, hourlyRate: Money, acceptCodeOfConduct } → { tutorId, status } */
  apply: (body) => api.post('/tutors/applications', body),

  /** { nin, bvn, selfieBlobKey, fullName } → { passed, provider, reference, failureReason } */
  identityCheck: (body) => api.post('/tutors/me/identity-check', body),

  /** { type (degree|certificate|work_history|linkedin), issuer, title, contentType } → { credentialId, uploadUrl, reviewDueAt } */
  addCredential: (body) => api.post('/tutors/me/credentials', body),

  /** { contentType: video/mp4|video/webm } → { uploadUrl, reviewDueAt } */
  demoLesson: (contentType) => api.post('/tutors/me/demo-lesson', { contentType }),

  /** { timeZone, weekly: [{ day, start "HH:mm:ss", end }], exceptions: [{ date, start?, end?, available }], bufferMinutes } */
  setAvailability: (body) => api.put('/tutors/me/availability', body),

  /** { hourlyRate: Money, rules: [{ format, durationMinutes, price: Money, maxLearners? }] } */
  setPricing: (body) => api.put('/tutors/me/pricing', body),

  /** { bankCode, accountNumber } → { bankCode, accountNumberMasked, accountName, nameMatched } */
  setPayoutAccount: (body) => api.put('/tutors/me/payout-account', body),

  /** { authorizationCode } → { provider, account } */
  connectGoogleCalendar: (authorizationCode) => api.post('/tutors/me/calendar/google', { authorizationCode }),
};

/* ---------------------------------------------------------------- Catalogue (authoring) */

export const courseApi = {
  /** { title, summary, level, language, category, skills[] } → { courseId, slug, status } */
  create: (body) => api.post('/catalogue/courses', body),

  /** CourseDetail (published, or unpublished for signed-in users; drafts are NOT readable). */
  bySlug: (slug) => api.get(`/catalogue/courses/${encodeURIComponent(slug)}`),

  /** { title?, summary?, level?, language?, category?, skills?, modules: [{ id?, title, order }] } → { courseId, status, modules[] } */
  updateOutline: (courseId, body) => api.put(`/catalogue/courses/${courseId}/outline`, body),

  /** { type (video|doc|assignment|live), title, order, durationMinutes, isPreview, contentJson? } → { lessonId, moduleId, type } */
  addLesson: (courseId, moduleId, body) => api.post(`/catalogue/courses/${courseId}/modules/${moduleId}/lessons`, body),

  /** { title, order, passMarkPercent, questions: [{ prompt, options[], correctIndex }] } → { lessonId, questionCount } */
  addQuiz: (courseId, moduleId, body) => api.post(`/catalogue/courses/${courseId}/modules/${moduleId}/quizzes`, body),

  /** → { mediaAssetId, provider, uploadId, uploadUrl } */
  videoUploadUrl: (lessonId) => api.post(`/catalogue/lessons/${lessonId}/video`, {}),

  /** { format (self_paced|cohort|one_to_one), price: Money, fundingTypes[] (self|sponsored|employer), capacity?, startAt?, durationWeeks? } */
  addOffering: (courseId, body) => api.post(`/catalogue/courses/${courseId}/offerings`, body),

  /** → { courseId, status, requiresHumanReview, checks: [{ code, passed, message }] } */
  submit: (courseId) => api.post(`/catalogue/courses/${courseId}/submit`, {}),

  /** → { courseId, versionNo, slug, publishedAt } */
  publish: (courseId) => api.post(`/catalogue/courses/${courseId}/publish`, {}),
};

/* ---------------------------------------------------------------- Lms */

export const lmsApi = {
  /** { courseId, offeringId, name, startsAt, endsAt, capacity, programmeId? } → { cohortId, courseId, offeringId, name, startsAt, endsAt, capacity } */
  createCohort: (body) => api.post('/lms/cohorts', body),

  /** { weekdays: ["monday", ...], startTimeUtc "HH:mm:ss", durationMinutes, titlePrefix? } → { cohortId, created, firstSession, lastSession } */
  scheduleCohortSessions: (cohortId, body) => api.post(`/lms/cohorts/${cohortId}/sessions`, body),

  /** { cohortId?, title, startsAt, durationMinutes, participantUserIds[] } → { liveSessionId, roomId, roomUrl, startsAt, endsAt } */
  createLiveSession: (body) => api.post('/lms/live-sessions', body),

  /** UpcomingSession[]: liveSessionId, title, source, bookingId, cohortId, startsAt, endsAt, status, isHost, joinOpen */
  upcoming: (days = 7) => api.get('/lms/live-sessions/upcoming', { days }),

  /** → { liveSessionId, provider, roomId, token, joinUrl, expiresAt, isHost, audioOnlyFallback } */
  joinToken: (liveSessionId) => api.post(`/lms/live-sessions/${liveSessionId}/join-token`, {}),

  /** Gradebook: courseId, courseTitle, rows: [{ enrolmentId, learnerId, progressPct, status, finalScore, items: [{ lessonId, title, type, status, bestScore, passed, attempts }] }] */
  gradebook: (courseId) => api.get(`/lms/courses/${courseId}/gradebook`),

  /** { rubric: [{ criterion, points, maxPoints, comment? }], feedback? } → { submissionId, score, maxScore, scorePercent, passed, enrolmentCompleted } */
  grade: (submissionId, body) => api.post(`/lms/submissions/${submissionId}/grade`, body),

  /** ThreadView[] */
  threads: (cohortId) => api.get(`/lms/cohorts/${cohortId}/threads`),
  createThread: (cohortId, body) => api.post(`/lms/cohorts/${cohortId}/threads`, body),
  reply: (threadId, body) => api.post(`/lms/threads/${threadId}/posts`, body),
};

/* ---------------------------------------------------------------- Booking */

export const bookingApi = {
  /** Page<BookingSummary>: bookingId, tutorId, tutorUserId, learnerId, format, slotStart, slotEnd, price, status, packageId, refundPercent */
  mine: ({ upcoming = false, cursor, limit = 50 } = {}) => api.get('/bookings/me', { role: 'tutor', upcoming, cursor, limit }),
  get: (bookingId) => api.get(`/bookings/${bookingId}`),
  /** { reason, tutorNoShow?, refundTo? (credit|card) } → { bookingId, status, cancelledBy, refundPercent, tutorStrike } */
  cancel: (bookingId, body) => api.post(`/bookings/${bookingId}/cancel`, body),
  /** { newSlotStart } → { bookingId, slotStart, slotEnd, rescheduledFrom } */
  reschedule: (bookingId, newSlotStart) => api.post(`/bookings/${bookingId}/reschedule`, { newSlotStart }),
};

/* ---------------------------------------------------------------- Payouts */

export const earningsApi = {
  /** MyEarningsView: gross, commission, net, held, available, pending, paid (Money), disputeHold, recentPayouts: [{ payoutId, status, amount, accountNumberMasked, bankReference, createdAt, settledAt, failureReason }] */
  me: () => api.get('/payouts/me'),
  /** CSV; from/to are YYYY-MM-DD */
  statement: (from, to) => api.download('/payouts/me/statement', { from, to }),
};

/* ---------------------------------------------------------------- Notifications */

export const announcementApi = {
  /** AnnouncementItem[]: announcementId, title, body, authorUserId, postedAt */
  list: (cohortId) => api.get(`/notifications/cohorts/${cohortId}/announcements`),
  /** { title, body } → { announcementId, cohortId, title, recipients, postedAt } */
  post: (cohortId, body) => api.post(`/notifications/cohorts/${cohortId}/announcements`, body),
};
