/*
 * Sponsor-area API calls. Routes are relative to /api/v1 (api.js adds the base and the Idempotency-Key header).
 * Seat sponsorship (buying seats in a cohort, giving them out, releasing them) lives in ./seats.js (ADR 0021).
 *
 * Shapes (from the C# records):
 *   OrganisationView { id, name, type, role, verificationStatus }   (from GET /identity/me → organisations[])
 *   Money            { amount: <kobo>, currency: 'NGN' }
 */
import { api } from '../../../shared/lib/api';

export const sponsorKeys = {
  courses: (params) => ['sponsor', 'courses', params],
  course: (slug) => ['sponsor', 'course', slug],
};

export const sponsorApi = {
  // Organisation (Identity)
  registerOrganisation: (body) => api.post('/identity/organisations', body),
  addMember: (orgId, body) => api.post(`/identity/organisations/${orgId}/members`, body),
  changeMemberRole: (orgId, userId, role) => api.put(`/identity/organisations/${orgId}/members/${userId}/role`, { role }),

  // Catalogue (public)
  searchCourses: (params) => api.get('/catalogue/courses', { limit: 20, ...params }, { auth: false }),
  getCourse: (slug) => api.get(`/catalogue/courses/${encodeURIComponent(slug)}`, undefined, { auth: false }),
};
