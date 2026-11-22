/*
 * Back-office API surface. Every function returns a Promise; shapes mirror the C# records:
 *   Money      → { amount: <kobo>, currency }
 *   Paging     → { items, nextCursor }
 * Mutations go through api.post/put which add the Idempotency-Key header.
 */
import { api } from '../../../shared/lib/api';

export const adminKeys = {
  dashboard: ['admin', 'dashboard'],
  verificationQueue: ['admin', 'queues', 'verification'],
  moderationQueue: (status) => ['admin', 'queues', 'course-moderation', status ?? null],
  refundQueue: ['admin', 'queues', 'refunds'],
  disputeQueue: ['admin', 'queues', 'disputes'],
  dataRequests: (status) => ['admin', 'data-requests', status ?? null],
  users: (params) => ['admin', 'users', params],
  user: (id) => ['admin', 'users', id],
  tutors: (status) => ['admin', 'tutors', status ?? null],
  sponsors: (status) => ['admin', 'sponsors', status ?? null],
  featureFlags: ['admin', 'feature-flags'],
  auditLog: (params) => ['admin', 'audit-log', params],
  ledgerEntries: (params) => ['admin', 'ledger', 'entries', params],
  balance: (code, ownerId) => ['admin', 'ledger', 'balance', code, ownerId ?? null],
  reconciliation: ['admin', 'ledger', 'reconciliation'],
  feeRules: (code) => ['admin', 'fee-rules', code ?? null],
  payoutQueue: ['admin', 'payouts', 'queue'],
  payoutBatch: (id) => ['admin', 'payouts', 'batch', id],
  roleGrants: (status) => ['admin', 'role-grants', status ?? null],
  templates: (key) => ['admin', 'templates', key ?? null],
  reviewQueue: (programmeId, params) => ['admin', 'applications', programmeId, params],
  opportunities: ['admin', 'opportunities'],
  course: (slug) => ['catalogue', 'course', slug],
  verify: (code) => ['verify', code],
};

