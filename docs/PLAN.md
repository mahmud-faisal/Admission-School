# School Admission System — Work Plan

Phases map 1:1 to the checkpoints in `instruction.md`. Each phase ends with: build → tests → commit
(Conventional Commits). After each **checkpoint**, update `docs/PROGRESS.md`, print the checkpoint report,
and **stop for confirmation**. Every bug or wrong assumption found goes into `docs/AI_LOG.md`.

Command conventions (run from repo root `E:\SchoolAssignment`):
- Local backend: `dotnet build backend/SchoolAdmission.slnx` / `dotnet test backend/SchoolAdmission.slnx`
- Docker-only backend build: `docker run --rm -v "${PWD}/backend:/src" -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet build SchoolAdmission.slnx`
- Local frontend: `npm --prefix frontend ci && npm --prefix frontend test -- --run`

---

## Checkpoint 0 — Bootstrap (DONE)
Commits `4701d13` … `a622981`.

## Checkpoint 1 — Design & plan (this document)
- Files: `docs/DESIGN.md`, `docs/PLAN.md`, `ASSUMPTIONS.md`.
- DoD: user answers the open questions; docs are updated to match.
- Commit: `docs: add system design, work plan and draft assumptions`

---

## Checkpoint 2 — Backend core

### 2.1 Solution scaffold
- Goal: an empty Clean Architecture solution that builds.
- Files: `backend/SchoolAdmission.slnx`, `backend/global.json`, `backend/Directory.Build.props`,
  `backend/Directory.Packages.props`, `backend/.config/dotnet-tools.json`, `backend/.dockerignore`,
  `backend/src/SchoolAdmission.{Domain,Application,Infrastructure,Api}/*.csproj`,
  `backend/tests/SchoolAdmission.{UnitTests,IntegrationTests}/*.csproj` (project references as in DESIGN §1).
  Packages pinned: MediatR 12.5.x, FluentValidation 12.x, FluentAssertions 7.x, xunit.v3.
- DoD: `dotnet build` passes with 0 warnings; the reference graph matches DESIGN §1 (Domain has no references).
- Test: `dotnet build backend/SchoolAdmission.slnx`
- Commit: `chore(backend): scaffold clean architecture solution and test projects`

### 2.2 Domain
- Goal: entities, status enum, StudentId value object, HoldRules.
- Files: `src/SchoolAdmission.Domain/**` (DESIGN §2); `tests/SchoolAdmission.UnitTests/Domain/*`.
- DoD: builds; `HoldRulesTests` and `StudentIdTests` pass.
- Test: `dotnet test backend/SchoolAdmission.slnx --filter FullyQualifiedName~UnitTests.Domain`
- Commit: `feat(domain): add class and hold model with status rules`

### 2.3 Application
- Goal: ports, Result type, DTOs, all commands/queries/handlers/validators, Logging + Validation behaviors,
  `HoldOptions`, `AddApplication()`.
- Files: `src/SchoolAdmission.Application/**` (DESIGN §5, §6 interfaces); unit tests for validators and
  handlers using fakes.
- DoD: builds; validator and handler unit tests pass; Application has no EF/Npgsql references.
- Test: `dotnet test backend/SchoolAdmission.slnx --filter FullyQualifiedName~UnitTests`
- Commits: `feat(application): add persistence ports, result type and dtos`,
  `feat(application): add class and hold commands, queries and validators`,
  `feat(application): add logging and validation pipeline behaviors`

### 2.4 Infrastructure: DbContext + migration
- Goal: EF model with the exact schema (CHECKs, partial unique index, indexes), connection-string resolver
  (`ConnectionStrings__Default` / `DATABASE_URL`), design-time factory, `InitialCreate` migration,
  `DatabaseMigrator.MigrateWithRetryAsync`.
- Files: `src/SchoolAdmission.Infrastructure/Persistence/{AdmissionDbContext,Configurations/*,
  DesignTimeDbContextFactory,ConnectionStringResolver,DatabaseMigrator}.cs`, `Persistence/Migrations/*`.
- DoD: `dotnet ef migrations script` output contains `ux_holds_class_student_active ... WHERE status IN ('HELD','CONFIRMED')`
  and all CHECK constraints; builds.
- Test: `dotnet tool restore --tool-manifest backend/.config/dotnet-tools.json && dotnet ef migrations script -p backend/src/SchoolAdmission.Infrastructure -s backend/src/SchoolAdmission.Infrastructure`
- Commit: `feat(infrastructure): add ef core model and initial migration`

