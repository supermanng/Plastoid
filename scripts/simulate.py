#!/usr/bin/env python3
"""Plastoids demo simulation: drives the real Development API through every user journey with realistic Nigerian data,
then writes a Markdown report with the accounts (and MFA secrets) you can sign in with and deep links into the web app.

    make simulate                       # same as: python3 scripts/simulate.py
    python3 scripts/simulate.py --learners 8 --seed 7
    python3 scripts/simulate.py --dry-run  # validates the plan and the API without writing anything

Only works against an API running in Development: it reads OTP codes and signs fake webhooks through /api/v1/dev/*.
Every run uses a fresh phone range and fresh CAC numbers, so it can be run again and again on the same database.
Standard library only (Python 3.10+).
"""
from __future__ import annotations

import argparse
import base64
import collections
import datetime as dt
import hashlib
import hmac
import json
import math
import os
import pathlib
import random
import re
import struct
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid

# ----------------------------------------------------------------------------------------------------------------------
# Seeded staff accounts (appsettings.Development.json → Seed:*). Development-only values.
# ----------------------------------------------------------------------------------------------------------------------
STAFF = {"email": "staff@plastoids.local", "password": "Staff-Pass-2026!", "secret": "JBSWY3DPEHPK3PXPJBSWY3DPEHPK3PXP", "name": "Seyi Adeyemi (staff)"}
ADMIN = {"email": "admin@plastoids.local", "password": "Admin-Pass-2026!", "secret": "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", "name": "Super Admin"}

OK = (200, 201, 202, 204)
KOBO = 100  # kobo per naira

FIRST_NAMES = [
    "Adaeze", "Chinedu", "Ngozi", "Emeka", "Ifeoma", "Obinna", "Chiamaka", "Uche", "Nneka", "Kelechi",
    "Folake", "Tunde", "Yemisi", "Kunle", "Bolanle", "Seun", "Funmilayo", "Dapo", "Temitope", "Bukola",
    "Hauwa", "Musa", "Zainab", "Abubakar", "Amina", "Sadiq", "Fatima", "Ibrahim", "Halima", "Usman",
    "Efe", "Osas", "Ese", "Ejiro", "Oghenekaro", "Tamuno", "Ibiene", "Boma", "Ene", "Ochanya",
]
SURNAMES = [
    "Okafor", "Eze", "Nwosu", "Okonkwo", "Obi", "Adebayo", "Bakare", "Adeyemi", "Ogunleye", "Balogun",
    "Bello", "Abdullahi", "Mohammed", "Yusuf", "Lawal", "Okoro", "Umeh", "Igwe", "Akpan", "Essien",
    "Oyelaran", "Ajayi", "Afolabi", "Ojo", "Oduya", "Chukwu", "Onyekwere", "Danjuma", "Sule", "Garba",
]
CAREER_GOALS = ["Data analyst", "Product designer", "Cloud engineer", "Digital marketer", "Software developer", "Business analyst", "Teacher", "Accountant"]
INTERESTS = [["sql", "excel", "dashboards"], ["ui", "figma", "research"], ["python", "cloud", "linux"], ["marketing", "social-media", "copywriting"], ["javascript", "react", "apis"], ["finance", "excel", "powerbi"]]

TUTOR_PROFILES = [
    {
        "headline": "Senior data analyst teaching SQL and dashboards",
        "bio": "Ten years of analytics in Nigerian banking and fintech. I teach SQL, Python and dashboards with real datasets and a weekly project you can show employers.",
        "subjects": ["sql", "python", "data"], "formats": ["one_to_one", "course"], "hourly": 12_000,
        "course": {"title": "Data Analysis with SQL for Nigerian Businesses", "category": "data", "skills": ["sql", "analytics", "excel"], "level": "beginner", "price": 45_000,
                    "summary": "Learn to query, clean and present data with SQL through eight hands-on projects drawn from Nigerian fintech, retail and telecoms datasets. Suitable for complete beginners.",
                    "modules": [("Foundations of data and SQL", ["What is a database?", "SELECT, WHERE and ORDER BY", "Cleaning messy Excel exports"]), ("Joins, aggregation and dashboards", ["Joins across tables", "GROUP BY and window functions", "Building a sales dashboard"])],
                    "quiz": ("Joins and aggregation check", [("Which join keeps unmatched rows from the left table?", ["INNER JOIN", "LEFT JOIN", "CROSS JOIN"], 1), ("Which function counts rows in a group?", ["COUNT(*)", "SUM()", "AVG()"], 0), ("Which clause filters groups after aggregation?", ["WHERE", "HAVING", "LIMIT"], 1)]),
                    "cohort": {"name": "Evening cohort", "weeks": 8, "capacity": 20}},
    },
    {
        "headline": "Product designer mentoring the next UI/UX generation",
        "bio": "Lead product designer with work at two Lagos startups and a Nairobi bank. I teach research, wireframing and Figma with weekly critiques and a portfolio case study at the end.",
        "subjects": ["design", "figma", "ux"], "formats": ["one_to_one", "course", "group"], "hourly": 15_000,
        "course": {"title": "Product Design Fundamentals with Figma", "category": "design", "skills": ["figma", "ux", "ui"], "level": "beginner", "price": 60_000,
                    "summary": "From user research to a polished prototype: twelve lessons on interviews, flows, wireframes, visual design and handoff, ending with a portfolio-ready case study built in Figma.",
                    "modules": [("Research and problem framing", ["Talking to users", "Journey maps and flows", "Writing a design brief"]), ("Wireframes to prototype", ["Low-fidelity wireframes", "Visual design and components", "Prototyping and handoff"])],
                    "quiz": ("Design process check", [("Which comes first in a design process?", ["Visual polish", "User research", "Developer handoff"], 1), ("A wireframe is best described as…", ["A low-fidelity layout", "A brand guideline", "A database"], 0)]),
                    "cohort": None},
    },
    {
        "headline": "Growth marketer teaching digital marketing that sells",
        "bio": "Eight years running performance campaigns for FMCG and e-commerce brands across West Africa. I teach paid social, SEO basics, email and analytics with live account walk-throughs.",
        "subjects": ["marketing", "seo", "analytics"], "formats": ["one_to_one", "course"], "hourly": 10_000,
        "course": {"title": "Digital Marketing for Small Businesses", "category": "business", "skills": ["marketing", "seo", "social-media"], "level": "beginner", "price": 25_000,
                    "summary": "Plan, run and measure campaigns on Instagram, WhatsApp, Google and email for a Nigerian small business, with templates, budgets and a measurement plan you can reuse every month.",
                    "modules": [("Strategy and channels", ["Knowing your customer", "Choosing channels that fit", "Budgets and offers"]), ("Campaigns and measurement", ["Paid social that converts", "SEO basics for local search", "Reading the numbers"])],
                    "quiz": ("Channels and measurement check", [("Which metric tells you the cost of one sale?", ["CPA", "Impressions", "Reach"], 0), ("SEO improves which traffic source?", ["Paid search", "Organic search", "Direct mail"], 1)]),
                    "cohort": None},
    },
]

PROVIDER_COURSE = {
    "title": "Analytics Engineering Bootcamp", "category": "data", "skills": ["dbt", "sql", "python"], "level": "intermediate", "price": 55_000,
    "summary": "Twelve weeks of hands-on analytics engineering: modelling with dbt, warehouse design, testing and orchestration on Nigerian fintech datasets, taught live by a verified tutor.",
    "modules": [("Modelling the warehouse", ["Why model data?", "Staging, marts and naming", "Incremental models"]), ("Testing and orchestration", ["Testing with dbt", "Scheduling and alerts", "Documentation that lasts"])],
    "quiz": ("Modelling and testing check", [("Which dbt test checks uniqueness?", ["unique", "not_null", "accepted_values"], 0), ("A mart model is…", ["Raw source data", "A business-ready table", "A scheduler"], 1)]),
    "cohort": {"name": "Weekend bootcamp", "weeks": 12, "capacity": 25},
}


