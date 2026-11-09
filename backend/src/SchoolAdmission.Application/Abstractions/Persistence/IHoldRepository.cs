using SchoolAdmission.Domain.Holds;

namespace SchoolAdmission.Application.Abstractions.Persistence;

public interface IHoldRepository
{
    /// <summary>Marks this class's HELD rows with <c>expires_at &lt;= dbNow</c> as EXPIRED. Call only under the class lock.</summary>
    Task<int> ExpireStaleForClassAsync(string classId, DateTime dbNow, CancellationToken ct);

    /// <summary>True if the student has a HELD (not expired) or CONFIRMED hold in the class.</summary>
    Task<bool> HasActiveOrConfirmedAsync(string classId, string studentId, DateTime dbNow, CancellationToken ct);

    /// <summary>Count of HELD (not expired) + CONFIRMED holds in the class.</summary>
    Task<int> CountOccupiedSeatsAsync(string classId, DateTime dbNow, CancellationToken ct);

    /// <summary>Inserts a HELD hold. Throws <see cref="DuplicateHoldException"/> on a unique-index violation.</summary>
    Task<HoldRow> InsertHeldAsync(
        string holdId,
        string classId,
        string studentId,
        DateTime dbNow,
        int holdDurationSeconds,
        CancellationToken ct);

    /// <summary>
    /// One conditional UPDATE: <c>status='HELD' AND expires_at &gt; now() AND student_id=@s</c> → CONFIRMED.
    /// Returns the updated row, or null if no row matched.
    /// </summary>
    Task<HoldRow?> TryConfirmAsync(string holdId, string studentId, CancellationToken ct);

    /// <summary>One conditional UPDATE → CANCELLED. Returns false if no row matched.</summary>
    Task<bool> TryCancelAsync(string holdId, string studentId, CancellationToken ct);

    /// <summary>Diagnostic read (stored status + DB clock) used to explain a 0-row UPDATE.</summary>
    Task<HoldSnapshot?> GetSnapshotAsync(string holdId, CancellationToken ct);

    Task<HoldRow?> GetViewAsync(string holdId, CancellationToken ct);

    /// <summary>All holds of the student, newest first.</summary>
    Task<IReadOnlyList<HoldRow>> ListByStudentAsync(string studentId, CancellationToken ct);

    /// <summary>Sweeper: expires up to <paramref name="batchSize"/> stale holds using FOR UPDATE SKIP LOCKED.</summary>
    Task<int> ExpireStaleBatchAsync(int batchSize, CancellationToken ct);
}
