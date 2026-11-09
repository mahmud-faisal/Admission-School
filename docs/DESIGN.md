# School Admission System — Design

Status: Checkpoint 1, approved by the user on 2026-11-09 (all recommended answers to open questions accepted). Source of truth for requirements: `School_Admission_System_Assignment_EN.md`
(read-only). Where this document and the spec disagree, the spec wins.

---

## 0. Stack and conflicts with the spec

| Item | Choice | Spec | Conflict? |
|---|---|---|---|
| Runtime | .NET 10 / ASP.NET Core 10 / C# 14 | ".NET 8 or newer" | None (10 is newer). |
| ORM | EF Core 10 + Npgsql 10 | "ORM of your choice" | None. |
| DB | PostgreSQL 16 (`postgres:16-alpine`) | PostgreSQL | None. |
| Mediator | MediatR **12.5.x** (last Apache-2.0 line) | — | v13+ needs a licence key. Pinned to 12.x. |
| Assertions | FluentAssertions **7.x** (Apache-2.0) | — | v8+ is under a commercial Xceed licence. Pinned to 7.x (or the AwesomeAssertions fork if 7.x stops working). |
| Test framework | xUnit v3 (`xunit.v3`) | — | None. We use v3 because it has `Assert.Skip`/`Assert.SkipUnless` for env-dependent tests. |
| Frontend | React 18+ / TypeScript / Vite / TanStack Query v5 / React Router v6+ | React 18+ | None. |
| API versioning | `Asp.Versioning.Mvc` + `.Mvc.ApiExplorer` + `Asp.Versioning.OpenApi` (10.x) | Exact paths in §1.4 | Possible conflict, resolved in favor of the spec: versioning uses header/query only, **paths are not changed**. See §8.2. |
| Optional `/api/v1/...` aliases | **Not added by default** | Exact paths | They would duplicate every operation in OpenAPI. Confirmed by the user (Q3). |

---

## 1. Solution layout (Clean Architecture)

```
backend/
  SchoolAdmission.slnx
  global.json                      # sdk 10.0.100, rollForward latestFeature
  Directory.Build.props            # net10.0, Nullable enable, TreatWarningsAsErrors, LangVersion latest
  Directory.Packages.props         # central package versions
  .config/dotnet-tools.json        # dotnet-ef 10.x
  Dockerfile                       # stages: build, test, publish, runtime
  .dockerignore
  src/
    SchoolAdmission.Domain/
      Classes/Class.cs
      Holds/Hold.cs
      Holds/HoldStatus.cs
      Holds/HoldRules.cs           # pure decision logic (effective status, why confirm/cancel failed)
      Students/StudentId.cs        # value object, exactly 17 ASCII digits
      Common/DomainErrors.cs       # error code constants
    SchoolAdmission.Application/
      Abstractions/
        Persistence/IClassRepository.cs
        Persistence/IHoldRepository.cs
        Persistence/IUnitOfWork.cs
        Persistence/ReadModels.cs  # ClassSeatsRow, HoldRow, ClassLockRow
        IIdGenerator.cs
      Common/
        Result.cs                  # Result, Result<T>, Error(code, message, ErrorKind)
        ErrorCodes.cs
        Behaviors/LoggingBehavior.cs
        Behaviors/ValidationBehavior.cs
        Options/HoldOptions.cs     # HoldDurationSeconds (600), SweepIntervalSeconds (10), SweeperEnabled, SweepBatchSize
      Classes/
        CreateClass/{CreateClassCommand, CreateClassHandler, CreateClassValidator}.cs
        ListClasses/{ListClassesQuery, ListClassesHandler}.cs
        GetClass/{GetClassQuery, GetClassHandler, GetClassValidator}.cs
        ClassDto.cs
      Holds/
        CreateHold/{CreateHoldCommand, CreateHoldHandler, CreateHoldValidator}.cs
        GetHold/{GetHoldQuery, GetHoldHandler, GetHoldValidator}.cs
        ConfirmHold/{ConfirmHoldCommand, ConfirmHoldHandler, ConfirmHoldValidator}.cs
        CancelHold/{CancelHoldCommand, CancelHoldHandler, CancelHoldValidator}.cs
        GetMyHolds/{GetMyHoldsQuery, GetMyHoldsHandler, GetMyHoldsValidator}.cs
        ExpireStaleHolds/{ExpireStaleHoldsCommand, ExpireStaleHoldsHandler}.cs
        HoldDtos.cs
      Validation/StudentIdRules.cs # shared FluentValidation rule: ^[0-9]{17}$ -> INVALID_STUDENT_ID
      DependencyInjection.cs       # AddApplication()
    SchoolAdmission.Infrastructure/
      Persistence/
        AdmissionDbContext.cs
        Configurations/ClassConfiguration.cs
        Configurations/HoldConfiguration.cs
        Migrations/                # EF migrations (InitialCreate, ...)
        DesignTimeDbContextFactory.cs
        Repositories/ClassRepository.cs
        Repositories/HoldRepository.cs
        UnitOfWork.cs
        ConnectionStringResolver.cs # ConnectionStrings__Default, else DATABASE_URL (URI or keyword form)
        DatabaseMigrator.cs         # MigrateWithRetryAsync
      Ids/GuidV7IdGenerator.cs
      BackgroundJobs/HoldExpirySweeper.cs
      DependencyInjection.cs        # AddInfrastructure(IConfiguration)
    SchoolAdmission.Api/
      Program.cs
      Controllers/ClassesController.cs
      Controllers/HoldsController.cs
      Controllers/MeController.cs
      Contracts/                    # request bodies (CreateClassRequest)
      ErrorHandling/GlobalExceptionHandler.cs   # IExceptionHandler
      ErrorHandling/ResultExtensions.cs         # Result -> IActionResult / ProblemDetails
      ErrorHandling/ProblemDetailsSetup.cs      # adds `code` + `traceId` to every ProblemDetails
      Json/UtcDateTimeConverter.cs
      Middleware/ServerTimeHeaderMiddleware.cs  # X-Server-Time
      OpenApi/OpenApiSetup.cs                   # versioning + OpenAPI + Swagger UI + Scalar
      appsettings.json
  tests/
    SchoolAdmission.UnitTests/
      Domain/HoldRulesTests.cs
      Domain/StudentIdTests.cs
      Application/Validators/*Tests.cs
      Application/Handlers/*Tests.cs  # with hand-written fakes of IHoldRepository etc.
    SchoolAdmission.IntegrationTests/
      Infrastructure/PostgresFixture.cs        # Testcontainers or TEST_DATABASE_URL
      Infrastructure/AdmissionApiFactory.cs    # WebApplicationFactory<Program>
      Infrastructure/ApiProcess.cs             # spawns a real Api process (two-process test)
      Api/ClassesEndpointsTests.cs
      Api/HoldsEndpointsTests.cs
      Api/ContractShapeTests.cs
      Concurrency/CreateHoldConcurrencyTests.cs
      Concurrency/ConfirmConcurrencyTests.cs
      Concurrency/TwoProcessOversellTests.cs
      Expiry/ExpiryTests.cs
      Persistence/SchemaTests.cs
```

### Dependency direction

```
Api ──► Infrastructure ──► Application ──► Domain
 └──────────────────────► Application
```