export const adminApi = {
  // Dashboard & risk
  dashboard: () => api.get('/admin/backoffice/dashboard'),
  resolveRiskAlert: (alertId, resolution) => api.post(`/admin/backoffice/risk-alerts/${alertId}/resolve`, { resolution }),

  // Queues
  verificationQueue: (limit = 100) => api.get('/admin/backoffice/queues/verification', { limit }),
  moderationQueue: (status, limit = 100) => api.get('/admin/backoffice/queues/course-moderation', { status, limit }),
  refundQueue: (limit = 100) => api.get('/admin/backoffice/queues/refunds', { limit }),
  disputeQueue: (limit = 100) => api.get('/admin/backoffice/queues/disputes', { limit }),
  dataRequests: (status, limit = 100) => api.get('/admin/backoffice/data-requests', { status, limit }),

  // Decisions behind the queues
  reviewCredential: (credentialId, decision, note) => api.post(`/admin/tutors/credentials/${credentialId}/review`, { decision, note: note || null }),
  reviewSubject: (tutorId, decision, score, note) => api.post(`/admin/tutors/${tutorId}/subject-review`, { decision, score: score ?? null, note: note || null }),
  verifyOrganisation: (organisationId, decision, reason) => api.post(`/admin/identity/organisations/${organisationId}/verification`, { decision, reason: reason || null }),
  reviewCourse: (courseId, decision, note) => api.post(`/admin/catalogue/courses/${courseId}/review`, { decision, note: note || null }),
  approveRefund: (refundId) => api.post(`/admin/payments/refunds/${refundId}/approve`, {}),
  issueRefund: (paymentIntentId, amountKobo, reason) => api.post(`/admin/payments/intents/${paymentIntentId}/refunds`, { amount: { amount: amountKobo, currency: 'NGN' }, reason }),

  // Users, tutors, sponsors
  searchUsers: ({ query, role, status, limit = 50 }) => api.get('/admin/backoffice/users', { query, role, status, limit }),
  getUser: (userId) => api.get(`/admin/backoffice/users/${userId}`),
  suspendUser: (userId, reason) => api.post(`/admin/backoffice/users/${userId}/suspend`, { reason }),
  reinstateUser: (userId) => api.post(`/admin/backoffice/users/${userId}/reinstate`, {}),
  listTutors: (status, limit = 100) => api.get('/admin/backoffice/tutors', { status, limit }),
  listSponsors: (verificationStatus, limit = 100) => api.get('/admin/backoffice/sponsors', { verificationStatus, limit }),

  // Programmes (staff side) — there is no staff list/read of programmes; published ones come from the public listing.
  publishedOpportunities: (limit = 100) => api.get('/sponsorship/opportunities', { limit }),
  reviewProgramme: (programmeId, body) => api.post(`/admin/sponsorship/programmes/${programmeId}/review`, body),
  publishProgramme: (programmeId) => api.post(`/admin/sponsorship/programmes/${programmeId}/publish`, {}),
  closeProgramme: (programmeId) => api.post(`/admin/sponsorship/programmes/${programmeId}/close`, {}),
  releaseSeat: (programmeId, seatId, reason) => api.post(`/admin/sponsorship/programmes/${programmeId}/seats/${seatId}/release`, { reason }),

  // Applications review
  reviewQueue: (programmeId, { status, blind, cursor, limit = 50 } = {}) => api.get(`/admin/applications/programmes/${programmeId}/queue`, { status, blind, cursor, limit }),
  scoreApplication: (applicationId, scores) => api.post(`/admin/applications/${applicationId}/score`, { scores }),
  bulkDecide: (programmeId, decisions) => api.post(`/admin/applications/programmes/${programmeId}/decisions`, { decisions }),
  sendOffers: (programmeId) => api.post(`/admin/applications/programmes/${programmeId}/offers`, {}),
  resolveAppeal: (appealId, decision, reason) => api.post(`/admin/applications/appeals/${appealId}/resolve`, { decision, reason }),

  // Catalogue preview (public read)
  course: (slug) => api.get(`/catalogue/courses/${encodeURIComponent(slug)}`),

  // Ledger
  ledgerEntries: ({ cursor, limit = 50, kind, accountCode, ownerId }) => api.get('/admin/ledger/entries', { cursor, limit, kind, accountCode, ownerId }),
  balance: (accountCode, ownerId) => api.get(`/admin/ledger/accounts/${accountCode}/balance`, { ownerId }),
  reconciliation: () => api.get('/admin/ledger/reconciliation'),
  runReconciliation: () => api.post('/admin/ledger/reconciliation/run', {}),
  unfreezePayouts: (reason) => api.post('/admin/ledger/reconciliation/unfreeze', { reason }),

  // Settlement & sweeps (query-string parameter, empty body)
  importSettlements: (upTo) => api.post('/admin/payments/settlements/import', {}, { query: upTo ? { upTo } : undefined }),
  runSweep: () => api.post('/admin/payments/sweeps/run', {}),

  // Payouts
  payoutQueue: () => api.get('/admin/payouts/queue'),
  createBatch: () => api.post('/admin/payouts/batches', {}),
  batch: (batchId) => api.get(`/admin/payouts/batches/${batchId}`),
  approveBatch: (batchId) => api.post(`/admin/payouts/batches/${batchId}/approve`, {}),
  rejectBatch: (batchId, reason) => api.post(`/admin/payouts/batches/${batchId}/reject`, { reason }),
  submitBatch: (batchId) => api.post(`/admin/payouts/batches/${batchId}/submit`, {}),
  placeHold: (payee, reason) => api.post('/admin/payouts/holds', { payeeType: payee.payeeType || 'tutor', payeeId: payee.payeeId, reason }),
  releaseHold: (holdId, resolution) => api.post(`/admin/payouts/holds/${holdId}/release`, { resolution }),

  // Fee rules
  feeRules: (ruleCode) => api.get('/admin/payments/fee-rules', { ruleCode }),
  proposeFeeRule: (body) => api.post('/admin/payments/fee-rules', body),
  decideFeeRule: (ruleId, decision, note) => api.post(`/admin/payments/fee-rules/${ruleId}/decision`, { decision, note: note || null }),

  // Role grants
  roleGrants: (status) => api.get('/admin/identity/role-grants', { status }),
  proposeRoleGrant: (body) => api.post('/admin/identity/role-grants', body),
  decideRoleGrant: (proposalId, decision, note) => api.post(`/admin/identity/role-grants/${proposalId}/decision`, { decision, note: note || null }),

  // Notification templates
  templates: (key) => api.get('/admin/notifications/templates', { key }),
  upsertTemplate: (body) => api.post('/admin/notifications/templates', body),
  approveTemplate: (templateId) => api.post(`/admin/notifications/templates/${templateId}/approve`, {}),

  // Feature flags
  featureFlags: () => api.get('/admin/backoffice/feature-flags'),
  setFeatureFlag: (key, enabled, description) => api.put(`/admin/backoffice/feature-flags/${encodeURIComponent(key)}`, { enabled, description: description || null }),

  // Compliance
  revokeCertificate: (certificateId, reason) => api.post(`/admin/certificates/${certificateId}/revoke`, { reason }),
  verifyCertificate: (publicCode) => api.get(`/verify/${encodeURIComponent(publicCode)}`, undefined, { auth: false }),

  // Audit (BackOffice view over the Audit module; same entry shape as /admin/audit-events)
  auditLog: ({ cursor, limit = 50, actorId, entity, entityId, from, to }) => api.get('/admin/backoffice/audit-log', { cursor, limit, actorId, entity, entityId, from, to }),
};

/** Ledger chart of accounts (src/Modules/Ledger/Contracts/ILedgerService.cs). */
export const LEDGER_ACCOUNTS = [
  ['1010', 'Bank: operating'],
  ['1020', 'Bank: client funds'],
  ['1030', 'Gateway receivable (in transit)'],
  ['2010', 'Tutor payable'],
  ['2020', 'Programme funds held'],
  ['2030', 'Learner refund credit'],
  ['2040', 'VAT payable'],
  ['4010', 'Course commission'],
  ['4020', 'Tutoring commission'],
  ['4030', 'Programme management fee'],
  ['4040', 'Platform seat fee'],
  ['4050', 'Subscriptions'],
  ['5010', 'Processing fees'],
];

export const SKIP_REASONS = {
  below_minimum: 'Below the minimum payout',
  dispute_open: 'Dispute hold open',
  no_payout_account: 'No verified payout account',
  name_mismatch: 'Bank account name mismatch',
};

export const FOUR_EYES_REFUND_KOBO = 100_000 * 100;
