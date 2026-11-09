# API contract audit, checkpoint 3 re-run

Method: same as the first audit. Live API in Production (HOLD_DURATION_SECONDS=5) against adm-pg, plus /openapi/v1.json. Code at commit 05e10b2 (fixes 2c44a69, c80f885, 330d0d2, f449f36). API stopped afterwards; adm-pg left running.

## Result
PASS 47, FAIL 0. All six earlier mismatches are fixed; no regressions found.

## Previous mismatches
| # | Status | Evidence |
|---|---|---|
| M1 | FIXED | `"totalSeats":"7"` now 400 VALIDATION_FAILED; 7.5 and 3000000000 also 400. OpenAPI numbers are `type: integer, format: int32` (ProblemDetails.status too). |
| M2 | FIXED | ProblemDetails and ValidationProblemDetails schemas now include required `code` and `traceId`; 415 documented on POST /classes. |
| M3 | FIXED | X-Student-Id `required: true` on endpoints 4, 6, 7, 8. |
| M4 | FIXED | confirmedAt, cancelledAt are `["null","string"]` in HoldDto, MyHoldDto, HoldConfirmedDto. |
| M5 | FIXED | Success responses list only application/json; errors only application/problem+json. |
| M6 | FIXED | Malformed JSON / wrong type / overflow 400s now carry type, instance, traceId, code. (Content-Type still has `; charset=utf-8` there; harmless.) |

## Full checklist (all PASS)
| Area | Check |
|---|---|
| Paths/methods | 8 endpoints at exact unprefixed paths |
| 1 POST /classes | 201 id,name,totalSeats(+extras), Location; 400 VALIDATION_FAILED (empty name, seats <=0, missing body, bad JSON, non-integer, string number, overflow); 415 UNSUPPORTED_MEDIA_TYPE |
| 2 GET /classes | 200 array with six spec fields, numeric |
| 3 GET /classes/{id} | 200; 404 NOT_FOUND |
| 4 POST holds | 201 holdId,classId,studentId,status HELD,expiresAt UTC Z; 409 SEATS_FULL; 409 DUPLICATE_APPLICATION; 404; X-Student-Id missing/16/17-with-letter 400 INVALID_STUDENT_ID |
| 5 GET /holds/{id} | 200 no header; EXPIRED after expiry with no job call; CANCELLED; CONFIRMED; 404 |
| 6 confirm | 200 {holdId,status CONFIRMED}; idempotent; 403 other student; 400 header; 404; 409 HOLD_EXPIRED; 409 HOLD_CANCELLED |
| 7 DELETE | 204; repeat 204; 409 HOLD_ALREADY_CONFIRMED; 409 HOLD_EXPIRED; 403; 400; 404 |
| 8 GET /me/holds | 200 array with holdId,classId,className,status,expiresAt; [] for unknown student; 400 on bad header |
| Wire format | camelCase, UPPERCASE status, numbers numeric, expiresAt ends in Z |
| Errors | All errors application/problem+json with `code`: 404 route, 405 (Allow header), 415, unsupported api-version (UNSUPPORTED_API_VERSION), 400/403/404/409 |
| Versioning | No version, ?api-version=1.0, header api-version both work; 9.0 gives 400 problem+json |
| Docs | /swagger, /scalar (/scalar/v1), /openapi/v1.json 200 in Production |
| CORS | Preflight from http://localhost:3000 allows x-student-id, content-type |
| OpenAPI | 8 operations, correct codes, schemas match wire names/types, HoldStatus UPPERCASE enum |

## Remaining observations (not mismatches)
- 405 and 5xx responses are not documented in OpenAPI (framework-level; optional).
- 5xx handler (GlobalExceptionHandler) not exercised live.
