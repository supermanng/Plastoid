# ADR 0008: Sprint 2 decisions — tutor verification, catalogue review and cross-module reads

**Status:** Accepted (2026-11-21)

## Context

Sprint 2 adds the Tutors and Catalogue modules. Several rules in the build prompt need a concrete mechanism that the
prompt leaves open: how Catalogue knows a tutor is verified, how "first 3 courses need human review, then sampled" is
decided, how a price is "honoured for 30 minutes", how a module grants a platform role owned by Identity, and how
outbound providers (KYC, bank resolve, calendar, video) are faked locally.

## Decisions

1. **Verified-tutor projection.** Catalogue keeps `catalogue_verified_tutors`, filled by consuming
   `tutors.tutor-verified.v1`. Publish/submit checks read that table; no runtime call crosses into Tutors. The
   consumer is idempotent (upsert by tutor id) so redelivery is harmless.
2. **Platform roles through an in-process contract.** `Plastoids.Modules.Identity.Contracts.IPlatformRoles` is the
   only way another module grants a role. Its implementation enlists the Identity context in the caller's unit of work,
   so "apply as tutor" and the `tutor` role commit together. The same pattern is reserved for Ledger in Sprint 3.
3. **Course review sampling.** A course needs a human reviewer when its owner has fewer than three reviewed courses;
   afterwards one in five is sampled deterministically from the course id. Automated checks (structure, video readiness,
   offering, summary length, skills) always run and block submission with a 422 listing the failed checks.
4. **Price quotes in Redis.** `GET /catalogue/courses/{slug}` issues a 30-minute quote per offering
   (`catalogue:quote:<id>`). Payments (Sprint 3) redeems the quote id so the shown price is charged even if the tutor
   changes it meanwhile.
5. **Review gate behind `IEnrolmentProgress`.** Reviews require a verified enrolment with ≥30 % progress. The
   interface lives in Catalogue's Contracts and is answered by a stub until Lms (Sprint 5) provides the real reader.
6. **Integration adapters with fakes.** `IKycProvider`, `IBankAccountResolver`, `ICalendarProvider` and
   `IVideoProvider` live in Infrastructure/Integrations with deterministic fakes (selected unless
   `Integrations:<Name>` names the real provider). The fake video provider signs its webhook with HMAC so the
   signature check is exercised end to end. The Smile ID, Paystack, Google and Mux implementations are stubs that fail
   loudly until credentials and SDK calls are added.
7. **Full-text search in MySQL.** `FULLTEXT` indexes on tutor headline/bio and course title/summary, queried through
   Pomelo's `EF.Functions.Match`, with a `LIKE` fallback on the title so short or stop-word queries still return
   results. JSON array filters (skills, languages, formats, funding types) use `JSON_CONTAINS`. OpenSearch remains the
   scale-up path from the blueprint.
8. **SLA timers.** Reviewer queue items carry a working-day due date (3 for credentials, 5 for subject review). A
   Quartz job raises `tutors.review-sla-breached.v1` once per item; Notifications consumes it in Sprint 8.

## Consequences

- Catalogue depends on the Tutors project only for `Tutors.Contracts` (architecture test enforces it).
- Raw NIN/BVN never persist: only the provider reference and pass/fail are stored on `tutors_verification_checks`.
- The "upload resumes after drop" and "240p–720p renditions" criteria of story C2 are the provider's responsibility;
  the API only records readiness and renditions reported by the webhook.
