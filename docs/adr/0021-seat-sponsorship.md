# ADR 0021: Seat sponsorship — sponsors buy seats in a cohort and give them to learners

**Status:** Accepted (2026-11-23)

## Context

Sponsorship so far was an open call: a sponsor built a campaign, Plastoids reviewed and invoiced it, the opportunity was
published, learners searched and applied, were scored, shortlisted and offered a seat. The user has decided learners will
not search for sponsors. A sponsor picks a **cohort** (a dated run of a course with a start and an end), pays for a number
of seats, and allocates each seat to a learner they name. Everything else about sponsorship (eligibility rules, entry
assessments, scoring, shortlists, offers, quotes, invoices, management fees) is not part of this flow.

## Decision

1. **Cohorts are what sponsors buy into.** Lms exposes upcoming cohorts publicly: `GET /lms/cohorts?courseId=&from=`
   and `GET /lms/cohorts/{cohortId}` return the cohort's name, start and end, capacity, seats left, the course and the
   price per seat (the cohort offering's price, from Catalogue). A cohort gains a `reserved` count: seats a sponsor has paid
   for but not yet filled. `seatsLeft = capacity − enrolled − reserved`.
2. **Seat blocks replace campaigns.** `SeatBlock` (`sponsorship_seat_blocks`) is one purchase of N seats in one cohort by
   one organisation. Buying redeems whole units from the organisation wallet for `N × pricePerSeat` (ADR 0018 rate) and
   posts **entry 14**: Dr 2050 unit balances (wallet) the naira released, Cr 2020 programme funds held with the block as the
   sub-ledger owner, any gap to 4060. The cohort reserves N seats. A block can be bought until the cohort ends.
3. **Allocations replace applications.** `SeatAllocation` (`sponsorship_seat_allocations`) gives one seat to one learner,
   named by phone or email. If the account exists the seat is **enrolled** at once; otherwise it is **invited** and
   converts automatically when that phone or email registers (`identity.user-registered.v1` now carries the email too).
   Enrolling posts **entry 15**: Dr 2020 (block) the seat price; Cr 4010 commission at the normal course rate
   (`course_platform`, or `course_provider` for an organisation course); Cr 2010 the engaged tutor's share; Cr 2060
   (organisation) or 2010 (tutor) the rest, exactly like a sale. It then emits `sponsorship.seat-allocated.v1`, which Lms
   turns into an enrolment in that cohort (funding `sponsored`, funding source id = block id, funding label = sponsor
   name), Payouts turns into earnings (source `seat`, 7-day hold like a course sale) and Notifications turns into a
   message to the learner. One allocation per learner per block; an allocation cannot be moved to another learner in this
   version.
4. **Releasing seats.** Unallocated seats can be released by the sponsor at any time, and are released automatically when
   the cohort ends (daily job). Release frees the cohort reservation, credits the units back to the organisation wallet
   (kind `release`, pro rata to the units redeemed) and posts **entry 16**: Dr 2020 (block) / Cr 2050 (wallet).
5. **The old flow stays in the backend, out of the interface.** Campaigns, quotes, invoices, opportunities and
   applications keep their endpoints, consumers, jobs and tests, but the web app no longer shows them: the Learner module
   loses Opportunities, Apply and Applications; the Sponsor module loses Programmes, the campaign builder, quotes and
   invoices; the Administration module loses programme review and applications review and gains seat-block oversight.
6. **Fees.** A seat costs the cohort offering's price. The platform's revenue is the commission taken when the seat is
   allocated; there is no management fee, seat fee or VAT line in this flow (the sponsor fee schedule of ADR 0009 applies
   only to the legacy campaign flow).

## API

| Method and path | Policy | Body / result |
| --- | --- | --- |
| `GET /lms/cohorts?courseId=&from=&limit=` | public | `CohortListing[]` upcoming (endsAt ≥ now), ordered by startsAt |
| `GET /lms/cohorts/{cohortId}` | public | `CohortListing` |
| `POST /sponsorship/organisations/{organisationId}/seat-blocks` | OrganisationFinance, Idempotency-Key | `{cohortId, seats}` → `SeatBlockView` (201) |
| `GET /sponsorship/organisations/{organisationId}/seat-blocks` | OrganisationMember | `SeatBlockView[]` |
| `GET /sponsorship/organisations/{organisationId}/seat-blocks/{blockId}` | OrganisationMember | `SeatBlockDetail` |
| `POST …/seat-blocks/{blockId}/allocations` | OrganisationFinance, Idempotency-Key | `{phone?, email?, displayName?}` → `SeatAllocationView` (201) |
| `POST …/seat-blocks/{blockId}/release` | OrganisationFinance, Idempotency-Key | `{seats}` → `SeatBlockView` |
| `GET /admin/sponsorship/seat-blocks?status=&limit=` | Staff | `SeatBlockView[]` (all organisations) |
| `GET /admin/sponsorship/seat-blocks/{blockId}` | Staff | `SeatBlockDetail` |

Shapes (camelCase JSON; Money = `{amount: kobo, currency}`):

- `CohortListing { cohortId, courseId, courseSlug, courseTitle, offeringId, name, startsAt, endsAt, capacity, enrolled,
  reserved, seatsLeft, pricePerSeat, tutorUserId, ownerType, providerName? }`
- `SeatBlockView { blockId, organisationId, sponsorName, cohortId, cohortName, courseId, courseTitle, courseSlug,
  startsAt, endsAt, seats, allocated, enrolled, invited, released, open, pricePerSeat, unitsRedeemed, unitRateKobo,
  status: active|closed, purchasedAt, closedAt }` where `open = seats − allocated − released`.
- `SeatBlockDetail = SeatBlockView + { allocations: SeatAllocationView[], walletBalance }`
- `SeatAllocationView { allocationId, blockId, seatNo, status: invited|enrolled, learnerId?, displayName, contact
  (masked phone like "+234803****01" or the email), invitedAt, enrolledAt?, enrolmentId? }`
- `EnrolmentSummary` (learner) gains `fundingLabel?` ("Sponsored by Lagos Data School").

Errors: `cohort_not_found`, `cohort_ended`, `cohort_full`, `seats_invalid`, `insufficient_units` (from Wallet),
`block_not_found`, `no_open_seats`, `contact_required`, `seat_already_allocated`, `release_exceeds_open`.

## Consequences

- Lms: `lms_cohorts.reserved`, `lms_enrolments.funding_label`; a new `ICohortSeats` contract (reserve / release /
  read) for Sponsorship; a consumer of `seat-allocated`.
- Identity: `IUserDirectory.FindByContactAsync(phone, email)`; `UserRegistered.Email`; email registration emits the event.
- Sponsorship: two tables, four entries' worth of postings (14–16), a consumer of `user-registered`, a daily close job,
  admin reads. Payouts: a `seat` earning source. Notifications: `sponsorship.seat.allocated`, `sponsorship.seat.invited`.
- Web app: Sponsor module gets Seats (list, buy, block detail with allocate and release); Learner module shows upcoming
  cohorts on course pages and "Sponsored by" on enrolled courses; Administration gets Seat blocks.
- Inviting someone who has no account sends them an SMS (phone) or an email with the sign-up link (`Web:BaseUrl`); the contact
  is stored encrypted on the allocation so the sponsor can resend (`POST …/allocations/{allocationId}/resend-invite`). The seat
  waits for them to sign up with that phone or email.
