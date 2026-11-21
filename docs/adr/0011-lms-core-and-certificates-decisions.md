# ADR 0011: Sprint 5 decisions — enrolments, progress, assessments, offline packages and certificates

**Status:** Accepted (2026-11-21)

## Decisions

1. **Enrolments come from events, never from a POST.** `payments.payment-succeeded.v1` (purpose `course`) and
   `applications.offer-accepted.v1` create enrolments through one service, idempotent on a `source_key`
   (`payment:<intent>` or `application:<id>`); a learner holds at most one active enrolment per course. The
   `OfferAccepted` contract lives in a Contracts-only Applications project until Sprint 6 fills the module.
2. **The course is read, not copied.** Lms stores the course version on the enrolment and reads the immutable outline
   through `ICourseOutlineReader` (Catalogue.Contracts). Required lessons are every lesson except live ones; the
   `lms_published_courses` projection only tells Lms which courses exist.
3. **Offline-safe progress.** `UpdateProgress` takes a batch; per lesson the newest client timestamp wins, a completion
   always applies and never regresses, and quizzes/assignments cannot be self-completed (they complete through
   grading). progress_pct = completed required lessons ÷ required lessons.
4. **Server-side timing.** An attempt carries a deadline from the quiz's `timeLimitMinutes` (default 30). Saves after
   the deadline are refused and expire the attempt; submissions after a 30-second grace are graded but never pass.
   Grading covers single choice, multi-select (exact set) and short answer (trimmed, case-insensitive).
5. **Completion rule in the aggregate.** `Enrolment.TryComplete` requires every required lesson complete and every quiz
   and assignment passed; the final score is the mean of best quiz scores and graded assignment percentages. It emits
   `lms.enrolment-completed.v1` once, from whichever path crosses the line (progress sync, quiz submit, grading, or the
   explicit complete endpoint).
6. **Offline packages.** A manifest of document and video lessons with media URLs valid until the enrolment ends and a
   one-time AES-256 key (stored encrypted with the field encryptor). The app encrypts the download with it; the key is
   tied to the account because it is only ever released to the signed-in learner.
7. **Cohorts and forums.** Cohort enrolment is automatic for cohort-format offerings (first cohort with seats, by start
   date); `ScheduleCohortSessions` fans weekly live classes out as Lms live sessions. Forum posts are screened for
   phone numbers and off-platform payment phrases and flag the thread rather than being dropped (blueprint §19).
8. **Certificates are immutable.** One certificate per enrolment, issued by consuming `lms.enrolment-completed.v1`,
   with a readable 12-character public code and a SHA-256 hash over the printed facts. A database trigger rejects any
   change to the identifying columns and any delete; revocation only sets the revocation columns. Every verification
   is logged. Templates are a key (`course:<id>` or `sponsor:<programme>`) for the renderer that arrives with the apps.

## Consequences

- Catalogue's review gate now answers from real enrolments (`IEnrolmentProgress` is implemented by Lms); the stub is
  overridden by registration order.
- Live lesson types count as attended, not as required lessons; attendance data is recorded by Sprint 4's webhook.
- The gradebook is computed on read; a materialised view can replace it when cohorts grow past hundreds of learners.