# ----------------------------------------------------------------------------------------------------------------------
# Small helpers
# ----------------------------------------------------------------------------------------------------------------------
def totp(secret_b32: str) -> str:
    key = base64.b32decode(secret_b32 + "=" * (-len(secret_b32) % 8))
    counter = struct.pack(">Q", int(time.time()) // 30)
    mac = hmac.new(key, counter, hashlib.sha1).digest()
    offset = mac[-1] & 0x0F
    return str((struct.unpack(">I", mac[offset:offset + 4])[0] & 0x7FFFFFFF) % 1_000_000).zfill(6)


def utcnow() -> dt.datetime:
    return dt.datetime.now(dt.timezone.utc)


def iso(t: dt.datetime) -> str:
    return t.astimezone(dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def parse_iso(value: str) -> dt.datetime:
    """.NET DateTime strings (up to 7 fractional digits, optional Z) → aware UTC datetime."""
    text = re.sub(r"(\.\d+)", lambda m: m.group(1)[:7], value.strip()).replace("Z", "+00:00")
    parsed = dt.datetime.fromisoformat(text)
    return parsed.replace(tzinfo=dt.timezone.utc) if parsed.tzinfo is None else parsed.astimezone(dt.timezone.utc)


def same_format(a: str | None, b: str) -> bool:
    return (a or "").replace("_", "").lower() == b.replace("_", "").lower()


def naira(kobo: int | None) -> str:
    if kobo is None:
        return "-"
    return f"₦{kobo / KOBO:,.0f}" if kobo % KOBO == 0 else f"₦{kobo / KOBO:,.2f}"


def mask_phone(phone: str) -> str:
    return phone[:7] + "****" + phone[-3:]


def short(obj, n=400) -> str:
    text = obj if isinstance(obj, str) else json.dumps(obj, default=str)
    return text if len(text) <= n else text[:n] + "…"


class StepError(Exception):
    def __init__(self, message: str, status: int | None = None, body=None):
        super().__init__(message)
        self.status, self.body = status, body


class Skip(Exception):
    """A step that cannot run because something it depends on failed earlier."""


# ----------------------------------------------------------------------------------------------------------------------
# HTTP client with rate-limit pacing, 429 back-off and per-user token re-issue
# ----------------------------------------------------------------------------------------------------------------------
class Api:
    def __init__(self, base: str, auth_limit: int, verbose: bool):
        self.base = base.rstrip("/") + "/api/v1"
        self.auth_limit = auth_limit
        self.verbose = verbose
        self.auth_calls: collections.deque[float] = collections.deque()
        self.requests = 0
        self.last = ""

    def _pace_auth(self):
        """Keep /identity/auth/* calls under the per-IP fixed-window limit (AuthPermitsPerMinute)."""
        now = time.monotonic()
        while self.auth_calls and now - self.auth_calls[0] > 61:
            self.auth_calls.popleft()
        budget = max(1, self.auth_limit - 3)
        if len(self.auth_calls) >= budget:
            wait = 61 - (now - self.auth_calls[0]) + 0.5
            print(f"    … pausing {wait:.0f}s to stay under the auth rate limit ({self.auth_limit}/min)")
            time.sleep(max(0.5, wait))
        self.auth_calls.append(time.monotonic())

    def raw(self, method: str, path: str, data: bytes | None = None, headers: dict | None = None, token: str | None = None):
        if path.startswith("/identity/auth/"):
            self._pace_auth()
        url = self.base + path
        hdrs = {"Accept": "application/json"}
        if headers:
            hdrs.update(headers)
        if token:
            hdrs["Authorization"] = "Bearer " + token
        self.last = f"{method} {path}"
        attempts = 0
        while True:
            attempts += 1
            self.requests += 1
            req = urllib.request.Request(url, data=data, method=method, headers=hdrs)
            retry_after = 15
            try:
                with urllib.request.urlopen(req, timeout=60) as response:
                    body = response.read()
                    status = response.status
            except urllib.error.HTTPError as e:
                body = e.read()
                status = e.code
                try:
                    retry_after = int(e.headers.get("Retry-After") or retry_after)
                except ValueError:
                    pass
            except urllib.error.URLError as e:
                raise StepError(f"cannot reach {url}: {e.reason}") from e
            if status == 429 and attempts <= 4:
                print(f"    … 429 on {method} {path}; waiting {retry_after}s")
                time.sleep(retry_after)
                continue
            text = body.decode("utf-8", "replace") if body else ""
            parsed = None
            if text:
                try:
                    parsed = json.loads(text)
                except ValueError:
                    parsed = text
            if self.verbose:
                print(f"      {method} {path} → {status} {short(parsed, 160)}")
            return status, parsed, text

    def call(self, method: str, path: str, body=None, user: "User | None" = None, token: str | None = None, idem: bool | None = None, headers: dict | None = None):
        """JSON call. Sends an Idempotency-Key on commands by default; re-issues the user's token once on 401."""
        data = json.dumps(body).encode() if body is not None else None
        hdrs = {"Content-Type": "application/json"}
        if idem is None:
            idem = method in ("POST", "PUT", "PATCH", "DELETE")
        if idem:
            hdrs["Idempotency-Key"] = uuid.uuid4().hex
        if headers:
            hdrs.update(headers)
        bearer = token or (user.token if user else None)
        status, parsed, _ = self.raw(method, path, data, hdrs, bearer)
        if status == 401 and user is not None and user.can_reissue():
            user.reissue(self)
            if idem:
                hdrs["Idempotency-Key"] = uuid.uuid4().hex
            status, parsed, _ = self.raw(method, path, data, hdrs, user.token)
        return status, parsed


def expect(result, ok=OK, what: str | None = None):
    status, body = result
    if status not in ok:
        raise StepError(f"{what or 'request'} failed with {status}", status, body)
    return body


def tokens_of(body) -> tuple[str, str | None]:
    """Both token shapes: {tokens:{accessToken,…}} (otp/verify) and {accessToken,…} (mfa/verify, token)."""
    t = body.get("tokens", body) if isinstance(body, dict) else {}
    access = t.get("accessToken")
    if not access:
        raise StepError("no access token in response", None, body)
    return access, t.get("refreshToken")


# ----------------------------------------------------------------------------------------------------------------------
# Users
# ----------------------------------------------------------------------------------------------------------------------
class User:
    def __init__(self, role: str, name: str, phone: str | None = None, email: str | None = None, dob: str | None = None):
        self.role, self.name, self.phone, self.email, self.dob = role, name, phone, email, dob
        self.user_id: str | None = None
        self.token: str | None = None
        self.refresh_token: str | None = None
        self.mfa_secret: str | None = None
        self.password: str | None = None
        self.notes: list[str] = []
        self.extra: dict = {}

    @property
    def contact(self) -> str:
        return self.email or self.phone or "-"

    def can_reissue(self) -> bool:
        return bool(self.refresh_token or (self.email and self.password and self.mfa_secret))

    def reissue(self, api: Api):
        """Fresh tokens after a role change or expiry. MFA users step up again so the token keeps amr=mfa."""
        if self.email and self.password and self.mfa_secret:
            first = expect(api.call("POST", "/identity/auth/token", {"grantType": "password", "email": self.email, "password": self.password}, idem=False), what="password grant")
            if first.get("mfaRequired"):
                second = expect(api.call("POST", "/identity/auth/mfa/verify", {"code": totp(self.mfa_secret), "mfaToken": first["mfaToken"]}, idem=False), what="mfa verify")
                self.token, self.refresh_token = tokens_of(second)
            else:
                self.token, self.refresh_token = tokens_of(first)
            return
        if self.refresh_token:
            refreshed = expect(api.call("POST", "/identity/auth/token", {"grantType": "refresh_token", "refreshToken": self.refresh_token}, idem=False), what="refresh grant")
            self.token, self.refresh_token = tokens_of(refreshed)
        if self.mfa_secret:
            stepped = expect(api.call("POST", "/identity/auth/mfa/verify", {"code": totp(self.mfa_secret)}, token=self.token, idem=False), what="mfa step-up")
            access, refresh = tokens_of(stepped)
            self.token = access
            self.refresh_token = refresh or self.refresh_token


# ----------------------------------------------------------------------------------------------------------------------
# The simulation
# ----------------------------------------------------------------------------------------------------------------------
class Simulation:
    def __init__(self, args):
        self.args = args
        self.api = Api(args.api, args.auth_limit, args.verbose)
        self.web = args.web.rstrip("/")
        self.rng = random.Random(args.seed)
        # Phones: +23480 + 8 digits = +23480RRRRRNNN (run id RRRRR from the clock, NNN sequential). Never reused across runs.
        self.run_id = int(time.time()) % 100_000
        self.phone_seq = 0
        self.cac_seq = 0
        self.started = utcnow()
        self.failures: list[dict] = []
        self.steps_ok = 0
        self.users: list[User] = []
        self.created: list[dict] = []  # {kind, name, link, detail}
        self.money: dict[str, str] = {}
        self.staff = User("staff", STAFF["name"], email=STAFF["email"])
        self.staff.password, self.staff.mfa_secret = STAFF["password"], STAFF["secret"]
        self.admin = User("super admin", ADMIN["name"], email=ADMIN["email"])
        self.admin.password, self.admin.mfa_secret = ADMIN["password"], ADMIN["secret"]
        self.tutors: list[User] = []
        self.learners: list[User] = []
        self.sponsor: User | None = None
        self.provider: User | None = None
        self.courses: list[dict] = []
        self.cohorts: list[dict] = []
        self.blocks: list[dict] = []
        self.purchases: list[dict] = []
        self.batch: dict | None = None
        self.used_names: set[str] = set()

    # -- helpers ------------------------------------------------------------------------------------------------------
    def next_phone(self) -> str:
        self.phone_seq += 1
        return f"+23480{self.run_id:05d}{self.phone_seq:03d}"

    def next_cac(self) -> str:
        self.cac_seq += 1
        return f"RC{self.run_id:05d}{self.cac_seq:02d}"

    def person(self) -> str:
        for _ in range(50):
            name = f"{self.rng.choice(FIRST_NAMES)} {self.rng.choice(SURNAMES)}"
            if name not in self.used_names:
                self.used_names.add(name)
                return name
        return f"{self.rng.choice(FIRST_NAMES)} {self.rng.choice(SURNAMES)} {self.phone_seq}"

    def dob(self, lo=1982, hi=2002) -> str:
        return f"{self.rng.randint(lo, hi)}-{self.rng.randint(1, 12):02d}-{self.rng.randint(1, 28):02d}"

    def account_number(self) -> str:
        return "01" + "".join(str(self.rng.randint(0, 9)) for _ in range(7)) + str(self.rng.randint(1, 8))

    def step(self, title: str, fn, *a, **kw):
        """Runs one step, prints one line, records failures and carries on."""
        t0 = time.monotonic()
        try:
            result = fn(*a, **kw)
            note = f" — {result}" if isinstance(result, str) else ""
            print(f"[ok]   {title}{note}  ({time.monotonic() - t0:.1f}s)")
            self.steps_ok += 1
            return result if not isinstance(result, str) else True
        except Skip as e:
            print(f"[skip] {title} — {e}")
            self.failures.append({"step": title, "status": "skipped", "detail": str(e)})
        except StepError as e:
            print(f"[FAIL] {title} — {e} at {self.api.last}\n       {short(e.body)}")
            self.failures.append({"step": title, "status": e.status, "detail": f"{e} at {self.api.last}: {short(e.body, 300)}"})
        except Exception as e:  # noqa: BLE001 - the simulation must keep going
            print(f"[FAIL] {title} — {type(e).__name__}: {e} at {self.api.last}")
            self.failures.append({"step": title, "status": "exception", "detail": f"{type(e).__name__}: {e} at {self.api.last}"})
        return None

    def wait_until(self, fn, what: str, timeout: float = 60.0, every: float = 1.0):
        deadline = time.monotonic() + timeout
        while True:
            value = fn()
            if value:
                return value
            if time.monotonic() > deadline:
                raise StepError(f"timed out after {timeout:.0f}s waiting for {what}")
            time.sleep(every)

    def link(self, kind: str, name: str, path: str | None, detail: str = ""):
        self.created.append({"kind": kind, "name": name, "link": (self.web + path) if path else "", "detail": detail})

    # -- identity -------------------------------------------------------------------------------------------------------
    def otp_code(self, phone: str) -> str:
        status, body = self.api.call("GET", "/dev/sms/last?" + urllib.parse.urlencode({"phone": phone}))
        if status == 404 and not isinstance(body, dict):
            raise StepError("GET /api/v1/dev/sms/last is not mapped: restart the API in Development (make run) so the /dev endpoints exist", status, body)
        body = expect((status, body), what="read OTP from the fake SMS")
        if not body.get("code"):
            raise StepError("no OTP code in the last SMS", status, body)
        return body["code"]

    def sign_up(self, user: User) -> User:
        """OTP request → /dev/sms/last → OTP verify (with registration details for a new phone)."""
        expect(self.api.call("POST", "/identity/auth/otp/request", {"phone": user.phone}, idem=False), what="otp request")
        code = self.otp_code(user.phone)
        body = {"phone": user.phone, "code": code}
        if user.dob:
            body.update({"dateOfBirth": user.dob, "displayName": user.name})
        verified = expect(self.api.call("POST", "/identity/auth/otp/verify", body, idem=False), what="otp verify")
        user.user_id = verified.get("userId")
        user.token, user.refresh_token = tokens_of(verified)
        self.users.append(user)
        return user

    def enable_mfa(self, user: User):
        enrolment = expect(self.api.call("POST", "/identity/me/mfa/enable", {}, user=user, idem=False), what="mfa enable")
        user.mfa_secret = enrolment["secret"]
        stepped = expect(self.api.call("POST", "/identity/auth/mfa/verify", {"code": totp(user.mfa_secret)}, user=user, idem=False), what="mfa verify")
        access, refresh = tokens_of(stepped)
        user.token = access
        user.refresh_token = refresh or user.refresh_token

    def staff_sign_in(self, user: User):
        user.reissue(self.api)
        me = expect(self.api.call("GET", "/identity/me", user=user), what="/identity/me")
        user.user_id = me.get("id")
        self.users.append(user)
        return f"{user.email} roles={','.join(me.get('roles', []))}"

    # -- webhooks ---------------------------------------------------------------------------------------------------------
    def confirm_charge(self, intent_id: str, user: User, amount_kobo: int | None = None) -> dict:
        intent = expect(self.api.call("GET", f"/payments/intents/{intent_id}", user=user), what="read intent")
        reference = intent["gatewayReference"]
        amount = amount_kobo if amount_kobo is not None else intent["amount"]["amount"]
        signed = expect(self.api.call("POST", "/dev/gateway/webhook", {"eventType": "charge.success", "reference": reference, "amountKobo": amount}, idem=False), what="sign gateway webhook")
        status, body, _ = self.api.raw("POST", "/payments/webhooks/fake", signed["payload"].encode(), {"Content-Type": "application/json", "x-paystack-signature": signed["signature"]})
        body = expect((status, body), what="gateway webhook")
        if body.get("outcome") not in ("confirmed", "duplicate"):
            raise StepError(f"webhook outcome {body.get('outcome')}", status, body)
        return intent

    def settle_payout(self, reference: str, amount_kobo: int, payout_id: str) -> dict:
        signed = expect(self.api.call("POST", "/dev/payouts/webhook", {"eventType": "transfer.success", "reference": reference, "amountKobo": amount_kobo, "bankReference": "NIP-DEMO-" + payout_id[:8]}, idem=False), what="sign payout webhook")
        status, body, _ = self.api.raw("POST", "/payouts/webhooks/fake", signed["payload"].encode(), {"Content-Type": "application/json", "x-payout-signature": signed["signature"]})
        return expect((status, body), what="payout webhook")

    def run_job(self, name: str, at: dt.datetime | None = None) -> dict:
        query = ("?" + urllib.parse.urlencode({"at": iso(at)})) if at else ""
        return expect(self.api.call("POST", f"/dev/jobs/{name}{query}", idem=False), what=f"job {name}")

    def enrolments(self, user: User) -> list:
        body = expect(self.api.call("GET", "/lms/enrolments/me", user=user), what="my enrolments")
        return body if isinstance(body, list) else body.get("items", [])

    def wait_enrolment(self, user: User, course_id: str) -> dict:
        return self.wait_until(lambda: next((e for e in self.enrolments(user) if e.get("courseId") == course_id), None), f"enrolment of {user.name} in course {course_id[:8]}")

    def quote_for(self, slug: str, fmt: str, user: User) -> tuple[str, int, dict]:
        detail = expect(self.api.call("GET", f"/catalogue/courses/{slug}", user=user), what="course detail")
        offerings = detail.get("offerings", [])
        offering = next((o for o in offerings if same_format(o.get("format"), fmt)), offerings[0] if offerings else None)
        if not offering:
            raise StepError(f"course {slug} has no offering", None, detail)
        price = offering.get("price", {}).get("amount") or offering.get("priceKobo")
        return offering["quoteId"], price, offering

    # -- 1. staff ---------------------------------------------------------------------------------------------------------
    def part_staff(self):
        print("\n== 1. Staff")
        self.step("staff signs in (password + TOTP)", self.staff_sign_in, self.staff)
        self.step("super admin signs in (password + TOTP)", self.staff_sign_in, self.admin)
        if not self.staff.token:
            raise SystemExit("Staff sign-in failed: nothing else can run. Is the API up in Development with Seed:Enabled?")

    # -- 2. tutors ----------------------------------------------------------------------------------------------------------
    def part_tutors(self):
        print("\n== 2. Tutors")
        for i, profile in enumerate(TUTOR_PROFILES, start=1):
            tutor = User("tutor", self.person(), self.next_phone(), dob=self.dob(1980, 1995))
            tutor.extra["profile"] = profile
            self.tutors.append(tutor)
            self.step(f"tutor {i}: signs up as {tutor.name} ({tutor.phone})", self.sign_up, tutor)
            if not tutor.token:
                continue
            self.step(f"tutor {i}: applies, enables MFA, passes identity check", self.tutor_apply, tutor, profile)
            self.step(f"tutor {i}: credential + demo lesson, staff approves → verified", self.tutor_verify, tutor)
            self.step(f"tutor {i}: availability, pricing and a name-matched payout account", self.tutor_setup, tutor, profile)

    def tutor_apply(self, tutor: User, profile: dict):
        applied = expect(self.api.call("POST", "/tutors/applications", {
            "headline": profile["headline"], "bio": profile["bio"], "subjects": profile["subjects"], "languages": ["en", "yo" if self.rng.random() < 0.5 else "ig"],
            "formats": profile["formats"], "timeZone": "Africa/Lagos", "hourlyRate": {"amount": profile["hourly"] * KOBO, "currency": "NGN"}, "acceptCodeOfConduct": True,
        }, user=tutor), what="tutor application")
        tutor.extra["tutorId"] = applied["tutorId"]
        # The access token predates the tutor role: enabling MFA re-issues it with the role and amr=mfa.
        self.enable_mfa(tutor)
        nin = "".join(str(self.rng.randint(0, 9)) for _ in range(10)) + str(self.rng.randint(1, 9))  # NIN ending in 0 fails the fake KYC
        bvn = "22" + "".join(str(self.rng.randint(0, 9)) for _ in range(9))
        check = expect(self.api.call("POST", "/tutors/me/identity-check", {"nin": nin, "bvn": bvn, "selfieBlobKey": f"selfies/{tutor.user_id}.jpg", "fullName": tutor.name}, user=tutor), what="identity check")
        if not check.get("passed", True):
            raise StepError("fake KYC did not pass", 200, check)
        return f"tutorId={applied['tutorId']}"

    def tutor_verify(self, tutor: User):
        if "tutorId" not in tutor.extra:
            raise Skip("application failed")
        issuer, title = self.rng.choice([("University of Lagos", "BSc Computer Science"), ("University of Ibadan", "BSc Statistics"), ("Ahmadu Bello University", "BSc Economics"), ("Obafemi Awolowo University", "BA Fine and Applied Arts")])
        credential = expect(self.api.call("POST", "/tutors/me/credentials", {"type": "degree", "issuer": issuer, "title": title, "contentType": "application/pdf"}, user=tutor), what="credential")
        expect(self.api.call("POST", "/tutors/me/demo-lesson", {"contentType": "video/mp4"}, user=tutor), what="demo lesson")
        expect(self.api.call("POST", f"/admin/tutors/credentials/{credential['credentialId']}/review", {"decision": "approved"}, user=self.staff), what="credential review")
        subject = expect(self.api.call("POST", f"/admin/tutors/{tutor.extra['tutorId']}/subject-review", {"decision": "approved", "score": self.rng.randint(82, 96)}, user=self.staff), what="subject review")
        dashboard = expect(self.api.call("GET", "/tutors/me", user=tutor), what="tutor dashboard")
        tutor.notes.append(f"status={dashboard.get('status')}")
        self.link("Tutor", tutor.name, f"/tutors/{tutor.extra['tutorId']}", dashboard.get("status", ""))
        return f"status={dashboard.get('status')} verified={subject.get('verified')}"

    def tutor_setup(self, tutor: User, profile: dict):
        if "tutorId" not in tutor.extra:
            raise Skip("application failed")
        weekly = [{"day": d, "start": "08:00", "end": "20:00"} for d in ("monday", "tuesday", "wednesday", "thursday", "friday", "saturday")]
        expect(self.api.call("PUT", "/tutors/me/availability", {"timeZone": "Africa/Lagos", "weekly": weekly, "exceptions": []}, user=tutor), what="availability")
        rules = [{"format": "group", "durationMinutes": 90, "price": {"amount": profile["hourly"] * KOBO // 2, "currency": "NGN"}, "maxLearners": 8}] if "group" in profile["formats"] else []
        expect(self.api.call("PUT", "/tutors/me/pricing", {"hourlyRate": {"amount": profile["hourly"] * KOBO, "currency": "NGN"}, "rules": rules}, user=tutor), what="pricing")
        account = self.account_number()
        expect(self.api.call("GET", "/dev/bank/register?" + urllib.parse.urlencode({"accountNumber": account, "name": tutor.name.upper()})), what="register bank account")
        payout = expect(self.api.call("PUT", "/tutors/me/payout-account", {"bankCode": "058", "accountNumber": account}, user=tutor), what="payout account")
        tutor.extra["payoutAccount"] = payout.get("accountNumberMasked")
        return f"hourly {naira(profile['hourly'] * KOBO)}, payout account {payout.get('accountNumberMasked')}"

    # -- 3. organisations ---------------------------------------------------------------------------------------------------
    def part_organisations(self):
        print("\n== 3. Organisations")
        self.sponsor = User("sponsor owner", self.person(), self.next_phone(), dob=self.dob(1975, 1990))
        self.sponsor.extra["org"] = {"name": "Access Foundation", "type": "sponsor"}
        self.provider = User("provider owner", self.person(), self.next_phone(), dob=self.dob(1975, 1990))
        self.provider.extra["org"] = {"name": "Lagos Data School", "type": "provider"}
        for owner in (self.sponsor, self.provider):
            org = owner.extra["org"]
            self.step(f"{org['name']}: owner {owner.name} signs up and registers the {org['type']}", self.register_organisation, owner)
            self.step(f"{org['name']}: staff verifies, owner enables MFA", self.verify_organisation, owner)
        self.step("Lagos Data School: adds a finance member and an author member", self.provider_members)
        self.step("Access Foundation: buys 600 units by card (gateway webhook)", self.buy_org_units, self.sponsor, 600)
        self.step("Lagos Data School: buys 100 units by card (gateway webhook)", self.buy_org_units, self.provider, 100)
        self.step("Access Foundation: staff goodwill adjustment of +25 units", self.goodwill_units, self.sponsor, 25)

    def register_organisation(self, owner: User):
        self.sign_up(owner)
        org = owner.extra["org"]
        signatory_email = f"{org['name'].split()[0].lower()}-{self.run_id}@example.org"
        registered = expect(self.api.call("POST", "/identity/organisations", {"name": org["name"], "type": org["type"], "cacNumber": self.next_cac(), "signatoryName": owner.name, "signatoryEmail": signatory_email}, user=owner), what="register organisation")
        org["id"] = registered["organisationId"]
        return f"organisationId={org['id']}"

    def verify_organisation(self, owner: User):
        org = owner.extra["org"]
        if "id" not in org:
            raise Skip("organisation not registered")
        expect(self.api.call("POST", f"/admin/identity/organisations/{org['id']}/verification", {"decision": "verified"}, user=self.staff), what="organisation verification")
        self.enable_mfa(owner)  # the re-issued token carries the sponsor role and amr=mfa for the organisation portal
        me = expect(self.api.call("GET", "/identity/me", user=owner), what="/identity/me")
        status = next((o.get("verificationStatus") for o in me.get("organisations", []) if o.get("id") == org["id"]), "?")
        self.link("Organisation", org["name"], "/sponsor", f"{org['type']}, {status}")
        return f"{status}; owner roles={','.join(me.get('roles', []))}"

    def provider_members(self):
        org = self.provider.extra["org"]
        if "id" not in org:
            raise Skip("provider not registered")
        added = []
        for role in ("finance", "author"):
            member = User(f"provider {role}", self.person(), self.next_phone(), dob=self.dob(1985, 2000))
            member.extra["org"] = org
            self.sign_up(member)
            expect(self.api.call("POST", f"/identity/organisations/{org['id']}/members", {"phone": member.phone, "role": role}, user=self.provider), what=f"add {role} member")
            self.enable_mfa(member)  # organisation portal needs an MFA session
            added.append(f"{role}={member.name}")
        return ", ".join(added)

    def buy_org_units(self, owner: User, units: int):
        org = owner.extra["org"]
        if "id" not in org or not owner.mfa_secret:
            raise Skip("organisation not ready")
        wallet_path = f"/wallet/organisations/{org['id']}"
        before = expect(self.api.call("GET", wallet_path, user=owner), what="organisation wallet")["balance"]
        purchase = expect(self.api.call("POST", "/wallet/purchases", {"units": units, "organisationId": org["id"]}, user=owner), what="unit purchase")
        self.confirm_charge(purchase["paymentIntentId"], owner, purchase["amount"]["amount"])
        self.wait_until(lambda: expect(self.api.call("GET", wallet_path, user=owner))["balance"] >= before + units, f"{units} units credited to {org['name']}")
        self.purchases.append({"who": org["name"], "kind": "units", "amount": purchase["amount"]["amount"], "intent": purchase["paymentIntentId"]})
        self.link("Organisation wallet", f"{org['name']} ({units} units)", "/sponsor/wallet", naira(purchase["amount"]["amount"]))
        return f"{units} units = {naira(purchase['amount']['amount'])}, wallet {purchase['walletId'][:8]}"

    def goodwill_units(self, owner: User, units: int):
        org = owner.extra["org"]
        if "id" not in org:
            raise Skip("organisation not ready")
        adjusted = expect(self.api.call("POST", f"/admin/wallet/organisation/{org['id']}/adjustments", {"units": units, "reason": "Goodwill: launch partner bonus units"}, user=self.staff), what="wallet adjustment")
        return f"balance after {adjusted.get('balanceAfter')}"

    # -- 4. catalogue --------------------------------------------------------------------------------------------------------
    def part_catalogue(self):
        print("\n== 4. Catalogue")
        for i, tutor in enumerate(self.tutors, start=1):
            if not tutor.token or "tutorId" not in tutor.extra:
                self.failures.append({"step": f"tutor {i} course", "status": "skipped", "detail": "tutor not verified"})
                continue
            course = self.step(f"tutor {i}: publishes '{tutor.extra['profile']['course']['title']}'", self.publish_course, tutor, tutor.extra["profile"]["course"], None)
            if course and course.get("cohortOfferingId"):
                self.step(f"tutor {i}: opens a cohort on the course", self.open_cohort, tutor, course, course["spec"]["cohort"], None)
        if self.provider and self.provider.mfa_secret and self.tutors and self.tutors[0].token:
            course = self.step("Lagos Data School: authors a course, engages tutor 1 on 40% revenue share, publishes", self.publish_course, self.provider, PROVIDER_COURSE, self.tutors[0])
            if course and course.get("cohortOfferingId"):
                self.step("Lagos Data School: opens a cohort taught by tutor 1", self.open_cohort, self.provider, course, PROVIDER_COURSE["cohort"], self.tutors[0])

    def publish_course(self, author: User, spec: dict, teaching_tutor: User | None) -> dict:
        is_org = author.role == "provider owner"
        create_path = f"/catalogue/organisations/{author.extra['org']['id']}/courses" if is_org else "/catalogue/courses"
        created = expect(self.api.call("POST", create_path, {"title": spec["title"], "summary": spec["summary"][:120], "level": spec["level"], "language": "en", "category": spec["category"], "skills": spec["skills"]}, user=author), what="create course")
        course_id, slug = created["courseId"], created["slug"]
        outline = expect(self.api.call("PUT", f"/catalogue/courses/{course_id}/outline", {"summary": spec["summary"], "modules": [{"title": title, "order": n} for n, (title, _) in enumerate(spec["modules"], start=1)]}, user=author), what="outline")
        module_ids = [m["id"] for m in outline["modules"]]
        lessons = []
        for module_id, (_, lesson_titles) in zip(module_ids, spec["modules"]):
            for n, lesson_title in enumerate(lesson_titles, start=1):
                content = json.dumps({"pages": 3, "text": f"{lesson_title}: notes, worked examples and a short exercise."})
                lesson = expect(self.api.call("POST", f"/catalogue/courses/{course_id}/modules/{module_id}/lessons", {"type": "doc", "title": lesson_title, "order": n, "durationMinutes": self.rng.choice([8, 10, 12, 15]), "isPreview": n == 1 and module_id == module_ids[0], "contentJson": content}, user=author), what="lesson")
                lessons.append(lesson["lessonId"])
        quiz_title, questions = spec["quiz"]
        quiz = expect(self.api.call("POST", f"/catalogue/courses/{course_id}/modules/{module_ids[-1]}/quizzes", {"title": quiz_title, "order": len(spec["modules"][-1][1]) + 1, "passMarkPercent": 70, "questions": [{"prompt": p, "options": o, "correctIndex": c} for p, o, c in questions]}, user=author), what="quiz")
        course = {"courseId": course_id, "slug": slug, "title": spec["title"], "author": author, "lessons": lessons, "quizId": quiz["lessonId"], "answers": {str(i): c for i, (_, _, c) in enumerate(questions)}, "price": spec["price"] * KOBO, "spec": spec, "tutorUserId": None}
        tutor_user_id = None
        if is_org and teaching_tutor:
            engagement = expect(self.api.call("POST", f"/catalogue/organisations/{author.extra['org']['id']}/courses/{course_id}/engagements", {"tutorId": teaching_tutor.extra["tutorId"], "terms": {"kind": "revenue_share", "shareBps": 4000}, "note": f"Teach the {spec['cohort']['name'].lower()} and keep the self-paced forum alive."}, user=author), what="engagement offer")
            accepted = expect(self.api.call("POST", f"/catalogue/engagements/{engagement['engagementId']}/respond", {"accept": True, "note": "Happy to teach this one."}, user=teaching_tutor), what="engagement accept")
            tutor_user_id = teaching_tutor.user_id
            course["engagement"] = {"id": engagement["engagementId"], "status": accepted.get("status")}
            self.link("Tutor engagement", f"{teaching_tutor.name} ← {author.extra['org']['name']} (40% revenue share)", "/tutor/offers", accepted.get("status", ""))
        course["tutorUserId"] = tutor_user_id
        offering_body = {"format": "self_paced", "price": {"amount": spec["price"] * KOBO, "currency": "NGN"}, "fundingTypes": ["self", "sponsored"], "capacity": None, "startAt": None, "durationWeeks": 6}
        if tutor_user_id:
            offering_body["tutorUserId"] = tutor_user_id
        self_paced = expect(self.api.call("POST", f"/catalogue/courses/{course_id}/offerings", offering_body, user=author), what="self-paced offering")
        course["selfPacedOfferingId"] = self_paced["offeringId"]
        if spec.get("cohort"):
            start = (utcnow() + dt.timedelta(days=self.rng.randint(14, 42))).replace(hour=18, minute=0, second=0, microsecond=0)
            cohort_body = {"format": "cohort", "price": {"amount": spec["price"] * KOBO, "currency": "NGN"}, "fundingTypes": ["self", "sponsored"], "capacity": spec["cohort"]["capacity"], "startAt": iso(start), "durationWeeks": spec["cohort"]["weeks"]}
            if tutor_user_id:
                cohort_body["tutorUserId"] = tutor_user_id
            cohort_offering = expect(self.api.call("POST", f"/catalogue/courses/{course_id}/offerings", cohort_body, user=author), what="cohort offering")
            course["cohortOfferingId"], course["cohortStart"] = cohort_offering["offeringId"], start
        submitted = expect(self.api.call("POST", f"/catalogue/courses/{course_id}/submit", {}, user=author), what="submit")
        reviewed = ""
        if submitted.get("requiresHumanReview"):
            expect(self.api.call("POST", f"/admin/catalogue/courses/{course_id}/review", {"decision": "approved"}, user=self.staff), what="staff review")
            reviewed = ", staff review approved"
        published = expect(self.api.call("POST", f"/catalogue/courses/{course_id}/publish", {}, user=author), what="publish")
        course["versionNo"] = published.get("versionNo")
        self.courses.append(course)
        owner_label = author.extra["org"]["name"] if is_org else author.name
        self.link("Course", spec["title"], f"/courses/{slug}", f"{naira(spec['price'] * KOBO)}, by {owner_label}, {len(lessons)} lessons + quiz")
        print(f"       /courses/{slug}{reviewed}")
        return course

    def open_cohort(self, opener: User, course: dict, cohort_spec: dict, teaching_tutor: User | None):
        start = course["cohortStart"]
        end = start + dt.timedelta(weeks=cohort_spec["weeks"])
        body = {"courseId": course["courseId"], "offeringId": course["cohortOfferingId"], "name": f"{cohort_spec['name']} · {start.strftime('%b %Y')}", "startsAt": iso(start), "endsAt": iso(end), "capacity": cohort_spec["capacity"], "programmeId": None}
        if teaching_tutor:
            body["tutorUserId"] = teaching_tutor.user_id
        cohort = expect(self.api.call("POST", "/lms/cohorts", body, user=opener), what="create cohort")
        teacher = teaching_tutor or opener
        sessions_body = {"weekdays": ["monday", "wednesday"], "startTimeUtc": "18:00", "durationMinutes": 90, "titlePrefix": "Live class"}
        status, scheduled = self.api.call("POST", f"/lms/cohorts/{cohort['cohortId']}/sessions", sessions_body, user=teacher)
        if status == 403 and teacher is not opener:
            status, scheduled = self.api.call("POST", f"/lms/cohorts/{cohort['cohortId']}/sessions", sessions_body, user=opener)
        scheduled = expect((status, scheduled), what="schedule sessions")
        thread_body = {"title": "Welcome", "body": "Introduce yourself and tell us what you want to build by the end of the cohort."}
        status, thread = self.api.call("POST", f"/lms/cohorts/{cohort['cohortId']}/threads", thread_body, user=teacher)
        if status == 403 and teacher is not opener:
            status, thread = self.api.call("POST", f"/lms/cohorts/{cohort['cohortId']}/threads", thread_body, user=opener)
        expect((status, thread), what="welcome thread")
        record = {"cohortId": cohort["cohortId"], "name": body["name"], "course": course, "tutor": teacher, "startsAt": start, "endsAt": end, "capacity": cohort_spec["capacity"], "price": course["price"]}
        self.cohorts.append(record)
        self.link("Cohort", f"{course['title']} — {body['name']}", f"/courses/{course['slug']}", f"{start:%d %b %Y} → {end:%d %b %Y}, capacity {cohort_spec['capacity']}, {scheduled.get('created')} live classes")
        return f"cohortId={cohort['cohortId']} {scheduled.get('created')} sessions"

    # -- 5. learners -----------------------------------------------------------------------------------------------------------
    def part_learners(self):
        print(f"\n== 5. Learners ({self.args.learners})")
        if not self.courses:
            self.failures.append({"step": "learners", "status": "skipped", "detail": "no published course"})
            return
        for i in range(self.args.learners):
            learner = User("learner", self.person(), self.next_phone(), dob=self.dob(1990, 2006))
            self.learners.append(learner)
            self.step(f"learner {i + 1}: {learner.name} signs up ({learner.phone}) and sets goals", self.learner_sign_up, learner, i)
            if not learner.token:
                continue
            course = self.courses[i % len(self.courses)]
            if i % 2 == 0:
                self.step(f"learner {i + 1}: buys '{course['title']}' by card", self.buy_by_card, learner, course)
            else:
                self.step(f"learner {i + 1}: buys units, then pays for '{course['title']}' with units", self.buy_with_units, learner, course)
        if self.learners and self.learners[0].token and self.tutors and self.tutors[0].token:
            self.step("learner 1: books a 1:1 session with tutor 1 (lock → booking → card)", self.book_session, self.learners[0], self.tutors[0])
        if len(self.learners) > 0 and self.learners[0].extra.get("enrolment"):
            self.step("learner 1: completes every lesson, passes the quiz, receives a certificate", self.complete_course, self.learners[0])
        if len(self.learners) > 1 and self.learners[1].extra.get("enrolment"):
            self.step("learner 2: partial progress and a quiz attempt", self.partial_progress, self.learners[1])
        if len(self.learners) > 2 and self.learners[2].extra.get("enrolment"):
            self.step("learner 3: progresses past 30% and reviews the course", self.review_course, self.learners[2])
        card_buyer = next((l for l in self.learners[2:] if l.extra.get("intent") and l.extra.get("paidBy") == "card"), None)
        if card_buyer:
            self.step(f"staff refunds 10% of {card_buyer.name}'s card purchase", self.refund_ten_percent, card_buyer)
        if len(self.learners) > 0 and self.learners[0].token:
            self.step("tutor 1 messages learner 1 (on-platform thread)", self.message_learner, self.tutors[0], self.learners[0])

    def learner_sign_up(self, learner: User, i: int):
        self.sign_up(learner)
        goal = CAREER_GOALS[i % len(CAREER_GOALS)]
        expect(self.api.call("PUT", "/identity/me/goals", {"careerGoal": goal, "interests": INTERESTS[i % len(INTERESTS)]}, user=learner), what="goals")
        learner.notes.append(goal)
        return f"goal: {goal}"

    def buy_by_card(self, learner: User, course: dict):
        quote_id, price, _ = self.quote_for(course["slug"], "self_paced", learner)
        intent = expect(self.api.call("POST", "/payments/intents", {"purpose": "course", "quoteId": quote_id}, user=learner), what="payment intent")
        self.confirm_charge(intent["paymentIntentId"], learner, price)
        enrolment = self.wait_enrolment(learner, course["courseId"])
        learner.extra.update({"enrolment": enrolment, "course": course, "intent": intent["paymentIntentId"], "paidBy": "card", "paid": price})
        self.purchases.append({"who": learner.name, "kind": "course (card)", "amount": price, "intent": intent["paymentIntentId"]})
        return f"{naira(price)}, enrolment {enrolment['enrolmentId'][:8]}"

    def buy_with_units(self, learner: User, course: dict):
        quote_id, price, _ = self.quote_for(course["slug"], "self_paced", learner)
        rate = expect(self.api.call("GET", "/wallet/rate"), what="unit rate")["rateKobo"]
        units = math.ceil(price / rate) + 5
        purchase = expect(self.api.call("POST", "/wallet/purchases", {"units": units}, user=learner), what="unit purchase")
        self.confirm_charge(purchase["paymentIntentId"], learner, purchase["amount"]["amount"])
        self.wait_until(lambda: expect(self.api.call("GET", "/wallet/me", user=learner))["balance"] >= units, f"{units} units for {learner.name}")
        paid = expect(self.api.call("POST", "/payments/intents", {"purpose": "course", "quoteId": quote_id, "payWithUnits": True}, user=learner), what="pay with units")
        enrolment = self.wait_enrolment(learner, course["courseId"])
        learner.extra.update({"enrolment": enrolment, "course": course, "intent": paid["paymentIntentId"], "paidBy": "units", "paid": price})
        self.purchases.append({"who": learner.name, "kind": f"units ({units})", "amount": purchase["amount"]["amount"], "intent": purchase["paymentIntentId"]})
        self.purchases.append({"who": learner.name, "kind": "course (units)", "amount": price, "intent": paid["paymentIntentId"]})
        return f"{units} units bought, {paid.get('unitsCharged')} units charged, enrolment {enrolment['enrolmentId'][:8]}"

    def book_session(self, learner: User, tutor: User):
        tutor_id = tutor.extra["tutorId"]
        slots = expect(self.api.call("GET", f"/tutors/{tutor_id}/availability?days=14", user=learner), what="tutor slots")
        earliest = utcnow() + dt.timedelta(hours=36)
        slot_start = None
        for slot in slots.get("slots", []):
            start = slot.get("startUtc") or slot.get("start")
            if not start:
                continue
            parsed = parse_iso(start)
            if parsed >= earliest and parsed.minute == 0:
                slot_start = parsed
                break
        if slot_start is None:
            slot_start = (earliest + dt.timedelta(days=1)).replace(hour=10, minute=0, second=0, microsecond=0)
        query = urllib.parse.urlencode({"tutorId": tutor_id, "slotStart": iso(slot_start), "durationMinutes": 60, "format": "one_to_one"})
        quote = expect(self.api.call("GET", f"/bookings/quote?{query}", user=learner), what="booking quote")
        price = quote["price"]["amount"]
        expect(self.api.call("POST", "/bookings/locks", {"tutorId": tutor_id, "slotStart": iso(slot_start), "durationMinutes": 60, "format": "one_to_one"}, user=learner), what="slot lock")
        booking = expect(self.api.call("POST", "/bookings", {"tutorId": tutor_id, "slotStart": iso(slot_start), "durationMinutes": 60, "format": "one_to_one", "note": "Please go through joins and window functions with my own dataset."}, user=learner), what="booking")
        self.confirm_charge(booking["paymentIntentId"], learner, price)
        self.wait_until(lambda: expect(self.api.call("GET", f"/bookings/{booking['bookingId']}", user=learner)).get("status") == "confirmed", "booking confirmation")
        upcoming = expect(self.api.call("GET", "/lms/live-sessions/upcoming", user=learner), what="upcoming live sessions")
        learner.extra["booking"] = {"id": booking["bookingId"], "slot": slot_start, "price": price}
        self.purchases.append({"who": learner.name, "kind": "1:1 session (card)", "amount": price, "intent": booking["paymentIntentId"]})
        self.link("Booking", f"{learner.name} with {tutor.name}", f"/learn/bookings/{booking['bookingId']}", f"{slot_start:%a %d %b %H:%M} UTC, {naira(price)}, confirmed")
        return f"{slot_start:%d %b %H:%M} UTC, {naira(price)}, {len(upcoming) if isinstance(upcoming, list) else '?'} upcoming live session(s)"

    def progress_items(self, lesson_ids: list[str], pct: int, when: dt.datetime) -> list[dict]:
        return [{"lessonId": lid, "status": "completed" if pct >= 100 else "inprogress", "positionSeconds": 0, "page": 3 if pct >= 100 else 1, "percentComplete": pct, "clientUpdatedAt": iso(when)} for lid in lesson_ids]

    def complete_course(self, learner: User):
        enrolment, course = learner.extra["enrolment"], learner.extra["course"]
        eid = enrolment["enrolmentId"]
        expect(self.api.call("GET", f"/lms/enrolments/{eid}/lessons/{course['lessons'][0]}", user=learner), what="lesson content")
        progress = expect(self.api.call("POST", f"/lms/enrolments/{eid}/progress", {"items": self.progress_items(course["lessons"], 100, utcnow() - dt.timedelta(minutes=5))}, user=learner), what="progress")
        attempt = expect(self.api.call("POST", f"/lms/enrolments/{eid}/lessons/{course['quizId']}/attempts", {}, user=learner), what="start quiz")
        expect(self.api.call("PUT", f"/lms/attempts/{attempt['attemptId']}/answers", {"answers": {"0": course["answers"]["0"]}}, user=learner), what="save answers")
        result = expect(self.api.call("POST", f"/lms/attempts/{attempt['attemptId']}/submit", {"answers": course["answers"]}, user=learner), what="submit quiz")
        certificate = None
        if result.get("enrolmentCompleted"):
            certificates = self.wait_until(lambda: [c for c in (expect(self.api.call("GET", "/certificates/me", user=learner)) or []) if c.get("enrolmentId") == eid], "certificate issuance")
            certificate = certificates[0]
            verify = expect(self.api.call("GET", f"/verify/{certificate['publicCode']}"), what="verify certificate")
            learner.extra["certificate"] = certificate
            self.link("Certificate", f"{learner.name} — {course['title']}", f"/verify/{certificate['publicCode']}", f"code {certificate['publicCode']}, {verify.get('status')}")
        return f"progress {progress.get('progressPct')}% → quiz {result.get('score')}% passed={result.get('passed')} completed={result.get('enrolmentCompleted')}" + (f", certificate {certificate['publicCode']}" if certificate else "")

    def partial_progress(self, learner: User):
        enrolment, course = learner.extra["enrolment"], learner.extra["course"]
        eid = enrolment["enrolmentId"]
        half = course["lessons"][: max(1, len(course["lessons"]) // 2)]
        items = self.progress_items(half, 100, utcnow() - dt.timedelta(minutes=20)) + self.progress_items(course["lessons"][len(half):len(half) + 1], 40, utcnow() - dt.timedelta(minutes=2))
        progress = expect(self.api.call("POST", f"/lms/enrolments/{eid}/progress", {"items": items}, user=learner), what="progress")
        attempt = expect(self.api.call("POST", f"/lms/enrolments/{eid}/lessons/{course['quizId']}/attempts", {}, user=learner), what="start quiz")
        wrong = {k: (v + 1) % 2 for k, v in course["answers"].items()}
        wrong["0"] = course["answers"]["0"]
        result = expect(self.api.call("POST", f"/lms/attempts/{attempt['attemptId']}/submit", {"answers": wrong}, user=learner), what="submit quiz")
        return f"progress {progress.get('progressPct')}%, quiz {result.get('score')}% passed={result.get('passed')}"

    def review_course(self, learner: User):
        enrolment, course = learner.extra["enrolment"], learner.extra["course"]
        eid = enrolment["enrolmentId"]
        progress = expect(self.api.call("POST", f"/lms/enrolments/{eid}/progress", {"items": self.progress_items(course["lessons"][:3], 100, utcnow() - dt.timedelta(minutes=10))}, user=learner), what="progress")
        review = expect(self.api.call("POST", f"/catalogue/courses/{course['courseId']}/reviews", {"enrolmentId": eid, "rating": 5, "body": "Clear, practical and paced for working people. The dashboard project was the highlight."}, user=learner), what="review")
        return f"progress {progress.get('progressPct')}%, rating average {review.get('courseRatingAverage')}"

    def refund_ten_percent(self, learner: User):
        amount = learner.extra["paid"] // 10
        refund = expect(self.api.call("POST", f"/admin/payments/intents/{learner.extra['intent']}/refunds", {"amount": {"amount": amount, "currency": "NGN"}, "reason": "Goodwill: lesson 2 video was late"}, user=self.staff), what="refund")
        learner.extra["refund"] = {"id": refund.get("refundId"), "amount": amount, "status": refund.get("status")}
        self.money["Refund issued"] = f"{naira(amount)} to {learner.name} ({refund.get('status')})"
        return f"{naira(amount)} {refund.get('status')}"

    def message_learner(self, tutor: User, learner: User):
        thread = expect(self.api.call("POST", "/notifications/threads", {"otherUserId": learner.user_id, "subject": "Welcome to the course", "body": "Great to have you. Post your first dashboard in the forum by Friday and I will review it."}, user=tutor), what="thread")
        expect(self.api.call("POST", f"/notifications/threads/{thread['threadId']}/messages", {"body": "Thank you! I will start with the sales dataset."}, user=learner), what="reply")
        self.link("Message thread", f"{tutor.name} ↔ {learner.name}", f"/learn/messages/{thread['threadId']}", "2 messages")
        return f"thread {thread['threadId'][:8]}"

    # -- 6. seat sponsorship -----------------------------------------------------------------------------------------------------
    def part_seats(self):
        print("\n== 6. Seat sponsorship")
        if not (self.sponsor and self.sponsor.mfa_secret and "id" in self.sponsor.extra["org"]):
            self.failures.append({"step": "seat sponsorship", "status": "skipped", "detail": "sponsor not ready"})
            return
        if not self.cohorts:
            self.failures.append({"step": "seat sponsorship", "status": "skipped", "detail": "no cohort"})
            return
        first = self.cohorts[0]
        block = self.step(f"Access Foundation buys 4 seats in '{first['name']}'", self.buy_seats, first, 4)
        if block:
            for learner in self.seat_recipients(first, 2):
                self.step(f"Access Foundation gives a seat to {learner.name}", self.allocate_seat, block, learner)
            invitee = User("learner (invited)", self.person(), self.next_phone(), dob=self.dob(1995, 2006))
            self.step(f"Access Foundation invites an unknown phone ({invitee.phone}); it signs up and the seat converts", self.invite_and_convert, block, invitee)
            self.step("Access Foundation releases 1 unallocated seat back to the wallet", self.release_seat, block, 1)
        if len(self.cohorts) > 1:
            second = self.cohorts[1]
            block2 = self.step(f"Access Foundation buys 2 seats in '{second['name']}'", self.buy_seats, second, 2)
            if block2:
                for recipient in self.seat_recipients(second, 1):
                    self.step(f"Access Foundation gives a seat to {recipient.name}", self.allocate_seat, block2, recipient)
        self.step("staff lists active seat blocks", self.admin_seat_blocks)

    def seat_recipients(self, cohort: dict, count: int) -> list[User]:
        """Learners with a token who are not enrolled in the cohort's course and have not had a seat yet, latest sign-ups first."""
        course_id = cohort["course"]["courseId"]
        candidates = [l for l in reversed(self.learners) if l.token and l.extra.get("course", {}).get("courseId") != course_id and not l.extra.get("seat")]
        chosen = candidates[:count]
        for learner in chosen:
            learner.extra["seat"] = cohort["cohortId"]
        return chosen

    def buy_seats(self, cohort: dict, seats: int) -> dict:
        org = self.sponsor.extra["org"]
        block = expect(self.api.call("POST", f"/sponsorship/organisations/{org['id']}/seat-blocks", {"cohortId": cohort["cohortId"], "seats": seats}, user=self.sponsor), what="seat block")
        block["cohort"] = cohort
        self.blocks.append(block)
        self.link("Seat block", f"{seats} seats · {cohort['course']['title']} — {cohort['name']}", f"/sponsor/seats/{block['blockId']}", f"{block.get('unitsRedeemed')} units, {naira(block.get('pricePerSeat', {}).get('amount'))} per seat")
        return block

    def allocate_seat(self, block: dict, learner: User):
        org = self.sponsor.extra["org"]
        allocation = expect(self.api.call("POST", f"/sponsorship/organisations/{org['id']}/seat-blocks/{block['blockId']}/allocations", {"phone": learner.phone, "displayName": learner.name}, user=self.sponsor), what="allocation")
        enrolment = self.wait_until(lambda: next((e for e in self.enrolments(learner) if e.get("cohortId") == block["cohort"]["cohortId"]), None), "sponsored enrolment")
        learner.notes.append(f"sponsored seat in {block['cohort']['name']}")
        return f"seat {allocation.get('seatNo')} {allocation.get('status')}, enrolment {enrolment['enrolmentId'][:8]} ({enrolment.get('fundingLabel')})"

    def invite_and_convert(self, block: dict, invitee: User):
        org = self.sponsor.extra["org"]
        path = f"/sponsorship/organisations/{org['id']}/seat-blocks/{block['blockId']}"
        invited = expect(self.api.call("POST", f"{path}/allocations", {"phone": invitee.phone, "displayName": invitee.name}, user=self.sponsor), what="invite")
        if invited.get("status") != "invited":
            raise StepError(f"expected an invite, got {invited.get('status')}", 201, invited)
        sms = expect(self.api.call("GET", "/dev/sms/last?" + urllib.parse.urlencode({"phone": invitee.phone})), what="invite SMS")
        expect(self.api.call("POST", f"{path}/allocations/{invited['allocationId']}/resend-invite", {}, user=self.sponsor), what="resend invite")
        self.sign_up(invitee)
        self.learners.append(invitee)
        converted = self.wait_until(lambda: next((a for a in expect(self.api.call("GET", path, user=self.sponsor)).get("allocations", []) if a.get("allocationId") == invited["allocationId"] and a.get("status") == "enrolled"), None), "invite conversion")
        self.wait_until(lambda: any(e.get("cohortId") == block["cohort"]["cohortId"] for e in self.enrolments(invitee)), "converted seat enrolment")
        invitee.notes.append("converted seat invite")
        return f"invite SMS: '{short(sms.get('message', ''), 70)}' → {invitee.name} signed up → seat {converted.get('seatNo')} enrolled"

    def release_seat(self, block: dict, seats: int):
        org = self.sponsor.extra["org"]
        released = expect(self.api.call("POST", f"/sponsorship/organisations/{org['id']}/seat-blocks/{block['blockId']}/release", {"seats": seats}, user=self.sponsor), what="release")
        wallet = expect(self.api.call("GET", f"/wallet/organisations/{org['id']}", user=self.sponsor), what="wallet")
        self.money["Access Foundation wallet"] = f"{wallet.get('balance')} units"
        return f"released {released.get('released')}, open {released.get('open')}, allocated {released.get('allocated')}; wallet {wallet.get('balance')} units"

    def admin_seat_blocks(self):
        blocks = expect(self.api.call("GET", "/admin/sponsorship/seat-blocks?status=active&limit=100", user=self.staff), what="admin seat blocks")
        self.link("Admin · seat blocks", f"{len(blocks)} active blocks", "/admin/seat-blocks")
        return f"{len(blocks)} active"

    # -- 7. money ops -----------------------------------------------------------------------------------------------------------------
    def part_money(self):
        print("\n== 7. Money operations")
        self.step("staff imports the gateway settlement report (entry 2)", self.settlement_import)
        self.step("staff runs the revenue sweep (entry 3)", self.sweep)
        self.step("jobs: booking-release and earnings-release", self.release_earnings)
        self.step("jobs: seat-block-close, monthly-fees, booking-reminders (as of now)", self.other_jobs)
        if self.provider and self.provider.mfa_secret and "id" in self.provider.extra["org"]:
            self.step("Lagos Data School registers a name-matched payout account", self.provider_payout_account)
        self.step("staff drafts a payout batch, super admin approves (maker–checker), staff submits", self.payout_batch)
        if self.batch:
            self.step("fake bank settles every payout in the batch (entry 4)", self.settle_batch)
        self.step("job: ledger reconciliation", self.reconcile)
        self.step("staff proposes a unit rate, super admin approves", self.unit_rate)
        self.step("ledger balances and the back-office dashboard", self.balances)

    def settlement_import(self):
        imported = expect(self.api.call("POST", "/admin/payments/settlements/import", {}, user=self.staff), what="settlement import")
        self.money["Settlement import"] = f"{imported.get('settled')} charges, gross {naira(imported.get('grossKobo'))}, fees {naira(imported.get('feesKobo'))}"
        return self.money["Settlement import"]

    def sweep(self):
        swept = expect(self.api.call("POST", "/admin/payments/sweeps/run", {}, user=self.staff), what="sweep")
        amount = swept.get("amount", {}).get("amount") if isinstance(swept.get("amount"), dict) else swept.get("amount")
        self.money["Revenue sweep"] = naira(amount)
        return naira(amount)

    def release_earnings(self):
        booking = self.run_job("booking-release")
        earnings = self.run_job("earnings-release")
        return f"booking {booking.get('result')}, earnings {earnings.get('result')}"

    def other_jobs(self):
        """The remaining jobs run as of now: nothing is due yet (cohorts start weeks ahead), which proves the routes without side effects."""
        results = {name: self.run_job(name).get("result") for name in ("seat-block-close", "monthly-fees", "booking-reminders")}
        return ", ".join(f"{k} {v}" for k, v in results.items())

    def provider_payout_account(self):
        org = self.provider.extra["org"]
        account = self.account_number()
        expect(self.api.call("GET", "/dev/bank/register?" + urllib.parse.urlencode({"accountNumber": account, "name": org["name"].upper()})), what="register bank account")
        result = expect(self.api.call("PUT", f"/payouts/organisations/{org['id']}/payout-account", {"bankCode": "058", "accountNumber": account}, user=self.provider), what="organisation payout account")
        earnings = expect(self.api.call("GET", f"/payouts/organisations/{org['id']}/earnings", user=self.provider), what="organisation earnings")
        self.money["Lagos Data School earnings"] = f"gross {naira(earnings.get('gross', {}).get('amount'))}, net {naira(earnings.get('net', {}).get('amount'))}, available {naira(earnings.get('available', {}).get('amount'))}"
        self.link("Provider payouts", org["name"], "/sponsor/payouts", self.money["Lagos Data School earnings"])
        return f"nameMatched={result.get('nameMatched')}; {self.money['Lagos Data School earnings']}"

    def payout_batch(self):
        queue = expect(self.api.call("GET", "/admin/payouts/queue", user=self.staff), what="payout queue")
        if queue.get("payoutsFrozen"):
            expect(self.api.call("POST", "/admin/ledger/reconciliation/unfreeze", {"reason": "Demo simulation: fake bank statement mirrors the ledger"}, user=self.staff), what="unfreeze")
        status, drafted = self.api.call("POST", "/admin/payouts/batches", {}, user=self.staff)
        if status == 409:
            raise StepError("no payable earnings for a batch yet", status, drafted)
        drafted = expect((status, drafted), what="draft batch")
        batch_id = drafted["batchId"]
        if not self.admin.token:
            raise StepError("super admin is not signed in; the batch stays drafted", None, drafted)
        expect(self.api.call("POST", f"/admin/payouts/batches/{batch_id}/approve", {}, user=self.admin), what="approve batch")
        expect(self.api.call("POST", f"/admin/payouts/batches/{batch_id}/submit", {}, user=self.staff), what="submit batch")
        self.batch = expect(self.api.call("GET", f"/admin/payouts/batches/{batch_id}", user=self.staff), what="read batch")
        total = sum(p.get("amount", {}).get("amount", 0) for p in self.batch.get("payouts", []))
        self.link("Payout batch", self.batch.get("reference") or batch_id, f"/admin/payouts/{batch_id}", f"{len(self.batch.get('payouts', []))} payouts, {naira(total)}")
        return f"{len(self.batch.get('payouts', []))} payouts totalling {naira(total)}, status {self.batch.get('status')}"

    def settle_batch(self):
        settled = 0
        for payout in self.batch.get("payouts", []):
            reference = payout.get("providerReference")
            if not reference or payout.get("status") not in ("submitted", "pending", None):
                continue
            self.settle_payout(reference, payout["amount"]["amount"], payout["payoutId"])
            settled += 1
        after = expect(self.api.call("GET", f"/admin/payouts/batches/{self.batch['batchId']}", user=self.staff), what="read batch")
        total = sum(p.get("amount", {}).get("amount", 0) for p in after.get("payouts", []))
        self.money["Payout batch"] = f"{settled} transfers settled, {naira(total)}, batch {after.get('status')}"
        return self.money["Payout batch"]

    def reconcile(self):
        result = self.run_job("reconciliation").get("result", {})
        status = expect(self.api.call("GET", "/admin/ledger/reconciliation", user=self.staff), what="reconciliation status")
        summary = f"assets {naira(result.get('assetsKobo'))} vs liabilities {naira(result.get('liabilitiesKobo'))}, shortfall={result.get('shortfall')}"
        if status.get("payoutsFrozen"):
            # A shortfall freezes payouts. Locally it comes from units granted by staff adjustments (no cash) being spent on seats, in
            # this run and in earlier ones on the same database; finance lifts the freeze so the demo payouts stay usable.
            expect(self.api.call("POST", "/admin/ledger/reconciliation/unfreeze", {"reason": "Demo simulation: shortfall is goodwill units (no cash) redeemed for seats on the local database"}, user=self.staff), what="unfreeze")
            summary += "; payouts were frozen and finance lifted the freeze"
        else:
            summary += "; payouts not frozen"
        self.money["Reconciliation"] = summary
        return summary

    def unit_rate(self):
        current = expect(self.api.call("GET", "/wallet/rate"), what="unit rate")["rateKobo"]
        proposed = expect(self.api.call("POST", "/admin/wallet/rates", {"rateKobo": current, "effectiveFrom": iso(utcnow() + dt.timedelta(days=30)), "rationale": "Quarterly review: hold the unit at its current value"}, user=self.staff), what="propose rate")
        if not self.admin.token:
            raise StepError("super admin not signed in; rate stays proposed", None, proposed)
        decided = expect(self.api.call("POST", f"/admin/wallet/rates/{proposed['rateId']}/decision", {"decision": "approved"}, user=self.admin), what="approve rate")
        self.link("Unit rate", f"{naira(current)} per unit (v{proposed.get('version')})", "/admin/unit-rates", decided.get("status", ""))
        return f"{naira(current)} per unit, version {proposed.get('version')} {decided.get('status')} (unchanged value, effective in 30 days)"

    def balances(self):
        accounts = {"1010": "Bank operating", "1020": "Bank client funds", "1030": "Gateway receivable", "2010": "Tutor payable", "2020": "Programme / seat funds held", "2050": "Unit balances held", "2060": "Provider payable", "4010": "Course commission"}
        for code, label in accounts.items():
            status, body = self.api.call("GET", f"/admin/ledger/accounts/{code}/balance", user=self.staff)
            if status == 200 and isinstance(body, dict):
                self.money[f"Ledger {code} {label}"] = naira(body.get("balance", {}).get("amount"))
        dashboard = expect(self.api.call("GET", "/admin/backoffice/dashboard", user=self.staff), what="dashboard")
        revenue = dashboard.get("revenue", {})
        self.money["Dashboard GMV"] = naira(revenue.get("gmv", {}).get("amount"))
        self.money["Dashboard course commission"] = naira(revenue.get("courseCommission", {}).get("amount"))
        self.link("Admin · finance", "Ledger balances and reconciliation", "/admin/finance")
        return f"GMV {self.money['Dashboard GMV']}, tutor payable {self.money.get('Ledger 2010 Tutor payable', '-')}"

    # -- 8. back office -------------------------------------------------------------------------------------------------------------------
    def part_backoffice(self):
        print("\n== 8. Back office")
        self.step("staff proposes a role grant, super admin approves (four-eyes)", self.role_grant)
        self.step("feature flag 'demo-mode' set and read back", self.feature_flag)
        self.step("audit log read", self.audit_log)
        if self.cohorts and self.cohorts[0]["tutor"].token:
            self.step("tutor announces to the cohort", self.announce)
        if self.learners and self.learners[0].token:
            self.step("job: notifications-delivery, then learner 1 reads the inbox", self.inbox, self.learners[0])

    def role_grant(self):
        candidate = next((u for u in self.users if u.role == "provider finance"), None) or (self.learners[0] if self.learners else None)
        if not candidate or not candidate.user_id:
            raise Skip("no candidate user")
        proposed = expect(self.api.call("POST", "/admin/identity/role-grants", {"userId": candidate.user_id, "role": "sponsor", "reason": "Runs seat sponsorship for the organisation"}, user=self.staff), what="role grant proposal")
        if not self.admin.token:
            raise StepError("super admin not signed in; proposal stays pending", None, proposed)
        decided = expect(self.api.call("POST", f"/admin/identity/role-grants/{proposed['proposalId']}/decision", {"decision": "approved"}, user=self.admin), what="role grant decision")
        self.link("Role grant", f"sponsor → {candidate.name}", "/admin/role-grants", decided.get("status", "approved") if isinstance(decided, dict) else "approved")
        return f"sponsor role for {candidate.name}: {decided.get('status', 'approved') if isinstance(decided, dict) else 'approved'}"

    def feature_flag(self):
        expect(self.api.call("PUT", "/admin/backoffice/feature-flags/demo-mode", {"enabled": True, "description": "Set by scripts/simulate.py"}, user=self.staff), what="feature flag")
        flags = expect(self.api.call("GET", "/admin/backoffice/feature-flags", user=self.staff), what="feature flags")
        self.link("Feature flags", f"{len(flags)} flags", "/admin/feature-flags")
        return f"{len(flags)} flags, demo-mode enabled"

    def audit_log(self):
        log = expect(self.api.call("GET", "/admin/backoffice/audit-log?limit=20", user=self.staff), what="audit log")
        items = log.get("items", [])
        self.link("Audit log", f"{len(items)} most recent entries", "/admin/audit")
        return f"{len(items)} entries on the first page"

    def announce(self):
        cohort = self.cohorts[0]
        announcement = expect(self.api.call("POST", f"/notifications/cohorts/{cohort['cohortId']}/announcements", {"title": "First live class", "body": f"We start on {cohort['startsAt']:%A %d %B} at 19:00 Lagos time. Bring a laptop with a spreadsheet tool installed."}, user=cohort["tutor"]), what="announcement")
        return f"announcement {str(announcement.get('announcementId', ''))[:8]} to '{cohort['name']}'"

    def inbox(self, learner: User):
        delivery = self.run_job("notifications-delivery")
        inbox = expect(self.api.call("GET", "/notifications/me", user=learner), what="inbox")
        items = inbox.get("items", [])
        if items:
            expect(self.api.call("POST", f"/notifications/{items[0]['notificationId']}/read", {}, user=learner), what="mark read")
        self.link("Learner inbox", learner.name, "/learn/notifications", f"{len(items)} notifications")
        return f"delivery {delivery.get('result')}, {len(items)} notifications, first marked read"

    # -- report -------------------------------------------------------------------------------------------------------------------------
    def report(self) -> str:
        elapsed = utcnow() - self.started
        lines = [f"# Plastoids demo simulation — {self.started:%Y-%m-%d %H:%M} UTC", ""]
        lines.append(f"Run `{self.run_id:05d}` against `{self.args.api}` · web app `{self.web}` · {self.steps_ok} steps ok, {len(self.failures)} failed or skipped · {elapsed.total_seconds():.0f}s · {self.api.requests} requests.")
        lines.append("")
        lines.append("Sign in with a TOTP app or `python3 scripts/totp.py <secret>` where an MFA secret is listed (development test data). "
                     "Phone accounts sign in by OTP: request the code, then read it from the API log (`[fake-sms]`) or `GET /api/v1/dev/sms/last?phone=`.")
        lines.append("")
        lines.append("## Accounts")
        lines.append("")
        lines.append("| Role | Name | Phone / email | Password | MFA secret | Sign in at | Notes |")
        lines.append("|---|---|---|---|---|---|---|")
        for user in [self.staff, self.admin] + [u for u in self.users if u not in (self.staff, self.admin)]:
            sign_in = {"staff": "/admin/auth/staff", "super admin": "/admin/auth/staff", "tutor": "/auth/sign-in → /tutor", "sponsor owner": "/sponsor/auth/sign-in", "provider owner": "/sponsor/auth/sign-in", "provider finance": "/sponsor/auth/sign-in", "provider author": "/sponsor/auth/sign-in"}.get(user.role, "/auth/sign-in → /learn")
            link = self.web + sign_in.split(" ")[0]
            notes = "; ".join(user.notes)
            secret = f"`{user.mfa_secret}`" if user.mfa_secret else "-"
            lines.append(f"| {user.role} | {user.name} | {user.contact} | {user.password or '-'} | {secret} | [{sign_in}]({link}) | {notes} |")
        lines.append("")
        lines.append("## What was created")
        lines.append("")
        lines.append("| Kind | Name | Link | Detail |")
        lines.append("|---|---|---|---|")
        for item in self.created:
            link = f"[{item['link'].replace(self.web, '')}]({item['link']})" if item["link"] else ""
            lines.append(f"| {item['kind']} | {item['name']} | {link} | {item['detail']} |")
        lines.append("")
        lines.append("Handy pages: "
                     + ", ".join(f"[{p}]({self.web}{p})" for p in ["/courses", "/courses?format=cohort", "/tutors", "/tutor/cohorts", "/learn/courses", "/learn/wallet", "/tutor/offers", "/tutor/earnings", "/sponsor/seats", "/sponsor/wallet", "/sponsor/courses", "/admin/seat-blocks", "/admin/payouts", "/admin/finance", "/admin/queues"]))
        lines.append("")
        lines.append("## Money")
        lines.append("")
        lines.append("| Item | Value |")
        lines.append("|---|---|")
        for key, value in self.money.items():
            lines.append(f"| {key} | {value} |")
        if self.purchases:
            lines.append("")
            lines.append("Payments made this run:")
            lines.append("")
            lines.append("| Who | What | Amount | Payment intent |")
            lines.append("|---|---|---|---|")
            for p in self.purchases:
                lines.append(f"| {p['who']} | {p['kind']} | {naira(p['amount'])} | `{p['intent']}` |")
        lines.append("")
        lines.append("## Failed or skipped steps")
        lines.append("")
        if not self.failures:
            lines.append("None.")
        else:
            lines.append("| Step | Status | Detail |")
            lines.append("|---|---|---|")
            for f in self.failures:
                lines.append(f"| {f['step']} | {f['status']} | {str(f['detail']).replace('|', '/')} |")
        lines.append("")
        return "\n".join(lines)

    def state(self) -> dict:
        return {
            "runId": self.run_id, "startedAt": iso(self.started), "api": self.args.api, "web": self.web,
            "accounts": [{"role": u.role, "name": u.name, "phone": u.phone, "email": u.email, "userId": u.user_id, "mfaSecret": u.mfa_secret, "notes": u.notes} for u in self.users],
            "courses": [{"courseId": c["courseId"], "slug": c["slug"], "title": c["title"], "price": c["price"]} for c in self.courses],
            "cohorts": [{"cohortId": c["cohortId"], "name": c["name"], "courseId": c["course"]["courseId"], "startsAt": iso(c["startsAt"]), "endsAt": iso(c["endsAt"])} for c in self.cohorts],
            "seatBlocks": [{"blockId": b["blockId"], "cohortId": b["cohort"]["cohortId"], "seats": b.get("seats")} for b in self.blocks],
            "payoutBatch": self.batch.get("batchId") if self.batch else None,
            "failures": self.failures,
        }

    # -- orchestration ------------------------------------------------------------------------------------------------------------------
    def preflight(self) -> bool:
        print(f"API {self.args.api} · web {self.web} · {self.args.learners} learners · phones +23480{self.run_id:05d}NNN · names seed {self.args.seed}")
        try:
            status, _, _ = self.api.raw("GET", "/wallet/rate")
        except StepError as e:
            print(f"[FAIL] {e}")
            return False
        if status != 200:
            print(f"[FAIL] GET /api/v1/wallet/rate → {status}; is the API running?")
            return False
        status, body, _ = self.api.raw("GET", "/dev/sms/last?phone=08031234567")
        # Mapped: 200 (a message exists) or our own 404 {code: sms_not_found}. Unmapped: the framework's bare 404.
        dev_ready = status == 200 or (status == 404 and isinstance(body, dict) and body.get("code") == "sms_not_found")
        print(f"[{'ok' if dev_ready else 'FAIL'}] /api/v1/dev/* {'is mapped' if dev_ready else 'is NOT mapped: restart the API in Development (make run) with the DevEndpoints change'}")
        return dev_ready

    def dry_run(self):
        print("\nDry run: the plan (nothing is written)")
        plan = [
            "1. staff + super admin sign in (password + TOTP)",
            f"2. {len(TUTOR_PROFILES)} tutors: sign up, apply, MFA, KYC, credential, demo lesson, staff approvals, availability, pricing, payout account",
            "3. Access Foundation (sponsor) and Lagos Data School (provider): owners with MFA, staff verification, finance + author members, units by card, a goodwill adjustment",
            f"4. {len(TUTOR_PROFILES)} tutor courses (one with a cohort) + 1 provider course with an engaged tutor and a cohort; staff reviews where required",
            f"5. {self.args.learners} learners: goals; half buy by card, half with units; one 1:1 booking; one completes a course → certificate; partial progress; a review; a 10% refund; a tutor–learner thread",
            "6. seat sponsorship: 2 seat blocks, allocations to existing learners, an invite that converts on sign-up, a release, staff list",
            "7. money ops: settlement import, sweep, booking-release + earnings-release jobs, provider payout account, payout batch (maker–checker), fake bank settlement, reconciliation, unit rate (four-eyes), balances",
            "8. back office: role grant (four-eyes), feature flag, audit log, cohort announcement, notifications delivery + inbox",
        ]
        for line in plan:
            print("  " + line)
        print(f"\nAccounts that would be created: {len(TUTOR_PROFILES)} tutors, 2 owners, 2 members, {self.args.learners + 1} learners; sample names: {self.person()}, {self.person()}, {self.person()}")
        print(f"Report would be written to {self.args.report}")

    def run(self) -> int:
        ready = self.preflight()
        if self.args.dry_run:
            self.dry_run()
            return 0 if ready else 1
        if not ready:
            return 1
        self.part_staff()
        self.part_tutors()
        self.part_organisations()
        self.part_catalogue()
        self.part_learners()
        self.part_seats()
        self.part_money()
        self.part_backoffice()

        report = self.report()
        path = pathlib.Path(self.args.report)
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(report, encoding="utf-8")
        path.with_suffix(".json").write_text(json.dumps(self.state(), indent=1, default=str), encoding="utf-8")
        print("\n" + report)
        print(f"Report written to {path} (and {path.with_suffix('.json')}).")
        return 0 if not [f for f in self.failures if f["status"] != "skipped"] else 2


def main(argv=None) -> int:
    here = pathlib.Path(__file__).resolve().parent
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--api", default=os.environ.get("PLASTOIDS_API", "http://localhost:5080"), help="API base URL (default http://localhost:5080)")
    parser.add_argument("--web", default=os.environ.get("PLASTOIDS_WEB", "http://localhost:5173"), help="web app base URL for the report's deep links (default http://localhost:5173)")
    parser.add_argument("--learners", type=int, default=6, help="number of learners to create (default 6; at least 3 recommended)")
    parser.add_argument("--seed", type=int, default=None, help="seed for reproducible names (phones and CAC numbers are always fresh)")
    parser.add_argument("--report", default=str(here / ".simulate" / "last-run.md"), help="where to write the Markdown report")
    parser.add_argument("--auth-limit", type=int, default=120, help="the API's RateLimiting:AuthPermitsPerMinute, used to pace /identity/auth calls (default 120)")
    parser.add_argument("--dry-run", action="store_true", help="check the API and print the plan without creating anything")
    parser.add_argument("--verbose", action="store_true", help="print every request and response")
    args = parser.parse_args(argv)
    if args.learners < 1:
        parser.error("--learners must be at least 1")
    return Simulation(args).run()


if __name__ == "__main__":
    try:
        sys.exit(main())
    except KeyboardInterrupt:
        print("\nInterrupted.")
        sys.exit(130)
