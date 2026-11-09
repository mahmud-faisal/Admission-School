using Microsoft.EntityFrameworkCore;
using Npgsql;
using SchoolAdmission.Application.Abstractions.Persistence;
using SchoolAdmission.Domain.Holds;
using SchoolAdmission.Infrastructure.Persistence.Configurations;

namespace SchoolAdmission.Infrastructure.Persistence.Repositories;

/// <summary>
/// Raw parameterized SQL for every seat/expiry decision (DESIGN §4). Every time comparison uses the DATABASE clock
/// (<c>now()</c>, or the <c>clock_timestamp()</c> value read after the class lock), never the app server clock.
/// </summary>
internal sealed class HoldRepository(AdmissionDbContext db) : IHoldRepository
{
    private const string UniqueViolation = "23505";

    public Task<int> ExpireStaleForClassAsync(string classId, DateTime dbNow, CancellationToken ct) =>
        db.Database.ExecuteSqlAsync($"""
            UPDATE holds
            SET status = 'EXPIRED', expired_at = {dbNow}
            WHERE class_id = {classId}
              AND status = 'HELD'
              AND expires_at <= {dbNow}
            """, ct);

    public async Task<bool> HasActiveOrConfirmedAsync(string classId, string studentId, DateTime dbNow, CancellationToken ct) =>
        (await db.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1 FROM holds
                WHERE class_id = {classId}
                  AND student_id = {studentId}
                  AND ((status = 'HELD' AND expires_at > {dbNow}) OR status = 'CONFIRMED')
            ) AS "Value"
            """).ToListAsync(ct)).Single();

    public async Task<int> CountOccupiedSeatsAsync(string classId, DateTime dbNow, CancellationToken ct) =>
        (await db.Database.SqlQuery<int>($"""
            SELECT count(*)::int AS "Value"
            FROM holds
            WHERE class_id = {classId}
              AND ((status = 'HELD' AND expires_at > {dbNow}) OR status = 'CONFIRMED')
            """).ToListAsync(ct)).Single();

    public async Task<HoldRow> InsertHeldAsync(
        string holdId, string classId, string studentId, DateTime dbNow, int holdDurationSeconds, CancellationToken ct)
    {
        try
        {
            // expires_at is rounded UP to a whole second, so the stored value and the JSON value are identical and a
            // hold always lasts at least HOLD_DURATION_SECONDS (at most 1 s longer), never less.
            var rows = await db.Database.SqlQuery<HoldSqlRow>($"""
                WITH ins AS (
                    INSERT INTO holds (id, class_id, student_id, status, created_at, expires_at)
                    VALUES ({holdId}, {classId}, {studentId}, 'HELD', {dbNow},
                            CASE WHEN {dbNow}::timestamptz = date_trunc('second', {dbNow}::timestamptz)
                                 THEN {dbNow}::timestamptz
                                 ELSE date_trunc('second', {dbNow}::timestamptz) + interval '1 second'
                            END + make_interval(secs => {holdDurationSeconds}))
                    RETURNING *
                )
                SELECT ins.id AS "Id", ins.class_id AS "ClassId", c.name AS "ClassName", ins.student_id AS "StudentId",
                       ins.status AS "Status", ins.created_at AS "CreatedAt", ins.expires_at AS "ExpiresAt",
                       ins.confirmed_at AS "ConfirmedAt", ins.cancelled_at AS "CancelledAt", {dbNow}::timestamptz AS "DbNow"
                FROM ins JOIN classes c ON c.id = ins.class_id
                """).ToListAsync(ct);

            return rows.Single().ToRow();
        }
        catch (PostgresException ex) when (ex.SqlState == UniqueViolation &&
                                           ex.ConstraintName == HoldConfiguration.ActiveUniqueIndexName)
        {
            throw new DuplicateHoldException("The student already has an active or confirmed hold in this class.", ex);
        }
    }

    public async Task<HoldRow?> TryConfirmAsync(string holdId, string studentId, CancellationToken ct)
    {
        // One atomic conditional UPDATE: HELD → CONFIRMED can happen at most once, so confirmedSeats grows once.
        var rows = await db.Database.SqlQuery<HoldSqlRow>($"""
            WITH u AS (
                UPDATE holds
                SET status = 'CONFIRMED', confirmed_at = now()
                WHERE id = {holdId}
                  AND student_id = {studentId}
                  AND status = 'HELD'
                  AND expires_at > now()
                RETURNING *
            )
            SELECT u.id AS "Id", u.class_id AS "ClassId", c.name AS "ClassName", u.student_id AS "StudentId",
                   u.status AS "Status", u.created_at AS "CreatedAt", u.expires_at AS "ExpiresAt",
                   u.confirmed_at AS "ConfirmedAt", u.cancelled_at AS "CancelledAt", now() AS "DbNow"
            FROM u JOIN classes c ON c.id = u.class_id
            """).ToListAsync(ct);

        return rows.SingleOrDefault()?.ToRow();
    }

    public async Task<bool> TryCancelAsync(string holdId, string studentId, CancellationToken ct) =>
        await db.Database.ExecuteSqlAsync($"""
            UPDATE holds
            SET status = 'CANCELLED', cancelled_at = now()
            WHERE id = {holdId}
              AND student_id = {studentId}
              AND status = 'HELD'
              AND expires_at > now()
            """, ct) == 1;

    public async Task<HoldSnapshot?> GetSnapshotAsync(string holdId, CancellationToken ct)
    {
        var rows = await db.Database.SqlQuery<HoldSnapshotSqlRow>($"""
            SELECT id AS "Id", student_id AS "StudentId", status AS "Status", expires_at AS "ExpiresAt", now() AS "DbNow"
            FROM holds
            WHERE id = {holdId}
            """).ToListAsync(ct);

        return rows.SingleOrDefault()?.ToSnapshot();
    }

    public async Task<HoldRow?> GetViewAsync(string holdId, CancellationToken ct)
    {
        var rows = await db.Database.SqlQuery<HoldSqlRow>($"""
            SELECT h.id AS "Id", h.class_id AS "ClassId", c.name AS "ClassName", h.student_id AS "StudentId",
                   CASE WHEN h.status = 'HELD' AND h.expires_at <= now() THEN 'EXPIRED' ELSE h.status END AS "Status",
                   h.created_at AS "CreatedAt", h.expires_at AS "ExpiresAt",
                   h.confirmed_at AS "ConfirmedAt", h.cancelled_at AS "CancelledAt", now() AS "DbNow"
            FROM holds h JOIN classes c ON c.id = h.class_id
            WHERE h.id = {holdId}
            """).ToListAsync(ct);

        return rows.SingleOrDefault()?.ToRow();
    }

    public async Task<IReadOnlyList<HoldRow>> ListByStudentAsync(string studentId, CancellationToken ct)
    {
        var rows = await db.Database.SqlQuery<HoldSqlRow>($"""
            SELECT h.id AS "Id", h.class_id AS "ClassId", c.name AS "ClassName", h.student_id AS "StudentId",
                   CASE WHEN h.status = 'HELD' AND h.expires_at <= now() THEN 'EXPIRED' ELSE h.status END AS "Status",
                   h.created_at AS "CreatedAt", h.expires_at AS "ExpiresAt",
                   h.confirmed_at AS "ConfirmedAt", h.cancelled_at AS "CancelledAt", now() AS "DbNow"
            FROM holds h JOIN classes c ON c.id = h.class_id
            WHERE h.student_id = {studentId}
            ORDER BY h.created_at DESC, h.id DESC
            """).ToListAsync(ct);

        return rows.Select(r => r.ToRow()).ToList();
    }

    public Task<int> ExpireStaleBatchAsync(int batchSize, CancellationToken ct) =>
        // SKIP LOCKED: concurrent sweepers (other processes) split the work instead of blocking, and rows locked by an
        // in-flight confirm/cancel are skipped. The outer status guard keeps the UPDATE idempotent.
        db.Database.ExecuteSqlAsync($"""
            UPDATE holds h
            SET status = 'EXPIRED', expired_at = now()
            WHERE h.id IN (
                SELECT id FROM holds
                WHERE status = 'HELD' AND expires_at <= now()
                ORDER BY expires_at
                LIMIT {batchSize}
                FOR UPDATE SKIP LOCKED
            )
            AND h.status = 'HELD'
            """, ct);
}
