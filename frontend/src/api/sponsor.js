/*
 * Sponsor-area API calls. Routes are relative to /api/v1 (api.js adds the base and the Idempotency-Key header).
 *
 * Shapes (from the C# records):
 *   ProgrammeView  { programmeId, organisationId, name, status: draft|quoted|quoteApproved|published|closed,
 *                    reviewStatus: pending|approved|changesRequested|rejected, objectives, courseId, offeringId, courseTitle,
 *                    seats, tuitionPerSeat, sponsorSharePercent, coPayPerSeat, budget, eligibilityRules, hasEntryAssessment,
 *                    hasScoringRubric, applicationsOpenAt, applicationsCloseAt, startsAt, endsAt, selectionMode, unusedFundsPolicy,
 *                    quote { version, seats, tuitionPerSeatSponsorShare, tuition, managementFee, seatFee, vat, total, quotedAt, approvedAt },
 *                    invoiceId, fundedAmount, isFunded, paymentPlanApproved, seatsUnlocked, publishedAt, closedAt }
 *   BudgetView     { programmeId, budget, quoteTotal, invoiced, received, isFunded, seatsUnlocked, seatsFilled, seatFeesEarned,
 *                    managementFeesEarned, tutorEarnings, closeoutRefund, fundsHeld, months[{ month, filledSeats, managementFee, tutorEarnings, postedAt }] }
 *   FunnelView     { programmeId, name, seats, seatsFilled, seatsOpen, stages[{ stage, count, percentOfApplied }], sessionsAttended, attendancePerSeat, averageScore, refreshedAt }
 *   QuoteResponse  { programmeId, version, status, lines[{ kind, description, quantity, unitAmount, amount }], subtotal, vat, total, coPayPerSeat, durationMonths }
 *   InvoiceDetail  { invoiceId, number, organisationId, programmeId, status, subtotal, vat, total, paidAmount, outstanding, issuedAt, dueAt, paidAt,
 *                    virtualAccountBank, virtualAccountNumber, virtualAccountName, lines[] }
 *   Money          { amount: <kobo>, currency: 'NGN' }
 */
import { api } from '../lib/api';

const base = (orgId) => `/sponsorship/organisations/${orgId}/programmes`;

export const sponsorKeys = {
  programme: (orgId, programmeId) => ['sponsor', 'programme', orgId, programmeId],
  budget: (orgId, programmeId) => ['sponsor', 'budget', orgId, programmeId],
  funnel: (orgId, programmeId) => ['sponsor', 'funnel', orgId, programmeId],
  invoice: (orgId, invoiceId) => ['sponsor', 'invoice', orgId, invoiceId],
  courses: (params) => ['sponsor', 'courses', params],
  course: (slug) => ['sponsor', 'course', slug],
};

export const sponsorApi = {
  // Organisation (Identity)
  registerOrganisation: (body) => api.post('/identity/organisations', body),
  addMember: (orgId, body) => api.post(`/identity/organisations/${orgId}/members`, body),
  changeMemberRole: (orgId, userId, role) => api.put(`/identity/organisations/${orgId}/members/${userId}/role`, { role }),

  // Campaign stepper
  createCampaign: (orgId, step) => api.post(base(orgId), step),
  saveStep: (orgId, programmeId, step) => api.put(`${base(orgId)}/${programmeId}`, step),
  getProgramme: (orgId, programmeId) => api.get(`${base(orgId)}/${programmeId}`),
  requestQuote: (orgId, programmeId) => api.post(`${base(orgId)}/${programmeId}/quote`, {}),
  approveQuote: (orgId, programmeId, dueDays) => api.post(`${base(orgId)}/${programmeId}/quote/approve`, { dueDays: dueDays ?? null }),
  publish: (orgId, programmeId) => api.post(`${base(orgId)}/${programmeId}/publish`, {}),
  getBudget: (orgId, programmeId) => api.get(`${base(orgId)}/${programmeId}/budget`),
  getFunnel: (orgId, programmeId) => api.get(`${base(orgId)}/${programmeId}/funnel`),
  downloadFunnelCsv: (orgId, programmeId) => api.download(`${base(orgId)}/${programmeId}/funnel`, { format: 'csv' }),
  downloadImpactReport: (orgId, programmeId) => api.download(`${base(orgId)}/${programmeId}/impact-report`),

  // Applications
  approveShortlist: (programmeId) => api.post(`/applications/programmes/${programmeId}/shortlist/approve`, {}),

  // Invoices (Payments)
  createInvoice: (orgId, body) => api.post(`/payments/organisations/${orgId}/invoices`, body),
  getInvoice: (orgId, invoiceId) => api.get(`/payments/organisations/${orgId}/invoices/${invoiceId}`),

  // Catalogue (public)
  searchCourses: (params) => api.get('/catalogue/courses', { funding: 'sponsored', limit: 20, ...params }, { auth: false }),
  getCourse: (slug) => api.get(`/catalogue/courses/${encodeURIComponent(slug)}`, undefined, { auth: false }),
};

