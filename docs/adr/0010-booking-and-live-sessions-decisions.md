# ADR 0010: Sprint 4 decisions — booking lifecycle, slot locking, refund credit and live sessions

**Status:** Accepted (2026-11-21)

## Decisions

1. **Two guards against double booking.** A Redis lock (`booking:lock:<tutor>:<slot>`, 10 minutes, owner-renewable)
   protects checkout; a unique `slot_key` column on `booking_bookings` (set while a booking is pending or confirmed,
   cleared on cancellation) protects against calendar-sync lag and expired locks. Quotes also reject overlapping
   confirmed bookings.
2. **Bookings pay through the Payments contract.** `IPaymentOperations.CreateIntentAsync` creates the intent in the
   booking's own transaction; `payments.payment-succeeded.v1` (purpose `booking`, reference = booking or package id)
   confirms the booking or activates the package, which emits `booking.booking-confirmed.v1`.
3. **Cancellation policy in the aggregate.** `Booking.Cancel` encodes §9: learner >24 h → 100 %, <24 h → 50 %, tutor →
   100 %, tutor no-show (reported by the learner from 10 minutes after the start, within 48 h) → 100 % + strike. Three
   strikes in 90 days raise `booking.tutor-strike-recorded.v1` with `reviewFlagged = true`.
4. **Refunds go to learner credit by default.** `IPaymentOperations.RefundByPolicyAsync` issues a policy-driven refund
   (no maker–checker: the rule is the approval) either to the card or, by default, to the learner's refund credit —
   ledger 2030 sub-ledger per learner — so the cash stays in client funds. Blueprint §18 forbids stored value beyond
   this: credit is spendable only on the platform and cashed out only to the original payment method.
5. **Earnings hold.** When a session ends the lifecycle job marks it completed and opens a 72-hour `EarningHold`;
   a released hold emits `booking.earning-released.v1`, which Payouts (Sprint 7) will aggregate into the weekly run.
6. **Reminders.** A 5-minute job sends `notifications.send-requested.v1` to both parties once at T-24h and once at
   T-1h (per-booking stamps make redelivery harmless). The event record lives in Infrastructure because every module
   produces it.
7. **Live sessions are Lms's.** `booking.booking-confirmed.v1` creates a `LiveSession` with a provider room
   (`ILiveVideoProvider`: fake locally, Daily stub); reschedule/cancel events move or cancel it. Join tokens are
   short-lived (15 min), issued only to the host or participants from 15 minutes before the start. Provider attendance
   webhooks (HMAC-verified) become join→leave spans; each closed span emits `lms.attendance-recorded.v1`.

## Consequences

- Package bookings skip checkout entirely and confirm synchronously; a partial refund of a package session is credited
  rather than returned, since half a session cannot go back on the package.
- Group-session capacity and request-to-book are not in this sprint; the format is stored and priced, and capacity
  enforcement arrives with cohorts in Sprint 5.
