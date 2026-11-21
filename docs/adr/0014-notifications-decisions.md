# ADR 0014: Sprint 8 decisions — Notifications

**Status:** Accepted (2026-11-21)

## Decisions

1. **One inbox item, at most one external delivery.** Every message becomes an in-app notification (always on) plus one
   delivery on the first channel in the user's order (push → WhatsApp → SMS → email by default) that has a destination
   and is not opted out. Failed deliveries retry twice with back-off; the next channel is a Phase 2 fallback.
2. **Two feeds, one dedup key.** Modules send `notifications.send-requested.v1` with a template key; Notifications also
   consumes the §2.8 domain events that imply a message (user-registered, booking-confirmed, offer-sent/accepted,
   certificate-issued, payout-settled, enrolment-created). Both paths collapse on a dedup key built from the template key
   and the first stable id in the data (certificateId, enrolmentId, bookingId, applicationId, payoutId, …), so a fact
   arriving twice renders once.
3. **Approved, versioned templates with built-in fallback.** `notifications_templates` holds (key, channel, version);
   only approved versions render, newest first, channel-specific before `any`. Every key the modules emit has built-in
   copy in `DefaultTemplates`; unknown keys log a warning and use the generic template. Rendering is `{{key}}`
   substitution; missing placeholders render empty, never the braces.
4. **Quiet hours are the recipient's local time** (IANA zone, Africa/Lagos default). A delivery queued inside the window
   is deferred to its end; the minute-by-minute delivery job re-checks preferences, so hours set after queuing still apply.
   In-app items are never deferred.
5. **Contacts through a dedicated Identity contract.** `IUserContacts` returns the decrypted phone and email for sending
   only; `IUserDirectory` keeps exposing hashes, not numbers.
6. **Threads are retained and scanned, never dropped.** Learner–tutor messages use the Lms forum word lists (Nigerian
   phone numbers, off-platform payment phrases); a hit flags the message and thread and raises
   `notifications.message-flagged.v1` for Back office. Participants and staff can read a thread; nobody else.
7. **Cohort announcements fan out through the dispatcher** using `ICohortMembers` (Lms.Contracts), so they respect each
   learner's preferences and quiet hours.
8. **Channel adapters live in Infrastructure** behind `Integrations:WhatsApp|Email|Push` switches: SMTP (Mailpit locally),
   BSP WhatsApp and FCM push are stubs until accounts exist; `FakeMessageChannels` records everything for tests.

## Consequences

- Booking, Lms, Certificates and Payouts already emit send-requested; the domain-event consumers only add messages for
  facts no module announces (welcome on registration) and otherwise dedupe.
- Per-message channel choice is not stored on the request; `channelPreference` in send-requested can force one channel.
- Delivery receipts (read/seen) from providers are Phase 2.
