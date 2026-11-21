# OpenAPI changes

First exported document (`artifacts/openapi.json`, OpenAPI 3.0.4) at the end of Sprint 9; earlier sprints had no committed export, so this is the baseline for future diffs (`make openapi` then `git diff --no-index` against the previous file).

Paths: 156  Operations: 168

| Tag | Operations |
|---|---|
| Applications | 8 |
| Applications (staff) | 5 |
| Audit (staff) | 1 |
| Back office (staff) | 16 |
| Bookings | 8 |
| Catalogue | 13 |
| Catalogue (staff) | 1 |
| Certificates | 2 |
| Certificates (staff) | 1 |
| Identity | 16 |
| Identity (staff) | 4 |
| LMS | 21 |
| Ledger (staff) | 5 |
| Notifications | 10 |
| Notifications (staff) | 3 |
| Payments | 8 |
| Payments (staff) | 7 |
| Payouts | 3 |
| Payouts (staff) | 8 |
| Sponsorship | 11 |
| Sponsorship (staff) | 4 |
| Tutors | 11 |
| Tutors (staff) | 2 |

Admin routes (`/api/v1/admin/*`) require staff + MFA + the IP allow-list; webhooks and `GET /verify/{code}` are anonymous; everything else is bearer-authenticated.
