# Functional verification, checkpoint 8 (final)

Run from a clean state: `docker compose down -v`, `docker compose build --no-cache`, `up -d`. Docker only. No browser available; UI rules rely on the checkpoint 7 code review plus Vitest (unchanged areas) and the 44 passing frontend tests.
Spec check: `git diff --stat 4701d13 HEAD -- School_Admission_System_Assignment_EN.md` printed nothing.

Result: 24 PASS, 1 FAIL (documentation count), 0 blocking issues.

| # | Rule | How checked | Evidence | Result |
|---|---|---|---|---|
| 1 | Clean rebuild, all healthy | `build --no-cache`, `up -d` | db, backend, frontend `(healthy)` | PASS |
| 2 | README URLs work | curl -L | `:3000` 200, `/classes` 200, `/swagger` 200, `/scalar` 200, `/openapi/v1.json` 200 | PASS |
| 3 | README run command | `docker compose up --build` equivalent (build + up) | works as written | PASS |
| 4 | README backend test command | `docker compose --profile test run --rm backend-tests` | 68 unit passed, 74 integration passed; README says 75 integration | FAIL |
| 5 | README frontend test command | `... frontend-tests` | 44 passed, 9 files (README says 44) | PASS |
| 6 | README named concurrency tests exist | grep | all 5 named tests found in `Concurrency/` | PASS |
| 7 | README bonus commands | `--profile bonus up -d --build`, `bonus-check` | "PASS: exactly 10 holds were granted across two processes" | PASS |
| 8 | All 8 endpoints, status codes, errors (400/403/404/409) | curl script rerun | same results as cp7: 403 other student, 400 no header, 409 DUPLICATE_APPLICATION, 409 HOLD_ALREADY_CONFIRMED | PASS |
| 9 | Repeated confirm increments once | confirm x4 | all 200, confirmedSeats 1 | PASS |
| 10 | No oversell, 200 parallel holds, 5 seats | `xargs -P 100` | 5 x 201, 195 x 409, held=5 | PASS |
| 11 | Two backend processes | bonus-check | 10 granted of 200 | PASS |
| 12 | HOLD_DURATION_SECONDS=15 with rounding | hold created 11:43:59.886, expiresAt 11:44:15Z | duration 15.1 s (between 15 and 16 s) | PASS |
| 13 | Still HELD shortly before end | GET at t+12 s | status HELD, heldSeats 1 | PASS |
| 14 | EXPIRED after the end, no API call | GET at t+16 s; separate hold with no calls for 18 s | EXPIRED, heldSeats 0, availableSeats 1 | PASS |
| 15 | Re-apply after expiry or cancel | cp7 flow plus second applicant after expiry | 201 | PASS |
| 16 | Persistence across `down` / `up` | md5 of `/classes` and holds row count | identical (hash equal, 29 holds before and after) | PASS |
| 17 | Final state clean | `down -v`, `up -d` | `/classes` returns `[]`, 3 healthy containers | PASS |
| 18 | Spec unchanged | `git diff --stat 4701d13 HEAD` | empty output | PASS |

## Section 1.7 checklist

| Item | Result | Notes |
|---|---|---|
| Git repo, commit history, `backend/` and `frontend/` | PASS | many small commits, working tree clean |
| Dockerfile for backend and frontend, root docker-compose.yml | PASS | both build from scratch |
| README.md: Docker steps, frontend URL, how to run tests | PASS | all commands work; one wrong count (see FAIL 1) |
| ASSUMPTIONS.md (short) | PASS | 97 lines, sections for two-process safety, API behaviour, time/expiry/IDs, frontend, infrastructure |
| At least 3 backend tests including a concurrency test | PASS | 142 backend tests; 200-parallel-hold test (5 x 201 on 5 seats reproduced by curl as well) |
| AI_USAGE.md (tools, parts, mistakes, how caught) | PASS with note | covers tools, parts and 9 mistakes with how caught; still marked DRAFT with `[you]` placeholders that the author must fill in before submitting |

## Failures

1. README says "75 integration tests" (section "Run the tests") but `backend-tests` runs 74 integration tests (68 unit + 74 integration = 142). Reproduce: `docker compose --profile test run --rm backend-tests` and read "Total tests" for each assembly. Fix: change 75 to 74 in `E:\SchoolAssignment\README.md`, or add the missing test if 75 was intended (the coordinator expected 75 after the new expiry test; the build contains `Expiry_HoldLastsAtLeastHoldDurationSeconds`).

## Observations

- `GET /holds/{id}` needs no X-Student-Id (spec lists none); documented in cp7.
- AI_USAGE.md cites "functional-verifier 41 PASS / 0 FAIL"; that matches the cp7 report.
