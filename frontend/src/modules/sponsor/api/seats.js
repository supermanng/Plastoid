/*
 * Seat sponsorship (ADR 0021): a sponsor buys N seats in a cohort from the organisation wallet and gives each seat to a
 * learner named by phone or email. Routes are relative to /api/v1 (api.js adds the base and the Idempotency-Key header).
 *
 * Shapes (camelCase JSON; Money = { amount: kobo, currency }):
 *   SeatBlockView      { blockId, organisationId, sponsorName, cohortId, cohortName, courseId, courseTitle, courseSlug, startsAt, endsAt,
 *                        seats, allocated, enrolled, invited, released, open, pricePerSeat, unitsRedeemed, unitRateKobo,
 *                        status: active|closed, purchasedAt, closedAt }   where open = seats − allocated − released
 *   SeatBlockDetail    SeatBlockView + { allocations: SeatAllocationView[], walletBalance }
 *   SeatAllocationView { allocationId, blockId, seatNo, status: invited|enrolled, learnerId?, displayName, contact, invitedAt, enrolledAt?, enrolmentId? }
 *
 * Errors: cohort_not_found, cohort_ended, cohort_full, seats_invalid, insufficient_units, block_not_found, no_open_seats,
 *         contact_required, seat_already_allocated, release_exceeds_open.
 */
import { api } from '../../../shared/lib/api';

const base = (orgId) => `/sponsorship/organisations/${orgId}/seat-blocks`;

export const seatsKeys = {
  all: (orgId) => ['sponsor', 'seat-blocks', orgId],
  blocks: (orgId) => ['sponsor', 'seat-blocks', orgId, 'list'],
  block: (orgId, blockId) => ['sponsor', 'seat-blocks', orgId, blockId],
};

export const seatsApi = {
  /** GET …/seat-blocks → SeatBlockView[] (OrganisationMember) */
  listBlocks: (orgId) => api.get(base(orgId)),
  /** GET …/seat-blocks/{blockId} → SeatBlockDetail (OrganisationMember) */
  getBlock: (orgId, blockId) => api.get(`${base(orgId)}/${blockId}`),
  /** POST …/seat-blocks { cohortId, seats } → SeatBlockView (201; OrganisationFinance). Redeems seats × pricePerSeat in units. */
  buySeats: (orgId, { cohortId, seats }) => api.post(base(orgId), { cohortId, seats: Number(seats) }),
  /** POST …/seat-blocks/{blockId}/allocations { phone?, email?, displayName? } → SeatAllocationView (201; OrganisationFinance) */
  allocate: (orgId, blockId, { phone, email, displayName }) =>
    api.post(`${base(orgId)}/${blockId}/allocations`, { phone: phone || null, email: email || null, displayName: displayName || null }),
  /** POST …/seat-blocks/{blockId}/release { seats } → SeatBlockView (OrganisationFinance). Units come back to the wallet pro rata. */
  release: (orgId, blockId, seats) => api.post(`${base(orgId)}/${blockId}/release`, { seats: Number(seats) }),
  /** Sends the SMS or email invite again for a seat whose learner has not signed up yet → SeatAllocationView. */
  resendInvite: (orgId, blockId, allocationId) => api.post(`${base(orgId)}/${blockId}/allocations/${allocationId}/resend-invite`, {}),
};

/** Human text for the seat-sponsorship error codes (falls back to the API message in ErrorAlert/toast). */
export const SEAT_ERRORS = {
  cohort_not_found: 'That cohort no longer exists.',
  cohort_ended: 'This cohort has already ended; seats can no longer be bought.',
  cohort_full: 'The cohort does not have that many seats left.',
  seats_invalid: 'Enter a whole number of seats, at least 1.',
  insufficient_units: 'The organisation wallet does not hold enough units for this purchase.',
  block_not_found: 'That seat block could not be found.',
  no_open_seats: 'Every seat in this block has been given out or released.',
  contact_required: 'Enter the learner’s phone number or email address.',
  seat_already_allocated: 'This learner already has a seat in this block.',
  release_exceeds_open: 'You can only release seats that have not been given to anyone.',
};

export function seatErrorText(error) {
  return (error?.code && SEAT_ERRORS[error.code]) || null;
}
