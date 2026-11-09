# School Admission System — Project Memory

## Source of truth
- The requirements are in `School_Admission_System_Assignment_EN.md`. It is READ-ONLY.
  Never edit, move, rename, reformat or "fix" it. Extra instructions live in `docs/` or come from the user.
- If an instruction from the user conflicts with the spec's API contract or mandatory rules (§1.4, §1.5),
  the spec wins. Tell the user about the conflict.
- Design decisions live in `docs/DESIGN.md`, the work plan in `docs/PLAN.md`. Read both before coding.

## Stack (fixed)
- Backend: .NET 10, ASP.NET Core, EF Core 10 + Npgsql, PostgreSQL 16+
- MediatR + CQRS (commands/queries, pipeline behaviors for validation and logging)
- FluentValidation for request validation
- Clean Architecture: Domain → Application → Infrastructure → Api (dependencies point inward only)
- Repository pattern + Unit of Work (interfaces in Application, implementations in Infrastructure)
- API versioning: `Asp.Versioning.Mvc` (+ ApiExplorer)
- OpenAPI docs: Swagger UI at `/swagger` and Scalar at `/scalar`, both enabled in ALL environments
  (the graders run the Docker image, which is not Development)
- Frontend: React 18+ with TypeScript, Vite, TanStack Query, served by nginx in Docker
- Tests: xUnit + FluentAssertions (backend), Vitest + React Testing Library (frontend)
- MediatR licensing: v13+ needs a license key (a free community key is available for qualifying users).
  Use the latest 12.x (Apache-2.0) unless the user says otherwise, and note this in ASSUMPTIONS.md.

## Non-negotiable invariants
1. `held + confirmed <= totalSeats` for every class, under any concurrency, including with 2+ backend
   processes on the same database. Enforce this IN THE DATABASE (row lock or atomic conditional SQL),
   never with in-process locks, `SemaphoreSlim`, or a C# "check then insert".
2. A hold counts as held only while `status = HELD AND expires_at > now()`. Seat release on expiry must
   be correct even if no background job has run yet. A background sweeper also marks stale holds `EXPIRED`.
3. Use the DATABASE clock (`now()`) for every expiry decision so multiple processes agree.
4. At most one active (HELD and not expired) or CONFIRMED hold per (student, class). Back this with a
   partial unique index, plus a check inside the locked transaction.
5. Confirm is atomic and idempotent: one conditional UPDATE (`... WHERE status='HELD' AND expires_at > now()
   AND student_id = @s`). Confirming an already-CONFIRMED hold of the same student returns 200 and does
   not change counts.
6. `HOLD_DURATION_SECONDS` env var, default 600.
7. Data survives `docker compose down` + `up` (named volume; migrations applied at startup with retry).

## API contract rules
- Paths must be EXACTLY as in spec §1.4 (`/classes`, `/holds/{holdId}`, `/me/holds`, ...) with no prefix.
  API versioning must not change those paths. Use header/query version reading
  (`api-version`) with `AssumeDefaultVersionWhenUnspecified = true` and default 1.0.
  Optional aliases like `/api/v1/classes` may be added; the unprefixed paths are mandatory.
- Never remove or rename spec fields. Extra fields are allowed (e.g. `serverTime`).
- JSON is camelCase, status values are UPPERCASE strings, timestamps are UTC ISO-8601 ending in `Z`.
- Status codes: 201 create, 200 read/confirm, 204 delete, 409 no seat. Errors use ProblemDetails
  (RFC 7807) with an extra machine-readable `code` (e.g. `SEATS_FULL`, `HOLD_EXPIRED`,
  `DUPLICATE_APPLICATION`, `INVALID_STUDENT_ID`, `NOT_FOUND`, `FORBIDDEN`).
- `X-Student-Id` must be exactly 17 digits; otherwise 400.
- CORS enabled for the frontend origin (configurable via env var).

## Environment variables
- Backend: `ConnectionStrings__Default` (or `DATABASE_URL`), `HOLD_DURATION_SECONDS`, `CORS_ORIGINS`,
  listens on 8080.
- Frontend: `VITE_API_BASE_URL` at build time AND a runtime override (`API_BASE_URL` written into
  `/env.js` by the nginx container entrypoint), so the backend URL can change without a rebuild.

## Workflow rules
- Work in the phases of `docs/PLAN.md`. After each phase: build, run tests, then `git commit` with a
  Conventional Commit message. Many small commits; never one big commit.
- Everything must run with only Docker installed. If a local .NET 10 SDK or Node is missing, build and
  test inside Docker containers.
- Keep `docs/AI_LOG.md` up to date: every bug or wrong assumption found by a subagent, a test, or the
  user, with how it was caught. This feeds `AI_USAGE.md`.
- When something in the spec is unclear, decide, then record it in `ASSUMPTIONS.md` with the reason.
- Use subagents: `system-architect` for design and plan, `functional-verifier` and `api-contract-auditor`
  after each major phase, `code-reviewer` before final submission.