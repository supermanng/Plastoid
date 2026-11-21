// k6 load test for the Plastoids read endpoints named in the Sprint 9 prompt (docs/Build_Prompt.md):
//   GET /api/v1/catalogue/courses                                                     SearchCourses      (anonymous)
//   GET /api/v1/sponsorship/opportunities                                             ListOpportunities  (anonymous)
//   GET /api/v1/lms/enrolments/{enrolmentId}/lessons/{lessonId}                       GetLessonContent   (learner bearer)
//   GET /api/v1/sponsorship/organisations/{orgId}/programmes/{programmeId}/funnel     GetProgrammeFunnel (sponsor bearer, org member)
//
// Target: 5,000 concurrent simulated learners, p95 < 300 ms per read endpoint, error rate < 1 %.
// Everything is driven by environment variables; see perf/README.md for seeding and the exact command.
//
// Routes verified against:
//   src/Modules/Catalogue/Features/Discovery/SearchCourses/SearchCoursesEndpoint.cs
//   src/Modules/Sponsorship/Features/Opportunities/ListOpportunities/ListOpportunities.cs
//   src/Modules/Lms/Features/Learning/GetLessonContent/GetLessonContentQuery.cs
//   src/Modules/Sponsorship/Features/Campaigns/GetProgrammeFunnel/GetProgrammeFunnel.cs

import http from 'k6/http';
import { check, sleep } from 'k6';
import { textSummary } from 'https://jslib.k6.io/k6-summary/0.1.0/index.js';

// ---- configuration (env) ---------------------------------------------------------------------------------------
const BASE_URL = (__ENV.BASE_URL || 'http://localhost:5080').replace(/\/+$/, '');
const MAX_VUS = parseInt(__ENV.MAX_VUS || '5000', 10);
const SPONSOR_VUS = parseInt(__ENV.SPONSOR_VUS || '50', 10);
const THINK_SECONDS = parseFloat(__ENV.THINK_SECONDS || '1');

const LEARNER_TOKEN = __ENV.LEARNER_TOKEN || '';
const ENROLMENT_ID = __ENV.ENROLMENT_ID || '';
const LESSON_ID = __ENV.LESSON_ID || '';

const SPONSOR_TOKEN = __ENV.SPONSOR_TOKEN || '';
const ORG_ID = __ENV.ORG_ID || '';
const PROGRAMME_ID = __ENV.PROGRAMME_ID || '';

const SEARCH_TERMS = (__ENV.SEARCH_TERMS || 'data,sql,python,design,marketing,excel').split(',').map((s) => s.trim()).filter(Boolean);
const SEARCH_FILTERS = ['', '&funding=sponsored', '&level=beginner', '&format=self_paced', '&maxPrice=5000000'];

const hasLesson = Boolean(LEARNER_TOKEN && ENROLMENT_ID && LESSON_ID);
const hasFunnel = Boolean(SPONSOR_TOKEN && ORG_ID && PROGRAMME_ID);

// Ramp: 0 → 1,000 in 2 min, → MAX_VUS in 3 min, hold 5 min, down 1 min = 11 min total.
// Access tokens live 15 minutes (Auth:AccessTokenMinutes), so mint them right before starting.
const STAGES = [
  { duration: __ENV.RAMP1 || '2m', target: Math.min(1000, MAX_VUS) },
  { duration: __ENV.RAMP2 || '3m', target: MAX_VUS },
  { duration: __ENV.HOLD || '5m', target: MAX_VUS },
  { duration: __ENV.RAMP_DOWN || '1m', target: 0 },
];

const thresholds = {
  'http_req_duration{endpoint:SearchCourses}': ['p(95)<300'],
  'http_req_duration{endpoint:ListOpportunities}': ['p(95)<300'],
  http_req_failed: ['rate<0.01'],
  checks: ['rate>0.99'],
};
if (hasLesson) {
  thresholds['http_req_duration{endpoint:GetLessonContent}'] = ['p(95)<300'];
}
if (hasFunnel) {
  thresholds['http_req_duration{endpoint:GetProgrammeFunnel}'] = ['p(95)<300'];
}

const scenarios = {
  learners: {
    executor: 'ramping-vus',
    exec: 'learner',
    startVUs: 0,
    stages: STAGES,
    gracefulRampDown: '30s',
    gracefulStop: '30s',
  },
};
if (hasFunnel) {
  scenarios.sponsors = {
    executor: 'constant-vus',
    exec: 'sponsor',
    vus: SPONSOR_VUS,
    duration: STAGES.reduce((total, s) => total + toSeconds(s.duration), 0) + 's',
    gracefulStop: '30s',
  };
}

