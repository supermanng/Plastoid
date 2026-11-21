# Plastoids — Business, Product & Technical Blueprint

**Where Learning Connects with Opportunity**

18 Oct 2026 · Tolulope Reuben Awode

---

## Table of contents

1. [Executive summary](#1-executive-summary)
2. [Business concept and vision](#2-business-concept-and-vision)
3. [Market opportunity and target customers](#3-market-opportunity-and-target-customers)
4. [Business Model Canvas](#4-business-model-canvas)
5. [Revenue and pricing strategy](#5-revenue-and-pricing-strategy)
6. [Product Requirements Document — overview](#6-product-requirements-document--overview)
7. [User personas](#7-user-personas)
8. [Stakeholder journeys](#8-stakeholder-journeys)
9. [Feature and module inventory](#9-feature-and-module-inventory)
10. [Sponsored learning workflow](#10-sponsored-learning-workflow)
11. [AI capabilities and architecture](#11-ai-capabilities-and-architecture)
12. [LMS requirements and LMS-as-a-Service](#12-lms-requirements-and-lms-as-a-service)
13. [Tutor marketplace requirements](#13-tutor-marketplace-requirements)
14. [UI/UX design specification](#14-uiux-design-specification)
15. [Technical system architecture](#15-technical-system-architecture)
16. [Database design](#16-database-design)
17. [API and integration specification](#17-api-and-integration-specification)
18. [Payments, settlement and accounting](#18-payments-settlement-and-accounting)
19. [Security and governance model](#19-security-and-governance-model)
20. [Three-year financial projections](#20-three-year-financial-projections)
21. [Product roadmap](#21-product-roadmap)
22. [Go-to-market strategy](#22-go-to-market-strategy)
23. [KPI framework](#23-kpi-framework)
24. [Competitive analysis](#24-competitive-analysis)
25. [Risks and mitigations](#25-risks-and-mitigations)
26. [MVP backlog and acceptance criteria](#26-mvp-backlog-and-acceptance-criteria)
27. [Investment and partnership pitch summary](#27-investment-and-partnership-pitch-summary)

---

## 1. Executive summary

Plastoids should launch as a sponsored-learning platform for Nigerian graduates and early-career professionals in tech and digital skills, then grow into a full learning marketplace and LMS on the learner, tutor and data base those programmes create. Sponsors fund seats; Plastoids selects learners, delivers through vetted tutors and an AI tutor, certifies them and reports outcomes — earning a 12% management fee plus ₦12,000 per seat, while fully sponsored learners pay nothing.

**Why this wedge.** Sponsored digital-skills training is an established buying behaviour in Nigeria — the federal 3MTT programme alone selected 30,000 fellows in Phase 1 and works through training providers. Sponsors are shifting from counting people trained to demanding completion and placement evidence, which a platform can supply and a training vendor with spreadsheets cannot. Each sponsored call also draws far more applicants than seats; those applicants become the paid marketplace at almost no acquisition cost.

**What gets built.** One platform with learner, tutor, sponsor and (later) institution apps; a marketplace for courses and tutoring; an LMS built for low bandwidth; an AI tutor grounded in each course's material; and a double-entry ledger that keeps sponsor and tutor money in a segregated client-funds account. The stack is a .NET modular monolith on Azure with Next.js and Flutter front ends — cheap to run at MVP, with clear seams to split later.

**Decisions already taken.** Independent company (not inside an existing group); a separate venture rather than an evolution of any existing LMS; graduates and tech skills as the beachhead; adult learners only until safeguarding for minors is proven.

**The numbers (base case, illustrative).** Launch in month 7 after a six-month build. Net revenue ₦40m in Year 1, ₦334m in Year 2 and ₦739m in Year 3, at a 57% gross margin. Peak cash need ₦931m; with a 25% buffer the company should plan to raise about ₦1.16bn in two rounds — about ₦450–500m seed to reach Gate 2 at month ~15, then the balance on proven completion data. The base case still loses ₦19m a month at month 36; about three more sponsored programmes a month in Year 3 would close that gap, and the aggressive case is EBITDA-positive from month 30.

**What must be proven first, in order.**

1. Two anchor sponsors sign before launch (Gate 1).
2. Sponsored cohorts complete at 65% or better and a sponsor renews (Gate 2).
3. Applicants convert to paid learners at the assumed rates, and tutors stay on-platform.
4. Only then: institutions, facilities and white-label (Gate 3).

**Biggest risks.** Slow sponsor sales cycles, completion rates, tutors taking learners off-platform, and naira depreciation raising AI and cloud costs. Each has a named mitigation and an early-warning signal (section 25).

The accompanying files are the 36-month financial model (three scenarios, all assumptions editable), an investor pitch deck, and the brand and screen designs on the Plastoids design canvas.

---

## 2. Business concept and vision

Plastoids is an Education-as-a-Service (EaaS) platform: one infrastructure on which learners find and take courses, tutors teach and get paid, sponsors fund and track programmes, and institutions run their own learning — with Plastoids operating the technology, the money flows and the programme administration in between.

**Vision.** An intelligent, inclusive learning ecosystem where anyone can access quality education, connect with qualified tutors, benefit from sponsored opportunities and build skills with AI.

**Mission.** Remove the barriers between learners, educators, organisations and opportunities with accessible, intelligent, affordable and scalable learning infrastructure.

### What makes it more than an LMS or a course marketplace

Most platforms own one relationship: Udemy owns learner ↔ course, Preply owns learner ↔ tutor, Moodle owns institution ↔ its own learners, scholarship sites own sponsor ↔ applicant but stop at the award. Plastoids owns the whole sponsored-learning chain — funding, selection, delivery, tracking and impact reporting — and reuses the same marketplace, LMS and AI layer for paid learning and for institutions. Each participant group brings demand or supply the others need, which is where the defensibility comes from (section 24).

### The five participants

| Participant | Brings | Gets | Pays |
|---|---|---|---|
| Learners | Demand, attention, completion data | Courses, tutors, sponsored seats, AI tutor, verifiable certificates | Course fees, tutoring, AI subscription — never a fee on a fully sponsored seat |
| Tutors & instructors | Supply: courses, live teaching, mentorship | Students, scheduling, payments, AI teaching tools, reputation | Commission on sales; optional Pro subscription |
| Sponsors (companies, NGOs, government, foundations) | Funding and real job pathways | A managed programme, verified beneficiaries, outcome and impact reports | Programme value + management and per-seat platform fees |
| Institutions (schools, academies, training firms) | Their own learners and content | LMS-as-a-Service, virtual classrooms, AI, sponsor access | Subscription, setup, white-label licence |
| Plastoids | Technology, AI, payments, admin, facilities network | Platform revenue | — |

### Corporate structure (decided: independent company)

Plastoids is planned as an independent Nigerian company. It does not hold a banking or payment licence; it collects through a licensed payment gateway and keeps sponsor programme funds and tutor earnings in dedicated accounts at a partner bank, with its own ledger as the record of who is owed what (section 18). This keeps regulatory scope small at launch; the open question of whether Plastoids ever needs a PSSP or similar licence is listed in section 27.

---

## 3. Market opportunity and target customers

The beachhead is Nigerian undergraduates, graduates and early-career professionals buying or being sponsored into tech and digital-skills training — data analytics, software, cloud, cybersecurity, UI/UX, product management. That segment already has proven sponsor money flowing into it, a clear job outcome to report on, and adult learners, which keeps child-safeguarding load low at launch.

### What the evidence says (facts, sourced)

- The Federal Government's 3 Million Technical Talent (3MTT) programme picked 30,000 fellows for Phase 1 from December 2023, works through registered training providers, runs hybrid three-month courses and targets 270,000 more in Phase 2 (3MTT). Its 12 skill tracks match Plastoids' launch catalogue almost one for one.
- The ministry reports nearly 300,000 Nigerians trained through 3MTT centres and platforms by end-2025; commentators note a surplus of junior talent and a shortage of mid-level engineers, and that placement is the harder problem (Techeconomy, July 2026).
- NUC put university enrolment at nearly 2 million in 2017 (ICIR); current enrolment is higher but should be confirmed from NUC's latest statistical digest before it goes in a pitch.

Two things follow. First, sponsored digital-skills training is an established buying behaviour in Nigeria, not a hypothesis — government, banks, telcos and foundations already fund it. Second, sponsors are moving from "how many trained" to "how many completed, certified and placed", which is exactly the reporting a platform can do and a training vendor with spreadsheets cannot.

### Target customers, in launch order

| # | Segment | Who exactly | Why they buy | When |
|---|---|---|---|---|
| 1 | Sponsors | Banks, telcos, fintechs, oil & gas CSR units, foundations, state digital-economy agencies, development partners | A managed, auditable programme with outcome reports | MVP |
| 2 | Learners | Final-year students, NYSC members, graduates 21–35, career switchers | Free/sponsored seats, affordable job-relevant courses, certificates employers trust | MVP |
| 3 | Tutors & training providers | Working practitioners (data, cloud, software), bootcamps, small academies | Students without marketing spend, payments, scheduling, AI teaching tools | MVP |
| 4 | Employers (corporate L&D) | Mid-size firms needing upskilling and graduate-trainee programmes | Cohort training with tracking; hiring pipeline | Phase 2–3 |
| 5 | Institutions | Private universities, polytechnics, professional bodies, training firms | LMS-as-a-Service with AI and sponsor access | Phase 3 |
| 6 | Secondary-school tutoring (WAEC/JAMB) | Parents and minors | Exam prep | Phase 3+, only after safeguarding controls are proven |

### Sizing (assumptions, not market data)

There is no reliable public figure for Nigerian spend on sponsored skills training, so the plan sizes bottom-up from the sponsor side instead of quoting a TAM. Assumption: a mid-size sponsored cohort is 200–1,000 learners at ₦150,000–₦400,000 per seat. Ten such sponsors a year is ₦0.3bn–₦4bn of programme value through the platform. The financial model (section 20 and the spreadsheet) builds revenue from sponsor count, cohort size and seat value so every number can be challenged.

**Validate before raising:** (1) interview 15 CSR/L&D heads on budget, decision process and reporting needs; (2) get NUC/NBS current enrolment and graduate figures; (3) confirm 3MTT Phase 2 procurement route for training platforms.

---

## 4. Business Model Canvas

The canvas has one engine — sponsor money buys learner seats delivered by marketplace tutors on Plastoids' LMS and AI — and every other stream reuses that engine.

| Block | Plastoids |
|---|---|
| Customer segments | Sponsors (corporate CSR, L&D, foundations, government agencies, development partners) · learners 18–35 (students, NYSC, graduates, career switchers) · tutors and small training providers · employers · institutions (later) · minors via parents (Phase 3+) |
| Value propositions | Sponsors: turnkey programme with verified beneficiaries and outcome reporting. Learners: free/sponsored seats, affordable courses, AI tutor 24/7, certificates employers can verify. Tutors: students, payments, scheduling and AI tools without building their own platform. Institutions: an LMS with AI, live classes and sponsor access, without an IT team |
| Channels | Web app and Android-first mobile app · campus ambassadors and student unions · NYSC CDS groups · LinkedIn and X for professionals · direct enterprise sales to CSR/L&D heads · partner training hubs · sponsor co-marketing (each sponsored call brings thousands of applicants) |
| Customer relationships | Self-serve for learners and tutors, with WhatsApp-based support · dedicated programme manager per sponsor · account management for institutions · community: cohorts, study groups, alumni |
| Revenue streams | Sponsor programme management and seat fees · course commissions · tutoring commissions · tutor Pro subscriptions · learner AI subscription · corporate training contracts · LMS-as-a-Service · facility booking commissions · proctored assessment and certificate fees · white-label licences (detail: section 5) |
| Key resources | The platform (marketplace, LMS, AI layer, ledger) · vetted tutor network · course content rights and licences · sponsor relationships and impact data · partner hub network · brand trust |
| Key activities | Platform engineering · tutor recruitment and vetting · course quality review · programme management (applications, selection, cohort operations) · sponsor sales · learner support · reporting · safeguarding and compliance |
| Key partners | Payment gateway (Paystack or Flutterwave) and a partner bank · cloud and LLM providers · video provider · identity verification (NIN/BVN via a licensed provider) · existing training centres and tech hubs · universities and NYSC · employers for placement · certification bodies · 3MTT and state digital agencies |
| Cost structure | Engineering and product staff (largest) · cloud, video and storage · AI inference · payment fees · tutor acquisition and vetting · marketing and sales · programme operations staff · compliance, audit and insurance · hub partner shares |

**Unfair advantage to build deliberately:** a sponsor-grade outcomes record. After two years Plastoids should be able to tell any sponsor "our last 20 programmes completed at X% and placed Y%" — data no single training vendor holds.

---

## 5. Revenue and pricing strategy

Sponsors pay for administration and platform use; learners pay only for what they choose to buy. A fully sponsored learner pays ₦0, and Plastoids recovers its costs from the sponsor through two disclosed line items — a programme management fee and a per-seat platform fee — never by charging the beneficiary.

All prices below are illustrative assumptions for planning and for the financial model, set against what Nigerian learners and sponsors plausibly pay; none is market data. Validate each with 10–20 buyer conversations before publishing.

### Who pays what

| Stream | Payer | Illustrative price (₦) | How Plastoids earns | Phase |
|---|---|---|---|---|
| 1. Learner access / 7. AI subscription (one product: Plastoids Plus) | Learner | Free tier; Plus 3,000/month or 30,000/year | Subscription. Plus = unlimited AI tutor, AI career advisor, offline downloads, priority booking | Free at MVP, Plus in Phase 2 |
| 2. Tutor commissions | Deducted from tutor earnings | Courses 20% when Plastoids found the learner, 8% when the tutor's own link did; live tutoring 15% | Take rate on gross sales | MVP |
| 3. Tutor subscriptions | Tutor | Free · Pro 7,500/month (course commission 15%, AI teaching assistant, analytics) · Academy 30,000/month (up to 10 instructors, 12%, branded storefront) | Subscription | Phase 2 |
| 4. Paid course enrolments | Learner | Self-paced 15,000–60,000 · live cohort bootcamp 120,000–250,000 · 1:1 tutoring 5,000–15,000/hour (tutor sets price) | Commission (stream 2) | MVP |
| 5. Sponsored programmes | Sponsor | Management fee 12% of tuition value (minimum 3m per programme) + platform seat fee 12,000 per beneficiary | Fees on top of tuition passed to tutors/providers | MVP |
| 6. Corporate training contracts | Employer | 60,000–250,000 per seat, or an annual contract | Margin over tutor/provider cost (target 30–40%) | Phase 2 |
| 8. LMS-as-a-Service | Institution | Starter 200,000/month (≤500 active learners) · Growth 600,000/month (≤2,000) · Enterprise from 1.5m/month; setup 0.75m–3m | Subscription + setup | Phase 3 |
| 9. Physical facility bookings | Learner, tutor, sponsor | Seat-day 6,000; lab room-day 80,000 (hub sets price) | 20% commission to Plastoids, 80% to the partner hub | Phase 3 |
| 10. Virtual classroom | Institutions, external hosts | Included for marketplace tutors; 30,000 per host licence/month for others | Subscription | Phase 3 |
| 11. Certification and assessment | Learner or sponsor | Verified certificate 5,000; proctored exam 15,000 (included in sponsored seat fee) | Fee per issue | Phase 2 |
| 12. White-label enterprise | Large institution, agency | Setup 8m–20m + licence from 1.5m/month | Licence + services | Phase 3 |

### Fully funded programmes: cost recovery without charging learners

Worked example — a bank sponsors 500 graduates on a 3-month Data Analytics programme at a tuition value of ₦150,000 per seat:

| Line | Basis | ₦ |
|---|---|---|
| Tuition passed to tutors / training provider | 500 × 150,000 | 75,000,000 |
| Programme management fee | 12% of tuition | 9,000,000 |
| Platform seat fee (LMS, AI tutor, certificate, reporting) | 500 × 12,000 | 6,000,000 |
| **Sponsor invoice** | | **90,000,000** |
| **Plastoids revenue** | fees only | **15,000,000** (16.7% of sponsor spend) |
| **Learner pays** | | **0** |

The fees are shown as separate lines on the sponsor's invoice and in their dashboard, so the sponsor sees exactly what reached learners and what paid for administration. Optional extras are quoted separately: custom curriculum design, physical hub days, placement drives.

**Commitment deposits (sponsor's choice).** Some sponsors want learners to have something at stake. Plastoids can hold a disclosed, refundable deposit (for example ₦5,000) returned on completion. Forfeited deposits go back into that sponsor's programme pool — never to Plastoids — so the platform never profits from learners dropping out.

### Partially sponsored programmes

The sponsor funds a stated share (say 70%) and the learner pays the rest (₦45,000 on a ₦150,000 seat), shown before they apply. The learner can pay in up to three instalments; the seat is held for 7 days after acceptance. Sponsor fees are charged as above on the sponsor's share; the learner's share is charged at tuition value with no extra platform fee, so the learner never pays more than the disclosed co-pay. If a learner never pays, the seat returns to the waitlist and the sponsor is not billed for it.

### Paid learning: how one ₦45,000 course sale splits

Gateway fee (1.5% + ₦100, capped at ₦2,000, per Paystack's Nigerian pricing) = ₦775 borne by Plastoids. Commission 20% = ₦9,000. Tutor earns ₦36,000. Plastoids net = ₦8,225 before AI, hosting and support costs.

### Recommended monetisation sequence

1. **Sponsored programmes first.** Few sales cycles, large tickets, and each programme brings hundreds of learners and tutors onto the platform at once — it solves cold start (section 22).
2. **Course and tutoring commissions** as soon as there is traffic from sponsored calls (each call typically draws far more applicants than seats; the unsuccessful applicants are the first paid-course market).
3. **Corporate training and Plastoids Plus** in Phase 2, once completion data proves the product.
4. **LMS-as-a-Service, facilities and white-label** in Phase 3 — highest margin but longest sales cycle and heaviest support.

---

## 6. Product Requirements Document — overview

The PRD below (sections 6–19) specifies one platform with four role-based apps — Learner, Tutor, Sponsor, Institution — plus a back office, all sharing one identity, one catalogue, one LMS, one AI layer and one ledger.

### Product principles

1. **Sponsored seats are first-class.** Every course, cohort and seat can carry a funding source (self, sponsor, employer, institution, split). Pricing, invoicing, eligibility and reporting all key off it.
2. **Built for Nigerian conditions.** Android-first, works on 3G and data-saver mode, video at 240p/360p with audio-only fallback, offline downloads, WhatsApp and SMS notifications, naira pricing, bank transfer and USSD alongside cards.
3. **Outcomes, not activity.** Every learner action that matters to a sponsor (attendance, assessment, completion, certificate, placement) is an event in one stream that reports and AI read from.
4. **AI guides; humans decide.** AI drafts, explains, recommends and flags. Grades that count, selection decisions and payouts always have a human or a deterministic rule behind them.
5. **One ledger of record.** Every naira movement is a double-entry posting; dashboards read from the ledger, never from screens that compute their own totals.

### Scope by release

| Release | In scope | Out of scope |
|---|---|---|
| MVP (Phase 1, months 0–6) | Auth, profiles, catalogue, tutor marketplace, paid courses, sponsored discovery + applications, basic LMS (video, quizzes, assignments), live class via third-party video, payments, payouts, admin | AI tutor, institution portal, facilities, white-label, multi-currency |
| Phase 2 (months 7–12) | AI tutor with RAG, recommendations, sponsor dashboards and impact reports, proctored assessment, verifiable certificates, tutor Pro, Plastoids Plus, corporate cohorts | Institution LMS, facilities |
| Phase 3 (months 13–24) | Institution portal and LMS-as-a-Service, facilities network, enterprise reporting, white-label, SCORM/xAPI | Multi-currency |
| Phase 4 (months 25–36) | Multi-currency, multilingual, cross-border tutoring, regional sponsor programmes, career ecosystem | — |

### Non-functional requirements

| Area | Requirement |
|---|---|
| Performance | Learner pages interactive in under 3 s on a mid-range Android over 3G; API p95 under 300 ms for reads |
| Availability | 99.5% monthly in MVP, 99.9% from Phase 3; live classes degrade to audio before failing |
| Scale | MVP sized for 50,000 registered and 5,000 concurrent learners; architecture path to 1m+ registered |
| Data | Nigeria Data Protection Act 2023 compliant; learner data region-pinned; encryption at rest and in transit |
| Accessibility | WCAG 2.2 AA on web; captions on all recorded video; screen-reader-tested core flows |
| Bandwidth | Course page under 500 KB before video; adaptive streaming; downloads resumable |
| Auditability | Every money movement, grade change, selection decision and admin action logged immutably |
| Localisation | English at launch; Yoruba, Hausa, Igbo and French interface and AI tutor language in Phase 4 |

---

## 7. User personas

Six personas drive the MVP and Phase 2 design; they are composites to design against, not research findings, and should be replaced with interview-based personas after discovery.

| Persona | Context | Goal | Frustration today | What Plastoids must do for them |
|---|---|---|---|---|
| Chiamaka, 23 — fresh graduate (Enugu, NYSC member) | Android phone, 2–5 GB data a month, no laptop at home | Land a data-analyst job within a year | Free resources are scattered; bootcamps cost ₦200k+; can't tell which certificates employers respect | Surface sponsored seats she qualifies for, low-data lessons, AI tutor at night, a portfolio employers can verify |
| Tunde, 29 — career switcher (Lagos, bank operations officer) | Laptop, evenings and weekends only | Move into cloud engineering | Courses don't fit his hours; no one to ask when stuck | Self-paced plus weekend live sessions, 1:1 tutor booking, instalments, progress that survives missed weeks |
| Ngozi, 34 — independent tutor (Ibadan, senior data analyst) | Teaches on weekends via WhatsApp and Zoom, collects by bank transfer | Earn ₦300k+/month teaching without running a business | Chasing payments, scheduling, marketing herself, no-shows | A storefront, booking with prepayment, automatic payouts, AI help preparing materials |
| Mrs Adebayo, 45 — CSR lead (Lagos, tier-2 bank) | Annual CSR budget, board reporting each quarter | Fund a credible youth programme and show impact | Vendors send spreadsheets; can't verify beneficiaries; drop-out is invisible until the end | Campaign builder, eligibility rules, live completion dashboard, audit-ready impact report |
| Mr Ibrahim, 52 — registrar (Kaduna, private polytechnic) | 3,000 students, Moodle installed but unused, small IT team | Blended learning without new hires | LMS is hard to run; lecturers won't adopt it | Hosted LMS with templates, onboarding, AI course-building help, support in his time zone |
| Seyi, 31 — Plastoids programme manager (internal) | Runs 5–8 sponsor programmes at once | Hit completion targets, keep sponsors renewing | Manual shortlisting, chasing tutors and learners on WhatsApp | Bulk screening, at-risk learner alerts, cohort messaging, one-click sponsor reports |

---

## 8. Stakeholder journeys

Each journey below is the happy path the MVP must make work end to end; the moments where people drop off are marked ⚠ and get specific design attention.

### Learner (Chiamaka): discovery to certificate

1. Sees a sponsor's call on X or from a campus ambassador; lands on the opportunity page.
2. Signs up with phone number + OTP (email optional); sets interests and career goal in three taps.
3. Eligibility check runs instantly on the stated criteria; she sees "You qualify" or why not, plus paid alternatives.
4. Applies: short form, entry assessment (30 min, mobile-friendly). ⚠ Long forms lose applicants — cap at 8 fields.
5. Tracks status on her dashboard; gets WhatsApp/SMS updates at each stage.
6. Accepted → accepts the offer and code of conduct within 5 days, else the seat goes to the waitlist.
7. Learns: weekly live class, recorded lessons, AI tutor, assignments. ⚠ Weeks 2–4 are where drop-out peaks; at-risk alerts go to the programme manager.
8. Passes the final assessment → verifiable certificate and portfolio entry; opted-in profile shared with the sponsor's hiring partners.
9. Gets recommendations for the next (paid or sponsored) course.

### Tutor (Ngozi): application to payout

1. Applies; uploads ID and credentials; NIN and BVN verified through a licensed provider.
2. Records a 5-minute demo lesson; passes a subject review by a Plastoids reviewer.
3. Builds profile, sets availability, rates and formats; publishes a course through the lesson builder.
4. Gets bookings and enrolments; teaches; marks assignments with AI-drafted feedback she edits.
5. Earnings clear after the hold period (section 18) and are paid to her bank account weekly.
6. Invited onto sponsored programmes once her rating and completion record meet the bar.

### Sponsor (Mrs Adebayo): brief to impact report

1. Books a call or self-registers; organisation verified (CAC, authorised signatory).
2. Builds a campaign: course or custom programme, seats, eligibility, dates, budget.
3. Approves the quote; pays the invoice (or the first milestone).
4. Plastoids publishes; applications flow in; she reviews the shortlist or delegates selection.
5. Watches enrolment, attendance and completion live; receives monthly reports.
6. Downloads the final impact report and beneficiary certificates; renews or expands.

The full sponsored workflow, with every hand-off, is drawn in section 10.

### Institution (Mr Ibrahim): pilot to rollout

1. Demo → 60-day pilot with two departments.
2. Tenant provisioned on a subdomain, branding applied, staff and students bulk-imported.
3. Lecturers onboarded with course templates; AI helps convert lecture notes into modules.
4. Pilot review against agreed adoption targets → annual contract.

### Plastoids programme manager (Seyi): running a cohort

1. Receives a funded programme; assigns tutors and a schedule.
2. Runs screening (rules + AI-assisted ranking + human review).
3. Onboards the cohort; monitors the at-risk queue daily; intervenes by message or call.
4. Signs off completions; generates and sends the sponsor's report; closes the programme ledger.

---

## 9. Feature and module inventory

Twenty modules make up the platform; 13 are needed in some form for the MVP (marked MVP). Each entry gives purpose, users, core features, business rules, approvals, integrations, key entities, permissions and edge cases.

| # | Module | Phase | Main users |
|---|---|---|---|
| 1 | Public website & landing pages | MVP | Everyone |
| 2 | Course discovery marketplace | MVP | Learners |
| 3 | Student portal | MVP | Learners |
| 4 | Tutor portal | MVP | Tutors |
| 5 | Sponsor portal | MVP (basic), P2 (analytics) | Sponsors |
| 6 | Institution portal | P3 | Institution admins, lecturers |
| 7 | LMS | MVP (basic) | Learners, tutors |
| 8 | AI learning assistant | P2 | All |
| 9 | Tutor booking & scheduling | MVP | Learners, tutors |
| 10 | Virtual classroom | MVP (via provider) | Learners, tutors |
| 11 | Payments & billing | MVP | Learners, sponsors, finance |
| 12 | Sponsorship management | MVP | Sponsors, programme managers |
| 13 | Application review & approval | MVP | Programme managers, sponsors |
| 14 | Certification management | P2 (basic certificate MVP) | Learners, verifiers |
| 15 | Physical facility management | P3 | Hubs, learners, tutors |
| 16 | Messaging & notifications | MVP | All |
| 17 | Wallet, settlement & payouts | MVP | Tutors, finance |
| 18 | Learning analytics | P2 | Sponsors, tutors, admins |
| 19 | Enterprise training management | P2 | Employers |
| 20 | Super Admin & back office | MVP | Plastoids staff |

### 1. Public website & landing pages — MVP

- **Purpose:** acquire learners, tutors, sponsors and institutions; SEO for course and opportunity pages.
- **Users:** anonymous visitors; marketing staff (CMS).
- **Features:** homepage, course and opportunity listings rendered server-side for search, Become a tutor/sponsor/institution pages, pricing, about, blog, campaign landing pages built from templates.
- **Rules:** only published, approved courses and open opportunities are indexed; closed opportunities stay visible with "closed" and similar open ones.
- **Approvals:** marketing edits go live after a second editor's review.
- **Integrations:** headless CMS, analytics, consent banner, CDN.
- **Entities:** Page, CampaignLanding, Lead.
- **Permissions:** public read; Marketing role write.
- **Edge cases:** sponsor asks for an unlisted (invite-only) programme → page reachable by link only, noindex.

### 2. Course discovery marketplace — MVP

- **Purpose:** help learners find the right paid, free or sponsored course or tutor.
- **Features:** search with typo tolerance; filters (subject, skill, industry, level, price, funding type, format, language, duration, rating); side-by-side compare of up to 3 courses or tutors; ratings and verified-learner reviews; "sponsored seats available" badge; recommendations (rules in MVP, ML in P2).
- **Rules:** only verified enrolments can review; reviews open after 30% completion; ranking blends relevance, quality score and completion rate — never pay-to-rank in organic results (promoted slots are labelled).
- **Integrations:** search engine (PostgreSQL full-text at MVP → OpenSearch at scale).
- **Entities:** Course, CourseVersion, Category, Skill, Review, Listing, SearchEvent.
- **Edge cases:** course price changes while in cart → honour the price for 30 minutes; course unpublished after purchase → access retained.

### 3. Student portal — MVP

- **Purpose:** the learner's home: dashboard, learning, applications, payments, certificates, portfolio.
- **Features:** profile and goals; dashboard (sponsored opportunities, recommended courses, upcoming classes, progress, AI assistant entry); application tracker; my courses; downloads; certificates; portfolio; billing and receipts; notification settings; data export and deletion.
- **Rules:** one account per person (phone + NIN on sponsored applications) to stop seat farming; under-18s blocked at MVP.
- **Entities:** User, LearnerProfile, Goal, Enrolment, Application, PortfolioItem.
- **Permissions:** learner sees own data only; guardian access in Phase 3 for minors.
- **Edge cases:** learner changes phone number → re-verify with email or NIN before transfer.

### 4. Tutor portal — MVP

- **Purpose:** let tutors build a reputation, teach and get paid.
- **Features:** onboarding wizard and verification status; profile, bio, specialisations, demo video; course creation and lesson builder; availability and pricing; class schedule; student list and progress; assignment marking; earnings dashboard (gross, commission, net, pending, paid); payout account; ratings; sponsored-programme invitations; AI teaching assistant (P2).
- **Rules:** tutors cannot publish until verification and subject review pass; contact details and off-platform payment requests in messages are flagged.
- **Approvals:** course publish → content review (automated checks + human for first 3 courses, sampled after).
- **Entities:** TutorProfile, Credential, VerificationCheck, Availability, PriceRule, Course, Payout.
- **Edge cases:** tutor suspended mid-cohort → cohort reassigned, earnings for delivered sessions still paid.

### 5. Sponsor portal — MVP basic, P2 analytics

- **Purpose:** let organisations fund, run and evidence learning programmes.
- **Features:** organisation verification; team members and roles (owner, finance, reviewer, viewer); campaign builder (objectives, course or custom request, seats, eligibility, budget, dates, selection mode); quotes and invoices; application review; beneficiary list; live enrolment, attendance and completion; spend vs budget; impact reports; certificate downloads.
- **Rules:** a campaign publishes only after funding (or an approved payment plan) is confirmed; eligibility rules must be lawful and non-discriminatory (reviewed by Plastoids — see section 19).
- **Approvals:** campaign → Plastoids programme review → sponsor sign-off on final quote → publish.
- **Entities:** Organisation, SponsorUser, Campaign, EligibilityRule, Quote, Invoice, ProgrammeBudget, ImpactReport.
- **Edge cases:** sponsor wants to cut seats after selection → only unfilled seats can be released; refunds per contract.

### 6. Institution portal — Phase 3

- **Purpose:** give schools and training firms their own branded LMS tenant.
- **Features:** tenant settings and branding; departments, programmes, terms; staff and student bulk import (CSV, SIS sync); course templates; gradebook export; tenant analytics; billing; option to list selected courses on the public marketplace.
- **Rules:** tenant data is isolated (section 15); institution owns its content and learner records; Plastoids processes on its behalf.
- **Integrations:** SSO (SAML/OIDC), SIS via CSV/API, LTI 1.3 tools.
- **Entities:** Tenant, Department, Term, Programme, TenantUser.
- **Edge cases:** student is both a tenant learner and a marketplace learner → one identity, two scoped enrolments.

### 7. LMS — MVP basic

Covered in full in section 12.

### 8. AI learning assistant — Phase 2

Covered in full in section 11.

### 9. Tutor booking & scheduling — MVP

- **Purpose:** book 1:1 and group sessions without back-and-forth.
- **Features:** availability calendar with time zones; instant book or request-to-book; packages (e.g. 5 sessions); group sessions with capacity; reminders (24 h, 1 h); reschedule and cancellation; calendar sync (Google, Outlook).
- **Rules:** payment captured at booking; learner cancels >24 h before → full refund to wallet or card; <24 h → 50%; tutor no-show → full refund + tutor strike; three strikes in 90 days → review.
- **Entities:** Booking, SessionPackage, CalendarSlot, CancellationPolicy.
- **Edge cases:** double booking from calendar sync lag → slot locked on checkout for 10 minutes.

### 10. Virtual classroom — MVP via provider

- **Purpose:** live classes, 1:1s, breakout rooms, recordings.
- **Features:** join from browser/app without install; screen share, whiteboard, chat, polls, breakout rooms, raise hand; auto-recording to the course; attendance captured from join/leave events; low-bandwidth mode (audio + slides).
- **Build vs buy:** buy at MVP (a video SDK such as Daily, 100ms or Zoom Video SDK — choose on Nigerian latency tests and per-minute price); Plastoids owns the room UI and attendance logic.
- **Entities:** LiveSession, Room, Recording, AttendanceEvent.
- **Edge cases:** tutor's connection drops → co-host or auto-promote a teaching assistant; recording fails → tutor notified to re-record summary.

### 11. Payments & billing — MVP

- **Purpose:** take money from learners and sponsors; issue invoices and receipts.
- **Features:** card, bank transfer, USSD via the gateway; dedicated virtual accounts for sponsor invoices; instalments for cohorts; coupons and promo codes; subscriptions (Plus, tutor Pro, LMS); VAT on Plastoids' fees; receipts and credit notes; refunds.
- **Rules:** VAT (7.5%) applies to Plastoids' fees and subscriptions, not to tuition passed through on behalf of a tutor — confirm treatment with a tax adviser; failed subscription renewal → 3 retries over 7 days, then downgrade.
- **Integrations:** Paystack or Flutterwave (pick one primary, one fallback), accounting export.
- **Entities:** PaymentIntent, Transaction, Invoice, Receipt, Refund, Subscription, Coupon.
- **Edge cases:** gateway webhook arrives before the redirect, or twice → idempotent by gateway reference; transfer paid to virtual account with wrong amount → hold and reconcile manually.

### 12. Sponsorship management — MVP

- **Purpose:** run a sponsored programme from funded brief to closed books. Full workflow: section 10.
- **Features:** programme setup; funding milestones; seat inventory; tutor/provider assignment; cohort calendar; budget vs spend; programme ledger; close-out.
- **Rules:** seats ≤ funded amount ÷ seat cost; unused funds at close → refunded or rolled into the sponsor's next programme as the contract says.
- **Entities:** Programme, FundingTranche, Seat, Cohort, ProgrammeLedger.

### 13. Application review & approval — MVP

- **Purpose:** fair, fast, auditable selection.
- **Features:** configurable form; automatic eligibility rules; entry assessment; scoring rubric; AI-assisted ranking with reasons (P2); reviewer queues with blind review option; bulk accept/reject/waitlist; offer letters; appeals.
- **Rules:** hard eligibility filters are deterministic; AI ranking is advisory and every final decision is a recorded human action; protected characteristics are never scoring inputs.
- **Approvals:** shortlist → sponsor approval (or delegated) → offers.
- **Entities:** Application, ApplicationAnswer, Assessment, Score, Decision, Appeal.
- **Edge cases:** duplicate applicants (same NIN/phone) → merged and flagged; applicant accepted on two overlapping programmes → must choose one.

### 14. Certification management — MVP basic, P2 full

- **Purpose:** certificates employers can trust.
- **Features:** templates per course or sponsor (co-branded); issue on rule (completion + pass mark); unique ID and QR verification page; revocation; LinkedIn add-to-profile; Open Badges 3.0 / verifiable credentials in P3.
- **Rules:** certificates are immutable once issued; revocation keeps the record and shows "revoked" on the verification page.
- **Entities:** CertificateTemplate, Certificate, VerificationLog.

### 15. Physical facility management — Phase 3

- **Purpose:** use partner hubs for labs, classes and exams without owning buildings.
- **Features:** hub onboarding and inspection; rooms and seats with capacity and equipment; reservations; QR check-in; usage logs; access codes; hub payouts; ratings.
- **Rules:** hubs paid 80% of booking value monthly after check-in data confirms use; minimum standards (power backup, internet speed, accessibility) checked before listing.
- **Entities:** Hub, Room, Resource, Reservation, CheckIn, HubPayout.
- **Edge cases:** power outage cancels a session → automatic rebooking credit; exam centre fails → backup hub list per city.

### 16. Messaging & notifications — MVP

- **Purpose:** reach people where they read.
- **Features:** in-app inbox; cohort announcements; 1:1 learner–tutor messages; WhatsApp Business API, SMS, email, push; per-user channel preferences; quiet hours; templates.
- **Rules:** learner–tutor messages are on-platform, retained and moderated; adult–minor direct messages are disabled (Phase 3 safeguarding); marketing only with consent.
- **Entities:** Message, Thread, Notification, Template, Preference.

### 17. Wallet, settlement & payouts — MVP

- **Purpose:** pay tutors, hubs and refunds accurately. Accounting detail: section 18.
- **Features:** earnings sub-ledger per tutor; hold periods; weekly payout runs; payout to verified bank account (name match); statements; tax withholding where required; payout holds for disputes.
- **Rules:** learner "wallet" is a store of refund credit only, not a stored-value product (no top-ups, no transfers) to stay outside e-money regulation.
- **Entities:** LedgerAccount, Posting, PayoutBatch, Payout, Hold.
- **Edge cases:** bank name mismatch → payout blocked until resolved; payout reversed by bank → funds return to tutor balance with a notice.

### 18. Learning analytics — Phase 2

- **Purpose:** turn learning events into decisions for learners, tutors, sponsors and Plastoids.
- **Features:** event pipeline (xAPI-style statements); dashboards per role; at-risk scoring; cohort comparisons; exportable sponsor reports; KPI framework of section 23.
- **Entities:** LearningEvent, MetricSnapshot, RiskScore, Report.

### 19. Enterprise training management — Phase 2

- **Purpose:** employers train staff in cohorts with tracking.
- **Features:** company workspace; seat purchase; assign courses to staff; mandatory training deadlines; manager dashboards; HRIS export; private courses.
- **Entities:** Company, Seat, Assignment, ComplianceDeadline.

### 20. Super Admin & back office — MVP

- **Purpose:** run the business safely.
- **Features:** executive dashboard; user, tutor, sponsor and institution management; verification queues; course moderation; programme operations; finance (reconciliation, payouts, refunds, invoices); disputes; facility management; compliance (data requests, safeguarding reports); audit log viewer; feature flags and platform configuration (fees, commissions, policies).
- **Rules:** four-eyes approval on payout batches, refunds above ₦100,000, fee-rule changes and role grants; all admin actions logged with before/after values.
- **Permissions:** fine-grained staff roles (section 19); no single staff role can both create and approve a payout.

---

## 10. Sponsored learning workflow

A sponsored programme moves through twelve hand-offs between sponsor, Plastoids, learners and tutors; the sponsor touches it at four points and Plastoids carries everything in between.

Plastoids holds the work between the sponsor's four touchpoints; teaching and learning run side by side, and the sponsor's last step — the impact report — is what earns the renewal.

### Worked example: 500 graduates, 3-month Data Analytics

| Week | Step | Owner | Output |
|---|---|---|---|
| −8 | Brief call; CAC and signatory verification | Sponsor, Plastoids sales | Verified organisation |
| −7 | Campaign built: course, 500 seats, eligibility (graduate, 21–30, resident in Nigeria, not previously sponsored), budget ₦90m | Plastoids programme manager | Quote and programme agreement |
| −6 | Invoice paid to a dedicated virtual account (or 50% now, 50% at week 6) | Sponsor finance | Programme funded; seats unlocked |
| −5 to −3 | Opportunity published and promoted; applications open 14 days | Plastoids marketing | Applicants (planning assumption: 8–15 per seat) |
| −3 | Entry assessment (numeracy, logic, Excel basics) | Learners | Scored pool |
| −2 | Rules filter + AI-assisted ranking + human review; 500 offers + 100 waitlist | Plastoids, sponsor approves or delegates | Signed-off cohort |
| −1 | Offers accepted in 5 days; waitlist backfills | Learners | Confirmed 500 |
| 0–12 | Weekly live classes (cohorts of 50, 10 tutors), recorded lessons, AI tutor, projects | Tutors, learners | Attendance, grades |
| 0–12 | At-risk queue reviewed daily; nudges, tutor office hours, peer groups | Plastoids | Retention |
| 12 | Capstone project + proctored assessment | Learners, tutors | Pass/fail |
| 13 | Certificates issued; opt-in talent profiles shared with sponsor's hiring partners | Plastoids | Verified certificates |
| 14 | Impact report: reach, demographics (aggregated), attendance, completion, scores, satisfaction, early placement; unspent funds reconciled | Plastoids | Report, closed programme ledger |
| +12 to +26 | Placement follow-up surveys at 3 and 6 months | Plastoids | Outcome update to sponsor |

### Variations the workflow supports

- **Partial sponsorship:** step 8 (accept offer) adds a co-pay checkout; the seat is confirmed only when the learner's share (or first instalment) is paid.
- **Delegated selection:** the sponsor sets criteria and a scoring rubric, then Plastoids selects and the sponsor sees the audit trail rather than every application.
- **Custom programmes:** before step 2, Plastoids scopes and prices curriculum design; the sponsor approves the outline before content is built.
- **Training-provider delivery:** a sponsor can name an existing provider (or 3MTT-style partners); the provider teaches on Plastoids and is paid through the same payout rails.
- **Milestone funding:** large programmes release funds in tranches tied to enrolment and completion milestones; the dashboard shows tranche status.

### Rules that protect each party

- **Learners:** a fully sponsored seat never generates a learner charge; any co-pay or deposit is shown before applying.
- **Sponsors:** they are billed only for confirmed seats; replacement of drop-outs before week 3 is free; every beneficiary is identity-verified.
- **Tutors:** paid per delivered session or milestone, not per completion, so they are not penalised for learner drop-out — completion bonuses are optional and sponsor-funded.

---

## 11. AI capabilities and architecture

Plastoids uses hosted LLM APIs behind its own AI gateway, grounded in each course's material through retrieval (RAG), with metering per user and per sponsor — and it should not self-host models until volume or a data-residency contract makes the economics clear (likely not before Phase 3).

### The six AI capabilities

| Capability | Who uses it | What it does | Guardrail | Revenue | Phase |
|---|---|---|---|---|---|
| AI Tutor | Learners | Explains concepts step by step, answers questions on course content with citations to the lesson, generates worked examples and practice questions, adapts to level, answers in English first (Pidgin, Yoruba, Hausa, Igbo, French in P4) | Socratic mode on graded work: hints and guiding questions, never the submitted answer; detects pasted assessment questions | Included in sponsored seats (in seat fee); free tier 20 messages/day; unlimited in Plus | P2 |
| Personalised learning engine | Learners | Recommends next lessons, revision schedule (spaced repetition), courses, tutors and sponsored programmes the learner is eligible for | Recommendations explainable ("because you scored 45% on joins"); eligibility is rule-based, not inferred | Drives course sales and programme fill | P2 |
| AI Tutor Assistant | Tutors | Drafts lesson plans, outlines, quizzes, slides and notes; summarises live sessions from transcripts; grades objective items; drafts feedback on assignments; flags struggling learners | Tutor reviews and owns every grade and feedback that reaches a learner | Tutor Pro (quota) | P2 |
| AI Career Advisor | Learners | Maps goal role → skills → gaps from assessments → courses and programmes; CV and portfolio feedback | Labour-market claims cite sources; no salary promises | Plus | P2 |
| Sponsor intelligence | Sponsors, programme managers | Natural-language questions over the sponsor's own programme data; drop-out risk explanations; draft narrative for impact reports | Answers only from that sponsor's data; aggregated, no personal data unless consented | In management fee; premium analytics later | P2 |
| Selection assistant | Programme managers | Ranks applications against the sponsor's rubric with reasons | Advisory only; protected characteristics excluded; humans decide; bias audit per programme | In management fee | P2 |

### Architecture

- **AI gateway service (inside the platform):** every model call goes through one service that applies the tenant's policy, picks the model, attaches retrieval context, logs prompts/responses (redacted), enforces quotas and records cost to the user, course and sponsor.
- **Model routing:** a small, cheap model for classification, moderation, quiz generation and short answers; a stronger model for tutoring explanations, feedback drafts and career advice. At least two providers (e.g. Anthropic and OpenAI, or a hosted open-weight model) behind one interface so price, outage or policy changes don't stop the product.
- **RAG:** course materials (transcripts, slides, PDFs, notes) are chunked, embedded and stored per course in a vector index (pgvector inside PostgreSQL at MVP/P2; a dedicated vector database only if retrieval latency or scale demands it). Retrieval is filtered by tenant, course and the learner's enrolment, so nobody retrieves content they haven't access to.
- **Prompt management:** versioned system prompts per capability stored as configuration, with evaluation sets (100–300 graded examples per capability) run on every change.
- **Agentic workflows only where justified:** multi-step jobs that are bounded and checkable — building a course outline into module drafts, assembling an impact report, revision plans. No autonomous actions on money, grades or selection.
- **Safety:** input and output moderation; prompt-injection defences on uploaded documents (retrieved text is data, never instructions); age-aware policies when minors arrive; refusal and escalation paths to a human tutor.
- **Privacy:** contracts with model providers that exclude training on Plastoids data and set short retention; personal identifiers stripped before prompts where the task doesn't need them; learner can view and delete their AI history.
- **Cost control:** per-user daily quotas, caching of repeated explanations, prompt caching for course context, short contexts by default, monthly cost dashboards per sponsor and per course. The financial model budgets AI at a per-active-learner cost (assumption in section 20).

### When to use APIs and when to self-host

| Situation | Choice | Why |
|---|---|---|
| MVP to Phase 2 | Hosted APIs | Best quality per naira at low volume, no GPU operations team |
| Classification, embeddings, moderation at high volume | Small hosted or open-weight models | Cost per call falls sharply; quality bar is lower |
| Government or institution contract requires in-country data or no third-party processing | Open-weight model in a private cloud deployment for that tenant | Contractual requirement outweighs cost |
| AI spend passes roughly 15% of revenue for two consecutive quarters | Re-evaluate self-hosting for the highest-volume capability | Economics may then justify GPUs |

---

## 12. LMS requirements and LMS-as-a-Service

Plastoids builds its own LMS core rather than embedding Moodle, because sponsored seats, the ledger, the AI layer and the marketplace all need to share one data model; it buys video, proctoring and conferencing, and adds SCORM/xAPI/LTI for interoperability in Phase 3.

### Capabilities by release

| Capability | MVP | Phase 2 | Phase 3 |
|---|:---:|:---:|:---:|
| Course catalogue, modules, lessons, versions | ✓ | | |
| Video (adaptive streaming, 240p–1080p, captions, downloads) | ✓ | | |
| Documents (PDF, slides) with page tracking | ✓ | | |
| Live lectures (via video provider) and auto-recorded sessions | ✓ | | |
| Quizzes (MCQ, multi-select, short answer, code), assignments with rubrics | ✓ | | |
| Gradebook, completion rules, basic certificates | ✓ | | |
| Attendance from live-session events | ✓ | | |
| Discussion forum per course and cohort; learner messaging | ✓ | | |
| Corporate and sponsored cohorts with own calendars | ✓ | | |
| Mobile-friendly web + Android app with offline lessons | ✓ | | |
| Examination management (timed, randomised banks, proctoring) | | ✓ | |
| Learning pathways (ordered courses with prerequisites) | | ✓ | |
| Progress analytics and at-risk alerts | | ✓ | |
| Multi-instructor courses with roles (lead, TA, grader) | | ✓ | |
| Peer review, group projects | | ✓ | |
| SCORM 1.2/2004 import, xAPI statement export, LTI 1.3 tool and platform | | | ✓ |
| Tenant gradebook export, SIS sync | | | ✓ |
| iOS app | | | ✓ |

### Low-bandwidth and accessibility rules

- Every video has an audio-only track and a text transcript; default quality is chosen from measured bandwidth, with a data-saver toggle that caps at 240p.
- Lessons download for offline viewing in the app (encrypted, tied to the account, expiring after the enrolment ends).
- Quizzes work offline and sync answers when the connection returns, with server-side timing for exams.
- WCAG 2.2 AA: captions, keyboard navigation, contrast, screen-reader labels, adjustable text size.

### LMS-as-a-Service for institutions (Phase 3)

1. **Tenant model:** each institution gets a tenant on a Plastoids subdomain or its own domain, with branding, its own users, courses and data, isolated at the database level (section 15).
2. **Onboarding package:** 60-day pilot, bulk user import, course templates, two lecturer training sessions, AI conversion of existing notes into modules.
3. **Plans:** Starter, Growth, Enterprise by active learners (section 5); add-ons for proctoring, AI tutor, extra storage, white-label app.
4. **Marketplace bridge:** institutions can list selected courses publicly (earning from external learners) and receive sponsored cohorts — something standalone LMS vendors can't offer.
5. **Support:** in-hours chat and email in WAT; named success manager on Growth and above; 99.9% uptime SLA on Enterprise.
6. **Exit:** full data export (users, grades, content, certificates) in open formats — institutions stay because the product is good, not because data is locked in.

---

## 13. Tutor marketplace requirements

Tutor quality is the product, so Plastoids vets before listing, ranks by learner outcomes rather than by price, and pays tutors fast and predictably enough that they keep their students on the platform.

### Onboarding and verification (MVP)

| Stage | Check | Who / how | Turnaround target |
|---|---|---|---|
| 1. Identity | NIN and BVN match, liveness selfie | Licensed KYC provider (e.g. Smile ID, Dojah, Prembly — pick on price and NIN coverage) | Minutes |
| 2. Qualifications | Degree/certificates uploaded; professional certificates checked on issuer sites where possible; LinkedIn and work history | Plastoids reviewer, sampling with issuer confirmation | 3 working days |
| 3. Subject competence | 5-minute demo lesson + short subject test | Subject reviewer panel (paid per review) | 5 working days |
| 4. Safeguarding | Code of conduct signed; for any tutor teaching minors (Phase 3): enhanced checks and training | Plastoids | Before first minor booking |
| 5. Payout account | Bank account name must match verified identity | Gateway account-resolve API | Instant |

**Levels:** New (verified, limited visibility) → Rising (10 sessions or 20 enrolments, rating ≥ 4.5) → Top (sustained quality, eligible for sponsored programmes and featured slots).

### Matching and discovery engine

Score for each tutor–learner pair = filters, then a weighted rank:

```
score = 0.30·R_subject + 0.20·Q_outcome + 0.15·A_availability + 0.10·P_price_fit
      + 0.10·F_format + 0.05·L_language + 0.10·E_exploration
```

- **Hard filters:** subject/skill, format (1:1, group, course), language, budget ceiling, time zone, verification level, and — for minors — safeguarding-cleared tutors only.
- **R subject relevance:** skill tags and embedding similarity between the learner's goal and the tutor's courses.
- **Q outcome quality:** completion rate, assessment improvement of their learners, rebooking rate, rating (Bayesian-smoothed so new tutors aren't punished by two reviews).
- **E exploration:** a guaranteed share of impressions for new verified tutors so supply can grow.
- The weights start as above and are tuned by A/B tests on booking and completion, not set by commercial deals.

### Quality monitoring

- **Automatic signals:** ratings, no-shows, late cancellations, refund rate, complaint rate, learner completion and score gains, response time.
- **Monthly session sampling:** reviewers watch recorded sessions against a rubric (preparation, clarity, accuracy, engagement, conduct).
- **Thresholds trigger actions:** coaching → reduced visibility → suspension → removal. Each step is logged and appealable.

### Disputes

| Dispute | First response | Resolution rule | Funds |
|---|---|---|---|
| Tutor no-show | Automatic on attendance data | Full refund; strike to tutor | Earning reversed before payout |
| Learner says session was poor | Learner files within 48 h with reason | Reviewer checks recording; partial or full refund | Earning held during review (max 5 working days) |
| Course not as described | Within 7 days and under 30% progress | Refund if review agrees | Reversal from tutor balance or future earnings |
| Off-platform payment solicitation | Flag from message scanning or report | Warning, then removal | Not a platform transaction; learner warned |
| Content IP complaint | Takedown request with evidence | Course hidden pending review; counter-notice allowed | Earnings on disputed course held |

### Tutor economics (why they stay)

A tutor at ₦8,000/hour teaching 40 hours a month grosses ₦320,000; at 15% commission they keep ₦272,000, paid weekly, with no chasing. Off-platform they would keep 100% but find, schedule and collect themselves. The platform must keep that trade worth it: steady learner flow, sponsored-programme work, AI preparation tools and Pro commission discounts.

---

## 14. UI/UX design specification

The interface follows the wordmark: navy for structure and text, royal blue for actions, the blue-to-teal gradient used sparingly for the brand and for progress, and teal for AI and positive outcomes — on white, with generous spacing, so it reads premium on a laptop and fast on a ₦100k Android phone. Mockups of the homepage and the student, tutor and sponsor dashboards are on the design canvas alongside the logo.

### Design system

| Token | Value | Use |
|---|---|---|
| Navy | `#003B7A` | Headings, body text on light, nav, primary surfaces in dark areas |
| Royal Blue | `#0866C6` | Primary buttons, links, active states |
| Cyan | `#00B8D9` | Info, charts, focus rings |
| Teal | `#11CDB5` | AI features, success, sponsored badges, progress |
| Gradient | `#003B7A` → `#0866C6` → `#00B8D9` → `#11CDB5` | Logo "P", hero accents, progress bars only — never behind body text |
| Neutrals | `#F4F7FB` surface, `#D5E0EC` borders, `#5B6B7F` secondary text (5.3:1 on white) | Cards, dividers, captions |
| Type | Nunito (display, 700–800) + Inter or Source Sans 3 (UI text, 400–600) | Rounded, friendly display; highly legible UI text |
| Radius | 8 px inputs, 12 px cards, 999 px chips | Soft, consistent |
| Spacing | 4 px base; 8/12/16/24/32/48 steps | Layout rhythm |
| Components | Tailwind + shadcn/ui primitives themed with these tokens | Speed and accessibility |

**Rules:** text contrast ≥ 4.5:1; touch targets ≥ 44 px; status never shown by colour alone (icon + label); dark mode in Phase 2.

### Responsive layout

- **Mobile (≤ 640 px):** bottom tab bar (Home, Learn, Explore, AI, Profile for learners; Home, Classes, Students, Earnings, Profile for tutors); single-column cards; sticky primary action.
- **Tablet (641–1024 px):** collapsible side navigation; two-column dashboards.
- **Desktop (> 1024 px):** fixed left navigation, 12-column grid, max content width 1280 px; dashboards in three columns.

### Core screens

| Screen | Key components | Primary actions | Empty state | Validation and error states |
|---|---|---|---|---|
| Homepage (public) | Hero with search, sponsored opportunities strip, categories, top courses, how it works (learners · tutors · sponsors), trust logos, impact numbers, footer | Search, Apply for sponsored seat, Become a tutor, Partner as sponsor | n/a | Search with no results → suggestions + "request a course" |
| Student dashboard | Greeting + goal, sponsored opportunities you qualify for, continue learning, upcoming classes, progress ring per course, AI tutor quick-ask, application tracker | Resume lesson, Join class, Apply, Ask AI | New learner: 3-step setup (goal, interests, level) and starter recommendations | Missed deadline banners; offline indicator; payment failed banner |
| Course details | Title, outcomes, syllabus, tutor card, format and schedule, price or "Sponsored seats" panel, reviews, FAQs, compare | Enrol / Apply / Add to compare | No reviews → "New course" badge and tutor rating instead | Price changed, seats full → join waitlist |
| Sponsored application | Eligibility checklist, form (≤ 8 fields), document upload, entry assessment, consent | Check eligibility, Submit, Start assessment | — | Inline field validation; save-and-resume; upload size/format errors; ineligible → reason + alternatives |
| Tutor discovery & booking | Filters, tutor cards (photo, rating, price, next slot, verified badge), profile with demo video, calendar picker, package options, checkout | Book, Message, Save | No tutors match → relax-filter suggestions | Slot taken during checkout → alternative slots |
| AI learning assistant | Chat with course context chip, citations to lessons, hint/answer mode, practice generator, voice input | Ask, Practise, Summarise lesson | Suggested starter prompts per lesson | Quota reached → Plus upsell (never on sponsored seats); moderation refusal with reason |
| Virtual classroom | Video grid, screen share, chat, raise hand, polls, breakout rooms, low-bandwidth toggle | Join, Mute, Share, Leave | Waiting room before start | Poor connection → auto audio-only; reconnecting overlay |
| Tutor dashboard | Today's sessions, earnings card (this month, pending, next payout), bookings requests, student alerts, course performance, AI assistant | Start session, Accept booking, Create course | New tutor: verification progress tracker | Payout account mismatch banner |
| Lesson builder | Module tree, drag-drop lessons, video uploader with progress, quiz editor, AI draft panel, preview as learner | Add lesson, Generate quiz, Publish for review | Template picker | Upload failed/resume; missing required fields block publish |
| Sponsor dashboard | Programme cards with seats/funded/filled, funnel (applied → accepted → active → completed → certified), budget vs spend, alerts, report downloads | Create campaign, Review applicants, Download report | First-time: guided campaign builder | Invoice overdue banner; campaign blocked until funded |
| Create campaign | Stepper: objectives → course → seats & budget → eligibility → dates → selection mode → review & quote | Next, Save draft, Request quote | — | Budget below seat cost; eligibility rule flagged for review |
| Back-office executive dashboard | GMV, net revenue, active learners/tutors/sponsors, programme health, payout queue, risk alerts | Drill into any metric | — | Data-freshness timestamp; stale feed warning |

### Remaining screens (same patterns)

- **Public:** marketplace, sponsored opportunities, find a tutor, become a tutor, become a sponsor, for institutions, AI features, pricing, about.
- **Learner:** course discovery, learning dashboard, assessments, certificates, profile and billing.
- **Tutor:** profile management, course management, schedules, student management, earnings.
- **Sponsor:** manage applications, approved beneficiaries, programme performance, budget management, impact analytics.
- **Back office:** students and tutors, programmes, course moderation, finance, facilities, compliance, audit logs, configuration.

---

## 15. Technical system architecture

Build a modular monolith in ASP.NET Core with strict module boundaries, one PostgreSQL database with a schema per module, and asynchronous integration through an outbox and a message bus — then extract modules into services only when a measured need appears (independent scaling, a separate team, or isolation for compliance).

### Modular monolith vs microservices

| Criterion | Modular monolith | Microservices |
|---|---|---|
| Cost to run at MVP | One app service + one database: lowest | Many services, gateways, per-service databases: 2–4× infra and DevOps time |
| Build speed with 6–10 engineers | Fast: one deploy, one debugger, refactors are cheap | Slow: contracts, versioning, distributed debugging |
| Reliability | One failure domain, but simpler; scale out horizontally behind Front Door | Fault isolation, but more moving parts to fail |
| Money correctness | Ledger postings in one transaction with the business change | Needs sagas and compensations for every money flow |
| Future scale | Modules already isolated by schema and interfaces; extract when needed | Ready from day one, paid for before it's needed |
| **Verdict** | **Choose for MVP–Phase 3** | Extract likely candidates later: video/live-session orchestration, AI gateway, search, notifications |

Each module owns its tables and talks to others only through in-process interfaces or published events; the AI gateway and outside services are reached only from inside the API, never from the apps directly.

### Stack

| Layer | Choice | Notes |
|---|---|---|
| API | ASP.NET Core (.NET 9 LTS successor when available), C#, REST + OpenAPI | Clean Architecture per module (Domain, Application, Infrastructure, API); MediatR-style CQRS where reads and writes diverge (catalogue, analytics, ledger) |
| Persistence | PostgreSQL 16 (Azure Flexible Server) with EF Core; pgvector for embeddings | Schema per module; read replicas for reporting from Phase 2 |
| Cache and queues | Redis; Azure Service Bus with transactional outbox; Hangfire for scheduled jobs | Idempotent consumers |
| Search | PostgreSQL full-text at MVP → Azure AI Search or OpenSearch when catalogue > ~20,000 items | |
| Web | Next.js (App Router), React, TypeScript, Tailwind, shadcn/ui | SSR for public pages and SEO |
| Mobile | Flutter (Android first, iOS Phase 3) | One codebase; strong offline storage and video plugins |
| Video | Hosted video pipeline (e.g. Mux or Azure-based transcoding) with HLS adaptive streaming; live via a video SDK | Buy, not build |
| Identity | OpenIddict (OIDC) in the Identity module; phone OTP, email, Google sign-in; MFA for tutors, sponsors and staff | Institutions federate via SAML/OIDC in Phase 3 |
| Infra | Azure (South Africa North region for latency and residency options), containers on Azure Container Apps, Front Door, Key Vault, Blob Storage | IaC with Bicep or Terraform |
| CI/CD | GitHub Actions or Azure DevOps: build, test, scan, deploy to staging, manual approval to production | Blue-green deploys, database migrations gated |
| Observability | OpenTelemetry → Azure Monitor / Application Insights; structured logs; alerting on SLOs | Business alerts too: payout failures, webhook backlog |

### Multi-tenancy strategy

- **Marketplace (Plastoids as tenant zero):** all marketplace learners, tutors and sponsors share the default tenant; sponsor data is isolated by organisation ID with row-level security policies in PostgreSQL.
- **Institutions (Phase 3):** shared database with tenant ID on every tenant-scoped table and PostgreSQL row-level security enforced from a per-request session setting — cheapest for the long tail of small institutions.
- **Large or regulated tenants:** dedicated database (same code, separate connection string) when a contract requires it; white-label tenants can additionally get a dedicated deployment.
- **Cross-tenant identity:** one global user identity; memberships are per tenant, so a learner can belong to an institution and the marketplace without two accounts.

### Hosting and deployment

Environments: dev → staging (production-like, anonymised data) → production. Production runs at least two API replicas across availability zones, PostgreSQL with zone-redundant high availability and point-in-time restore (35 days), geo-redundant backups to a second region, and Front Door caching of static and video assets. Target recovery: RPO ≤ 15 minutes, RTO ≤ 4 hours (tighten in Phase 3).

---

## 16. Database design

One PostgreSQL database with a schema per module; every table carries `id` (UUID v7, time-ordered), `tenant_id`, `created_at`, `updated_at` and `row_version`, and money is stored as integer kobo with a currency code — never floating point.

### Core entities by schema

| Schema | Entity | Key fields | Relationships |
|---|---|---|---|
| identity | User | phone (unique), email, password_hash, mfa_enabled, status, dob, nin_verified_at | 1–n Membership, 1–1 LearnerProfile / TutorProfile |
| identity | Organisation | type (sponsor, institution, employer, hub), cac_number, verification_status | 1–n Membership |
| identity | Membership | user_id, organisation_id or tenant_id, role | Joins users to organisations and tenants |
| learner | LearnerProfile | goals, interests[], level, location_state, consent flags | 1–n Enrolment, Application, PortfolioItem |
| tutor | TutorProfile | headline, bio, subjects[], level (new/rising/top), hourly_rate_kobo, quality_score | 1–n Credential, Availability, Course (as owner) |
| tutor | Credential / VerificationCheck | type, issuer, evidence_uri, result, checked_by | n–1 TutorProfile |
| catalogue | Course | owner_type (tutor, tenant, plastoids), title, slug, level, language, status | 1–n CourseVersion, Offering |
| catalogue | CourseVersion | version_no, published_at, outline_json | 1–n Module → Lesson |
| catalogue | Offering | course_id, format (self-paced, cohort, 1:1), price_kobo, funding_types[], capacity, start_at | 1–n Enrolment, Cohort |
| lms | Module, Lesson | order, type (video, doc, quiz, assignment, live), media_asset_id | Lesson 1–n Progress |
| lms | Enrolment | learner_id, offering_id, cohort_id, funding_source_id, status, progress_pct, completed_at | n–1 Offering; 1–1 Seat (if sponsored) |
| lms | Assessment, Attempt, Grade | max_score, pass_mark, proctoring; score, graded_by (human/auto), feedback | Attempt n–1 Enrolment |
| lms | LiveSession, AttendanceEvent | room_ref, starts_at; joined_at, left_at, duration | n–1 Cohort |
| booking | Booking | tutor_id, learner_id, slot_start, slot_end, status, payment_intent_id | 1–1 LiveSession |
| sponsorship | Campaign / Programme | sponsor_org_id, offering_id, seats_funded, budget_kobo, selection_mode, status | 1–n EligibilityRule, Seat, FundingTranche |
| sponsorship | Seat | programme_id, enrolment_id, status (open, offered, filled, released) | 1–1 Enrolment |
| sponsorship | FundingSource | type (self, sponsor, employer, tenant, split), shares_json | Referenced by Enrolment |
| applications | Application, Answer, Score, Decision | applicant_id, programme_id, status; rubric scores; decided_by, reason | Decision is append-only |
| payments | PaymentIntent, Transaction | payer_id, amount_kobo, gateway, gateway_ref (unique), status | 1–n Posting |
| payments | Invoice, InvoiceLine | org_id, lines (tuition, management fee, seat fee, VAT), due_at | n–1 Programme |
| ledger | Account | owner_type, owner_id, kind (asset, liability, revenue, expense), code | 1–n Posting |
| ledger | JournalEntry, Posting | entry_id, account_id, debit_kobo, credit_kobo, reference | Postings sum to zero per entry; append-only |
| payouts | PayoutBatch, Payout, Hold | batch status, approved_by; amount, bank_ref; reason | Payout n–1 TutorProfile |
| certs | Certificate | public_code (unique), enrolment_id, issued_at, revoked_at, hash | n–1 Enrolment |
| facilities | Hub, Room, Reservation, CheckIn | capacity, equipment[]; slot; scan_at | Phase 3 |
| ai | AiInteraction, UsageMeter, KnowledgeChunk | capability, tokens_in/out, cost_kobo; embedding vector(1536) | Meter rolls up per user, course, sponsor |
| audit | AuditEvent | actor_id, action, entity, before_json, after_json, ip | Append-only, separate retention |

### Rules the schema enforces

- Ledger postings are insert-only; corrections are reversing entries. A database constraint plus a nightly job verify each journal entry balances.
- `gateway_ref` and every inbound webhook ID are unique, so retries can't double-post.
- Personal data columns (NIN, DOB, phone) are encrypted at column level with keys in Key Vault; analytics reads a pseudonymised replica.
- Soft delete for user-facing records; hard delete for personal data on erasure requests, leaving anonymised learning and ledger records.

---

## 17. API and integration specification

The platform exposes one versioned REST API (`/api/v1`) described in OpenAPI 3.1, authenticated with OIDC bearer tokens; every write that moves money or changes a decision requires an `Idempotency-Key` header, and every outbound integration sits behind an adapter interface so a provider can be swapped without touching business logic.

### Conventions

- JSON, camelCase, ISO 8601 UTC times, money as `{ "amount": 4500000, "currency": "NGN" }` in kobo.
- Cursor pagination (`?cursor=…&limit=50`), filtering by query parameters, ETag/If-Match for optimistic concurrency.
- Errors as RFC 9457 problem details with a stable code.
- Rate limits per token and per IP at Front Door and in the API; stricter on auth and AI endpoints.
- Webhooks out (for sponsors and institutions in Phase 3) signed with HMAC-SHA256 and retried with backoff.

### Core endpoints (MVP)

| Area | Method and path | Purpose |
|---|---|---|
| Auth | `POST /auth/otp/request`, `POST /auth/otp/verify`, `POST /auth/token` | Phone OTP sign-in, token issue/refresh |
| Profile | `GET/PATCH /me`, `PUT /me/goals`, `POST /me/data-export`, `DELETE /me` | Profile, goals, NDPA rights |
| Catalogue | `GET /courses?q=&skill=&funding=sponsored`, `GET /courses/{slug}`, `GET /courses/compare?ids=` | Search, detail, compare |
| Opportunities | `GET /opportunities`, `GET /opportunities/{id}/eligibility` | Sponsored listings, instant eligibility check |
| Applications | `POST /opportunities/{id}/applications`, `GET /me/applications`, `POST /applications/{id}/accept-offer` | Apply, track, accept |
| Enrolment | `POST /offerings/{id}/enrolments`, `GET /me/enrolments`, `PUT /enrolments/{id}/progress` | Enrol, list, sync progress (batched for offline) |
| Assessments | `POST /assessments/{id}/attempts`, `PUT /attempts/{id}/answers`, `POST /attempts/{id}/submit` | Take quizzes and exams |
| Tutors | `GET /tutors?skill=&maxRate=&lang=`, `GET /tutors/{id}/availability` | Discovery and slots |
| Bookings | `POST /bookings`, `POST /bookings/{id}/cancel`, `POST /bookings/{id}/reschedule` | 1:1 and group bookings |
| Live | `POST /live-sessions/{id}/join-token` | Short-lived video SDK token |
| Payments | `POST /payment-intents`, `POST /webhooks/{gateway}` | Checkout, gateway callbacks (signature-verified) |
| Tutor | `POST /tutor/courses`, `POST /tutor/courses/{id}/submit-review`, `GET /tutor/earnings`, `PUT /tutor/payout-account` | Course authoring, earnings, payouts |
| Sponsor | `POST /sponsor/campaigns`, `POST /campaigns/{id}/quote`, `GET /campaigns/{id}/funnel`, `POST /campaigns/{id}/decisions`, `GET /campaigns/{id}/reports/impact` | Campaign lifecycle and reporting |
| AI (P2) | `POST /ai/tutor/messages` (streaming SSE), `POST /ai/practice-sets`, `GET /me/ai/usage` | Tutor chat, practice generation, quotas |
| Certificates | `GET /verify/{publicCode}` (public, no auth) | Certificate verification |
| Admin | `/admin/*` — verification queues, moderation, payouts, refunds, configuration | Staff only, MFA + IP allow-list |

### Integrations

| Capability | Primary candidates (verify pricing and terms) | Pattern | Phase |
|---|---|---|---|
| Payment collection | Paystack (primary), Flutterwave (fallback) | Hosted checkout + webhooks; dedicated virtual accounts for sponsor invoices | MVP |
| Payouts | Gateway transfers API; partner bank bulk payments as fallback | Batch create → four-eyes approval → submit → status webhooks | MVP |
| KYC | Smile ID, Dojah, Prembly (NIN, BVN, liveness) | Synchronous check, result stored, raw data minimised | MVP |
| Video on demand | Mux or Azure Media alternatives (Azure Media Services was retired) | Direct upload → transcode → signed HLS URLs | MVP |
| Live classes | Daily, 100ms, Zoom Video SDK, or LiveKit (self-hostable) | Server-issued room tokens; webhooks for attendance | MVP |
| Messaging | WhatsApp Business Platform via a BSP, Termii or Africa's Talking for SMS, SendGrid or Amazon SES for email, Firebase Cloud Messaging for push | Notification module fans out by preference | MVP |
| Calendar | Google Calendar, Microsoft Graph | OAuth, two-way sync of tutor availability | MVP |
| LLMs | Anthropic, OpenAI (two providers behind the AI gateway) | Server-side only; streaming | P2 |
| Proctoring | Browser lockdown + webcam sampling vendor | Launch link + result webhook | P2 |
| Certificates | Own verification page; LinkedIn add-to-profile; Open Badges 3.0 | Signed credential JSON | P2–P3 |
| Institutions | SAML/OIDC SSO, LTI 1.3, SCORM/xAPI, CSV/REST SIS sync | Standards-based | P3 |
| Accounting | Export to Sage, QuickBooks or Zoho Books | Daily journal export | MVP |

---

## 18. Payments, settlement and accounting

Money owed to tutors, sponsors and learners sits in a segregated client-funds bank account and is never used for Plastoids' own costs; Plastoids' ledger records who is owed what, and a daily reconciliation proves the bank holds at least that much.

### Ledger balances vs bank-held funds

A tutor's "balance" or a sponsor's "programme funds" on screen is a liability in Plastoids' ledger — a record of what Plastoids owes. The asset behind it is cash in the client-funds account at the partner bank (plus settlements in transit from the gateway). The control that matters:

```
client bank balance + gateway in transit
  ≥ tutor payable + programme funds held + refund credits + hub payable
```

The reconciliation job runs daily (ledger vs gateway report vs bank statement); any shortfall pages finance and freezes payouts until explained. Plastoids' own revenue is swept from client funds to the operating account only after it is earned.

### Chart of accounts (extract)

| Code | Account | Type |
|---|---|---|
| 1010 | Bank — operating | Asset |
| 1020 | Bank — client funds (segregated) | Asset |
| 1030 | Gateway receivable (in transit) | Asset |
| 2010 | Tutor payable (sub-ledger per tutor) | Liability |
| 2020 | Sponsor programme funds held (sub-ledger per programme) | Liability |
| 2030 | Learner refund credit | Liability |
| 2040 | VAT payable | Liability |
| 4010 / 4020 | Course commission / tutoring commission | Revenue |
| 4030 / 4040 | Programme management fee / platform seat fee | Revenue |
| 4050 | Subscriptions (Plus, Pro, LMS) | Revenue |
| 5010 | Payment processing and transfer fees | Expense |

### Illustrative entries (₦; VAT shown only where noted)

| # | Event | Debit | Credit | Amount |
|---|---|---|---|---|
| 1 | Learner buys a ₦45,000 course by card | 1030 Gateway receivable | 2010 Tutor payable 36,000 · 4010 Course commission 9,000 | 45,000 |
| 2 | Gateway settles next day, net of its fee | 1020 Client funds 44,225 · 5010 Fees 775 | 1030 Gateway receivable | 45,000 |
| 3 | Earned commission swept to operating | 1010 Operating | 1020 Client funds | 8,225 |
| 4 | Tutor paid after the 7-day refund window | 2010 Tutor payable | 1020 Client funds | 36,000 |
| 5 | Refund within the window (instead of 4) | 2010 Tutor payable 36,000 · 4010 Course commission 9,000 | 1020 Client funds | 45,000 |
| 6 | Sponsor pays invoice: tuition 75m + fees 15m + VAT on fees 1.125m | 1020 Client funds | 2020 Programme funds held 90,000,000 · 2040 VAT payable 1,125,000 | 91,125,000 |
| 7 | Cohort starts with 480 seats filled: seat fees earned | 2020 Programme funds held | 4040 Seat fee | 5,760,000 |
| 8 | Each programme month: management fee on 480 filled seats (480 × 18,000 ÷ 3) | 2020 Programme funds held | 4030 Management fee | 2,880,000 |
| 9 | Each programme month: tutors earn for sessions delivered (480 × 150,000 ÷ 3) | 2020 Programme funds held | 2010 Tutor payable | 24,000,000 |
| 10 | Close-out: 20 unfilled seats refunded to sponsor, 20 × (150,000 + 18,000 + 12,000) | 2020 Programme funds held | 1020 Client funds | 3,600,000 |

Entries 7 and 8 are followed by a sweep like entry 3. VAT treatment of pass-through tuition versus Plastoids' fees, and withholding tax on tutor payments, must be confirmed with a tax adviser before launch.

### Fee calculation rules

```
tutor net = gross × (1 − commission rate) − refunds − chargebacks
```

- Commission rate is resolved at sale time from the fee rules (source of learner, tutor plan, promotion) and stored on the transaction, so later rule changes never alter past earnings.
- Gateway fees are borne by Plastoids on learner payments (priced into commission); payout transfer fees are borne by Plastoids.
- Coupons funded by Plastoids reduce Plastoids' commission, not the tutor's earnings; tutor-funded coupons reduce gross.

### Settlement rules

| Earning type | Hold before payable | Payout |
|---|---|---|
| 1:1 or group session | 72 hours after the session (dispute window) | Weekly run, Thursdays |
| Self-paced course sale | 7 days (refund window) | Weekly run |
| Sponsored programme teaching | Per delivered session, confirmed by attendance data | Bi-weekly in arrears |
| Partner hub bookings | After check-in data confirms use | Monthly |
| Any earning under dispute | Until resolved (max 5 working days for review) | Next run after release |

Minimum payout ₦5,000; payouts only to an account whose name matches the verified identity; batches need maker–checker approval; failed transfers return to the tutor's balance with a notice.

### Safeguards on holding funds

- Segregated client-funds account with the partner bank, ideally under a trust or escrow letter so the funds are protected if Plastoids becomes insolvent (legal advice needed).
- Plastoids does not offer stored value: no top-ups, no peer transfers, no cash-out of refund credit except to the original payment method. That keeps it outside e-money rules; if that ever changes, a licensed partner holds the wallet.
- Sponsor funds for large programmes can be released in tranches (enrolment, mid-point, completion) so less money is ever held.
- Monthly external review of the reconciliation in Year 1; annual audit with a client-funds attestation.

---

## 19. Security and governance model

Plastoids launches adult-only, registers with the Nigeria Data Protection Commission as a data controller, and treats three things as the crown jewels — learner personal data, money movements and assessment integrity — each with its own controls and audit trail.

### Roles and permissions

| Role | Scope | Can | Cannot |
|---|---|---|---|
| Learner | Own records | Learn, apply, pay, review, export/delete own data | See other learners' data |
| Tutor | Own courses, own students | Author, teach, grade, message own students, see own earnings | See learners' contact details beyond the platform; access other tutors' data |
| Sponsor owner / finance / reviewer / viewer | Own organisation's programmes | Create campaigns, pay, review applicants, see beneficiary progress (consented fields) | See learners outside their programmes; change grades |
| Institution admin / lecturer / student | Own tenant | Manage tenant users, courses, grades | Cross-tenant access |
| Hub manager | Own hub | Manage rooms, see bookings, confirm check-ins | See learning data |
| Staff: support, programme manager, reviewer, finance maker, finance checker, compliance, admin | By function | As named; finance checker approves what a maker prepares | Any one role both create and approve payouts or role grants |

Permissions are policy-based (role + resource ownership + tenant), checked in the API, with PostgreSQL row-level security as a second line for tenant and organisation data.

### Controls

| Area | Control |
|---|---|
| Authentication | Phone OTP + password or passkey; MFA mandatory for tutors, sponsors, institution admins and all staff; staff access only via SSO + hardware-key MFA + IP allow-list |
| Verification | Tutors: NIN/BVN + liveness + credentials (section 13). Sponsors: CAC, authorised signatory, beneficial owner check above a funding threshold |
| Encryption | TLS 1.2+ everywhere; AES-256 at rest; column-level encryption for NIN, DOB, phone; keys in Key Vault with rotation |
| Payments | No card data touches Plastoids (gateway-hosted checkout keeps PCI scope minimal); webhook signatures verified; idempotency; maker–checker payouts |
| Privacy and consent | NDPA 2023 lawful bases recorded per purpose; granular consent (marketing, sharing profile with sponsor's hiring partners); data-subject rights in-app; DPIA before launch and before minors; DPO appointed; data processing agreements with every vendor |
| AI data protection | No provider training on Plastoids data; prompt/response logs redacted and kept 30 days; retrieval scoped to the user's entitlements; sponsor analytics aggregated with minimum cell size of 10 |
| Content moderation | Automated screening of uploads, reviews and messages; human review queue; takedown SLA 24 h (2 h for safeguarding) |
| Fraud prevention | One account per NIN for sponsored applications; device and IP signals; velocity limits on sign-ups and payments; review fraud detection; tutor–learner collusion checks (fake bookings to farm sponsor funds) |
| Assessment integrity | Randomised question banks, timed attempts, proctoring on certified exams, plagiarism and AI-text checks as signals (never sole evidence) |
| Audit | Immutable audit log of admin actions, money movements, grade changes and selection decisions; retained 7 years for financial records |
| Availability | Backups with point-in-time restore; quarterly restore drills; DR runbook; RPO 15 min / RTO 4 h |
| Secure development | Threat modelling per module, SAST/DAST and dependency scanning in CI, annual penetration test, bug bounty from Phase 2 |

### Child safeguarding (before any learner under 18 is admitted)

- Age gate at sign-up; under-18 accounts only through a verified parent or guardian, who sees activity and approves bookings.
- Only safeguarding-cleared tutors can teach minors; no private 1:1 messages between adults and minors — messages go through the guardian-visible thread.
- All sessions with minors recorded and retained for review; no recording disabled by the tutor.
- Reporting button in every session and message; designated safeguarding lead; escalation to authorities under a written policy.
- No AI career profiling or sponsor data sharing for minors; stricter AI tutor policies.

### Retention

| Data | Retention |
|---|---|
| Financial records and ledger | 7 years |
| Certificates and verification records | Indefinitely (the learner can hide but not alter) |
| Learning records | Life of account + 2 years, then anonymised |
| Session recordings | 12 months (minors: 24 months) unless a dispute holds them |
| AI conversation logs | 30 days redacted for operations; learner-visible history until they delete it |
| Rejected applications | 12 months, then anonymised |

### Intellectual property

- Tutors own the courses they create and grant Plastoids a non-exclusive licence to host, sell and promote them while listed (exclusive licences available for higher revenue share).
- Sponsor-commissioned custom content: ownership set in the programme agreement — default is sponsor owns, Plastoids keeps a licence to deliver it to that sponsor's cohorts.
- Learner work belongs to learners; portfolio sharing is opt-in.
- Plastoids owns the platform, AI prompts and aggregate, anonymised analytics.

### Regulatory checklist

Nigeria first: NDPA 2023 and NDPC registration/audits; CBN rules as they apply through the payment gateway and partner bank; FIRS VAT and withholding tax; consumer protection (FCCPA) for refunds and pricing transparency. International expansion adds GDPR (EU learners or partners), Kenya and Ghana data protection acts, and the US COPPA/FERPA only if those markets are entered.

---

## 20. Three-year financial projections

On the model's assumptions, Plastoids needs about ₦1.16bn of funding in the base case (peak cash need ₦931m plus a 25% buffer) and is still losing ₦19m a month at month 36; the aggressive case turns EBITDA-positive from month 30 and needs ₦808m. Every number here is an illustrative output of stated assumptions, not market data — the full model is in `Plastoids_Financial_Model.xlsx`.

The gap between the base and aggressive lines is almost entirely sponsor volume and paid-learner growth; staff cost is the same plan scaled ±15–30%.

### Results by scenario

| Scenario | Net revenue Y1 (₦m) | Y2 | Y3 | Gross margin Y3 | EBITDA Y3 (₦m) | Funding to raise (₦m) | EBITDA break-even |
|---|---|---|---|---|---|---|---|
| Conservative | 21.8 | 145.4 | 276.4 | 58% | (435.0) | 1,393 | Beyond month 36; still −₦35m/month |
| Base | 39.5 | 333.6 | 739.1 | 57% | (287.5) | 1,164 | Beyond month 36; −₦19m/month at month 36 |
| Aggressive | 73.2 | 695.5 | 1,811.3 | 55% | 48.6 | 808 | Month 30, sustained |

In the base case, about 3 more sponsored programmes a month in Year 3 (roughly 70 a year instead of 32, or ~14,000 seats) would close the month-36 gap — the model computes this on the Summary sheet. That is the single most important number to test with sponsors.

### Key assumptions (base case; all editable)

| Driver | Base value | Why |
|---|---|---|
| Launch | Month 7, after a 6-month build | MVP scope in section 26 |
| Sponsored programmes started | 4 in Y1, 16 in Y2, 32 in Y3; 200 seats each | Two anchor sponsors in Y1, then repeat and referral business |
| Sponsor fees | 12% management fee + ₦12,000 seat fee on ₦150,000 tuition = ₦30,000 per seat | Section 5 |
| Applicants per seat | 10 register | Sponsored calls are oversubscribed; they seed the free/paid base |
| Paid course enrolments | 250 in month 7, growing 20%/8%/5% a month in Y1/Y2/Y3; ₦35,000 average; 17% blended commission | Conversion from the applicant pool and organic sign-ups |
| Corporate training | From month 13: 60 seats, +6%/month, ₦100,000 per seat, 65% delivery cost | Upsell to sponsors' HR teams |
| LMS-as-a-Service | From month 19: one institution every two months, ₦350,000/month + ₦1.5m setup | Phase 3 |
| AI cost | ₦300 → ₦260 per active learner per month | Quotas, caching, cheap-model routing |
| Payment processing | 1.6% on learner payments; 0.1% on invoiced bank transfers | Paystack Nigerian pricing |
| Staff | ₦17m/month (Phase 1) → ₦23m → ₦30m → ₦38m (Phase 4) | ~10 → 28 people at ~₦1.35m average fully loaded |
| Marketing | ₦4m/month in Y1 after launch, ₦8m Y2, ₦13m Y3 | Ambassadors, content, sponsor co-marketing |
| G&A | ₦3.5m → ₦6m → ₦8m per month | Rent, legal, audit, insurance, tools |

### Cost picture (base case)

- **Start-up (Phase 1, months 1–6):** ₦148m — mostly the build team (₦102m), G&A, a ₦15m one-off for incorporation, legal, brand and security review, and seeding 60 tutors.
- **Monthly infrastructure:** ₦1.2m fixed plus about ₦60 per active learner for hosting and video; AI adds ₦260–300 per active learner — ₦119m in Year 3, the largest variable cost after corporate delivery.
- **Gross margin** settles at 55–58% because corporate training is resold at a 35% margin; platform fee and commission streams alone run above 85%.

### What the model says commercially

1. **Sponsor fees are the best business:** near-pure margin, low acquisition cost per learner, and they create the learner base. Every extra programme adds ₦6m of fee revenue in the base case.
2. **Course commissions become the largest stream by Year 3** (₦156m base) because the applicant pool converts — but they depend on sponsored calls to feed them.
3. **Corporate training is big but thin:** ₦204m revenue in Y3 base at 35% margin. Worth doing for sponsor retention, not for profit.
4. **LMS-as-a-Service and white-label are small in three years** (₦35m and ₦23m in Y3 base). They are strategic options, not the plan's engine — don't build them before Gate 3.
5. **Raise in two steps:** a seed round sized to reach Gate 2 (about ₦450–500m covers months 1–15 in every scenario), then a larger round on proven completion and renewal data.

---

## 21. Product roadmap

The build runs in four phases over 36 months, and each phase is released only when the gate before it passes — so spend follows proof, not the calendar.

Phase 1 is the only phase funded unconditionally; the gate criteria under each boundary decide whether the next phase's team is hired.

### Phase detail

| Phase | Team roles added | Dependencies | Milestones | Acceptance criteria |
|---|---|---|---|---|
| 1 · MVP (months 0–6) | Product lead, tech lead (.NET), 2 backend, 2 frontend, 1 Flutter, 1 QA/automation, 1 product designer, programme manager, sponsor sales lead | Company registration, partner bank client-funds account, gateway onboarding, KYC and video vendor contracts, NDPA registration | M2 design system + clickable prototype · M4 private beta with 50 tutors · M5 first sponsor contract · M6 public launch + first sponsored cohort | 99.5% uptime over launch month; checkout success ≥ 95%; payout run completes with zero reconciliation breaks; first sponsored cohort ≥ 150 learners enrolled; p95 page load < 3 s on 3G |
| 2 · AI and sponsor intelligence (months 7–12) | ML/AI engineer, data engineer, 1 backend, learner success lead, 2nd programme manager | Phase 1 event stream; LLM provider contracts; evaluation sets per capability | M8 AI tutor beta · M9 sponsor dashboard · M10 proctored exams + verifiable certificates · M11 Plus and tutor Pro launch · M12 first corporate cohort | AI tutor answer accuracy ≥ 90% on eval set and CSAT ≥ 4.2/5; AI cost per active learner within budget; sponsor reports generated in < 1 minute; programme completion ≥ 65% |
| 3 · Enterprise and hybrid (months 13–24) | Enterprise sales (2), solutions engineer, 2 backend, DevOps/SRE, customer success (2), partnerships manager (hubs) | Multi-tenancy hardening, SSO, SOC 2-style controls, hub agreements | M15 institution pilot · M18 LMS-as-a-Service GA · M20 10 partner hubs live · M22 first white-label · M24 SCORM/xAPI/LTI | 3 paying institutions; tenant isolation pen-test passed; 99.9% uptime; hub bookings reconciled monthly with zero disputes > 5 days |
| 4 · Regional scale (months 25–36) | Country lead (Ghana or Kenya), localisation, 2 engineers, compliance officer | Partner bank/gateway in the second country; local data rules | M27 multi-currency (NGN, GHS or KES, USD) · M30 multilingual AI tutor · M33 cross-border tutoring · M36 first regional sponsor programme | Second-country revenue ≥ 10% of total; FX reconciliation exact to the kobo/pesewa; AI tutor quality parity across languages |

Spend in the drawing is base-case operating spend per phase (team, marketing, infrastructure, AI, G&A; excludes corporate-training delivery cost) from the financial model. Conservative to aggressive: Phase 2 ₦186m–₦220m, Phase 3 ₦483m–₦867m, Phase 4 ₦665m–₦1.45bn.

---

## 22. Go-to-market strategy

Plastoids enters through sponsors, not learners: one signed sponsor brings funding, hundreds of learners, a reason for tutors to join and a public call that draws thousands of applicants — so the first sales hire sells programmes, and learner marketing rides on sponsor announcements.

### Solving cold start, in order

1. **Seed supply before launch.** Recruit 50–80 vetted tutors in the six launch skills (data analytics, software development, cloud, cybersecurity, UI/UX, product management) with guaranteed paid work on the first sponsored cohorts and zero commission for their first 90 days.
2. **Land two anchor sponsors.** Offer the first two programmes at the management fee only (seat fee waived) in exchange for a public case study and impact report. Target banks, telcos and fintechs with existing CSR or graduate-trainee budgets.
3. **Turn applicants into the marketplace.** Every sponsored call draws far more applicants than seats (planning assumption 8–15 per seat). Unsuccessful applicants get a free starter course, AI tutor trial and discounted paid courses — this is the first paid-learner market, acquired at near-zero cost.
4. **Make tutors bring their own learners.** The 8% commission on tutor-referred sales makes Plastoids cheaper than running their own payments and booking, so tutors move existing students over.
5. **Show outcomes publicly.** Publish completion and placement figures per programme (aggregated). Sponsors buy evidence; this becomes the sales deck.

### Channels by audience

| Audience | Channels | Offer | Owner |
|---|---|---|---|
| Sponsors (CSR, L&D, foundations) | Direct enterprise sales; referrals from anchor sponsors; CSR forums and HR associations; LinkedIn thought leadership; proposals to state digital-economy agencies and 3MTT-style programmes | Managed programme with impact report; pilot cohorts of 100 | Head of partnerships |
| Government and development partners | Respond to calls for training providers; consortium bids with established providers; development-partner youth-employment programmes | Platform + delivery capacity + reporting | CEO + partnerships |
| Universities and polytechnics | Career-services offices; student unions; faculty champions | Free access to sponsored opportunities for students; later LMS pilots | Campus lead |
| Learners | Campus ambassadors (stipend + per-signup bonus); NYSC CDS groups; X, TikTok, Instagram and LinkedIn short videos; WhatsApp communities; sponsor co-marketing | Sponsored seats; free starter courses; referral credit (₦2,000 off for both) | Growth lead |
| Tutors | Professional communities (data, cloud, dev); meetups; LinkedIn outreach; tutor referral bonus | Guaranteed sponsored work; 90 days commission-free; AI teaching tools | Supply lead |
| Employers (corporate training) | Account expansion from sponsors; HR and L&D associations | Cohort training with dashboards | Enterprise sales |

### Focused initial market

- **Geography:** Lagos, Abuja, Ibadan, Port Harcourt and Enugu first for physical touchpoints; online learners nationwide from day one.
- **Learners:** 21–35, graduates, final-year students, NYSC members, career switchers.
- **Course categories at launch (six):** data analytics, software development (web), cloud computing, cybersecurity fundamentals, UI/UX design, product management — the skills sponsors already fund and employers recognise.
- **Next categories (Phase 2–3):** digital marketing, AI/ML, project management certifications, financial analysis, then professional exams (ICAN, CIPM) and secondary-school exam prep only once safeguarding is in place.

### Referral and ambassador mechanics

- **Learner referral:** both get ₦2,000 credit on a paid course; capped per month; paid only after the referee's first paid enrolment (fraud control).
- **Campus ambassadors:** 1 per target campus, monthly stipend ₦25,000 plus ₦500 per verified sign-up and ₦2,000 per paid enrolment, reviewed monthly.
- **Tutor referral:** ₦10,000 when a referred tutor completes 10 paid sessions.

---

## 23. KPI framework

The one number the company runs on is **certified completions per month** — it is what sponsors pay for, what learners come for and what tutors are measured by; every other KPI below explains or protects it.

### Student

| KPI | Formula | Dashboard view | Year-1 target (assumption) |
|---|---|---|---|
| Registered learners | Count of verified learner accounts | Cumulative line | 23,000 (base model) |
| Active learners | Learners with ≥ 1 learning event in the last 28 days | Line, weekly | 3,800 in month 12 (base model) |
| Enrolment conversion | Enrolments ÷ course-page visitors (paid); offers accepted ÷ offers (sponsored) | Funnel | 3% paid; 90% offers |
| Course completion | Completed enrolments ÷ enrolments that reached their end date | Bars by course and by funding type | 65% sponsored; 35% paid self-paced |
| Engagement | Median active learning minutes per active learner per week | Line + distribution | 90 min |
| Retention | Learners active in month n who are active in month n+1 | Cohort heatmap | 55% |

### Tutor

| KPI | Formula | Dashboard view |
|---|---|---|
| Active tutors | Tutors with ≥ 1 paid session or sale in last 30 days | Line |
| Teaching hours | Sum of delivered live minutes ÷ 60 | Bars by month |
| Course sales | Paid enrolments per tutor per month | Ranked table |
| Average rating | Bayesian average: (C × m + Σ ratings) ÷ (C + n), with m = platform mean, C = 10 | Distribution |
| Tutor earnings | Net payouts per active tutor per month | Median + quartiles |
| Tutor retention | Tutors active in quarter q who are active in q+1 | Cohort heatmap |

### Sponsor

| KPI | Formula | Dashboard view |
|---|---|---|
| Active sponsors | Sponsors with a programme running or funded in the period | Count |
| Sponsorship funding | Sum of programme value invoiced (tuition + fees) | Bars by quarter |
| Sponsored beneficiaries | Filled seats | Funnel: applied → offered → enrolled → active → completed → certified |
| Programme completion | Certified ÷ enrolled seats | Bars per programme vs target line |
| Learning outcomes | Mean post-assessment − mean entry assessment (same instrument) | Paired dot plot |
| Placement (6 months) | Respondents employed or self-employed in field ÷ respondents | Bars, with response rate shown |
| Renewal rate | Sponsors who fund again within 12 months ÷ sponsors whose programme ended | Line |

### Business

| KPI | Formula |
|---|---|
| Monthly recurring revenue (MRR) | Sum of active subscriptions (Plus, Pro, LMS, white-label licences) for the month |
| Gross merchandise value (GMV) | Learner payments + sponsor tuition + corporate seats + hub bookings (before any split) |
| Net platform revenue | Commissions + fees + subscriptions + services revenue (excludes tuition passed to tutors) |
| Take rate | Net platform revenue ÷ GMV |
| Customer acquisition cost (CAC) | Sales and marketing spend in period ÷ new paying customers in period, by segment |
| Customer lifetime value (LTV) | Average monthly contribution per customer × gross margin ÷ monthly churn |
| Contribution margin | Net platform revenue − payment fees − AI − hosting/video − delivery costs − programme ops staff |
| Operating profitability | EBITDA = contribution − staff (non-ops) − marketing − G&A |

**Dashboards:** an executive view (GMV, net revenue, take rate, contribution, cash runway), a sponsor-health view (programmes, funnel, completion vs target, renewals due), and a marketplace view (supply/demand by skill, search-with-no-result terms, tutor utilisation).

---

## 24. Competitive analysis

No competitor runs the whole sponsored chain — fund, select, deliver, certify, report — on a marketplace with an AI tutor for the Nigerian market. Global players are strong at content and AI but sell to institutions and employers, not to CSR sponsors. Local players run sponsored or tech-training programmes but with no open tutor marketplace. That gap is Plastoids' position. Facts were checked on 18–19 October 2026 against the sources at the end of this section; anything not confirmed is marked unverified.

### Market changes that matter

- **Coursera and Udemy are now one company.** The all-stock deal announced on 17 December 2025 closed on 11 May 2026; Udemy is a wholly owned subsidiary and the two platforms still run separately (Business Wire, Pulse 2.0).
- **Udemy keeps squeezing instructors.** Its share of subscription revenue for instructors fell from 25% to 15% over 2024–2026 (Udemy). This leaves room to recruit tutors with fairer terms.
- **Nigerian EdTech is hard.** Edukoya shut down in 2025 and returned capital (Techpoint). Decagon moved away from income-share loans after graduates struggled to repay (LaunchBase). Both are reasons to lead with sponsor money rather than learner credit.
- **Sponsored seats are happening locally.** AltSchool launched a ₦250m scholarship for NYSC members in July 2026 (MSME Africa). This proves sponsor appetite, but it runs inside one school, not as a platform any sponsor can use.

### Comparison

| Platform | Primary model and market | Revenue model (verified) | Delivery | Tutor discovery | Sponsored learning | AI | Enterprise |
|---|---|---|---|---|---|---|---|
| Coursera | Global catalogue from universities and companies; Q2 2026 revenue $298.6m | Consumer subscriptions and degrees; enterprise licences (12,107 enterprise customers) | Self-paced, degrees | Curated partners, no open marketplace | Coursera for Campus free licences; government programmes funded by grants | Coursera Coach on 97% of courses, role play, translation | Strong |
| Udemy (Coursera subsidiary) | Open course marketplace + Udemy Business | Instructor gets 37% of marketplace sales, 15% of subscription revenue | Self-paced video | Open marketplace | Unverified | AI Assistant, skills mapping, role play | Udemy Business, cohorts |
| Preply | Global 1:1 tutoring marketplace; $1.2bn valuation (Jan 2026) | 100% of the first lesson; then 33% falling to 18% with tutor hours | Live 1:1 | Open marketplace | Unverified | Lesson Insights reports, used by 70%+ of tutors | Preply Business |
| Outschool | Kids' classes, mainly US | Teacher keeps 70%; parents pay a separate fee | Live group, 1:1, self-paced | Open marketplace | US school-choice funding planned for 2026 | Progress-report writer | Limited |
| Moodle | Open-source LMS institutions run | MoodleCloud from US$160/yr (50 users); Workplace via partners | Whatever the institution builds | None | None | AI text tools; Anthropic built in from 5.3 (Oct 2026) | Workplace: multi-tenancy, programmes |
| Andela | Talent marketplace; free learning community | Placement fees; training as a service for companies | Self-paced resources, AI Academy | Not a tutor marketplace | Free community; AI Academy for network members | AI Academy tracks | Training as a service |
| Ingressive for Good | African nonprofit, free and scholarship training | Donor and partner funded | Partner platforms (Coursera, DataCamp) | None | Core model: scholarships, e.g. 5,000 Coursera seats | Unverified | None |
| AltSchool Africa | Online tech and business school | Nano-Diplomas at ₦45,000 | Self-paced and instructor-led | Own instructors | ₦250m scholarship (Jul 2026) | Unverified | Unverified |
| uLesson / Miva | K-12 app (100,000+ active learners) + Miva Open University (30,000+ students) | Unverified | Offline video; online degrees | None | Unverified | Founder cites AI tutors | Unverified |
| Gradely | K-12 personalised learning + international tutoring | Tutoring revenue ("high six-figure" ARR, Mar 2025) | Live tutoring | Curated tutors | Unverified | AI personalisation | Unverified |
| **Plastoids** | Sponsored-first skills platform, Nigeria | Sponsor fees + 8–20% commissions + subscriptions | Live, cohort, self-paced, hybrid hubs | Open, vetted marketplace | Full chain: fund, select, deliver, certify, report | Course-grounded tutor, tutor assistant, sponsor insights | Phase 3 LMS-as-a-Service |

### Defensible advantages to build

1. **Sponsor-grade outcomes data.** Completion, score gains and placement across many programmes. Sponsors buy evidence, and it accumulates only with volume.
2. **Selection and delivery operations.** Eligibility rules, assessments, at-risk management and audit trails are operational know-how that is slow to copy, unlike features.
3. **Two-sided local network.** Vetted Nigerian tutors who earn on sponsored work, and applicants who stay as paid learners.
4. **Fairer tutor economics** than global marketplaces (Udemy's 15% subscription share, Preply's 100% first-lesson take), paid weekly in naira to local bank accounts.
5. **Built for local conditions.** Naira pricing, bank transfer and USSD, low-data and offline learning, WhatsApp — areas where global platforms are weaker.

**Not verified, so not claimed:** Coursera for Government and Business pricing; Preply's middle commission tiers; Outschool's parent fee; Moodle Workplace pricing; AltSchool's current official tuition; uLesson pricing; any sponsored-seat programmes at Udemy, Preply or Outschool.

All facts in this section come from the pages linked in the table and the bullets above, opened 18–19 October 2026. One fee is from a secondary source: Udemy's 97% instructor share on coupon sales, not used above.

---

## 25. Risks and mitigations

The two risks that could end the company are slow sponsor sales and poor completion rates; both are tested in the first nine months, before Phase 3 money is spent.

| Risk | Likelihood | Impact | Mitigation | Early signal |
|---|---|---|---|---|
| Sponsor sales cycles longer than planned (budget cycles, procurement) | High | High | Start selling in month 1; pilot cohorts of 100 that fit discretionary budgets; partner with established training providers already on approved vendor lists; bid for government/development-partner calls | Fewer than 2 signed sponsors by month 6 |
| Low completion on sponsored programmes | Medium | High | Entry assessment, commitment deposits (sponsor's choice), cohort structure, weekly live sessions, daily at-risk queue, AI tutor at night | Week-3 active rate below 80% |
| Tutors take students off-platform | High | Medium | Value only on-platform (payments, sponsored work, AI tools, reputation); low commission on tutor-referred sales; message scanning | Rebooking rate falls while tutor ratings stay high |
| Naira depreciation raises USD-priced costs (cloud, AI, video) | High | Medium | Price in naira with annual review; AI quotas and caching; open-weight models for high-volume tasks; prepay commitments when FX is favourable | AI + infra cost per active learner up > 30% in a quarter |
| AI gives wrong or harmful answers | Medium | Medium | RAG grounding with citations, evaluation sets, moderation, human escalation, clear labelling | Accuracy below 90% on eval set; complaint rate |
| Payment and payout errors or fraud | Medium | High | Double-entry ledger, daily reconciliation, maker–checker, idempotency, holds, KYC on tutors | Any unreconciled difference > ₦0 older than 24 h |
| Data breach | Low–medium | High | Encryption, least privilege, pen tests, bug bounty, NDPA incident plan with 72-hour notification | Anomalous access alerts |
| Regulatory change (payments, data, education accreditation) | Medium | Medium | Stay outside stored value; licensed partners hold funds; legal review each phase; certificates positioned as training, not academic degrees | New CBN/NDPC guidance |
| Selection seen as unfair or biased | Medium | High (sponsor brand risk) | Deterministic eligibility, rubric scoring, human decisions, bias audits, appeals, transparent criteria | Appeal rate > 5% |
| Poor connectivity and power for learners | High | Medium | Offline lessons, audio-only mode, low-data defaults, partner hubs for exams | Session drop rate by network type |
| Key-person and hiring risk (senior .NET and AI talent emigrating) | High | Medium | Competitive equity, documentation, pairing, remote-friendly hiring across Nigeria | Attrition > 20% a year |
| Competitors copy sponsored-learning features | Medium | Medium | Win on outcomes data, sponsor relationships and delivery operations — harder to copy than features | Sponsor churn to a competitor |

---

## 26. MVP backlog and acceptance criteria

The MVP is 24 stories across 9 epics, sized for a 10-person team over six months in two-week sprints; anything not on this list waits for Phase 2.

| ID | Epic | Story | Acceptance criteria | Priority |
|---|---|---|---|---|
| A1 | Identity | As a learner I sign up with my phone and an OTP | OTP delivered by SMS within 30 s in 95% of attempts; 5 attempts/hour limit; account created only after verification | Must |
| A2 | Identity | As a tutor, sponsor or staff member I must use MFA | MFA enforced on login; recovery codes issued; staff restricted to SSO | Must |
| A3 | Identity | As a learner I can export or delete my data | Export (JSON + PDF) within 24 h; deletion anonymises learning records and keeps ledger entries | Must |
| B1 | Catalogue | As a learner I search and filter courses, including "sponsored" | Results in < 500 ms p95; filters combine; zero-result page suggests alternatives | Must |
| B2 | Catalogue | As a learner I compare up to 3 courses | Side-by-side on price, duration, format, rating, outcomes; works on mobile | Should |
| C1 | Tutor | As a tutor I apply and get verified | NIN/BVN + liveness via provider; status visible; reviewer queue with SLA timers | Must |
| C2 | Tutor | As a tutor I publish a course with video lessons and a quiz | Upload resumes after drop; video playable at 240p–720p; course goes to review before going live | Must |
| C3 | Tutor | As a tutor I set availability and prices | Calendar with time zone; buffer times; Google Calendar sync | Must |
| D1 | Booking | As a learner I book and pay for a session | Slot locked 10 min at checkout; payment captured; both get confirmation + reminders 24 h and 1 h | Must |
| D2 | Booking | As a learner or tutor I cancel or reschedule | Refund rules from section 9 applied automatically; ledger reversed correctly | Must |
| E1 | LMS | As a learner I take lessons, quizzes and assignments | Progress saved per lesson; quiz auto-graded; assignment submitted with file; tutor grades with rubric | Must |
| E2 | LMS | As a learner I join a live class from my phone | Join in ≤ 2 taps without install on web; audio-only fallback; attendance recorded | Must |
| E3 | LMS | As a learner I download lessons for offline viewing | Encrypted download; plays offline; expires at enrolment end | Should |
| E4 | LMS | As a learner I receive a certificate on completion | Issued automatically on rule; public verification page by code/QR | Must |
| F1 | Sponsorship | As a sponsor I create a campaign and get a quote | Stepper saves drafts; quote shows tuition, management fee, seat fee and VAT lines | Must |
| F2 | Sponsorship | As a learner I see sponsored opportunities and check eligibility instantly | Rules evaluated in < 1 s; ineligible shows reason and alternatives | Must |
| F3 | Applications | As a learner I apply and take the entry assessment | ≤ 8 fields; save and resume; timed assessment works on 3G | Must |
| F4 | Applications | As a programme manager I screen, shortlist and send offers | Bulk actions; every decision logged with reviewer and reason; waitlist auto-backfills declined offers | Must |
| F5 | Sponsorship | As a sponsor I see my programme funnel and completion | Funnel and attendance update at least hourly; CSV export | Must |
| G1 | Payments | As a learner I pay by card, transfer or USSD | Webhooks idempotent; receipt emailed; failed payments recoverable | Must |
| G2 | Payments | As a sponsor I pay an invoice by bank transfer | Dedicated virtual account per invoice; auto-match within 5 min of credit; partial payments flagged | Must |
| G3 | Payouts | As a tutor I get paid weekly | Payout batch requires maker + checker; name-match enforced; statements downloadable; zero reconciliation breaks in UAT | Must |
| H1 | Notifications | As a user I get updates on WhatsApp, SMS or email | Per-channel preferences honoured; templates approved; quiet hours | Must |
| I1 | Back office | As staff I manage users, moderation, refunds and configuration | Role-based access; four-eyes on refunds > ₦100,000 and fee changes; full audit log | Must |

**MVP definition of done:** a sponsor can fund a 150-seat programme, 150 learners can be selected, taught, assessed and certified, tutors are paid correctly, and the sponsor downloads a report — with no manual spreadsheet in the loop.

---

## 27. Investment and partnership pitch summary

Plastoids asks for about ₦450–500m of seed funding to build the MVP, run the first sponsored cohorts and reach Gate 2 (month ~15), with a larger round of roughly ₦700m–₦1bn to follow once completion and renewal data are in hand. A separate pitch deck accompanies this document.

### The pitch in eight lines

1. **Problem:** Nigerian graduates need job-ready skills; sponsors spend on training but can't see who completed or got placed; good tutors can't reach learners or get paid reliably.
2. **Solution:** one platform where sponsors fund seats, Plastoids selects and teaches learners through vetted tutors and an AI tutor, and everyone sees the outcomes.
3. **Why now:** government (3MTT) and corporate sponsors are already funding digital-skills training at scale and shifting from "trained" to "completed and placed"; LLMs make a course-aware tutor affordable per learner.
4. **Wedge:** sponsored programmes — large tickets, near-pure-margin fees, and every call brings thousands of applicants who become the paid marketplace.
5. **Business model:** sponsor fees (12% + ₦12,000 per seat), course and tutoring commissions (8–20%), subscriptions (Plus, Tutor Pro, LMS), corporate training.
6. **Traction plan:** two anchor sponsors and 150+ learners in the first cohort by month 6–7; 16 programmes in Year 2 (base case).
7. **Financials (base case, illustrative):** net revenue ₦40m → ₦334m → ₦739m over three years; gross margin 57%; peak cash need ₦931m. The plan's single biggest lever is sponsor volume.
8. **Team ask:** product, engineering and programme-delivery leaders; advisors with CSR, government-procurement and accreditation networks.

### Partnership asks

| Partner | What Plastoids asks | What they get |
|---|---|---|
| Anchor sponsors (banks, telcos, fintechs, foundations) | Fund a 100–500-seat pilot programme | Fee-only pricing on the first programme, co-branded certificates, a public impact report |
| Training providers and tech hubs | Deliver cohorts and host sessions on Plastoids | Paid seats, payout rails, reporting their own sponsors can see |
| Universities and NYSC | Promote opportunities; career-services integration | Free access to sponsored seats for their students |
| Payment gateway and partner bank | Client-funds account, virtual accounts, payouts | Transaction volume |
| LLM and cloud providers | Startup credits, data-protection terms | A growing African EdTech workload |

### Open decisions before raising

- [ ] Legal entity, cap table and founding team roles
- [ ] Partner bank and client-funds (trust/escrow) structure — legal opinion
- [ ] Whether to bid as a 3MTT-style training provider in Year 1
- [ ] Final pricing after 15–20 sponsor and learner interviews
- [ ] VAT and withholding-tax treatment of pass-through tuition — tax opinion
- [ ] Accreditation strategy: partner with an awarding body or stay with skills certificates
- [ ] Second country for Phase 4 (Ghana or Kenya)
