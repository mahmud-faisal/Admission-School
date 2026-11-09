using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Application.Abstractions.Persistence;
using SchoolAdmission.Domain.Classes;

namespace SchoolAdmission.Infrastructure.Persistence.Repositories;

/// <summary>
/// All SQL is parameterized (interpolated <see cref="FormattableString"/>). Queries are not composed with LINQ
/// (<c>ToListAsync</c> only), so the SQL sent is exactly what is written here.
/// </summary>
internal sealed class ClassRepository(AdmissionDbContext db) : IClassRepository
{
    public void Add(Class entity) => db.Classes.Add(entity);

    public async Task<ClassLockRow?> LockForHoldCreationAsync(string classId, CancellationToken ct)
    {
        // Step 1 (DESIGN §4.1): row lock held by PostgreSQL until COMMIT/ROLLBACK, so every backend process
        // serializes hold creation for this class.
        var locked = (await db.Database.SqlQuery<ClassLockSqlRow>($"""
            SELECT id AS "Id", name AS "Name", total_seats AS "TotalSeats"
            FROM classes
            WHERE id = {classId}
            FOR UPDATE
            """).ToListAsync(ct)).SingleOrDefault();

        if (locked is null)
        {
            return null;
        }

        // Step 2: read the DB clock AFTER the lock is held. now() would be the transaction start time,
        // which can be seconds earlier if we waited on the lock.
        var dbNow = (await db.Database.SqlQuery<DateTime>($"""SELECT clock_timestamp() AS "Value" """)
            .ToListAsync(ct)).Single();

        return new ClassLockRow(locked.Id, locked.Name, locked.TotalSeats, dbNow);
    }

    public async Task<ClassSeatsRow?> GetSeatsAsync(string classId, CancellationToken ct)
    {
        var rows = await db.Database.SqlQuery<ClassSeatsSqlRow>($"""
            SELECT c.id AS "Id", c.name AS "Name", c.total_seats AS "TotalSeats", c.created_at AS "CreatedAt",
                   s.held AS "HeldSeats", s.confirmed AS "ConfirmedSeats",
                   GREATEST(0, c.total_seats - s.held - s.confirmed) AS "AvailableSeats"
            FROM classes c
            CROSS JOIN LATERAL (
                SELECT (count(*) FILTER (WHERE h.status = 'HELD' AND h.expires_at > now()))::int AS held,
                       (count(*) FILTER (WHERE h.status = 'CONFIRMED'))::int                    AS confirmed
                FROM holds h
                WHERE h.class_id = c.id AND h.status IN ('HELD','CONFIRMED')
            ) s
            WHERE c.id = {classId}
            """).ToListAsync(ct);

        return rows.SingleOrDefault()?.ToRow();
    }

    public async Task<IReadOnlyList<ClassSeatsRow>> ListSeatsAsync(CancellationToken ct)
    {
        var rows = await db.Database.SqlQuery<ClassSeatsSqlRow>($"""
            SELECT c.id AS "Id", c.name AS "Name", c.total_seats AS "TotalSeats", c.created_at AS "CreatedAt",
                   s.held AS "HeldSeats", s.confirmed AS "ConfirmedSeats",
                   GREATEST(0, c.total_seats - s.held - s.confirmed) AS "AvailableSeats"
            FROM classes c
            CROSS JOIN LATERAL (
                SELECT (count(*) FILTER (WHERE h.status = 'HELD' AND h.expires_at > now()))::int AS held,
                       (count(*) FILTER (WHERE h.status = 'CONFIRMED'))::int                    AS confirmed
                FROM holds h
                WHERE h.class_id = c.id AND h.status IN ('HELD','CONFIRMED')
            ) s
            ORDER BY c.created_at, c.id
            """).ToListAsync(ct);

        return rows.Select(r => r.ToRow()).ToList();
    }
}
