# Performance results — read endpoints (Sprint 9)

**Status: not yet run.** The table below is the template the k6 script fills in; replace it with the rows from `perf/results/<timestamp>.md` after a run against the Docker stack.

Target (docs/Build_Prompt.md, Sprint 9): 5,000 concurrent simulated learners; API p95 < 300 ms for reads against the Docker stack (`make up` + `make run`).

## How to produce the numbers

Seed the data and mint the tokens as described in `perf/README.md`, then:

```bash
# API started with the per-IP rate limiter lifted and Development seed data (see perf/README.md):
RateLimiting__GlobalTokensPerMinute=100000000 RateLimiting__AuthPermitsPerMinute=100000 \
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Plastoids.Api/Plastoids.Api.csproj -c Release

# In another shell:
ulimit -n 65536
mkdir -p perf/results
BASE_URL=http://localhost:5080 MAX_VUS=5000 \
LEARNER_TOKEN=... ENROLMENT_ID=... LESSON_ID=... \
SPONSOR_TOKEN=... ORG_ID=... PROGRAMME_ID=... \
k6 run perf/load-test.js
```

The script prints a Markdown table at the end and writes it to `perf/results/<timestamp>.md`; paste those rows into the table below and fill in the environment block.

## Environment

| Item | Value |
|---|---|
| Date | not yet run |
| Commit | not yet run |
| Host (CPU / RAM) | not yet run |
| API | `dotnet run -c Release`, `ASPNETCORE_ENVIRONMENT=Development`, rate limiter lifted |
| Stack | `docker compose` MySQL 8.4 (`innodb-buffer-pool-size=512M`), Kafka 3.9, Redis 7, S3Mock |
| k6 version | not yet run |
| Dataset | courses: ?, opportunities: ?, enrolments: ? (see `perf/README.md`) |

## Results

| Endpoint | VUs | p50 (ms) | p95 (ms) | p99 (ms) | Error rate | Notes |
|---|---|---|---|---|---|---|
| SearchCourses (`GET /api/v1/catalogue/courses`) | 5000 | – | – | – | – | not yet run |
| ListOpportunities (`GET /api/v1/sponsorship/opportunities`) | 5000 | – | – | – | – | not yet run |
| GetLessonContent (`GET /api/v1/lms/enrolments/{id}/lessons/{lessonId}`) | 5000 | – | – | – | – | not yet run |
| GetProgrammeFunnel (`GET /api/v1/sponsorship/organisations/{orgId}/programmes/{programmeId}/funnel`) | 50 | – | – | – | – | not yet run (sponsor scenario, 50 VUs) |

Threshold per endpoint: p95 < 300 ms; overall `http_req_failed` < 1 %.

## Observations and follow-ups

- (fill in) bottlenecks seen (MySQL CPU, FULLTEXT search on `catalogue_courses`, Kestrel thread pool, Redis quote cache, signed-URL generation in GetLessonContent).
- (fill in) changes made as a result, and the re-run numbers.
- Known caveats before the first run:
  - The per-IP rate limiter (`src/Plastoids.Api/Program.cs`, 300 tokens/min globally, 10/min on `/auth/*`) must be lifted for a single-host load generator, otherwise everything returns 429.
  - Access tokens expire after 15 minutes (`Auth:AccessTokenMinutes`); the default ramp is 11 minutes.
  - The Docker MySQL is configured with `max-connections=300`; the API uses one connection per scope, so a connection-pool wait shows up as latency, not errors.