### 2.5 Infrastructure: repositories, unit of work, id generator, sweeper
- Goal: raw-SQL repositories exactly as DESIGN §4 and §6, `UnitOfWork` (READ COMMITTED), `GuidV7IdGenerator`,
  `HoldExpirySweeper`, `AddInfrastructure()`.
- Files: `Persistence/Repositories/{ClassRepository,HoldRepository}.cs`, `Persistence/UnitOfWork.cs`,
  `Ids/GuidV7IdGenerator.cs`, `BackgroundJobs/HoldExpirySweeper.cs`, `DependencyInjection.cs`.
- DoD: builds; a code check finds no `lock`, `SemaphoreSlim`, `DateTime.UtcNow` or `DateTime.Now` in any
  expiry or seat logic (`Grep` for them); all SQL is parameterized.
- Test: `dotnet build backend/SchoolAdmission.slnx` (DB behavior is covered in Checkpoint 4)
- Commits: `feat(infrastructure): add repositories with db-level seat locking`,
  `feat(infrastructure): add hold expiry sweeper background service`

---

## Checkpoint 3 — API

### 3.1 Host skeleton
- Goal: `Program.cs` with JSON options (camelCase, UPPERCASE enums, UTC `Z` converter), ProblemDetails with
  `code`, `GlobalExceptionHandler`, model-state factory, versioning (header/query `api-version`, default 1.0),
  OpenAPI + Swagger UI `/swagger` + Scalar `/scalar` in all environments, CORS from `CORS_ORIGINS`,
  `X-Server-Time` middleware, health checks, migrate-on-startup with retry, `GET /` → `/swagger`.
- Files: `src/SchoolAdmission.Api/{Program.cs, appsettings.json, ErrorHandling/*, Json/*, Middleware/*, OpenApi/*}`.
- DoD: `dotnet run` against a local Postgres (`docker run -d --name adm-pg -e POSTGRES_PASSWORD=admission -p 55432:5432 postgres:16-alpine`)
  starts on 8080, `/swagger`, `/scalar`, `/openapi/v1.json`, `/health/ready` return 200.
- Test: `dotnet build backend/SchoolAdmission.slnx`
- Commit: `feat(api): add host with problem details, versioning, openapi, cors and startup migration`

### 3.2 Controllers
- Goal: the 8 endpoints at the exact spec paths (DESIGN §8.4).
- Files: `Controllers/{ClassesController,HoldsController,MeController}.cs`, `Contracts/CreateClassRequest.cs`,
  `ErrorHandling/ResultExtensions.cs`.
- DoD: a manual curl smoke test of all 8 endpoints, plus 400/404/409 cases, gives the expected codes.
- Test: `dotnet build backend/SchoolAdmission.slnx`
- Commit: `feat(api): add classes, holds and me endpoints at spec paths`

### 3.3 Contract audit
- Goal: run **api-contract-auditor**, fix every mismatch, re-run until clean.
- Files: whatever the audit flags; `docs/AI_LOG.md`.
- DoD: audit reports zero mismatches.
- Commit(s): `fix(api): <mismatch>` (one per fix)

---

## Checkpoint 4 — Backend tests

### 4.1 Test infrastructure
- Goal: `PostgresFixture` (TEST_DATABASE_URL or Testcontainers, a database per test class), `AdmissionApiFactory`,
  `ApiProcess` helper.
- Files: `tests/SchoolAdmission.IntegrationTests/Infrastructure/*`.
- DoD: one smoke test (`GetClasses_Empty_ReturnsEmptyArray`) passes against real Postgres.
- Test: `dotnet test backend/SchoolAdmission.slnx --filter FullyQualifiedName~IntegrationTests`
- Commit: `test(backend): add postgres fixture and web application factory`

### 4.2 API and contract integration tests
- Files: `Api/{ClassesEndpointsTests,HoldsEndpointsTests,ContractShapeTests}.cs`, `Persistence/SchemaTests.cs`.
- DoD: all tests in DESIGN §11.3 "API and contract" + "Persistence" pass.
- Commit: `test(backend): add endpoint, contract and schema integration tests`

### 4.3 Concurrency, idempotency, expiry
- Files: `Concurrency/{CreateHoldConcurrencyTests,ConfirmConcurrencyTests}.cs`, `Expiry/ExpiryTests.cs`.
- DoD: (a) 200 parallel → exactly 10 × 201, (b) idempotent confirm, (c) expiry without an API call, (d) duplicate
  rejected. All green 5 runs in a row (`for /L` or a bash loop).
- Commit: `test(backend): add concurrency, idempotent confirm and expiry tests`