- **Domain**: no package references (pure C#).
- **Application**: references Domain; packages MediatR 12.5.x, FluentValidation 12.x,
  Microsoft.Extensions.Logging.Abstractions, Microsoft.Extensions.Options. **Owns all ports**:
  `IClassRepository`, `IHoldRepository`, `IUnitOfWork`, `IUnitOfWorkTransaction`, `IIdGenerator`.
  No EF Core reference.
- **Infrastructure**: references Application; Npgsql.EntityFrameworkCore.PostgreSQL 10.x,
  EF Core Design (PrivateAssets). Implements all ports, hosts the sweeper.
- **Api**: references Application and Infrastructure (composition root only); Asp.Versioning.*,
  Microsoft.AspNetCore.OpenApi, Swashbuckle.AspNetCore.SwaggerUI, Scalar.AspNetCore,
  Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore.
- Controllers only talk to `ISender`. They never touch a repository or the DbContext.

---

## 2. Domain model

### 2.1 Types

```csharp
public sealed class Class
{
    public string Id { get; }            // "c_" + 32 hex
    public string Name { get; }          // trimmed, 1..200 chars
    public int TotalSeats { get; }       // 1..100_000
    public DateTime CreatedAt { get; }   // UTC, set by the DB (DEFAULT now()) — no app clock
    public static Class Create(string id, string name, int totalSeats);
}

public enum HoldStatus { Held, Confirmed, Cancelled, Expired }   // stored and serialized as HELD/CONFIRMED/CANCELLED/EXPIRED

public sealed class Hold
{
    public string Id { get; }            // "h_" + 32 hex
    public string ClassId { get; }
    public string StudentId { get; }     // 17 digits
    public HoldStatus Status { get; }    // stored status (may be a stale HELD)
    public DateTime CreatedAt { get; }
    public DateTime ExpiresAt { get; }
    public DateTime? ConfirmedAt { get; }
    public DateTime? CancelledAt { get; }
    public DateTime? ExpiredAt { get; }
}

public readonly record struct StudentId(string Value)
{
    public static bool TryCreate(string? raw, out StudentId id); // ^[0-9]{17}$, ASCII only
}

public static class HoldRules
{
    // Effective status: a HELD hold whose expiry has passed is EXPIRED, whatever the stored value.
    public static HoldStatus EffectiveStatus(HoldStatus stored, DateTime expiresAtUtc, DateTime dbNowUtc);
    // Used after a conditional UPDATE matched 0 rows, to explain why.
    // HoldSnapshot (Domain/Holds) carries DbNow, read from PostgreSQL together with the row.
    public static ConfirmOutcome ClassifyConfirm(HoldSnapshot? hold, string studentId);
    public static CancelOutcome  ClassifyCancel (HoldSnapshot? hold, string studentId);
}

public sealed record HoldSnapshot(string Id, string StudentId, HoldStatus Status, DateTime ExpiresAt, DateTime DbNow);
```

`dbNowUtc` always comes from PostgreSQL (`now()` / `clock_timestamp()`), never from `DateTime.UtcNow`.
`HoldRules` makes the decisions that unit tests can check. The **database conditional UPDATE**
is what actually changes state, so a race can never get past these rules.

### 2.2 Invariants

| # | Invariant | Enforced by |
|---|---|---|
| I1 | For every class: `count(HELD and expires_at > now) + count(CONFIRMED) <= total_seats` | Class row lock (`FOR UPDATE`) inside the create-hold transaction; confirm never adds to the count; cancel/expire only lower it. |
| I2 | At most one (HELD and not expired) or CONFIRMED hold per (student, class) | Check inside the locked transaction + partial unique index `ux_holds_class_student_active`. |
| I3 | A hold occupies a seat only while `status='HELD' AND expires_at > now()` or `status='CONFIRMED'` | All counts and reads compute this in SQL with the DB clock. |
| I4 | Only the owner's non-expired HELD hold can be confirmed; confirm is idempotent | One conditional UPDATE. |
| I5 | `total_seats` is in 1..100000; `student_id` matches `^[0-9]{17}$`; status is one of the 4 values | CHECK constraints + validators. |
| I6 | Terminal states (CONFIRMED, CANCELLED, EXPIRED) never change again | Every UPDATE has `WHERE status = 'HELD'`. |

### 2.3 State transitions

`HELD` is the only non-terminal state. The terminal states are `CONFIRMED`, `CANCELLED` and `EXPIRED`.

| From | To | Trigger | Who | Condition (in SQL) | Result |
|---|---|---|---|---|---|
| (none) | HELD | `POST /classes/{id}/holds` | Student (X-Student-Id) | class lock held; no active/confirmed hold for (student,class); occupied < total | 201 |
| HELD | CONFIRMED | `POST /holds/{id}/confirm` | Owner student only | `status='HELD' AND expires_at > now() AND student_id=@s` | 200 |
| HELD | CANCELLED | `DELETE /holds/{id}` | Owner student only | `status='HELD' AND expires_at > now() AND student_id=@s` | 204 |
| HELD | EXPIRED (effective) | time passes | Nobody (computed on read) | `status='HELD' AND expires_at <= now()` | reads show EXPIRED, seat already free |
| HELD | EXPIRED (stored) | sweeper / create-hold on same class | System | `status='HELD' AND expires_at <= now()` | row updated, `expired_at` set |
| CONFIRMED | CONFIRMED | confirm again | Owner | — | 200, no change (idempotent) |
| CONFIRMED | CANCELLED | `DELETE` | — | **not allowed** | 409 `HOLD_ALREADY_CONFIRMED` (Q1) |
| CANCELLED | CANCELLED | `DELETE` again | Owner | — | 204 (idempotent DELETE) |
| CANCELLED | CONFIRMED | confirm | — | not allowed | 409 `HOLD_CANCELLED` |
| EXPIRED | CONFIRMED | confirm | — | not allowed | 409 `HOLD_EXPIRED` |
| EXPIRED | CANCELLED | `DELETE` | — | not allowed | 409 `HOLD_EXPIRED` |
| any | any | action by another student | — | not allowed | 403 `FORBIDDEN` |

A student whose previous hold in a class is CANCELLED or EXPIRED may apply again. This creates a **new** hold
with a new id.

---

## 3. Database schema

All timestamps are `timestamptz`, stored in UTC. The connection string sets `Timezone=UTC`.
Names are snake_case, set explicitly in the EF configurations (no extra naming package).

### 3.1 Tables

```sql
CREATE TABLE classes (
    id           varchar(40)  PRIMARY KEY,
    name         varchar(200) NOT NULL,
    total_seats  integer      NOT NULL,
    created_at   timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT ck_classes_total_seats CHECK (total_seats BETWEEN 1 AND 100000),
    CONSTRAINT ck_classes_name_not_blank CHECK (length(btrim(name)) > 0)
);
CREATE INDEX ix_classes_created_at ON classes (created_at, id);

CREATE TABLE holds (
    id            varchar(40) PRIMARY KEY,
    class_id      varchar(40) NOT NULL REFERENCES classes(id) ON DELETE RESTRICT,
    student_id    varchar(17) NOT NULL,
    status        varchar(16) NOT NULL,
    created_at    timestamptz NOT NULL,
    expires_at    timestamptz NOT NULL,
    confirmed_at  timestamptz NULL,
    cancelled_at  timestamptz NULL,
    expired_at    timestamptz NULL,
    CONSTRAINT ck_holds_status     CHECK (status IN ('HELD','CONFIRMED','CANCELLED','EXPIRED')),
    CONSTRAINT ck_holds_student_id CHECK (student_id ~ '^[0-9]{17}$'),
    CONSTRAINT ck_holds_confirmed_at CHECK (status <> 'CONFIRMED' OR confirmed_at IS NOT NULL)
);

-- I2: one active-or-confirmed hold per (class, student)
CREATE UNIQUE INDEX ux_holds_class_student_active
    ON holds (class_id, student_id)
    WHERE status IN ('HELD','CONFIRMED');

-- seat counting per class
CREATE INDEX ix_holds_class_status_expires ON holds (class_id, status, expires_at);
-- GET /me/holds
CREATE INDEX ix_holds_student_created ON holds (student_id, created_at DESC);
-- sweeper
CREATE INDEX ix_holds_held_expires ON holds (expires_at) WHERE status = 'HELD';
```

EF Core mapping: `HasIndex(h => new { h.ClassId, h.StudentId }).IsUnique().HasFilter("status IN ('HELD','CONFIRMED')")
.HasDatabaseName("ux_holds_class_student_active")`; CHECKs via `ToTable(t => t.HasCheckConstraint(...))`;
`Status` via `HasConversion(v => v.ToString().ToUpperInvariant(), s => Enum.Parse<HoldStatus>(s, true))`.

### 3.2 The partial index and expired-but-not-swept rows

A partial index predicate must be IMMUTABLE, so it **cannot** use `now()`. The index therefore covers
every row with stored `status IN ('HELD','CONFIRMED')`, including a HELD row whose `expires_at` has passed
but that the sweeper has not updated yet. Without extra care, that stale row would block a legitimate
re-application (it would raise a unique violation).

How we deal with it: the create-hold transaction, **while holding the class row lock**, first runs
`UPDATE holds SET status='EXPIRED' ... WHERE class_id=@c AND status='HELD' AND expires_at <= @now`
(step 3 in §4.1). After that, no stale HELD row for this class is left inside the index predicate, so:
- the in-transaction duplicate check (step 4) sees only truly active or confirmed holds, and
- the INSERT (step 6) can only hit the unique index when a real duplicate exists. That cannot happen
  because of step 4 under the lock, so the index is defence in depth. If it ever fires (`23505` on
  `ux_holds_class_student_active`), the repository maps it to `DUPLICATE_APPLICATION` (409).

### 3.3 IDs

- Format: `c_` + 32 lowercase hex for classes, `h_` + 32 lowercase hex for holds,
  e.g. `h_01936f3e8b2a7c4d9e0f112233445566`. Total length 34; columns are `varchar(40)`.
- Generated in the app by `IIdGenerator` (Infrastructure: `GuidV7IdGenerator`), using
  `Guid.CreateVersion7().ToString("N")`. The IDs are time-ordered, so index locality is good. 74 random bits make them hard to guess.
- Route values that do not exist (including badly formed ones) return 404 `NOT_FOUND`, not 400.

---

## 4. Concurrency design

Principles:
1. **The database is the only place where coordination happens.** No `lock`, `SemaphoreSlim`, `Mutex`,
   in-memory cache or "check in C# then insert" anywhere in the write path.
2. **Every expiry decision uses the DB clock.** Single-statement operations use `now()`. The create-hold transaction
   uses one `clock_timestamp()` value read **after** it gets the lock (see why below).
3. Isolation level: **READ COMMITTED** (the PostgreSQL default). Do **not** use REPEATABLE READ here: its
   snapshot is taken at the first statement, so after waiting on the lock it would count holds using an
   old snapshot and could oversell.
4. Npgsql `EnableRetryOnFailure` is **not** enabled. It does not work with user-started transactions, and we do
   not need it: the startup migration has its own retry loop.

### 4.1 Create hold (`POST /classes/{classId}/holds`)

Repository calls run inside `IUnitOfWork.BeginTransactionAsync` (READ COMMITTED) on one connection:

```sql
BEGIN;  -- READ COMMITTED

-- 1. Serialize all hold creation for THIS class, across all processes.
SELECT id, total_seats
FROM classes
WHERE id = @classId
FOR UPDATE;
-- 0 rows -> ROLLBACK, 404 NOT_FOUND

-- 2. DB time, read AFTER the lock is acquired (now() would be the tx start time, which may be
--    seconds earlier if we waited on the lock).
SELECT clock_timestamp() AS db_now;           -- -> @now (used by every statement below)

-- 3. Materialize expiry for this class so stale HELD rows leave the partial unique index.
UPDATE holds
SET status = 'EXPIRED', expired_at = @now
WHERE class_id = @classId
  AND status = 'HELD'
  AND expires_at <= @now;

-- 4. Duplicate check (I2).
SELECT EXISTS (
    SELECT 1 FROM holds
    WHERE class_id = @classId
      AND student_id = @studentId
      AND ( (status = 'HELD' AND expires_at > @now) OR status = 'CONFIRMED' )
) AS has_active;
-- true -> ROLLBACK, 409 DUPLICATE_APPLICATION   (checked before seats: more useful message)

-- 5. Count occupied seats (I1). Fresh READ COMMITTED snapshot, taken after the lock.
SELECT count(*) AS occupied
FROM holds
WHERE class_id = @classId
  AND ( (status = 'HELD' AND expires_at > @now) OR status = 'CONFIRMED' );
-- occupied >= total_seats -> ROLLBACK, 409 SEATS_FULL

-- 6. Insert.
INSERT INTO holds (id, class_id, student_id, status, created_at, expires_at)
VALUES (@holdId, @classId, @studentId, 'HELD', @now,
        ceil_to_second(@now) + make_interval(secs => @holdDurationSeconds))   -- see note below
RETURNING id, class_id, student_id, status, created_at, expires_at;

COMMIT;
```

`expires_at` is rounded UP to a whole second (implemented as a CASE on `date_trunc`), so the stored value and the JSON
value (`...:00Z`) are exactly the same, and a hold lasts between `HOLD_DURATION_SECONDS` and `HOLD_DURATION_SECONDS + 1`
seconds, never less (changed in checkpoint 8 after code review finding S1; it was rounded down before).

**Why this is correct across multiple processes.** `FOR UPDATE` takes a row-level lock in PostgreSQL. It does not
matter which process, container or connection asks for it. Two transactions for the same class, from
any number of backend instances, cannot both be between step 1 and COMMIT. The second one waits at step 1
until the first one commits. Under READ COMMITTED, each later statement takes a new snapshot, so the
second transaction's step 5 sees the first one's committed INSERT. "Count then insert" is therefore
atomic per class. Holds for different classes do not block each other.

**Why the other write paths cannot break I1 without taking the class lock:**
- Confirm moves HELD (counted) to CONFIRMED (counted): the occupied count does not change.
- Cancel and expire only lower the count.
- Race between confirm and step 3 on the same hold row: both are UPDATEs with `WHERE status='HELD'`. The
  second one waits for the row lock and then re-checks its WHERE against the new row version (PostgreSQL
  EvalPlanQual), so exactly one wins. If confirm wins, step 5 counts the row as CONFIRMED. If expire wins,
  confirm matches 0 rows and returns 409 `HOLD_EXPIRED`.

**Why in-process locks are wrong.** A `SemaphoreSlim`/`lock` exists only inside one process. With two
backend instances (the bonus, or any scale-out), each process has its own lock. Both can count 9/10 and
both insert, which gives 11/10. A C# "read count, then insert" is also wrong even in one process without a lock,
because two requests interleave between the read and the write. Only the database sees every writer.

**Deadlock analysis.** Create-hold locks exactly one class row, then hold rows of that class. Confirm and cancel
lock one hold row. The sweeper locks hold rows with `SKIP LOCKED`, so it never waits. No transaction waits on a
class lock while holding a hold lock, so no cycle can form.

**Why not a single `INSERT ... SELECT ... WHERE count < total`?** Under READ COMMITTED, two such statements
take their snapshots at the same time, both see 9, and both insert. It is only safe with an extra lock or
SERIALIZABLE plus retries. The class row lock is simpler and easy to reason about.

**Throughput.** All holds for one class run one after another. Each transaction is 5 short statements on
indexed data (a few ms), so one class handles hundreds of holds per second. That is enough for this
assignment. The integration test sends 200 parallel requests.

### 4.2 Confirm (`POST /holds/{holdId}/confirm`)

One atomic, auto-committed statement:

```sql
UPDATE holds
SET status = 'CONFIRMED', confirmed_at = now()
WHERE id = @holdId
  AND student_id = @studentId
  AND status = 'HELD'
  AND expires_at > now()
RETURNING id, class_id, status, expires_at, confirmed_at;
```

- 1 row: return 200 `{ holdId, status: "CONFIRMED", ... }`.
- 0 rows: read the row to explain why (see the next query), then `HoldRules.ClassifyConfirm`:

```sql
SELECT id, class_id, student_id, status, expires_at, confirmed_at, cancelled_at, now() AS db_now
FROM holds WHERE id = @holdId;
```

| Situation (checked in this order) | Response |
|---|---|
| no row | 404 `NOT_FOUND` |
| `student_id <> @studentId` | 403 `FORBIDDEN` (ownership is checked before status, so status is not leaked) |
| `status = 'CONFIRMED'` | **200** `{ holdId, status: "CONFIRMED" }` (idempotent, nothing written) |
| `status = 'CANCELLED'` | 409 `HOLD_CANCELLED` |
| `status = 'EXPIRED'` or (`HELD` and `expires_at <= db_now`) | 409 `HOLD_EXPIRED` |
| `HELD` and `expires_at > db_now` (should not happen) | retry the UPDATE once, then 409 `CONFLICT` |

Idempotency: the UPDATE can only match while `status='HELD'`, so the row changes from HELD to CONFIRMED at
most once, no matter how many parallel confirms arrive. `confirmedSeats` therefore grows by exactly 1.
Parallel confirms of the same hold: one gets 1 row, the others wait on the row lock, re-check, match 0 rows,
read CONFIRMED and return 200.

### 4.3 Cancel (`DELETE /holds/{holdId}`)

```sql
UPDATE holds
SET status = 'CANCELLED', cancelled_at = now()
WHERE id = @holdId
  AND student_id = @studentId
  AND status = 'HELD'
  AND expires_at > now()
RETURNING id;
```

- 1 row: 204.
- 0 rows: use the same diagnostic SELECT, then `HoldRules.ClassifyCancel`: no row → 404 `NOT_FOUND`; other
  student → 403 `FORBIDDEN`; `CANCELLED` → **204** (idempotent DELETE); `CONFIRMED` → 409
  `HOLD_ALREADY_CONFIRMED`; expired (stored or effective) → 409 `HOLD_EXPIRED`.

### 4.4 Expiry

**(a) Computed on read: correct even if no job has ever run.** Every read and count uses the DB clock:

```sql
CASE WHEN h.status = 'HELD' AND h.expires_at <= now() THEN 'EXPIRED' ELSE h.status END AS status
```

and seat counts use `status='HELD' AND expires_at > now()`. When a hold expires, its seat is free for the
very next request, and its status reads as `EXPIRED`. No API call or background job is needed.

**(b) Sweeper: `HoldExpirySweeper : BackgroundService`** (Infrastructure/BackgroundJobs) writes the
stored state so it matches, for tidiness and for the partial index:

- Loop: `PeriodicTimer(TimeSpan.FromSeconds(SweepIntervalSeconds))`. Each tick creates a DI scope and sends
  `ExpireStaleHoldsCommand(BatchSize)` through MediatR. The handler repeats `ExpireStaleBatchAsync` until it
  returns fewer than `BatchSize`. Exceptions are logged and swallowed; the next tick tries again.
  `OperationCanceledException` on shutdown ends the loop.
- Config: `HOLD_SWEEP_INTERVAL_SECONDS` (default 10), `HOLD_SWEEP_BATCH_SIZE` (default 500),
  `HOLD_SWEEPER_ENABLED` (default true; the expiry-on-read test turns it off).
- SQL (auto-commit, one statement per batch):

```sql
UPDATE holds h
SET status = 'EXPIRED', expired_at = now()
WHERE h.id IN (
    SELECT id FROM holds
    WHERE status = 'HELD' AND expires_at <= now()
    ORDER BY expires_at
    LIMIT @batchSize
    FOR UPDATE SKIP LOCKED
)
AND h.status = 'HELD';
```

- **Safe in several processes at once:** the UPDATE is idempotent (`status='HELD'` guard). `SKIP LOCKED`
  lets two sweepers split the work instead of blocking each other. A row that a confirm or cancel has locked
  is skipped, and the next tick handles it if it is still stale. No leader election is needed.

### 4.5 Seat counts (one SQL statement)

```sql
SELECT c.id,
       c.name,
       c.total_seats,
       c.created_at,
       count(h.id) FILTER (WHERE h.status = 'HELD' AND h.expires_at > now()) AS held_seats,
       count(h.id) FILTER (WHERE h.status = 'CONFIRMED')                    AS confirmed_seats,
       GREATEST(0, c.total_seats
                   - count(h.id) FILTER (WHERE h.status = 'HELD' AND h.expires_at > now())
                   - count(h.id) FILTER (WHERE h.status = 'CONFIRMED'))      AS available_seats
FROM classes c
LEFT JOIN holds h
       ON h.class_id = c.id
      AND h.status IN ('HELD','CONFIRMED')
WHERE (@classId IS NULL OR c.id = @classId)
GROUP BY c.id
ORDER BY c.created_at, c.id;
```

(Two repository methods use the same SQL: one with `@classId`, one without.)

**As implemented (Checkpoint 2):** the code uses two separate statements, because Npgsql cannot infer a type for
a NULL `@classId`. Each statement uses a `CROSS JOIN LATERAL` subquery with the same `FILTER` counts
(cast to `int`); the semantics are unchanged. See `ClassRepository`.

### 4.6 Hold reads

```sql
-- GET /holds/{id}
SELECT h.id, h.class_id, c.name AS class_name, h.student_id,
       CASE WHEN h.status='HELD' AND h.expires_at <= now() THEN 'EXPIRED' ELSE h.status END AS status,
       h.created_at, h.expires_at, h.confirmed_at, h.cancelled_at, now() AS db_now
FROM holds h JOIN classes c ON c.id = h.class_id
WHERE h.id = @holdId;

-- GET /me/holds
SELECT ... same columns ...
FROM holds h JOIN classes c ON c.id = h.class_id
WHERE h.student_id = @studentId
ORDER BY h.created_at DESC, h.id DESC;
```

---

## 5. Application layer (MediatR 12.5.x + FluentValidation)

### 5.1 Result type

```csharp
public enum ErrorKind { Validation, NotFound, Forbidden, Conflict, Unexpected }
public sealed record Error(string Code, string Message, ErrorKind Kind);
public class Result { bool IsSuccess; Error? Error; static Result Success(); static Result Failure(Error e); }
public sealed class Result<T> : Result { T? Value; }
```

Expected business outcomes (409, 404, 403) are `Result` failures, not exceptions. Validation failures are
thrown by `ValidationBehavior` as `FluentValidation.ValidationException` and turned into 400 by the global
exception handler.

### 5.2 Commands and queries

| Request | Returns | Handler | Validator rules |
|---|---|---|---|
| `CreateClassCommand(string? Name, int? TotalSeats)` | `Result<ClassDto>` | `CreateClassHandler`: `IIdGenerator.NewClassId()`, `IClassRepository.AddAsync`, `IUnitOfWork.SaveChangesAsync`, then `GetSeatsAsync` | Name: not null/blank after trim, max 200 → `VALIDATION_FAILED`. TotalSeats: not null, 1..100000 → `VALIDATION_FAILED` |
| `ListClassesQuery()` | `IReadOnlyList<ClassDto>` | `ListClassesHandler`: `IClassRepository.ListSeatsAsync` | none |
| `GetClassQuery(string ClassId)` | `Result<ClassDto>` | `GetClassHandler`: `GetSeatsAsync`, null → `NOT_FOUND` | ClassId not empty, max 40 (otherwise treated as 404 by the handler, not 400) |
| `CreateHoldCommand(string ClassId, string? StudentId)` | `Result<HoldCreatedDto>` | `CreateHoldHandler`: the §4.1 transaction | StudentId `^[0-9]{17}$` → `INVALID_STUDENT_ID` |
| `GetHoldQuery(string HoldId)` | `Result<HoldDto>` | `GetHoldHandler`: `GetViewAsync` | none (unknown id → 404) |
| `ConfirmHoldCommand(string HoldId, string? StudentId)` | `Result<HoldConfirmedDto>` | `ConfirmHoldHandler`: §4.2 | StudentId rule |
| `CancelHoldCommand(string HoldId, string? StudentId)` | `Result` | `CancelHoldHandler`: §4.3 | StudentId rule |
| `GetMyHoldsQuery(string? StudentId)` | `Result<IReadOnlyList<MyHoldDto>>` | `GetMyHoldsHandler`: `ListByStudentAsync` | StudentId rule |
| `ExpireStaleHoldsCommand(int BatchSize)` | `int` (rows expired) | `ExpireStaleHoldsHandler`: loops `ExpireStaleBatchAsync` | BatchSize 1..10000 |

`StudentIdRules.ValidStudentId()` is a shared rule extension:
`RuleFor(x => x.StudentId).NotEmpty().Matches("^[0-9]{17}$").WithErrorCode("INVALID_STUDENT_ID")`
(`[0-9]` on purpose: `\d` in .NET also matches non-ASCII Unicode digits).

### 5.3 Pipeline behaviors (registration order = execution order, outermost first)

1. `LoggingBehavior<TRequest,TResponse>`: logs request name, elapsed ms and outcome
   (`Success` / failure `Error.Code` / exception type) as structured fields. Never logs the full student ID
   (it is masked to the last 4 digits).
2. `ValidationBehavior<TRequest,TResponse>`: runs all `IValidator<TRequest>`; on failure it throws
   `ValidationException`.

```csharp
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<CreateClassCommand>();
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});
services.AddValidatorsFromAssemblyContaining<CreateClassValidator>(includeInternalTypes: true);
```

### 5.4 DTOs (Application)

```csharp
public sealed record ClassDto(string Id, string Name, int TotalSeats, int HeldSeats, int ConfirmedSeats,
                              int AvailableSeats, DateTime CreatedAt);
public sealed record HoldCreatedDto(string HoldId, string ClassId, string StudentId, HoldStatus Status,
                                    DateTime ExpiresAt, string ClassName, DateTime CreatedAt, DateTime ServerTime);
public sealed record HoldDto(string HoldId, string ClassId, HoldStatus Status, DateTime ExpiresAt,
                             string StudentId, string ClassName, DateTime CreatedAt,
                             DateTime? ConfirmedAt, DateTime? CancelledAt, DateTime ServerTime);
public sealed record HoldConfirmedDto(string HoldId, HoldStatus Status, string ClassId, DateTime ExpiresAt,
                                      DateTime? ConfirmedAt);
public sealed record MyHoldDto(string HoldId, string ClassId, string ClassName, HoldStatus Status,
                               DateTime ExpiresAt, DateTime CreatedAt, DateTime? ConfirmedAt);
```

---

## 6. Repositories and Unit of Work (Application ports, Infrastructure implementations)

```csharp
namespace SchoolAdmission.Application.Abstractions.Persistence;

public interface IUnitOfWork
{
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct); // READ COMMITTED
    Task<int> SaveChangesAsync(CancellationToken ct);
}

public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct);
    Task RollbackAsync(CancellationToken ct);   // DisposeAsync rolls back if not committed
}

public interface IClassRepository
{
    void Add(Class entity);                                                       // EF tracked insert
    Task<ClassLockRow?> LockForHoldCreationAsync(string classId, CancellationToken ct); // §4.1 steps 1+2
    Task<ClassSeatsRow?> GetSeatsAsync(string classId, CancellationToken ct);     // §4.5
    Task<IReadOnlyList<ClassSeatsRow>> ListSeatsAsync(CancellationToken ct);      // §4.5
}

public interface IHoldRepository
{
    Task<int>  ExpireStaleForClassAsync(string classId, DateTime dbNow, CancellationToken ct);        // §4.1 step 3
    Task<bool> HasActiveOrConfirmedAsync(string classId, string studentId, DateTime dbNow, CancellationToken ct); // step 4
    Task<int>  CountOccupiedSeatsAsync(string classId, DateTime dbNow, CancellationToken ct);         // step 5
    Task<HoldRow> InsertHeldAsync(string holdId, string classId, string studentId, DateTime dbNow,
                                  int holdDurationSeconds, CancellationToken ct);                     // step 6; throws DuplicateHoldException on 23505
    Task<HoldRow?> TryConfirmAsync(string holdId, string studentId, CancellationToken ct);            // §4.2
    Task<bool> TryCancelAsync(string holdId, string studentId, CancellationToken ct);                 // §4.3
    Task<HoldSnapshot?> GetSnapshotAsync(string holdId, CancellationToken ct);                        // diagnostic read incl. db_now
    Task<HoldRow?> GetViewAsync(string holdId, CancellationToken ct);                                 // §4.6
    Task<IReadOnlyList<HoldRow>> ListByStudentAsync(string studentId, CancellationToken ct);          // §4.6
    Task<int> ExpireStaleBatchAsync(int batchSize, CancellationToken ct);                             // §4.4b
}

public sealed record ClassLockRow(string Id, string Name, int TotalSeats, DateTime DbNow);
public sealed record ClassSeatsRow(string Id, string Name, int TotalSeats, int HeldSeats, int ConfirmedSeats,
                                   int AvailableSeats, DateTime CreatedAt);
public sealed record HoldRow(string Id, string ClassId, string ClassName, string StudentId, HoldStatus Status,
                             DateTime CreatedAt, DateTime ExpiresAt, DateTime? ConfirmedAt,
                             DateTime? CancelledAt, DateTime DbNow);
// HoldSnapshot lives in Domain/Holds (HoldRules consumes it); see §2.1.
```

`DuplicateHoldException` lives in Application/Abstractions/Persistence, so the handler can turn it into
`DUPLICATE_APPLICATION` without depending on Npgsql.

**Where raw SQL is used, and why.** EF Core LINQ cannot express `FOR UPDATE`, `SKIP LOCKED`, `FILTER (WHERE ...)`,
`clock_timestamp()`, `make_interval` or `UPDATE ... RETURNING`. Npgsql also turns `DateTime.UtcNow` into a
**client-side parameter**, which would break rule 3 (DB clock). So:
- Raw parameterized SQL (`Database.SqlQuery<T>($"...")` / `ExecuteSql($"...")` with interpolated
  `FormattableString`, which is always parameterized) is used for: the lock, all expiry/seat logic,
  confirm/cancel UPDATEs, the sweeper, and the hold/seat read models.
- EF Core is used for: the schema and migrations, `Class` inserts (`Add` + `SaveChangesAsync`), and transactions
  (`Database.BeginTransactionAsync(IsolationLevel.ReadCommitted)`).
- All raw SQL is in the two repository classes, nowhere else.

`UnitOfWork` wraps the scoped `AdmissionDbContext`. Repositories share the same scoped context, so raw SQL
inside a transaction runs on the transaction's connection.

---

## 7. Configuration

| Key (env var) | Default | Used by |
|---|---|---|
| `ConnectionStrings__Default` | — | Infrastructure (highest priority) |
| `DATABASE_URL` | — | Used if the above is missing. Accepts `postgres://user:pass@host:port/db` or Npgsql keyword form |
| `HOLD_DURATION_SECONDS` | `600` | `HoldOptions.HoldDurationSeconds` (must be >= 1, checked at startup) |
| `HOLD_SWEEP_INTERVAL_SECONDS` | `10` | sweeper |
| `HOLD_SWEEP_BATCH_SIZE` | `500` | sweeper |
| `HOLD_SWEEPER_ENABLED` | `true` | sweeper |
| `CORS_ORIGINS` | `http://localhost:3000,http://localhost:5173` | CORS policy; comma-separated; `*` = any origin |
| `ASPNETCORE_HTTP_PORTS` | `8080` (image default) | Kestrel |
| `DB_MIGRATE_ON_STARTUP` | `true` | `DatabaseMigrator` |
| `DB_MIGRATE_MAX_ATTEMPTS` | `15` | `DatabaseMigrator` |

The env vars are read from root `IConfiguration` keys (for example `configuration["HOLD_DURATION_SECONDS"]`), so they also
work from `appsettings.json` and test overrides. A missing connection string makes startup fail with a clear
message.

---

## 8. API

### 8.1 Cross-cutting

- Controllers with `[ApiController]`. Routes are attributes with the **exact** spec paths: `[Route("classes")]`,
  `[Route("holds")]`, `[Route("me")]`. No global prefix.
- JSON: `System.Text.Json`, camelCase, `JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper)` so that
  `HoldStatus.Held` → `"HELD"`. `UtcDateTimeConverter` writes `yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'`
  (trailing zeros and the dot are dropped, so `expiresAt` looks like `2026-11-01T04:10:00Z`). It always
  converts to UTC first. `DefaultIgnoreCondition = Never` (null extras are serialized as `null`).
- Errors: RFC 7807 `application/problem+json` with `type`, `title`, `status`, `detail`, `instance`, plus
  extensions **`code`** and `traceId`. Validation errors also include `errors: { field: [messages] }`.
  `AddProblemDetails(o => o.CustomizeProblemDetails = ...)` makes sure **every** ProblemDetails
  has a `code`, including framework ones (404 on unknown route → `NOT_FOUND`, 405 → `METHOD_NOT_ALLOWED`,
  415 → `UNSUPPORTED_MEDIA_TYPE`, 500 → `INTERNAL_ERROR`).
  `ApiBehaviorOptions.InvalidModelStateResponseFactory` turns model-binding errors (bad JSON,
  `"totalSeats": "abc"`) into 400 `VALIDATION_FAILED`.
- `GlobalExceptionHandler : IExceptionHandler`: `ValidationException` → 400. If any failure has error code
  `INVALID_STUDENT_ID`, the problem `code` is `INVALID_STUDENT_ID`; otherwise `VALIDATION_FAILED`.
  Anything else → 500 `INTERNAL_ERROR` (no stack trace in the body).
- Validation order: header/body validation (400) runs before any lookup (404/403/409).
- `ServerTimeHeaderMiddleware` adds `X-Server-Time: <UTC ISO-8601 ms>` to every response. CORS exposes
  `X-Server-Time`, `Location`, `api-supported-versions`.
- CORS: policy `"frontend"` = `WithOrigins(CORS_ORIGINS split)`, `AllowAnyHeader()` (needed for `X-Student-Id`
  and `api-version`), `AllowAnyMethod()`. `*` → `AllowAnyOrigin()` (no credentials are used).
- Health: `GET /health` (liveness, no DB) and `GET /health/ready` (EF DbContext check). These are minimal-API endpoints,
  not versioned, and hidden from OpenAPI.
- `GET /` → 302 to `/swagger`.

### 8.2 API versioning (paths stay exactly as in the spec)

```csharp
builder.Services.AddApiVersioning(o =>
{
    o.DefaultApiVersion = new ApiVersion(1, 0);
    o.AssumeDefaultVersionWhenUnspecified = true;
    o.ReportApiVersions = true;                                  // api-supported-versions header
    o.ApiVersionReader = ApiVersionReader.Combine(
        new HeaderApiVersionReader("api-version"),
        new QueryStringApiVersionReader("api-version"));
})
.AddMvc()
.AddApiExplorer(o => { o.GroupNameFormat = "'v'VVV"; o.SubstituteApiVersionInUrl = false; })
.AddOpenApi();                                                   // Asp.Versioning.OpenApi bridge
```

- Every controller has `[ApiVersion("1.0")]`. A request with no version gets 1.0. `?api-version=1.0` or the header
  `api-version: 1.0` also work. An unsupported version (`2.0`) → 400 with `code: "UNSUPPORTED_API_VERSION"`.
- There is no URL-segment reader, so **no path ever changes**. A future v2 would be a new controller with
  `[ApiVersion("2.0")]` on the same path, picked by header or query.
- Optional `/api/v1/...` aliases are **not added** (Q3). If they are wanted, we add a second
  `[Route("api/v{version:apiVersion}/classes")]` on each controller and a `UrlSegmentApiVersionReader` to
  `Combine(...)`, and we keep the unprefixed routes.

### 8.3 OpenAPI, Swagger UI, Scalar (.NET 10, ALL environments)

- Document generation: `Microsoft.AspNetCore.OpenApi` 10.x (built in, OpenAPI 3.1), connected to versioning by
  `Asp.Versioning.OpenApi`. `app.MapOpenApi().WithDocumentPerVersion();` → `/openapi/v1.json`.
  Fallback if the bridge causes trouble: `builder.Services.AddOpenApi("v1")`. The default
  `ShouldInclude` matches ApiExplorer group `v1`.
- Document transformer: title "School Admission API", version 1.0, description, and the `X-Student-Id`
  header documented with pattern `^[0-9]{17}$`.
- Swagger UI: `Swashbuckle.AspNetCore.SwaggerUI` (UI only, no SwaggerGen):
  `app.UseSwaggerUI(o => { o.RoutePrefix = "swagger"; o.SwaggerEndpoint("/openapi/v1.json", "v1"); });`
- Scalar: `Scalar.AspNetCore` 2.x: `app.MapScalarApiReference("/scalar", o => o.WithTitle("School Admission API"));`
  (it reads `/openapi/{documentName}.json` by default).
- None of these are inside `if (app.Environment.IsDevelopment())`.

### 8.4 Endpoints

Common error rows used below:
`400 INVALID_STUDENT_ID` = X-Student-Id is missing, or is not exactly 17 ASCII digits.
`400 UNSUPPORTED_API_VERSION` = possible on every endpoint.
`500 INTERNAL_ERROR` = possible on every endpoint.

#### 1. `POST /classes` (no header)
Request: `{ "name": "Class 6 - Morning Shift", "totalSeats": 120 }`
| Status | Body | code |
|---|---|---|
| 201 | `{ id, name, totalSeats }` + extras `heldSeats: 0, confirmedSeats: 0, availableSeats, createdAt`; header `Location: /classes/{id}` | — |
| 400 | ProblemDetails + `errors` | `VALIDATION_FAILED` (name missing/blank/>200, totalSeats missing/<1/>100000/not integer, malformed JSON) |
| 415 | ProblemDetails | `UNSUPPORTED_MEDIA_TYPE` |

Duplicate names are allowed (Q2).

#### 2. `GET /classes`
| Status | Body |
|---|---|
| 200 | `[{ id, name, totalSeats, heldSeats, confirmedSeats, availableSeats, createdAt }]`, oldest first. An empty list is `[]`. |

#### 3. `GET /classes/{classId}`
| Status | Body | code |
|---|---|---|
| 200 | `{ id, name, totalSeats, heldSeats, confirmedSeats, availableSeats, createdAt }` | — |
| 404 | ProblemDetails | `NOT_FOUND` |

#### 4. `POST /classes/{classId}/holds` (header `X-Student-Id`, no body; any body is ignored)
| Status | Body | code |
|---|---|---|
| 201 | `{ holdId, classId, studentId, status: "HELD", expiresAt }` + extras `className, createdAt, serverTime`; `Location: /holds/{holdId}` | — |
| 400 | ProblemDetails | `INVALID_STUDENT_ID` |
| 404 | ProblemDetails | `NOT_FOUND` (class) |
| 409 | ProblemDetails | `DUPLICATE_APPLICATION` (student already has an active or confirmed hold here; checked first) |
| 409 | ProblemDetails | `SEATS_FULL` |

#### 5. `GET /holds/{holdId}` (header not required; ignored if sent)
| Status | Body | code |
|---|---|---|
| 200 | `{ holdId, classId, status, expiresAt }` + extras `studentId, className, createdAt, confirmedAt, cancelledAt, serverTime`. `status` is the **effective** status. | — |
| 404 | ProblemDetails | `NOT_FOUND` |

#### 6. `POST /holds/{holdId}/confirm` (header `X-Student-Id`)
| Status | Body | code |
|---|---|---|
| 200 | `{ holdId, status: "CONFIRMED" }` + extras `classId, expiresAt, confirmedAt`. Also returned when it was already CONFIRMED (idempotent). | — |
| 400 | ProblemDetails | `INVALID_STUDENT_ID` |
| 403 | ProblemDetails | `FORBIDDEN` (hold belongs to another student) |
| 404 | ProblemDetails | `NOT_FOUND` |
| 409 | ProblemDetails | `HOLD_EXPIRED` |
| 409 | ProblemDetails | `HOLD_CANCELLED` |

#### 7. `DELETE /holds/{holdId}` (header `X-Student-Id`)
| Status | Body | code |
|---|---|---|
| 204 | empty (also when it was already CANCELLED) | — |
| 400 | ProblemDetails | `INVALID_STUDENT_ID` |
| 403 | ProblemDetails | `FORBIDDEN` |
| 404 | ProblemDetails | `NOT_FOUND` |
| 409 | ProblemDetails | `HOLD_ALREADY_CONFIRMED` (Q1) |
| 409 | ProblemDetails | `HOLD_EXPIRED` |

#### 8. `GET /me/holds` (header `X-Student-Id`)
| Status | Body | code |
|---|---|---|
| 200 | `[{ holdId, classId, className, status, expiresAt }]` + extras `createdAt, confirmedAt`. All statuses (effective), newest first; `[]` if none. | — |
| 400 | ProblemDetails | `INVALID_STUDENT_ID` |

### 8.5 Error code catalogue (`ErrorCodes.cs`)

`VALIDATION_FAILED`, `INVALID_STUDENT_ID`, `NOT_FOUND`, `FORBIDDEN`, `SEATS_FULL`, `DUPLICATE_APPLICATION`,
`HOLD_EXPIRED`, `HOLD_CANCELLED`, `HOLD_ALREADY_CONFIRMED`, `CONFLICT`, `UNSUPPORTED_API_VERSION`,
`UNSUPPORTED_MEDIA_TYPE`, `METHOD_NOT_ALLOWED`, `INTERNAL_ERROR`.

### 8.6 Startup sequence (`Program.cs`)

1. Build config; resolve the connection string (fail fast if missing).
2. `AddApplication()`, `AddInfrastructure(config)` (DbContext pooling off; scoped context), hosted `HoldExpirySweeper`.
3. Controllers + JSON options, ProblemDetails, exception handler, versioning, OpenAPI, CORS, health checks.
4. `await app.Services.MigrateDatabaseWithRetryAsync()`: up to `DB_MIGRATE_MAX_ATTEMPTS` attempts with
   exponential backoff (1 s, 2 s, 4 s ... capped at 10 s) on `NpgsqlException`/`SocketException`. It logs each attempt.
   EF Core 9+ takes a migration lock, so two instances that start together do not run migrations at the same time.
   The sweeper only starts after migration: hosted services start in `app.Run`, which comes after this step.
5. Middleware: `UseExceptionHandler()`, `UseStatusCodePages()`, `ServerTimeHeaderMiddleware`, `UseCors("frontend")`,
   `MapOpenApi`, `UseSwaggerUI`, `MapScalarApiReference`, `MapControllers`, `MapHealthChecks`.
6. `public partial class Program;` for `WebApplicationFactory`.

---

## 9. Frontend

```
frontend/
  index.html                  # <script src="/env.js"></script> before the module script
  public/env.js               # dev default: window.__ENV__ = {};
  vite.config.ts              # vitest config: jsdom, setupFiles
  package.json                # react, react-dom, react-router-dom, @tanstack/react-query; dev: vitest, @testing-library/*, msw, jsdom
  src/
    main.tsx                  # QueryClientProvider, BrowserRouter, StudentIdProvider, ErrorBoundary
    App.tsx                   # routes + Layout
    config.ts                 # apiBaseUrl()
    api/client.ts             # fetchJson(): adds X-Student-Id, parses ProblemDetails, records server clock offset
    api/errors.ts             # ApiError, NetworkError, toUserMessage(err)
    api/types.ts              # ClassSeats, Hold, MyHold, HoldStatus, ProblemDetails
    api/hooks.ts              # TanStack Query hooks
    state/StudentIdContext.tsx
    time/serverClock.ts       # offset store + useServerNow()
    hooks/useCountdown.ts
    components/Layout.tsx, StudentIdBar.tsx, ClassTable.tsx, CreateClassForm.tsx,
               HoldStatusBadge.tsx, Countdown.tsx, ErrorAlert.tsx, ErrorBoundary.tsx, ConnectionBanner.tsx
    pages/ClassListPage.tsx, CreateClassPage.tsx, HoldPage.tsx, MyHoldsPage.tsx, NotFoundPage.tsx
    test/setup.ts, test/server.ts (MSW handlers), test/utils.tsx (renderWithProviders)
  docker/nginx.conf
  docker/40-write-env.sh
  Dockerfile                  # stages: deps, test, build, runtime(nginx)
```

### 9.1 Routes

| Route | Page | Content |
|---|---|---|
| `/` | `ClassListPage` | Table of classes: name, total, held, confirmed, available, an **Apply** button (disabled with a hint if there is no valid student ID or available = 0). Apply → POST, then navigate to `/holds/:holdId`. |
| `/admin/classes/new` | `CreateClassPage` | `CreateClassForm` (name, totalSeats). On success, show a toast and go to `/`. |
| `/holds/:holdId` | `HoldPage` | Class name, status badge, countdown, **Pay admission fee** (enabled only if effective status is HELD and remaining > 0 and the hold belongs to the current student), **Cancel**. |
| `/me` | `MyHoldsPage` | All holds of the current student, each linking to `/holds/:holdId`, with live status. |
| `*` | `NotFoundPage` | |

`Layout` has a header with nav links (Classes / My applications / Create class) and `StudentIdBar` at the top
of every page.

### 9.2 State

- Student ID: `StudentIdContext`, stored in `localStorage["admission.studentId"]`, read on load. `StudentIdBar`
  accepts digits only and shows "Birth registration number must be exactly 17 digits" until it is valid. Only a
  valid value is saved and used. Changing it invalidates the `['myHolds']` query.
- Server state: TanStack Query only, no other global store. After a refresh or browser restart, the student ID
  comes back from localStorage and `GET /me/holds` shows the ongoing applications.

### 9.3 Queries and polling

| Hook | Key | Endpoint | refetchInterval |
|---|---|---|---|
| `useClasses()` | `['classes']` | GET /classes | 3000 ms |
| `useClass(id)` | `['classes', id]` | GET /classes/{id} | 3000 ms |
| `useHold(id)` | `['holds', id]` | GET /holds/{id} | 5000 ms while HELD; off for terminal statuses |
| `useMyHolds(sid)` | `['myHolds', sid]` | GET /me/holds | 5000 ms |
| `useCreateClass()`, `useApply()`, `useConfirm()`, `useCancel()` | mutations | | invalidate `classes`, `holds/id`, `myHolds` on settle |

`retry`: queries 1 (not for 4xx), mutations 0. `refetchOnWindowFocus: true`. `placeholderData: keepPreviousData`, so a poll that fails
keeps showing the last data together with a `ConnectionBanner`.

### 9.4 Countdown using the server clock

- `fetchJson` reads `X-Server-Time` (or `serverTime` in hold bodies) on every response and sets
  `offsetMs = serverTime - (requestStart + rtt/2)`. The value is smoothed and kept in `serverClock.ts`.
- `useServerNow(1000)` returns `Date.now() + offsetMs` and re-renders every second.
- `Countdown` shows `mm:ss` of `expiresAt - serverNow`, clamped at 0.

### 9.5 EXPIRED without reload

`displayStatus = (hold.status === 'HELD' && serverNow >= expiresAt) ? 'EXPIRED' : hold.status`.
When the countdown reaches 0, the badge switches to **EXPIRED** right away and **Pay admission fee** becomes
`disabled`. At the same moment `queryClient.invalidateQueries(['holds', id])` asks the server, which also returns
the effective `EXPIRED` (computed on read). If a confirm click races the expiry and the server answers
409 `HOLD_EXPIRED`, the page shows the expired message and refetches.

### 9.6 Error-to-message mapping (`toUserMessage`)

| Condition | Message |
|---|---|
| `SEATS_FULL` | "Sorry, all seats in this class are taken right now. Seats may free up if other applications expire, so please try again later." |
| `DUPLICATE_APPLICATION` | "You already have an active application or admission for this class. See My applications." |
| `HOLD_EXPIRED` | "This application has expired, so the seat was released. You can apply again if seats are available." |
| `HOLD_CANCELLED` | "This application was cancelled." |
| `HOLD_ALREADY_CONFIRMED` | "Admission is already confirmed; it can't be cancelled here." |
| `INVALID_STUDENT_ID` | "Please enter a valid 17-digit birth registration number at the top of the page." |
| `VALIDATION_FAILED` | "Please check the form: " + first field error from `errors` |
| `FORBIDDEN` | "This application belongs to a different birth registration number." |
| `NOT_FOUND` | "We couldn't find that class or application." |
| `NetworkError` (fetch threw / timeout 10 s via AbortController) | "Can't reach the admission server. Check your connection; we'll keep retrying." |
| 5xx / unknown | "Something went wrong on the server. Please try again in a moment." |

`ErrorAlert` (role="alert") shows these messages. Form validation runs on the client first (name required,
seats 1..100000 integer, student ID 17 digits).

### 9.7 Error boundary

`ErrorBoundary` (class component) wraps the routes. Its fallback shows "Something went wrong while showing this page."
with **Reload** and **Go to classes** buttons, so the user never sees a blank page. It resets when the route
changes (`resetKey = location.pathname`).

### 9.8 Runtime API URL

- `config.ts`: `apiBaseUrl = window.__ENV__?.API_BASE_URL || import.meta.env.VITE_API_BASE_URL || 'http://localhost:8080'`,
  trailing slash removed.
- `docker/40-write-env.sh` is copied to `/docker-entrypoint.d/` (the official nginx image runs these scripts on start):
  `printf 'window.__ENV__ = { API_BASE_URL: "%s" };\n' "${API_BASE_URL:-}" > /usr/share/nginx/html/env.js`
  (the value is JSON-escaped). The script uses LF line endings (`.gitattributes`) and has `chmod +x` set in the Dockerfile.
- `nginx.conf`: `try_files $uri /index.html` (SPA fallback); `location = /env.js { add_header Cache-Control "no-store"; }`.
- Note: `API_BASE_URL` is used by the **browser**, so it must be a URL the host can reach
  (`http://localhost:8080`), not `http://backend:8080`.

---

## 10. Docker

### 10.1 `backend/Dockerfile`

```
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build      # restore (csproj + props first, for layer caching), build -c Release
FROM build AS test                                   # ENTRYPOINT ["dotnet","test","SchoolAdmission.slnx","-c","Release","--no-build","--logger","console;verbosity=normal"]
FROM build AS publish                                # dotnet publish src/SchoolAdmission.Api -c Release -o /app/publish /p:UseAppHost=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime # apt-get install curl (healthcheck); USER app; EXPOSE 8080
ENTRYPOINT ["dotnet","SchoolAdmission.Api.dll"]
```

### 10.2 `frontend/Dockerfile`

```
FROM node:24-alpine AS deps     # npm ci
FROM deps AS test               # CMD ["npm","run","test","--","--run"]
FROM deps AS build              # ARG VITE_API_BASE_URL=http://localhost:8080; npm run build
FROM nginx:1.27-alpine AS runtime # copy dist, nginx.conf, 40-write-env.sh; EXPOSE 80
```

### 10.3 `docker-compose.yml` (repo root)

| Service | Image/build | Ports | Profile | Notes |
|---|---|---|---|---|
| `db` | `postgres:16-alpine` | **not published** (so it cannot clash with a local Postgres) | default | env `POSTGRES_DB=admission`, `POSTGRES_USER=admission`, `POSTGRES_PASSWORD=admission`; volume `pgdata:/var/lib/postgresql/data`; healthcheck `pg_isready -U admission -d admission` every 5 s, 20 retries |
| `backend` | `./backend` target `runtime` | `8080:8080` | default | `ConnectionStrings__Default=Host=db;Port=5432;Database=admission;Username=admission;Password=admission;Timezone=UTC`, `HOLD_DURATION_SECONDS=${HOLD_DURATION_SECONDS:-600}`, `CORS_ORIGINS=${CORS_ORIGINS:-http://localhost:3000}`; `depends_on: db: condition: service_healthy`; healthcheck `curl -fsS http://localhost:8080/health/ready` (interval 10 s, start_period 30 s); `restart: unless-stopped` |
| `frontend` | `./frontend` target `runtime`, build arg `VITE_API_BASE_URL=http://localhost:8080` | `3000:80` | default | env `API_BASE_URL=${API_BASE_URL:-http://localhost:8080}`; `depends_on: backend: condition: service_healthy`; healthcheck `wget -qO- http://localhost/ >/dev/null` |
| `backend2` | same as backend | `8081:8080` | `bonus` | same env; `depends_on: backend: service_healthy` (backend runs the migrations first) |
| `backend-tests` | `./backend` target `test` | none | `test` | env `TEST_DATABASE_URL=Host=db;Port=5432;Username=admission;Password=admission;Database=postgres`; `depends_on: db healthy` |
| `frontend-tests` | `./frontend` target `test` | none | `test` | |

Volume: `pgdata` (named). `docker compose down` keeps it; only `down -v` deletes it.
`CORS_ORIGINS` in compose includes `http://localhost:3000` (and `http://localhost:5173` for `npm run dev`).

Commands:
- Run app: `docker compose up --build` → frontend http://localhost:3000, API http://localhost:8080,
  http://localhost:8080/swagger, http://localhost:8080/scalar.
- Backend tests: `docker compose --profile test run --rm backend-tests`
- Frontend tests: `docker compose --profile test run --rm frontend-tests`
- Bonus demo: `docker compose --profile bonus up --build` → second API on http://localhost:8081.

---

## 11. Test strategy

### 11.1 Database for integration tests

`PostgresFixture` (xUnit v3 assembly fixture):
- If `TEST_DATABASE_URL` is set (Docker `test` profile), it connects there.
- Otherwise it starts `postgres:16-alpine` through **Testcontainers.PostgreSql** (local `dotnet test` with Docker running).
- Each test class gets a fresh database `adm_test_{guid:N}` (`CREATE DATABASE`), migrated with EF, and drops it
  at the end. Tests never share mutable state.

`AdmissionApiFactory : WebApplicationFactory<Program>` overrides `ConnectionStrings:Default`,
`HOLD_DURATION_SECONDS`, `HOLD_SWEEPER_ENABLED` and `HOLD_SWEEP_INTERVAL_SECONDS` per test.

### 11.2 Unit tests (`SchoolAdmission.UnitTests`)

- `StudentIdTests`: `TryCreate_Accepts17AsciiDigits`, `TryCreate_Rejects16Or18Digits`,
  `TryCreate_RejectsLettersSpacesAndUnicodeDigits` (e.g. Arabic-Indic digits), `TryCreate_RejectsNullOrEmpty`.
- `HoldRulesTests`: `EffectiveStatus_HeldBeforeExpiry_IsHeld`, `EffectiveStatus_HeldAtOrAfterExpiry_IsExpired`,
  `EffectiveStatus_TerminalStatuses_Unchanged`, `ClassifyConfirm_OtherStudent_IsForbidden_EvenIfExpired`,
  `ClassifyConfirm_AlreadyConfirmed_IsIdempotentSuccess`, `ClassifyConfirm_Cancelled_IsHoldCancelled`,
  `ClassifyConfirm_StaleHeld_IsHoldExpired`, `ClassifyCancel_AlreadyCancelled_IsSuccess`,
  `ClassifyCancel_Confirmed_IsAlreadyConfirmed`, `ClassifyCancel_Expired_IsHoldExpired`.
- Validators: `CreateClassValidator_RejectsBlankName`, `_RejectsNameOver200`, `_RejectsSeatsBelow1OrAbove100000`,
  `CreateHoldValidator_RejectsBadStudentId_WithCodeInvalidStudentId`, `GetMyHoldsValidator_RequiresStudentId`.
- Handlers (with fake repositories / UoW): `CreateHoldHandler_ClassMissing_ReturnsNotFound_AndRollsBack`,
  `CreateHoldHandler_Duplicate_ReturnsDuplicateApplication_BeforeSeatCheck`,
  `CreateHoldHandler_Full_ReturnsSeatsFull`, `CreateHoldHandler_ExpiresStaleBeforeCounting` (call order is checked),
  `CreateHoldHandler_UniqueViolation_MapsToDuplicateApplication`,
  `ConfirmHoldHandler_UpdateMatched_ReturnsConfirmed`, `ConfirmHoldHandler_ZeroRows_UsesClassification`.
- `ValidationBehaviorTests_ThrowsWithAllFailures`, `UtcDateTimeConverterTests_WritesZSuffix_TrimsZeroFraction`.

### 11.3 Integration tests (`SchoolAdmission.IntegrationTests`, real PostgreSQL)

API and contract:
- `PostClasses_Returns201_WithSpecFields_AndLocation`
- `PostClasses_InvalidBody_Returns400_ProblemDetailsWithCode`
- `GetClasses_ReturnsSeatCounts_CamelCase`
- `GetClass_Unknown_Returns404_NotFound`
- `PostHold_MissingOrInvalidStudentId_Returns400_InvalidStudentId` (theory: missing, 16, 18, letters)
- `PostHold_Returns201_StatusHeld_ExpiresAtUtcZ`
- `PostHold_DuplicateActive_Returns409_DuplicateApplication`
- `PostHold_AfterCancel_AllowsReapply_Returns201`
- `PostHold_LastSeat_ThenNext_Returns409_SeatsFull`
- `GetHold_WithoutHeader_Returns200`
- `ConfirmHold_OtherStudent_Returns403`
- `ConfirmHold_Twice_Returns200_Both_ConfirmedSeatsIncrementsOnce` (b)
- `ConfirmHold_Cancelled_Returns409_HoldCancelled`
- `CancelHold_Returns204_SeatReleased`; `CancelHold_Twice_Returns204`; `CancelHold_Confirmed_Returns409`
- `GetMyHolds_ReturnsAllStatuses_WithClassName_NewestFirst`
- `Versioning_NoVersion_HeaderVersion_QueryVersion_AllReturn200_SamePaths`; `Versioning_Unsupported_Returns400`
- `OpenApi_SwaggerAndScalar_AreServed_InProductionEnvironment` (factory with `UseEnvironment("Production")`:
  `/swagger/index.html`, `/scalar`, `/openapi/v1.json` all return 200)
- `Cors_Preflight_AllowsConfiguredOrigin_AndXStudentIdHeader`
- `ContractShapeTests`: every spec field is present, with the right JSON type and casing, for all 8 endpoints.

Concurrency:
- (a) `CreateHold_200ParallelRequests_10Seats_ExactlyTenSucceed`: class with 10 seats, 200 distinct student IDs,
  all sent at once (`Task.WhenAll`, released together by a `TaskCompletionSource` start gate). Assert exactly 10 × 201
  and 190 × 409 `SEATS_FULL`; DB `count(*) WHERE status='HELD'` = 10; `GET /classes/{id}` shows held 10, available 0.
- `CreateHold_SameStudent50ParallelRequests_ExactlyOneSucceeds`: 1 × 201, 49 × 409 `DUPLICATE_APPLICATION`.
- `ConfirmHold_50ParallelConfirms_AllReturn200_ConfirmedSeatsIsOne` (b, under contention).
- `MixedLoad_HoldsConfirmsCancels_NeverExceedTotalSeats`: random mix for 5 s; a monitor polls the seat-count SQL
  every 50 ms and asserts `held + confirmed <= total` every time.

Expiry (c):
- `Expiry_SeatReleasedWithoutApiCallOrSweeper` (`HOLD_DURATION_SECONDS=2`, sweeper disabled): hold → wait 3 s →
  GET class shows held 0 / available back; GET hold shows `EXPIRED`; stored row is still `HELD` (proves it is computed on read).
- `Expiry_ConfirmAfterExpiry_Returns409_HoldExpired`
- `Expiry_ReapplyAfterExpiry_NotBlockedByUnsweptRow_Returns201` (checks the §3.2 logic)
- `Expiry_FullClassFreesSeatOnExpiry_NextApplicantGets201`
- `Sweeper_MarksStaleHoldsExpired` (interval 1 s): stored status becomes `EXPIRED`, `expired_at` set.
- `Sweeper_TwoConcurrentSweepers_NoErrors_AllExpiredOnce`

Persistence:
- `SchemaTests_PartialUniqueIndex_RejectsSecondActiveRow` (raw insert → 23505)
- `SchemaTests_CheckConstraints_RejectBadStatusAndStudentId`

Two processes (e, bonus):
- `TwoProcessOversellTests.TwoApiProcesses_SameDb_300Requests_10Seats_ExactlyTenSucceed`. `ApiProcess` starts
  **two real OS processes** (`dotnet <path>/SchoolAdmission.Api.dll --urls http://127.0.0.1:{freePort}`, path from
  `API_DLL_PATH` or found relative to the test assembly) against the same test database. It waits for `/health/ready`,
  then sends 150 parallel requests to each and asserts exactly 10 × 201 in total and DB count = 10. Processes are killed on dispose.
  Trait `Category=MultiProcess`. Skipped via `Assert.SkipUnless` if the Api dll cannot be found.
- Manual demo: `docker compose --profile bonus up`, then `scripts/bonus-oversell.sh` (curl in parallel against
  8080 and 8081, prints the 201/409 tally).

### 11.4 Frontend tests (Vitest + RTL + MSW)

- `config.test.ts`: runtime `window.__ENV__.API_BASE_URL` wins over `VITE_API_BASE_URL`.
- `errors.test.ts`: `toUserMessage` for every code, network error and 5xx.
- `StudentIdBar.test.tsx`: rejects non-17-digit input and shows a message; saves a valid ID to localStorage; restores it on mount.
- `ClassListPage.test.tsx`: shows counts; updates after the polling interval (fake timers); Apply on full class shows
  the seats-full message; Apply disabled without a student ID.
- `HoldPage.test.tsx`: countdown shows; when time passes expiry (fake timers), shows EXPIRED and Pay is disabled without
  remount; Pay → CONFIRMED; 409 HOLD_EXPIRED shows the expired message.
- `MyHoldsPage.test.tsx`: lists holds for the stored ID after "reload" (re-render with localStorage pre-set).
- `NetworkDown.test.tsx`: MSW `HttpResponse.error()` → "Can't reach the admission server" banner, no blank page.
- `ErrorBoundary.test.tsx`: a throwing child renders the fallback.
- `CreateClassForm.test.tsx`: client validation; 400 from the server shows the field message.

Commands: `npm test -- --run` (local) / `docker compose --profile test run --rm frontend-tests`.
