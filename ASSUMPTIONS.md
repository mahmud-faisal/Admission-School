# Assumptions

Where the spec is silent or unclear, this is what I decided and why.

## How two backend processes never oversell (bonus §1.6)

The rule `held + confirmed <= totalSeats` is enforced **inside PostgreSQL**, never in application memory. So it
holds for any number of backend processes on the same database. The `bonus` compose profile runs two of them,
and a test starts two real OS processes against one database.

1. **Apply** (`POST /classes/{id}/holds`) is one READ COMMITTED transaction:
   `SELECT … FROM classes WHERE id = @c FOR UPDATE` → read `clock_timestamp()` → mark this class's stale holds
   EXPIRED → check for a duplicate → count `HELD (not expired) + CONFIRMED` → insert, or return 409.
   The row lock is held by PostgreSQL until COMMIT. A transaction from *any* process that wants the same class waits
   at the first statement. Under READ COMMITTED, each later statement takes a fresh snapshot, so the waiting
   transaction then sees the first one's committed insert. That makes "count, then insert" atomic per class.
   REPEATABLE READ would count from a stale snapshot, so it is deliberately not used.
2. **Confirm** and **cancel** are single conditional UPDATEs
   (`… WHERE id=@h AND student_id=@s AND status='HELD' AND expires_at > now()`). HELD → CONFIRMED can happen at most
   once, so `confirmedSeats` grows once however many confirms arrive. Confirm moves a seat from held to confirmed,
   and cancel and expiry only free seats, so none of them can push the count over the limit.
   If a confirm and an expiry race on the same row, PostgreSQL's row lock lets exactly one win.
3. **One clock:** every expiry decision uses the database clock (`now()` / `clock_timestamp()`), so all processes
   agree on when a hold has expired.
4. **One active application per student per class:** checked inside the locked transaction, and backed by a
   partial unique index on `(class_id, student_id) WHERE status IN ('HELD','CONFIRMED')`. An index condition
   cannot use `now()`, so step 1 first marks this class's expired-but-unswept holds as EXPIRED, under the lock,
   which lets a student re-apply after expiry.
5. **The background sweeper** is safe in several processes at once: its UPDATE uses `FOR UPDATE SKIP LOCKED` and only
   touches rows still `HELD`. Correctness never depends on it.
6. In-process locks (`lock`, `SemaphoreSlim`) would only serialize requests inside one process. Two processes could
   both count 9/10 and both insert, giving 11/10. That is why none are used.

## API behaviour

- **Another student's hold** (confirm or cancel) → `403 FORBIDDEN`. Ownership is checked before status, so nothing
  about the hold leaks.
- **Confirm an expired / cancelled hold** → `409 HOLD_EXPIRED` / `409 HOLD_CANCELLED`.
  **Confirm an already-confirmed hold** of the same student → `200`, and nothing changes (idempotent).
- **Cancel a CONFIRMED hold** → `409 HOLD_ALREADY_CONFIRMED`. The fee is paid; refunds are out of scope.
  **Cancel an expired hold** → `409 HOLD_EXPIRED`. **Cancel an already-cancelled hold** → `204` (DELETE is idempotent).
- **Applying twice to the same class** → `409 DUPLICATE_APPLICATION`, checked before seat availability because that
  message is more useful. **Re-applying after expiry or cancellation** is allowed and creates a new hold.
- **`GET /holds/{id}` needs no `X-Student-Id`**, because the spec lists no header for it. Anyone with the
  unguessable hold ID can read it, including `studentId`; pay and cancel still require the owner's header.
- **`X-Student-Id`** must be exactly 17 ASCII digits (leading zeros allowed), otherwise `400 INVALID_STUDENT_ID`.
  Unicode digits are rejected. ASP.NET Core trims surrounding spaces from header values.
- **Class input:** the name is trimmed and must be 1–200 characters; `totalSeats` must be a JSON integer from 1 to
  100000 (`"7"` as a string is rejected). Otherwise `400 VALIDATION_FAILED`. **Duplicate class names are allowed**,
  because shifts can share names and the ID is the identity.
- **`GET /me/holds`** returns all of the student's applications (every status), newest first.
- **Errors** use RFC 7807 ProblemDetails plus a machine-readable `code` (e.g. `SEATS_FULL`). The spec defines no
  error format. **Extra response fields** (`className`, `createdAt`, `serverTime`, seat counts on create) are added;
  no spec field is removed or renamed.
- **No authentication** for the admin "create class" screen, because the spec says no login is needed.
- **API versioning** reads `api-version` from a header or the query string, with 1.0 assumed when none is given.
  The paths must stay exactly as in the spec, so there is no URL-segment versioning and no `/api/v1` alias.

## Time, expiry and IDs

- All timestamps are `timestamptz` and returned as UTC ISO-8601 ending in `Z`.
- **`expiresAt`** is rounded **up** to a whole second, so a hold lasts between `HOLD_DURATION_SECONDS` and
  `HOLD_DURATION_SECONDS + 1` seconds, never less. The stored value and the JSON value are then identical.
- **Expiry is computed on read:** a HELD hold past `expiresAt` immediately counts and reads as `EXPIRED`, so its seat
  is free without any API call or background job. A sweeper (every 10 s, `HOLD_SWEEP_INTERVAL_SECONDS`) also
  updates the stored rows.
- **The `X-Server-Time` header** uses the API server's clock and is informational only; the UI uses it to correct
  countdown skew. Hold responses also carry `serverTime` from the database clock, which the UI prefers.
- **IDs:** `c_` / `h_` + 32 hex characters (UUIDv7): time-ordered for good indexing and not guessable.

## Frontend

- The **student ID** field accepts digits only, at most 17. Only a valid ID is saved to localStorage and used, so
  ongoing applications are visible after a refresh or a browser restart. Clearing the field removes it.
- **Pay and Cancel** are enabled only for the hold's own student, while it is HELD and not past `expiresAt` on the
  server-corrected clock. The status flips to EXPIRED and Pay is disabled at zero, with no reload.
- **Polling:** classes every 3 s, an open application every 5 s while HELD, My applications every 5 s.
  **Apply** is disabled for a full class. If seats run out between polls, the 409 is shown as a plain message.
- The **backend URL** is `VITE_API_BASE_URL` at build time and `API_BASE_URL` at runtime (written to `/env.js` at
  container start, no rebuild). It must be reachable from the **browser** (`http://localhost:8080`).

## Infrastructure and tooling

- **.NET 10** (the spec allows ".NET 8 or newer"). **MediatR 12.5** and **FluentAssertions 7.2** are used because newer
  majors require commercial licences or keys. Frontend packages use the latest release of each well-established
  major (React 19, Vite 7, Vitest 3, TypeScript 5.9).
- **Swagger UI (`/swagger`) and Scalar (`/scalar`)** are enabled in every environment, because graders run the
  non-Development Docker image.
- **Ports:** backend 8080 (spec), frontend 3000 (`FRONTEND_PORT`). PostgreSQL is not published to the host, so it
  cannot clash with a local install.
- **PostgreSQL `max_connections=500`:** each backend process keeps a connection pool (Npgsql default 100), so two
  processes plus tests can exceed PostgreSQL's default of 100, which caused 500 errors in testing.
- **Migrations** run at startup with retry while PostgreSQL starts; data lives in the named volume `pgdata`.
- **Line endings** are LF (`.gitattributes`), and the images also strip CRs from shell scripts, so a Windows
  checkout cannot break the containers.
- **Integration tests** use a real PostgreSQL (Testcontainers locally, or the compose `db` service), with one
  fresh database per test cloned from a migrated template.