/* ---------------------------------------------------------------- Local memory
 * The API has no "list my programmes" / "list my invoices" endpoints, so this browser remembers the ids it has seen.
 */
function readList(key) {
  try {
    const raw = localStorage.getItem(key);
    const list = raw ? JSON.parse(raw) : [];
    return Array.isArray(list) ? list : [];
  } catch {
    return [];
  }
}

function writeList(key, list) {
  try {
    localStorage.setItem(key, JSON.stringify(list));
  } catch {
    /* storage unavailable */
  }
}

export const programmeMemory = {
  key: (orgId) => `plastoids.sponsor.programmes.${orgId}`,
  list: (orgId) => readList(programmeMemory.key(orgId)),
  remember: (orgId, programmeId) => {
    if (!orgId || !programmeId) return;
    const list = programmeMemory.list(orgId).filter((id) => id !== programmeId);
    writeList(programmeMemory.key(orgId), [programmeId, ...list].slice(0, 200));
  },
  forget: (orgId, programmeId) => writeList(programmeMemory.key(orgId), programmeMemory.list(orgId).filter((id) => id !== programmeId)),
};

export const invoiceMemory = {
  key: (orgId) => `plastoids.sponsor.invoices.${orgId}`,
  list: (orgId) => readList(invoiceMemory.key(orgId)),
  remember: (orgId, invoiceId) => {
    if (!orgId || !invoiceId) return;
    const list = invoiceMemory.list(orgId).filter((id) => id !== invoiceId);
    writeList(invoiceMemory.key(orgId), [invoiceId, ...list].slice(0, 200));
  },
};

/* ---------------------------------------------------------------- Quote maths (mirrors Programme.QuoteLines.Compute) */
export const FEES = {
  managementFeeBps: 1200,
  minimumManagementFeeKobo: 3_000_000 * 100,
  seatFeeKobo: 12_000 * 100,
  vatBps: 750,
};

/** Compute the quote in kobo. Returns null when inputs are incomplete. */
export function computeQuote({ seats, tuitionPerSeatKobo, sponsorSharePercent }) {
  const s = Number(seats);
  const t = Number(tuitionPerSeatKobo);
  const p = Number(sponsorSharePercent);
  if (!s || !t || !p || s <= 0 || t <= 0 || p <= 0) return null;
  const sharePerSeat = Math.round((t * p) / 100);
  const tuition = sharePerSeat * s;
  let managementFee = Math.round((tuition * FEES.managementFeeBps) / 10_000);
  if (managementFee < FEES.minimumManagementFeeKobo) managementFee = FEES.minimumManagementFeeKobo;
  const seatFee = FEES.seatFeeKobo * s;
  const vat = Math.round(((managementFee + seatFee) * FEES.vatBps) / 10_000);
  const subtotal = tuition + managementFee + seatFee;
  return {
    sharePerSeat,
    tuition,
    managementFee,
    seatFee,
    vat,
    subtotal,
    total: subtotal + vat,
    coPayPerSeat: t - sharePerSeat,
  };
}

export const money = (kobo) => ({ amount: Math.round(kobo), currency: 'NGN' });

export const PROGRAMME_STATUS_LABEL = {
  draft: 'Draft',
  quoted: 'Quote requested',
  quoteapproved: 'Quote approved',
  quoteApproved: 'Quote approved',
  published: 'Published',
  closed: 'Closed',
};

export const REVIEW_STATUS_LABEL = {
  pending: 'Awaiting Plastoids review',
  approved: 'Review approved',
  changesrequested: 'Changes requested',
  changesRequested: 'Changes requested',
  rejected: 'Rejected',
};
