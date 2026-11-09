# School Admission System

First-come-first-served online admission. A parent applies for a seat in a class (each shift is its own class),
the seat is **held** for a limited time, and paying the admission fee **confirms** it. If the fee is not paid in
time, the hold expires and the seat goes back to the pool automatically.

- **Backend:** .NET 10, ASP.NET Core, EF Core + PostgreSQL 16, MediatR (CQRS), FluentValidation, Clean Architecture
- **Frontend:** React 19, TypeScript, Vite, TanStack Query, served by nginx
- **Everything runs with Docker only.** Nothing else needs to be installed.

## Run the app

```bash
docker compose up --build
```

The first build takes a few minutes. When `db`, `backend` and `frontend` are healthy, open:

| What | URL |
|---|---|
| **Frontend (UI)** | **http://localhost:3000** |
| API | http://localhost:8080 (e.g. http://localhost:8080/classes) |
| Swagger UI | http://localhost:8080/swagger |
| Scalar API reference | http://localhost:8080/scalar |
| OpenAPI document | http://localhost:8080/openapi/v1.json |

### Using the UI
1. Type the student's 17-digit **birth registration number** in the bar at the top. It is saved in the browser,
   so it survives a refresh or a browser restart.
2. **Create class** (school admin): enter a name such as `Class 6 - Morning Shift` and a seat count.
3. **Classes**: shows total, held, confirmed and available seats, updated every few seconds. Click **Apply**.
4. **Application**: shows the status and a countdown. **Pay admission fee** confirms the seat, and **Cancel**
   releases it. When time runs out, the page shows **EXPIRED** and the Pay button is disabled, without a reload.
5. **My applications**: all applications of this student and their status.

### Stop and keep the data
```bash
docker compose down      # data is kept in the named volume "pgdata"
docker compose up        # everything is still there
docker compose down -v   # removes the data too (fresh start)
```

## Run the tests

All tests run with Docker only:

```bash
docker compose --profile test run --rm backend-tests    # 68 unit + 75 integration tests (real PostgreSQL)
docker compose --profile test run --rm frontend-tests   # 44 Vitest + React Testing Library tests
```

The backend integration tests include the required concurrency tests:
- `CreateHold_200ParallelRequests_10Seats_ExactlyTenSucceed`: 200 simultaneous applications for 10 seats give
  exactly 10 × `201` and 190 × `409 SEATS_FULL`, and the database holds exactly 10 HELD rows.
- `ConfirmHold_50ParallelConfirms_AllReturn200_ConfirmedSeatsIsOne`, `CreateHold_SameStudent50ParallelRequests_ExactlyOneSucceeds`
  and `MixedLoad_HoldsConfirmsCancels_NeverExceedTotalSeats`.
- `TwoApiProcesses_SameDb_300Requests_10Seats_ExactlyTenSucceed` (bonus): two real API processes, one database.

<details>
<summary>Running the tests without Docker (optional)</summary>

With the .NET 10 SDK, Node 22+ and Docker running (the backend tests start PostgreSQL through Testcontainers):

```bash
dotnet test backend/SchoolAdmission.slnx
npm --prefix frontend ci && npm --prefix frontend test -- --run
```
</details>

## Bonus: two backend processes on one database

```bash
docker compose --profile bonus up -d --build      # adds backend2 on http://localhost:8081
docker compose --profile bonus run --rm bonus-check
```

`bonus-check` creates a class with 10 seats and sends 200 simultaneous applications, alternating between the two
backends. It prints the result and fails unless exactly 10 were granted. `ASSUMPTIONS.md` explains how this is
guaranteed (a row lock in PostgreSQL, not in the application).

## Configuration

| Variable | Service | Default | Meaning |
|---|---|---|---|
| `HOLD_DURATION_SECONDS` | backend | `600` | How long a hold lasts before it expires |
| `ConnectionStrings__Default` or `DATABASE_URL` | backend | set in compose | PostgreSQL connection (keyword string or `postgres://user:pass@host:port/db`) |
| `CORS_ORIGINS` | backend | `http://localhost:3000,http://localhost:5173` | Allowed browser origins (comma-separated, `*` for any) |
| `HOLD_SWEEP_INTERVAL_SECONDS` | backend | `10` | Background job that marks stale holds EXPIRED (seats are released on time even without it) |
| `API_BASE_URL` | frontend | `http://localhost:8080` | Backend URL used **by the browser**, written to `/env.js` at container start (no rebuild needed) |
| `VITE_API_BASE_URL` | frontend build arg | `http://localhost:8080` | Build-time default, used if `API_BASE_URL` is empty |
| `FRONTEND_PORT` | compose | `3000` | Host port of the UI (if 3000 is taken, also add the new origin to `CORS_ORIGINS`) |

Example: `HOLD_DURATION_SECONDS=60 docker compose up -d` gives one-minute holds, for a quick demo of expiry.

## API

All paths are exactly as in the assignment (no prefix). JSON is camelCase, statuses are `HELD`, `CONFIRMED`,
`EXPIRED` and `CANCELLED`, and times are UTC ending in `Z`. Errors are RFC 7807 `application/problem+json` with
a machine-readable `code`, e.g. `SEATS_FULL`, `DUPLICATE_APPLICATION`, `HOLD_EXPIRED` or `INVALID_STUDENT_ID`.

| Method & path | Success | Main errors |
|---|---|---|
| `POST /classes` | 201 | 400 |
| `GET /classes` | 200 | |
| `GET /classes/{classId}` | 200 | 404 |
| `POST /classes/{classId}/holds` (header `X-Student-Id`) | 201 | 400, 404, 409 `SEATS_FULL` / `DUPLICATE_APPLICATION` |
| `GET /holds/{holdId}` | 200 | 404 |
| `POST /holds/{holdId}/confirm` (header `X-Student-Id`) | 200 (also when already confirmed) | 400, 403, 404, 409 `HOLD_EXPIRED` / `HOLD_CANCELLED` |
| `DELETE /holds/{holdId}` (header `X-Student-Id`) | 204 | 400, 403, 404, 409 `HOLD_ALREADY_CONFIRMED` / `HOLD_EXPIRED` |
| `GET /me/holds` (header `X-Student-Id`) | 200 | 400 |

API versioning: an optional `api-version` header or query parameter; version 1.0 is assumed when none is given.

## Repository layout

```
backend/    .NET solution: Domain, Application, Infrastructure, Api, plus unit and integration tests
frontend/   React app, nginx config and runtime env.js script
scripts/    bonus-oversell.sh (used by the bonus-check service)
docs/       DESIGN.md, PLAN.md, PROGRESS.md, AI_LOG.md, review and audit reports
ASSUMPTIONS.md, AI_USAGE.md
```
