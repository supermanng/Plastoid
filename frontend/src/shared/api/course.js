/*
 * Catalogue authoring API, shared by the tutor course builder (learner module) and the organisation portal's course
 * editor (sponsor module). Routes are relative to /api/v1 (api.js adds the base and the Idempotency-Key header).
 * Shapes come from the C# records: CreateCourse, UpdateCourseOutline, AddLesson, AddQuiz, UploadLessonVideo,
 * CreateOffering, SubmitForReview, PublishCourse, GetCourse (CourseDetail).
 */
import { api } from '../lib/api';

export const courseKeys = {
  course: (slug) => ['course', slug],
  myCourses: ['tutor', 'courses'],
  authoring: (courseId) => ['course', 'authoring', courseId],
};

export const courseApi = {
  /** { title, summary, level, language, category, skills[] } → { courseId, slug, status } */
  create: (body) => api.post('/catalogue/courses', body),

  /** CourseDetail (published, or unpublished for signed-in users; drafts are NOT readable). */
  bySlug: (slug) => api.get(`/catalogue/courses/${encodeURIComponent(slug)}`),

  /** [CourseSummary] for the signed-in tutor (ADR 0019): courseId, slug, title, status, level, category, language, ownerType, currentVersionNo, publishedAt, updatedAt, offeringCount, activeEngagements */
  listMine: () => api.get('/catalogue/courses/mine'),

  /** Draft outline for the builder (owner, organisation owner/author or staff): course fields, status, modules[lessons], offerings. */
  authoring: (courseId) => api.get(`/catalogue/courses/${courseId}/authoring`),

  /** { title?, summary?, level?, language?, category?, skills?, modules: [{ id?, title, order }] } → { courseId, status, modules[] } */
  updateOutline: (courseId, body) => api.put(`/catalogue/courses/${courseId}/outline`, body),

  /** { type (video|doc|assignment|live), title, order, durationMinutes, isPreview, contentJson? } → { lessonId, moduleId, type } */
  addLesson: (courseId, moduleId, body) => api.post(`/catalogue/courses/${courseId}/modules/${moduleId}/lessons`, body),

  /** { title, order, passMarkPercent, questions: [{ prompt, options[], correctIndex }] } → { lessonId, questionCount } */
  addQuiz: (courseId, moduleId, body) => api.post(`/catalogue/courses/${courseId}/modules/${moduleId}/quizzes`, body),

  /** → { mediaAssetId, provider, uploadId, uploadUrl } */
  videoUploadUrl: (lessonId) => api.post(`/catalogue/lessons/${lessonId}/video`, {}),

  /** { format (self_paced|cohort|one_to_one), price: Money, fundingTypes[] (self|sponsored|employer), capacity?, startAt?, durationWeeks?, tutorUserId? (engaged tutor on organisation courses) } */
  addOffering: (courseId, body) => api.post(`/catalogue/courses/${courseId}/offerings`, body),

  /** → { courseId, status, requiresHumanReview, checks: [{ code, passed, message }] } */
  submit: (courseId) => api.post(`/catalogue/courses/${courseId}/submit`, {}),

  /** → { courseId, versionNo, slug, publishedAt } */
  publish: (courseId) => api.post(`/catalogue/courses/${courseId}/publish`, {}),
};
