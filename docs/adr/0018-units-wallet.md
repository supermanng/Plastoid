# ADR 0018: Units — a prepaid, closed-loop balance for learners and sponsors

**Status:** Accepted (2026-11-22)

## Context

The user asked that every payment, from a learner or a sponsor, buys **units**, that the naira value of a unit is set by
the admin, and that courses, cohorts and other learning are priced in units. Up to Sprint 9 learners paid naira at
checkout (ledger entry 1) and sponsors paid invoices by bank transfer (entry 6).

## Decision

1. **Wallet module** (`src/Modules/Wallet`, table prefix `wallet_`). A wallet per person and per organisation holds a
   unit balance made of **lots**: each lot remembers how many units were bought and the naira paid for them. Redemptions
   consume lots first-in, first-out and release the naira those units were bought for. Every movement is one append-only
   transaction row (purchase, redemption, refund, release, adjustment), idempotent on its reference.
2. **Unit rate**: `UnitRate` is versioned and effective-from, proposed by one staff member and approved by another
   (`maker_checker_violation` otherwise), exactly like fee rules. Until one is approved the configured
   `Wallet:DefaultRateKobo` (₦1,000) applies. `GET /wallet/rate` is public so prices can be shown in units everywhere.
3. **Buying units** is a payment intent with purpose `units` (`POST /wallet/purchases`). The units and rate are fixed when
   checkout starts (`UnitPurchase`); the gateway webhook posts **entry 11** (Dr 1030 gateway receivable, Cr 2050 unit
   balances held, sub-ledger per wallet) and the `payment-succeeded` event credits the lot. No revenue is recognised on
   purchase: the cash is a liability until the units are spent.
4. **Paying with units**: `POST /payments/intents` with `payWithUnits: true` (and `IntentRequest.PayWithUnits` for Booking
   and Applications) takes `ceil(price / rate)` units from the payer's wallet and confirms the sale at once with
   **entry 12**: Dr 2050 for the naira the units were bought for; Cr tutor payable and commission (or programme funds for a
   co-pay) on the price; any gap to **4060 unit revaluation** (units bought at an older rate, or whole-unit rounding). The
   same `payment-succeeded` event fires, so enrolment, booking confirmation and tutor earnings are unchanged.
5. **Refunds** of a units sale always go back to the wallet (`RefundDestinations.Units`), pro rata in units, with entry 5
   crediting 2050 instead of cash. Units are never paid out as cash: this keeps them a prepaid credit, not stored value.
6. **Sponsors** buy units into the organisation wallet (`organisationId` on the purchase; owner or finance role) and may
   settle a programme invoice from it: `POST …/programmes/{id}/fund-from-wallet` redeems `ceil(outstanding / rate)` units
   and Payments posts entry 6 (units variant: Dr 2050, Cr 2020 programme funds and 2040 VAT), then the usual
   `invoice-paid` event funds the programme. Bank transfer to the virtual account still works.
7. **Reconciliation** counts 2050 as a liability, so cash must cover unsold units as well as payables and programme funds.
8. **Back office**: `GET/POST /admin/wallet/rates`, `POST …/rates/{id}/decision`, `GET /admin/wallet/{ownerType}/{ownerId}`
   and goodwill / correction adjustments with a reason (zero-value lots; no ledger movement).

## Decisions taken with the defaults the user accepted

- **No expiry** of units; a dormancy notice is a Phase 2 item.
- **Refunds back to units**, cash refunds only through the existing staff refund path for card-paid intents.
- **VAT timing** is unchanged (VAT on sponsor fees at invoice; learner sales carry no VAT line yet). Whether VAT should
  attach to the unit purchase instead needs tax advice before launch.
- **Transfers between wallets** are not offered; a sponsor gives a seat, not units.
- Prices stay stored in naira; the unit price shown is `ceil(naira / rate)`. Storing offerings in units would make every
  rate change reprice the catalogue, which the user did not ask for.

## Consequences

- The chart of accounts grows to fifteen codes (2050, 4060); the migration `UnitAccounts` inserts them.
- `PaymentIntent` gains `payment_method`, `units_charged`, `unit_rate_kobo`, `units_value_kobo`, `units_wallet_id`.
- `IntentHandle.CheckoutUrl` and the booking / package / co-pay responses are nullable: a units sale has no checkout.
- Catalogue, Booking and Sponsorship did not need to change their prices; the web app shows units from the public rate.