### 4.4 Two-process test (bonus)
- Files: `Concurrency/TwoProcessOversellTests.cs`.
- DoD: two real Api processes on one DB → exactly `totalSeats` successes in total.
- Test: `dotnet test backend/SchoolAdmission.slnx --filter Category=MultiProcess`
- Commit: `test(backend): add two-process oversell test`

### 4.5 Docker-only test run
- Goal: backend tests runnable with only Docker.
- Files: `backend/Dockerfile` (build + test stages), `docker-compose.yml` (initial: `db` + `backend-tests` in profile `test`).
- DoD: `docker compose --profile test run --rm backend-tests` passes, including the two-process test.
- Commit: `build(docker): run backend tests in compose test profile`

---

## Checkpoint 5 — Frontend

### 5.1 Scaffold
- Files: `frontend/{package.json, vite.config.ts, tsconfig*.json, index.html, public/env.js, src/main.tsx,
  src/App.tsx, src/config.ts, src/test/setup.ts}`.
- DoD: `npm run build` and `npm test -- --run` (with `config.test.ts`) pass.
- Commit: `chore(frontend): scaffold vite react typescript app with vitest`

### 5.2 API client, errors, server clock
- Files: `src/api/{client,errors,types,hooks}.ts`, `src/time/serverClock.ts`, `src/hooks/useCountdown.ts`;
  `errors.test.ts`.
- Commit: `feat(frontend): add api client, error mapping and server clock`

### 5.3 Layout, student ID, error boundary
- Files: `components/{Layout,StudentIdBar,ErrorBoundary,ErrorAlert,ConnectionBanner}.tsx`,
  `state/StudentIdContext.tsx`; tests `StudentIdBar.test.tsx`, `ErrorBoundary.test.tsx`.
- Commit: `feat(frontend): add layout, student id bar and error boundary`

### 5.4 Classes (list + create)
- Files: `pages/{ClassListPage,CreateClassPage}.tsx`, `components/{ClassTable,CreateClassForm}.tsx`; tests.
- Commit: `feat(frontend): add class list with live seat counts and create class form`

### 5.5 Application + My applications
- Files: `pages/{HoldPage,MyHoldsPage,NotFoundPage}.tsx`, `components/{Countdown,HoldStatusBadge}.tsx`; tests
  `HoldPage.test.tsx`, `MyHoldsPage.test.tsx`, `NetworkDown.test.tsx`.
- DoD: all DESIGN §11.4 tests pass; manual run with `npm run dev` against the local backend.
- Commit: `feat(frontend): add application page with countdown and my applications`

---

## Checkpoint 6 — Docker
- Goal: full stack with one `docker compose up`.
- Files: `backend/Dockerfile` (runtime stage + curl), `frontend/Dockerfile`, `frontend/docker/{nginx.conf,40-write-env.sh}`,
  `frontend/.dockerignore`, `docker-compose.yml` (all services and profiles from DESIGN §10.3), `scripts/bonus-oversell.sh`.
- DoD:
  - `docker compose down -v && docker compose up --build -d` → all services healthy; http://localhost:3000,
    http://localhost:8080/classes, /swagger, /scalar work.
  - Create a class and a hold → `docker compose down` → `up -d` → data still there.
  - `API_BASE_URL` changed at runtime is picked up without a rebuild (`curl localhost:3000/env.js`).
  - `docker compose --profile test run --rm backend-tests` and `... frontend-tests` pass.
  - `docker compose --profile bonus up -d` → 8081 serves; the bonus script shows no oversell.
- Commits: `build(backend): add multi-stage runtime dockerfile`, `build(frontend): add nginx dockerfile with runtime env.js`,
  `build: add docker compose with healthchecks, volume, test and bonus profiles`

## Checkpoint 7 — Verification loop
- Goal: run **functional-verifier** and **api-contract-auditor** in parallel; fix every FAIL; re-run until clean.
- Files: as flagged; `docs/AI_LOG.md`.
- DoD: both report all PASS (or the remaining items are escalated to the user).
- Commits: one `fix(<scope>): ...` per finding.

## Checkpoint 8 — Review & documentation
- Goal: **code-reviewer** pass (fix Blocker + Should-fix), `README.md`, final `ASSUMPTIONS.md`
  (including the multi-process explanation), draft `AI_USAGE.md` from `docs/AI_LOG.md`, a final functional-verifier run.
- DoD: `git diff --stat 4701d13 HEAD -- School_Admission_System_Assignment_EN.md` prints nothing; §1.7 checklist all ✅.
- Commits: `refactor: address code review findings`, `docs: add readme with docker, test and bonus instructions`,
  `docs: finalize assumptions`, `docs: draft ai usage`
