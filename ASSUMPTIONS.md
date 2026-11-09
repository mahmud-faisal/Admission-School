# Assumptions (draft)

Where the spec is silent, this is what we decided and why.

## API behaviour
- **Wrong student** (confirm/cancel someone else's hold) → `403 FORBIDDEN`. The hold exists; the caller just does not own it. Ownership is checked before status, so nothing about the hold leaks.
- **Confirm an expired hold** → `409 HOLD_EXPIRED`. It conflicts with the hold's current state; the client can apply again.
- **Confirm a cancelled hold** → `409 HOLD_CANCELLED`. Same reasoning.
- **Confirm an already-confirmed hold (same student)** → `200` with no change. The spec requires confirm to be idempotent.
- **Cancel an already-cancelled hold** → `204`. DELETE is idempotent.
- **Cancel a CONFIRMED hold** → not allowed, `409 HOLD_ALREADY_CONFIRMED`. The fee is paid; refunds and withdrawals are out of scope.
- **Cancel an expired hold** → `409 HOLD_EXPIRED`. The seat is already released; there is nothing to cancel.
- **Applying twice to the same class** → `409 DUPLICATE_APPLICATION`. This is checked before seat availability, because that message is more useful.
- **Re-applying after expiry or cancellation** → allowed, and it creates a new hold id. The rule only limits *active or confirmed* holds.
- **`GET /holds/{id}` needs no `X-Student-Id`** (it is ignored if sent). The spec does not list a header for it, and hold IDs cannot realistically be guessed.
- **`X-Student-Id`** must be exactly 17 ASCII digits (leading zeros allowed); otherwise `400 INVALID_STUDENT_ID`. This is what the spec says, and checking ASCII rules out Unicode digits.
- **Duplicate class names** are allowed. Shifts can share names, and automated tests may create the same name many times; the ID is the identity.
- **Class input limits:** name trimmed, 1–200 characters; `totalSeats` an integer in 1–100000 → otherwise `400 VALIDATION_FAILED`. A class with 0 seats makes no sense, and the upper bound stops absurd input.
- **`GET /me/holds`** returns all of the student's holds (every status), newest first. The spec says "all applications … and the status of each".
- **Errors** use RFC 7807 ProblemDetails with an extra `code` field (e.g. `SEATS_FULL`). The spec defines no error format, and a machine-readable code lets the UI show clear messages.
- **Extra response fields** (e.g. `className`, `createdAt`, `serverTime`, seat counts on create) are added and no spec field is removed. The spec allows extra fields.
- **No authentication** for the admin "create class" screen. The spec says no login is needed.

## Time and IDs
- **Time zone:** all timestamps are stored as `timestamptz` and returned as UTC ISO-8601 ending in `Z`. This avoids any ambiguity for clients in other time zones.
- **Clock:** every expiry decision uses the PostgreSQL clock (`now()` / `clock_timestamp()`), so several backend processes always agree.
- **`expiresAt` precision:** cut to whole seconds, so the stored value and the JSON value are identical. A hold lasts at most 1 s less than `HOLD_DURATION_SECONDS`.
- **Expiry:** a HELD hold whose `expiresAt` has passed counts and reads as `EXPIRED` right away (computed on read). A background sweeper (every 10 s by default) also updates the stored rows. Seats are freed even if no job has run.
- **ID format:** `c_` / `h_` + 32 hex characters (UUIDv7). Time-ordered for good indexing and not guessable; the spec leaves the format open.

## Concurrency (bonus, draft)
- Each hold creation locks the class row in PostgreSQL (`SELECT … FOR UPDATE`), then counts and inserts in the same transaction. The lock lives in the database, so it works across any number of backend processes; in-process locks would not. Confirm and cancel are single conditional UPDATEs. A partial unique index backs the one-active-hold-per-student rule.

## Tooling and stack
- **.NET 10** is used; the spec allows ".NET 8 or newer".
- **API versioning** reads `api-version` from a header or the query string, with default 1.0 assumed. The spec says the paths must be exact, so we do not use URL-segment versioning.
- **MediatR 12.x** (Apache-2.0) is used. v13+ needs a licence key.
- **FluentAssertions 7.x** (Apache-2.0) is used. v8+ has a commercial licence.
- **Swagger UI (`/swagger`) and Scalar (`/scalar`)** are enabled in all environments, because graders run the non-Development Docker image.
- **Ports:** backend 8080 (spec), frontend 3000. PostgreSQL is not published to the host, so it cannot clash with a local install.
- **Frontend API URL:** `VITE_API_BASE_URL` at build time, overridden at runtime by `API_BASE_URL` (written to `/env.js`). The URL can then change without a rebuild. It must be reachable from the browser (e.g. `http://localhost:8080`).
- **Line endings:** LF everywhere (`.gitattributes`), so shell scripts work inside Linux containers.

## Confirmed by the user (2026-11-09)
All recommended answers to the Checkpoint 1 open questions were accepted: cancelling a CONFIRMED hold → 409
`HOLD_ALREADY_CONFIRMED`; another student's hold → 403 `FORBIDDEN`; cancelling an expired hold → 409 `HOLD_EXPIRED`;
duplicate class names allowed; no `/api/v1` aliases; seats 1–100000; `GET /holds/{id}` needs no header;
`expiresAt` cut to whole seconds; sweeper every 10 s (`HOLD_SWEEP_INTERVAL_SECONDS`); frontend on port 3000;
FluentAssertions 7.x.

## Added in Checkpoint 3
- **Numbers sent as strings** (`"totalSeats": "7"`): rejected with 400. The contract uses JSON numbers, and ASP.NET's web default (accept strings) would hide client bugs.
- **`X-Server-Time` header:** uses the API server's clock and is informational only (it helps the UI correct countdown skew). Every expiry decision, and the `serverTime` field in hold responses, uses the database clock.
- **OpenAPI:** generated by the built-in .NET 10 generator through the `Asp.Versioning.OpenApi` bridge, at `/openapi/v1.json`. 405 and 5xx responses are not listed per operation; they still return problem+json with a `code`.

## Added in Checkpoint 4
- **PostgreSQL `max_connections=500`** in docker compose. Each backend process keeps a connection pool (Npgsql default: 100), so two processes (the bonus) plus the test runner can exceed the default of 100. At that point PostgreSQL rejects connections and the API returns 500s.
- **Integration tests use a real PostgreSQL:** Testcontainers locally, or the compose `db` service through `TEST_DATABASE_URL`. Each test gets its own database, cloned from a migrated template.

## Added in Checkpoint 5 (frontend)
- **Package versions:** the latest release of each well-established major (React 19, TanStack Query 5, React Router 7, Vite 7, Vitest 3, TypeScript 5.9, MSW 2), not the brand-new majors, for a reliable build on the graders' machines.
- **Student ID field:** accepts digits only and at most 17. Only a valid 17-digit value is saved to localStorage and used. While the user is editing, the last valid ID stays saved, so a refresh still restores it. Clearing the field removes it.
- **Apply on a full class:** the button is disabled when `availableSeats` is 0. If seats run out between polls, the server's 409 `SEATS_FULL` is shown as a plain message.
- **Pay/Cancel:** only enabled for the hold's own student (the entered ID matches the hold's `studentId`) while it is HELD and not past `expiresAt`, measured on the server-corrected clock.
- **Countdown clock:** corrected for clock skew using `serverTime` (DB clock) from hold responses, or the `X-Server-Time` header.
- **Polling:** classes every 3 s, an open application every 5 s while HELD, My applications every 5 s.

## Added in Checkpoint 7 (from the verification reviews)
- **`GET /holds/{id}` is public:** the spec lists no header for it, so anyone with the (unguessable, UUIDv7-based) hold ID can read it, including `studentId`. Pay and cancel still require the owner's `X-Student-Id`.
- **`X-Student-Id` with surrounding spaces is accepted:** ASP.NET Core trims header values before validation; the value must still be exactly 17 digits.
- **OpenAPI** documents one `409` per endpoint. The individual `code` values (SEATS_FULL, DUPLICATE_APPLICATION, HOLD_EXPIRED, ...) are listed in DESIGN.md §8.4 and in the schema description of `code`.
