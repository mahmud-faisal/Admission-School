# API contract audit, checkpoint 7 (final pre-submission)

Method: code read (controllers, DTO/JSON setup, error handling, OpenAPI), live calls against a private instance on
http://localhost:8090 (Production, HOLD_DURATION_SECONDS=5, shared PostgreSQL on 55432), OpenAPI doc
`/openapi/v1.json`, and a read of `frontend/src/api/*.ts` and the pages that consume it.
Decisions applied: cancel CONFIRMED = 409 HOLD_ALREADY_CONFIRMED; other student's hold = 403 FORBIDDEN;
cancel EXPIRED = 409 HOLD_EXPIRED; cancel CANCELLED = 204; GET /holds/{id} needs no header; duplicate names allowed;
no /api/v1 aliases.

Result: 58 PASS, 0 FAIL, 3 observations (all LOW, no spec violation).

## Endpoints (all with no api-version supplied)

| # | Check | Expected | Actual | Result |
|---|---|---|---|---|
| 1 | POST /classes path/status | 201 | 201, `Location: /classes/{id}` | PASS |
| 1 | fields | id, name, totalSeats | id, name, totalSeats (+heldSeats, confirmedSeats, availableSeats, createdAt); numbers are JSON numbers | PASS |
| 1 | validation | 400 ProblemDetails, code | blank name / 0 / 100001 / 1.5 / "5" / malformed JSON / empty body: 400 `VALIDATION_FAILED` with `errors` | PASS |
| 1 | wrong content type | 415 | 415 `UNSUPPORTED_MEDIA_TYPE` | PASS |
| 1 | duplicate names | allowed | two "AuditCP7" created, 201 each | PASS |
| 2 | GET /classes | 200 array | 200, array with id, name, totalSeats, heldSeats, confirmedSeats, availableSeats (+createdAt) | PASS |
| 3 | GET /classes/{id} | 200 object | 200, same fields | PASS |
| 3 | unknown id | 404 NOT_FOUND | 404 problem+json `NOT_FOUND` | PASS |
| 4 | POST /classes/{id}/holds | 201 | 201, `Location: /holds/{id}` | PASS |
| 4 | fields | holdId, classId, studentId, status, expiresAt | all present, camelCase; status `HELD`; expiresAt `2026-11-09T10:54:29Z` (UTC, Z) (+className, createdAt, serverTime) | PASS |
| 4 | no seat | 409 SEATS_FULL | 409 `SEATS_FULL` | PASS |
| 4 | same student again | 409 DUPLICATE_APPLICATION | 409 `DUPLICATE_APPLICATION` (checked before seats) | PASS |
| 4 | header missing / "123" / 16 digits + letter / 18 digits / Arabic-Indic digits | 400 INVALID_STUDENT_ID | 400 `INVALID_STUDENT_ID` with `errors` for each | PASS |
| 4 | unknown class | 404 | 404 `NOT_FOUND` | PASS |
| 4 | concurrency (20 parallel students, 2 seats) | 2x201, 18x409, held+confirmed<=total | 2x201, 18x409, held=2 | PASS |
| 5 | GET /holds/{id} | 200 | 200, no header needed; an invalid header ("abc") is ignored | PASS |
| 5 | fields | holdId, classId, status, expiresAt | all present (+studentId, className, createdAt, confirmedAt, cancelledAt, serverTime) | PASS |
| 5 | effective status | EXPIRED after expiry without API call to expire | HELD turned EXPIRED after 7 s; class seats back to available | PASS |
| 5 | unknown id | 404 NOT_FOUND | 404 `NOT_FOUND` | PASS |
| 6 | confirm own HELD | 200 `{holdId,status:"CONFIRMED"}` | 200, plus classId, expiresAt, confirmedAt | PASS |
| 6 | idempotent | 200, count unchanged | second confirm 200, same confirmedAt, confirmedSeats stayed 1 | PASS |
| 6 | missing header | 400 INVALID_STUDENT_ID | 400 | PASS |
| 6 | other student | 403 FORBIDDEN | 403 `FORBIDDEN` | PASS |
| 6 | unknown id | 404 | 404 `NOT_FOUND` | PASS |
| 6 | expired | 409 HOLD_EXPIRED | 409 `HOLD_EXPIRED` | PASS |
| 6 | cancelled | 409 HOLD_CANCELLED | 409 `HOLD_CANCELLED` | PASS |
| 7 | DELETE own HELD | 204 empty | 204 | PASS |
| 7 | already cancelled | 204 | 204 | PASS |
| 7 | CONFIRMED | 409 HOLD_ALREADY_CONFIRMED | 409 `HOLD_ALREADY_CONFIRMED` | PASS |
| 7 | EXPIRED | 409 HOLD_EXPIRED | 409 `HOLD_EXPIRED` | PASS |
| 7 | other student | 403 FORBIDDEN | 403 | PASS |
| 7 | missing header | 400 | 400 `INVALID_STUDENT_ID` | PASS |
| 7 | unknown id | 404 | 404 `NOT_FOUND` | PASS |
| 8 | GET /me/holds | 200 array | 200, newest first, all statuses | PASS |
| 8 | fields | holdId, classId, className, status, expiresAt | all present (+createdAt, confirmedAt) | PASS |
| 8 | missing header | 400 | 400 `INVALID_STUDENT_ID` | PASS |
| 8 | student with none | [] | other student sees only own holds (isolation OK) | PASS |

