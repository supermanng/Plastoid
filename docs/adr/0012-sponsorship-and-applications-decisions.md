# ADR 0012: Sprint 6 decisions — Sponsorship and Applications

**Status:** Accepted (2026-11-21)

## Decisions

1. **Mutual module dependencies get a Contracts assembly.** Sponsorship consumes Applications' events (offer-accepted,
   decision-recorded, application-submitted, offer-sent) and Applications reads programmes and seats through
   Sponsorship.Contracts. Project references cannot be circular, so each module's `Contracts/` folder builds as its own
   assembly (`Plastoids.Modules.<Module>.Contracts`), excluded from the module project with `<Compile Remove>`. The
   namespaces are unchanged and the architecture tests still hold. Use the same split for any future pair of modules that
   depend on each other; a one-way dependency keeps Contracts inside the module project.
2. **Quote and fee schedule in the domain.** `QuoteLines.Compute` reproduces blueprint §5: tuition (sponsor share × seats),
   management fee 12 % of tuition with a ₦3,000,000 floor, seat fee ₦12,000 × seats, VAT 7.5 % on the two fee lines only.
   The budget is compared before VAT. Approving the quote issues the invoice through `ISponsorInvoicing`
   (Payments.Contracts) in the same transaction, so `payments.invoice-paid.v1` carries the programme id.
3. **Publication gates.** Publish requires an approved quote, an approved programme review with the lawful-rules attestation,
   and either full funding (invoice paid) or a staff-approved payment plan. Seats are created at publication; with partial
   funding and no plan, seats = funded ÷ (invoice total ÷ seats). A second tranche does not add seats automatically (staff
   republish is not needed; the inventory is fixed at publication for MVP).
4. **Eligibility is a whitelist.** Rules may only use minAge, maxAge, residency, states, education, employmentStatus and
   notPreviouslySponsored; any protected characteristic is rejected at save time and again at review. Age comes from the
   verified profile (`IUserDirectory.AgeYears`, derived from the encrypted DOB, never the DOB itself); the other facts are
   self-declared on the form. "Previously sponsored" is answered from the seat inventory.
5. **Seats are reserved by offers.** `IProgrammeSeats.ReserveAsync` runs when the offer is sent; the seat is filled when
   `applications.offer-accepted.v1` arrives (entry 7 posted then), re-opened when the offer is declined, expires (5 days)
   or the co-pay is not paid within 7 days, and refunded at close-out if still open (entry 10). A filled seat released by
   staff (drop-out) goes back to Open and the waitlist backfills it.
6. **Monthly entries from the inventory.** Entry 8 (management fee per seat ÷ months) and entry 9 (tuition share per seat ÷
   months) are posted per completed programme month on the seats filled at that time. Attendance is recorded for the
   impact report; paying tutors strictly per attended session is deferred until live cohorts carry attendance (Phase 2).
7. **Scoring cannot see the form.** `ScoringInput` holds the assessment percentage and reviewer criterion scores only;
   the architecture test forbids `Scorer` from depending on JSON or the application aggregate, and the form validator
   rejects protected-characteristic fields outright. Blind review strips identity fields and the learner id.
8. **Entry assessment reuses the Lms engine** through `IAssessmentGrader` (grading, time limit, redaction) while attempts
   are stored by Applications (`applications_entry_attempts`) with the same deadline-plus-30-seconds rule. One attempt per
   application.
9. **Duplicates and overlaps.** A submission whose NIN blind index or phone blind index matches another applicant on the
   same programme is merged into the original (status `merged`, flag `duplicate_identity`) and cannot be accepted until
   the flag is resolved. Accepting an offer while holding another on an overlapping programme requires
   `withdrawFromApplicationId`; an existing enrolment on an overlapping programme blocks acceptance.
10. **Co-pay via Payments.Contracts.** Partial sponsorship creates a `co_pay` intent (reference = programme id, so entry 1
    credits the programme pool); `payments.payment-succeeded.v1` confirms the seat and emits offer-accepted. Instalments
    (blueprint: up to three) are deferred to Phase 2; MVP takes the co-pay in one payment.
11. **Decisions are append-only** (database triggers on `applications_decisions`), each with reviewer, reason and source
    (bulk, backfill, system, appeal). Appeals are allowed once within 7 days of rejection; upheld appeals return the
    application to the waitlist.
12. **Funnel is event-driven.** Sponsorship keeps per-programme counters updated by consumers (applied, offered, enrolled,
    active, completed, certified), so the sponsor view is current within seconds rather than hourly. CSV export and a
    text-only PDF impact report ship with MVP; narrative and demographics are Phase 2.

## Consequences

- `PublishedOffering` (Catalogue.Contracts) now carries `Price` and `AcceptsSponsorship`; `UserSummary` carries
  `PhoneHash` and `AgeYears`; `IOrganisationDirectory` is new in Identity.Contracts.
- Lms consumes `offer-accepted` from the Contracts assembly (reference changed; behaviour unchanged).
- The worked example (500 seats, 480 filled, 20 refunded) is a domain scenario test; the HTTP flow is exercised with a
  3-seat programme at 70 % sponsorship in the integration suite.
