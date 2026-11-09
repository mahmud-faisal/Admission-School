# Functional verification, checkpoint 7

Scope: spec sections 1.2, 1.3, 1.5, 1.6, 1.7 and the CLAUDE.md invariants. Spec file unchanged (one commit, `git diff --quiet` clean).
Only Docker was used for the stack and for both test suites. No browser was available, so UI rules were verified by code reading plus Vitest results.

Result: 41 PASS, 0 FAIL, 2 PENDING (README.md and AI_USAGE.md, planned for checkpoint 8), 2 observations.

| # | Rule | How checked | Evidence | Result |
|---|---|---|---|---|
| 1 | Clean start | `docker compose down -v && up --build -d` | db, backend, frontend all `(healthy)`; no host tools needed | PASS |
| 2 | Frontend on 3000, API on 8080 | curl | `/` 200, `/health/ready` 200 | PASS |
| 3 | Swagger and Scalar load (non-Development) | `curl -L` | `/swagger` 200 (301 then 200), `/scalar` 200 (302 then 200) | PASS |
| 4 | POST /classes | curl | 201 `{id,name,totalSeats,...}` | PASS |
| 5 | Invalid class input | 7 bad bodies (empty name, 0, -1, missing fields, non-JSON, string seats) | all 400 ProblemDetails, code `VALIDATION_FAILED` | PASS |
| 6 | GET /classes, GET /classes/{id} | curl | id, name, totalSeats, heldSeats, confirmedSeats, availableSeats present; unknown id gives 404 `NOT_FOUND` | PASS |
| 7 | POST hold | curl | 201 `{holdId,classId,studentId,status:"HELD",expiresAt:"...Z"}` | PASS |
| 8 | GET /holds/{id}, 404 unknown | curl | 200 `{holdId,classId,status,expiresAt}`; unknown gives 404 | PASS |
| 9 | POST confirm | curl | 200 `{holdId,status:"CONFIRMED"}`; confirmedSeats 0 to 1 | PASS |
| 10 | DELETE | curl | 204; hold becomes CANCELLED; seat freed | PASS |
| 11 | GET /me/holds | curl | holdId, classId, className, status, expiresAt present; per-student isolation | PASS |
| 12 | X-Student-Id validation | empty, missing, 3 digits, 16, 18, trailing letter, on holds / confirm / me | all 400 `INVALID_STUDENT_ID` | PASS |
| 13 | 409 when full | class of 2 seats, third student | 409 `SEATS_FULL` | PASS |
| 14 | Duplicate application rejected | same student, same class, while HELD and after CONFIRMED | 409 `DUPLICATE_APPLICATION`; same student in another class gets 201 | PASS |
| 15 | Re-apply after cancel | cancel then hold | 201 | PASS |
| 16 | Re-apply after expiry | HOLD_DURATION_SECONDS=15, wait 17 s, apply again | 201 | PASS |
| 17 | Confirm only own | other student confirms or deletes | 403 `FORBIDDEN`; missing header gives 400 | PASS |
| 18 | Confirm only HELD | confirm a CANCELLED hold | 409 `HOLD_CANCELLED` | PASS |
| 19 | Confirm only non-expired | confirm after expiry | 409 `HOLD_EXPIRED` | PASS |
| 20 | Repeated confirm increments once | confirm 4 times | all 200, confirmedSeats stays 1, same confirmedAt | PASS |
| 21 | Cannot cancel a confirmed hold | DELETE on CONFIRMED | 409 `HOLD_ALREADY_CONFIRMED`; counts unchanged (extra rule, reasonable) | PASS |
| 22 | No oversell: 200 parallel holds, 5 seats (`xargs -P 100`) | 200 distinct students | 5 x 201, 195 x 409; held=5 | PASS |
| 23 | Same student, 60 parallel holds | 1 class | 1 x 201, 59 x 409; held=1 | PASS |
| 24 | Mixed parallel hold+confirm, 300 students, 20 seats | psql group-by | CONFIRMED 10 + HELD 10 = 20 = totalSeats | PASS |
| 25 | Two backend processes, same DB | `--profile bonus up`, `bonus-check` | 200 requests alternating backend/backend2: 10 x 201, 190 x 409; "PASS: exactly 10 holds" | PASS |
| 26 | HOLD_DURATION_SECONDS respected | `HOLD_DURATION_SECONDS=15 docker compose up -d backend`; `printenv` = 15 | expiresAt = createdAt + 15 s; default run shows 600 s | PASS |
| 27 | Expiry with no API call | hold taken, no calls for 17 s, then GET /classes | heldSeats 1 to 0; hold status EXPIRED | PASS |
| 28 | Lazy expiry independent of the sweeper | psql set `expires_at` into the past while row still HELD | GET /classes showed heldSeats 1 of 2 and GET /holds showed EXPIRED while the DB row was still `HELD`; sweeper flipped it to EXPIRED about 10 s later | PASS |
| 29 | DB clock used | code grep | `now()` / `clock_timestamp()` in HoldRepository and ClassRepository | PASS |
| 30 | DB-level guards | `\d holds` | partial unique index `ux_holds_class_student_active` (HELD, CONFIRMED); status and student_id CHECK constraints | PASS |
| 31 | Persistence | `docker compose down` then `up` (no -v) | `/classes` byte-identical before and after; holds present | PASS |
| 32 | Env vars and CORS | DB string via `ConnectionStrings__Default` (works; `DATABASE_URL` also supported in code); CORS preflight | `Access-Control-Allow-Origin` set for `http://localhost:3000`, absent for an unknown origin | PASS |
| 33 | Backend tests in Docker | `docker compose --profile test run --rm backend-tests` | 74 passed, 0 failed, including `TwoProcessOversellTests` and Expiry tests | PASS |
| 34 | Frontend tests in Docker | `... frontend-tests` | 9 files, 44 tests passed | PASS |
| 35 | Frontend build and `/env.js` | docker build; `curl /` | index.html has `<script src="/env.js">`; SPA fallback `/holds/h_1` returns 200 | PASS |
| 36 | Runtime API_BASE_URL override | `API_BASE_URL=http://example.test:9999 docker compose up -d frontend` | `/env.js` changed with no rebuild; reverted afterwards | PASS |
| 37 | UI polling and countdown (code) | `hooks.ts`, `HoldPage.tsx`, `Countdown.tsx` | classes polled every 3 s, hold and my-holds every 5 s; server-corrected countdown; on expiry shows EXPIRED, Pay and Cancel `disabled={!canAct}`, immediate refetch; Vitest "switches to EXPIRED and disables Pay without a reload" passes | PASS |
| 38 | Applications after refresh (code) | `StudentIdContext.tsx`, `MyHoldsPage.tsx` | student ID in localStorage, applications loaded from GET /me/holds; `/holds/:holdId` deep link works after refresh | PASS |
| 39 | Plain-language errors for every kind (code) | `errors.ts` | messages for SEATS_FULL, DUPLICATE, EXPIRED, CANCELLED, ALREADY_CONFIRMED, INVALID_STUDENT_ID, FORBIDDEN, NOT_FOUND, VALIDATION, 5xx, unknown, NETWORK; `errors.test.ts` 12 tests | PASS |
| 40 | Server down, no blank page (code) | `connection.ts`, `ConnectionBanner.tsx`, `client.ts`, `ErrorBoundary.tsx` in `main.tsx` | fetch failure or timeout raises NetworkError, banner "Can't reach the admission server..."; with backend stopped the API gave 000 while the frontend still served 200; `NetworkDown.test.tsx` and `ErrorBoundary.test.tsx` pass. Not seen in a real browser | PASS |
| 41 | Deliverables: backend/, frontend/, both Dockerfiles, root docker-compose.yml, ASSUMPTIONS.md, 56 commits | `ls`, `git log` | all present | PASS |
| 42 | README.md | file check | missing | PENDING (checkpoint 8) |
| 43 | AI_USAGE.md | file check | missing | PENDING (checkpoint 8) |

## Failures

None.

## Observations (not failures)

1. `GET /holds/{id}` needs no `X-Student-Id` and returns the studentId to any caller. The spec lists no header for this endpoint, so it is compliant, but worth a line in ASSUMPTIONS.md.
2. A space-padded `X-Student-Id` (" 2015...") is accepted because the HTTP layer trims header values. Standard behaviour; the stored ID is clean.

## State at end

Stack restored to defaults (HOLD_DURATION_SECONDS 600, API_BASE_URL http://localhost:8080). db, backend and frontend are healthy; backend2 and bonus-check were removed. adm-pg, port 8090 and assignment_frontend were not touched. The database still holds test classes and holds created during this audit.