export const options = {
  scenarios,
  thresholds,
  // Bodies are not inspected (status codes only), which keeps 5,000 VUs within a laptop's memory.
  discardResponseBodies: true,
  summaryTrendStats: ['avg', 'min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
  userAgent: 'plastoids-k6-load-test/1.0',
  tags: { suite: 'sprint9-reads' },
};

// ---- helpers ----------------------------------------------------------------------------------------------------
function toSeconds(duration) {
  const m = /^(\d+)(ms|s|m|h)$/.exec(duration);
  if (!m) return 0;
  const n = parseInt(m[1], 10);
  return { ms: n / 1000, s: n, m: n * 60, h: n * 3600 }[m[2]];
}

function pick(list) {
  return list[Math.floor(Math.random() * list.length)];
}

function params(endpoint, token) {
  const headers = { Accept: 'application/json' };
  if (token) headers.Authorization = `Bearer ${token}`;
  return { headers, tags: { endpoint, name: endpoint }, timeout: '30s' };
}

function think() {
  if (THINK_SECONDS > 0) sleep(THINK_SECONDS * (0.5 + Math.random()));
}

// ---- scenarios --------------------------------------------------------------------------------------------------
export function setup() {
  // Fail fast if the API is not up; also warms the search index cache.
  const live = http.get(`${BASE_URL}/health/live`, { tags: { endpoint: 'setup', name: 'setup' } });
  if (live.status !== 200) {
    throw new Error(`API not reachable at ${BASE_URL} (GET /health/live → ${live.status}). See perf/README.md.`);
  }
  if (!hasLesson) {
    console.warn('LEARNER_TOKEN / ENROLMENT_ID / LESSON_ID not set: GetLessonContent is skipped.');
  }
  if (!hasFunnel) {
    console.warn('SPONSOR_TOKEN / ORG_ID / PROGRAMME_ID not set: GetProgrammeFunnel is skipped.');
  }
  return { startedAt: new Date().toISOString() };
}

// One "learner" iteration: browse the catalogue, look at sponsored opportunities, then open a lesson.
export function learner() {
  const q = encodeURIComponent(pick(SEARCH_TERMS));
  const search = http.get(`${BASE_URL}/api/v1/catalogue/courses?q=${q}&limit=20${pick(SEARCH_FILTERS)}`, params('SearchCourses'));
  check(search, { 'SearchCourses 200': (r) => r.status === 200 });
  think();

  const opportunities = http.get(`${BASE_URL}/api/v1/sponsorship/opportunities?limit=20`, params('ListOpportunities'));
  check(opportunities, { 'ListOpportunities 200': (r) => r.status === 200 });
  think();

  if (hasLesson) {
    const lesson = http.get(
      `${BASE_URL}/api/v1/lms/enrolments/${ENROLMENT_ID}/lessons/${LESSON_ID}`,
      params('GetLessonContent', LEARNER_TOKEN),
    );
    check(lesson, { 'GetLessonContent 200': (r) => r.status === 200 });
    think();
  }
}

// Sponsors poll the funnel of a live programme (far fewer of them than learners).
export function sponsor() {
  const funnel = http.get(
    `${BASE_URL}/api/v1/sponsorship/organisations/${ORG_ID}/programmes/${PROGRAMME_ID}/funnel`,
    params('GetProgrammeFunnel', SPONSOR_TOKEN),
  );
  check(funnel, { 'GetProgrammeFunnel 200': (r) => r.status === 200 });
  sleep(2 + Math.random() * 3);
}

// ---- reporting --------------------------------------------------------------------------------------------------
// Writes perf/results/<timestamp>.json (raw) and perf/results/<timestamp>.md (rows for docs/perf.md).
export function handleSummary(data) {
  const ts = new Date().toISOString().replace(/[:.]/g, '-');
  const endpoints = ['SearchCourses', 'ListOpportunities', 'GetLessonContent', 'GetProgrammeFunnel'];
  const rows = endpoints.map((endpoint) => {
    const m = data.metrics[`http_req_duration{endpoint:${endpoint}}`];
    const reqs = data.metrics[`http_reqs{endpoint:${endpoint}}`];
    const failed = data.metrics[`http_req_failed{endpoint:${endpoint}}`];
    if (!m) {
      return `| ${endpoint} | ${MAX_VUS} | – | – | – | – | skipped (env vars not set) |`;
    }
    const v = m.values;
    const count = reqs ? reqs.values.count : '–';
    const errorRate = failed ? `${(failed.values.rate * 100).toFixed(2)} %` : '–';
    const passed = m.thresholds ? Object.values(m.thresholds).every((t) => t.ok) : true;
    return `| ${endpoint} | ${endpoint === 'GetProgrammeFunnel' ? SPONSOR_VUS : MAX_VUS} | ${v.med.toFixed(1)} | ${v['p(95)'].toFixed(1)} | ${v['p(99)'].toFixed(1)} | ${errorRate} | ${count} requests, ${passed ? 'threshold met' : 'THRESHOLD FAILED'} |`;
  });

  const markdown = [
    `Run: ${ts}  BASE_URL=${BASE_URL}  MAX_VUS=${MAX_VUS}  stages=${JSON.stringify(STAGES)}`,
    '',
    '| Endpoint | VUs | p50 (ms) | p95 (ms) | p99 (ms) | Error rate | Notes |',
    '|---|---|---|---|---|---|---|',
    ...rows,
    '',
  ].join('\n');

  return {
    stdout: textSummary(data, { indent: ' ', enableColors: true }) + '\n' + markdown,
    [`perf/results/${ts}.json`]: JSON.stringify(data, null, 2),
    [`perf/results/${ts}.md`]: markdown,
  };
}
