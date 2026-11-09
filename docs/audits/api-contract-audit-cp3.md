# API contract audit, checkpoint 3

Method: code read plus live calls against the API (Production, HOLD_DURATION_SECONDS=5) and /openapi/v1.json. The API process was stopped afterwards; adm-pg was left running.

## Summary
PASS 41, FAIL 0 blocking, 6 minor mismatches (M1-M6), all low severity.

## Pass table
| Area | Check | Result |
|---|---|---|
| Paths | All 8 endpoints at exact unprefixed paths, correct methods | PASS |
| 1 POST /classes | 201 + id,name,totalSeats (+heldSeats,confirmedSeats,availableSeats,createdAt extras); Location header; numbers are JSON numbers | PASS |
| 1 | 400 VALIDATION_FAILED for empty name, seats <=0 or >100000, missing body, malformed JSON, non-integer, overflow | PASS |
| 1 | text/plain gives 415 problem+json with code UNSUPPORTED_MEDIA_TYPE | PASS |
| 2 GET /classes | 200 array with all 6 spec fields, numeric | PASS |
| 3 GET /classes/{id} | 200 same shape; unknown id 404 NOT_FOUND | PASS |
| 4 POST /classes/{id}/holds | 201 holdId,classId,studentId,status=HELD,expiresAt (UTC, ends in Z) | PASS |
| 4 | 409 SEATS_FULL; 409 DUPLICATE_APPLICATION; 404 NOT_FOUND unknown class | PASS |
| 4 | X-Student-Id missing / 3 digits / 16 / 18 / letter gives 400 INVALID_STUDENT_ID | PASS |
| 5 GET /holds/{id} | 200 holdId,classId,status,expiresAt; no header needed; UPPERCASE status; EXPIRED shown after expiry with no job call; CANCELLED shown; 404 NOT_FOUND | PASS |
| 6 confirm | 200 {holdId,status:"CONFIRMED"} (+classId,expiresAt,confirmedAt); idempotent repeat 200; other student 403 FORBIDDEN; missing header 400; unknown 404; expired 409 HOLD_EXPIRED; cancelled 409 HOLD_CANCELLED | PASS |
| 7 DELETE | 204 empty body; repeat cancel 204; confirmed 409 HOLD_ALREADY_CONFIRMED; expired 409 HOLD_EXPIRED; other student 403; missing header 400; unknown 404 | PASS |
| 8 GET /me/holds | 200 array holdId,classId,className,status,expiresAt (+createdAt,confirmedAt); empty array for unknown student; missing/bad header 400 INVALID_STUDENT_ID | PASS |
| JSON | camelCase everywhere, case-insensitive request binding, nulls serialised | PASS |
| Timestamps | expiresAt like 2026-11-09T08:43:03Z (UTC, Z); other timestamps UTC with Z | PASS |
| Errors | Every error is application/problem+json with `code`, including 404 route (NOT_FOUND), 405 (METHOD_NOT_ALLOWED, Allow header), 415, unsupported api-version (UNSUPPORTED_API_VERSION) | PASS |
| Versioning | No version, `?api-version=1.0`, header `api-version: 1.0` all work on same paths; 9.0 gives 400 problem+json | PASS |
| Docs | /swagger (301 then 200), /scalar (302 then /scalar/v1 200), /openapi/v1.json 200 in Production | PASS |
| CORS | Preflight from http://localhost:3000 for POST with x-student-id,content-type: 204, Allow-Origin set, Allow-Headers echoed; Expose-Headers include X-Server-Time, Location | PASS |
| OpenAPI | All 8 operations, correct methods/paths, response codes 201/200/204/400/403/404/409, DTO schemas match wire field names, HoldStatus enum UPPERCASE | PASS |

## Mismatches
| # | Severity | Evidence | Suggested fix |
|---|---|---|---|
| M1 | Low | OpenAPI numeric fields are `type: ["integer","string"]` with a digit pattern (totalSeats, heldSeats, ..., ProblemDetails.status). Wire output is a real number, but the doc suggests strings are valid. The server really accepts `"totalSeats":"7"` (201). Spec says numbers. | Disable `JsonNumberHandling.AllowReadingFromString` in JsonSetup (and set none in the OpenAPI transformer), or accept as a deliberate leniency and note it in ASSUMPTIONS.md. |
| M2 | Low | OpenAPI ProblemDetails / ValidationProblemDetails schemas lack the extension members `code` and `traceId`, so clients reading the doc do not know about `code`. Also no 415 or 405 documented. | Add a custom ApiProblemDetails schema (with `code`) via an OpenAPI schema transformer, or a subclass used in ProducesResponseType. |
| M3 | Low | `X-Student-Id` is documented as optional nullable string in OpenAPI (`required` null) for endpoints 4, 6, 7, 8, though it is mandatory. Spec-driven test generators may omit it. | Mark it required in an operation transformer (the 400 is still enforced by the handler, so keep the string? binding). |
| M4 | Low | Nullable response fields (HoldDto.confirmedAt, cancelledAt; MyHoldDto.confirmedAt) are not marked nullable in OpenAPI, and are listed as required, yet are emitted as null. HoldConfirmedDto.confirmedAt is documented non-null (ok in practice). | Mark nullable in the schema transformer (`type: ["string","null"]`). |
| M5 | Low | OpenAPI 2xx responses list `text/plain` and `text/json` content types in addition to application/json. Actual server returns only application/json. | Add `[Produces("application/json")]` on controllers (or clear the extra formatters); an `Accept: text/plain` request still returns JSON/406 behaviour untested. |
| M6 | Cosmetic | Body-binding 400s (malformed JSON, wrong type, overflow) lack `type`, `instance`, `traceId` that other problem bodies have (they do have title, status, detail, errors, code). Content-Type also has `; charset=utf-8` unlike others. | Route ModelStateProblemFactory/JSON-error path through ProblemDetailsFactory so the same enrichment applies. |

## Notes (not mismatches)
- Extra fields (className, createdAt, serverTime, confirmedAt, cancelledAt, studentId on GET hold) are allowed by spec.
- expiresAt has second precision; HOLD_DURATION_SECONDS=5 gave 08:43:03Z for a hold created 08:42:58.38, so expiry is truncated up to 1 s early relative to the DB value only if rounding is used; not a contract issue but worth confirming the DB uses the same truncated value for expiry decisions (GET showed EXPIRED at 08:43:10, no precise boundary test done).
- 5xx path (GlobalExceptionHandler) not exercised live; read in code only.
- `Access-Control-Allow-Methods` for the preflight echoed only the requested method (POST); fine.