## Cross-cutting

| Check | Expected | Actual | Result |
|---|---|---|---|
| Paths unprefixed | exact spec paths | all 8 routes unprefixed; OpenAPI lists exactly the 8 | PASS |
| No `/api/v1` aliases | not present | not present | PASS |
| api-version absent | works | works everywhere (default 1.0) | PASS |
| `?api-version=1.0` and `api-version: 1.0` header | work | both 200 | PASS |
| `api-version=2.0` | 400 | 400 `UNSUPPORTED_API_VERSION` | PASS |
| camelCase JSON | yes | yes, including OpenAPI schema | PASS |
| Status UPPERCASE | HELD/CONFIRMED/EXPIRED/CANCELLED | yes; enum in OpenAPI | PASS |
| Timestamps UTC with Z | yes | all end in Z | PASS |
| Numbers as numbers | yes | integers in JSON, strict number parsing on input | PASS |
| ProblemDetails with `code` | yes | all errors are `application/problem+json` with `code` | PASS |
| 404 unknown route / 405 | ProblemDetails + code | `NOT_FOUND`, `METHOD_NOT_ALLOWED` (+Allow) | PASS |
| CORS | frontend origin | preflight for POST and DELETE with x-student-id allowed for http://localhost:3000 | PASS |
| OpenAPI | /openapi/v1.json, 3.1 | served; 8 paths; X-Student-Id pattern `^[0-9]{17}$` | PASS |
| Swagger UI and Scalar in Production | 200 | `/swagger/index.html` 200, `/scalar` 302 then 200 | PASS |
| Health | 200 | `/health`, `/health/ready` 200 | PASS |

## Frontend use of the API (`frontend/src/api/*.ts`)

| Check | Expected | Actual | Result |
|---|---|---|---|
| GET /classes | exact path | `fetchJson('/classes')` | PASS |
| POST /classes | body `{name,totalSeats}` | `/classes` POST, JSON body | PASS |
| POST apply | `/classes/{id}/holds` + X-Student-Id | exact, header `X-Student-Id` | PASS |
| GET hold | `/holds/{id}` no header | exact, no header sent | PASS |
| Confirm | `/holds/{id}/confirm` POST + header | exact | PASS |
| Cancel | `DELETE /holds/{id}` + header, handles 204 | exact; empty body handled | PASS |
| GET /me/holds | + header | exact | PASS |
| No `/api/v1`, no api-version needed | none used | none used | PASS |
| Types parse exact fields | match spec names | `types.ts` matches the live JSON; extras optional | PASS |
| Error handling | read `code` | `ApiError.code` from ProblemDetails; messages for all 8 codes | PASS |
| Base URL | runtime `/env.js` then build-time | `config.ts`, default http://localhost:8080 | PASS |
| Status display | UPPERCASE | compares to `'HELD'`, `'EXPIRED'` etc. | PASS |

## Mismatches

None against the spec or the confirmed decisions. Observations (LOW, no change required):

1. LOW, docs. OpenAPI lists a single `409` response per endpoint and does not name the possible `code` values
   (SEATS_FULL, DUPLICATE_APPLICATION, HOLD_EXPIRED and so on). Evidence: `/openapi/v1.json`,
   `DELETE /holds/{holdId}` 409 has only description "Conflict". Fix (optional): add the codes to the response
   descriptions in `OpenApi/ContractTransformers.cs`.
2. LOW, consistency. `api-supported-versions` response header is missing on some 400 responses produced by model
   binding (for example malformed JSON). No contract impact.
3. LOW, data. `expiresAt` has whole-second precision while `createdAt`/`serverTime` have microseconds. Both are valid
   ISO-8601 UTC with Z, and the spec example is whole-second.

Cleanup: the 8090 instance was stopped (verified no listener). Test rows (classes named AuditCP7, CP7b, CP7c, Conc) remain in the shared `adm-pg` database.
