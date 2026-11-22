# ADR 0019: Organisation-owned courses with engaged tutors

**Status:** Accepted (2026-11-22)

## Context

Up to ADR 0018 a course belongs to one verified tutor, who authors it, opens cohorts, teaches and is paid. Organisations
exist only as sponsors that fund seats. The user asked that a sponsor (or any verified organisation) can create courses and
cohorts, employ verified tutors to teach them, and sell seats to learners, with the money split between Plastoids, the
organisation and the tutor. This ADR is the first of the two sprints sized in chat; the learning-intelligence module is not
part of it.

## Decision

1. **Any verified organisation may own courses.** `CourseOwnerType.Organisation` is added; `Course.OrganisationId` holds
   the owner and `OwnerUserId` the member who created it. A new organisation type `provider` exists for organisations that
   teach without sponsoring; registering one grants the `sponsor` platform role so the organisation portal is reachable.
   A new organisation role `author` (alongside owner, finance, reviewer, viewer) may create and edit the organisation's
   courses and engage tutors; owners may do everything an author can. Policy `IdentityPolicies.OrganisationAuthor` = owner
   or author on the route's `{organisationId}`.
2. **Authoring endpoints are resource-authorised, not role-authorised.** `/catalogue/courses/{courseId}/…` (outline,
   lessons, quizzes, offerings, submit, publish, authoring view) move from `Policies.Tutor` to `Policies.Mfa`;
   `CourseAccess.OwnedAsync` decides: the owning tutor, an owner/author of the owning organisation, or staff. Tutors still
   create their own courses at `POST /catalogue/courses`; organisations create theirs at
   `POST /catalogue/organisations/{organisationId}/courses` (organisation must be verified: `organisation_not_verified`).
3. **Tutor engagements** (`catalogue_engagements`). An organisation offers a verified tutor terms for one course; the tutor
   accepts or declines; the organisation may end it. Terms are `revenue_share` (basis points of the sale net of platform
   commission) or `per_enrolment` (a fixed naira fee per paid enrolment, capped at the sale net of commission). One
   open-or-active engagement per course and tutor. Statuses: `offered → active | declined`, `active → ended`.
   A per-cohort fixed fee was considered and deferred: it needs an organisation-to-tutor settlement at cohort end that is
   not backed by a sale, so it would have to be funded from the organisation's wallet; Phase 2.
4. **Who teaches an offering is fixed at the offering.** `Offering.TutorUserId` / `EngagementId` name the engaged tutor for
   an organisation course's offering (required for cohort and one-to-one formats, optional for self-paced). Cohorts on that
   offering default to that tutor; `CreateCohort` may name another tutor who has an active engagement (`tutor_not_engaged`
   otherwise). Lms teaching access (gradebook, grading, scheduling sessions, opening cohorts) is: the cohort tutor, the
   course owner, an owner/author of the owning organisation, an actively engaged tutor, or staff.
5. **Money.** A sale of an organisation course is still one payment intent with purpose `course`; the intent carries
   `payee_type` (`tutor` | `organisation`), `payee_organisation_id`, `tutor_share` and `engagement_id`. Platform commission
   uses fee-rule code `course_provider` (default `Fees:CourseProviderBps` = 1500). The tutor share is computed from the
   engagement terms on the net of commission; the organisation receives the rest. Ledger: new liability **2060 Provider
   payable** (sub-ledger per organisation). Entry 1 (card) and entry 12 (units) gain a provider variant: Dr 1030 / 2050;
   Cr 4010 commission, Cr 2010 (tutor) the tutor share, Cr 2060 (organisation) the organisation net. Entry 5 reverses all
   three pro rata. Reconciliation counts 2060 as a liability.
6. **Payouts to organisations.** Earnings and payouts carry `payee_type` + `payee_id`. The organisation records a payout
   account (`PUT /payouts/organisations/{id}/payout-account`, owner or finance; the bank account name must match the
   registered organisation name) and sees its statement at `GET /payouts/organisations/{id}/earnings`. The weekly four-eyes
   batch pays tutors from 2010 and organisations from 2060 (entry 4 provider variant: Dr 2060 / Cr 1020). Course-sale
   earnings hold 7 days for both payee types.
7. **Events.** `payments.payment-succeeded.v1` gains optional `payeeType`, `payeeOrganisationId`, `tutorShare`; existing
   consumers ignore them. `catalogue.course-published.v1` gains `ownerType`, `organisationId`. No new topics.

## API added or changed

| Method and path | Policy | Notes |
| --- | --- | --- |
| `POST /catalogue/organisations/{organisationId}/courses` | OrganisationAuthor | body as CreateCourse; → `{courseId, slug, status}` |
| `GET /catalogue/organisations/{organisationId}/courses` | OrganisationMember | `[CourseSummary]` |
| `GET /catalogue/courses/mine` | Tutor | `[CourseSummary]` for the signed-in tutor |
| `GET /catalogue/courses/{courseId}/authoring` | Mfa + CourseAccess | draft outline: course fields, status, modules[lessons], offerings |
| `POST /catalogue/courses/{courseId}/offerings` | Mfa + CourseAccess | body gains `tutorUserId?` |
| `POST /catalogue/organisations/{organisationId}/courses/{courseId}/engagements` | OrganisationAuthor | `{tutorId, terms:{kind, shareBps?, fixedFee?}, note?}` → EngagementView |
| `GET /catalogue/organisations/{organisationId}/courses/{courseId}/engagements` | OrganisationMember | `[EngagementView]` |
| `POST /catalogue/engagements/{engagementId}/end` | Mfa + CourseAccess | `{reason?}` |
| `GET /catalogue/engagements/mine` | Tutor | `[EngagementView]` |
| `POST /catalogue/engagements/{engagementId}/respond` | Tutor | `{accept: bool, note?}` |
| `POST /lms/cohorts` | Mfa + teaching access | body gains `tutorUserId?` |
| `PUT /payouts/organisations/{organisationId}/payout-account` | OrganisationFinance | `{bankCode, accountNumber}` |
| `GET /payouts/organisations/{organisationId}/earnings` | OrganisationMember | statement as `/payouts/me/earnings` + `payoutAccount` |

`CourseSummary { courseId, slug, title, status, level, category, language, ownerType, organisationId?, currentVersionNo,
publishedAt, updatedAt, offeringCount, activeEngagements }`.
`EngagementView { engagementId, courseId, courseTitle, courseSlug, organisationId, organisationName, tutorId, tutorUserId,
terms { kind, shareBps, fixedFee }, status, note, offeredBy, offeredAt, respondedAt, endedAt }`.
`CourseDetail` (public) gains `ownerType` and `provider { organisationId, name }?`; `CourseCard` gains `providerName?`.
`GET /identity/me` organisations already carry `type` and `role`; `author` appears there.

## Consequences

- Chart of accounts: sixteen codes (2060 added by migration `ProviderAccount`).
- `PaymentIntent` gains `payee_type`, `payee_organisation_id`, `tutor_share_*`, `engagement_id`; Payouts earnings and
  payouts rename `tutor_user_id` to `payee_id` and add `payee_type`.
- Sponsorship on an organisation course still pays entry 9 to the programme's tutor; routing sponsored tuition to 2060 is a
  follow-up, as is the learning-intelligence module and per-cohort engagement fees.
- The web app gains organisation Courses, course tutors (engagements) and Payouts pages, a tutor Offers page, and shows
  the provider on public course pages.
