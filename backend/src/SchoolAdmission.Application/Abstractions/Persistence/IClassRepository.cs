using SchoolAdmission.Domain.Classes;

namespace SchoolAdmission.Application.Abstractions.Persistence;

public interface IClassRepository
{
    /// <summary>Tracked insert; persisted by <see cref="IUnitOfWork.SaveChangesAsync"/>.</summary>
    void Add(Class entity);

    /// <summary>
    /// <c>SELECT ... FOR UPDATE</c> on the class row, then reads <c>clock_timestamp()</c>. Must run inside a transaction.
    /// Returns null if the class does not exist.
    /// </summary>
    Task<ClassLockRow?> LockForHoldCreationAsync(string classId, CancellationToken ct);

    Task<ClassSeatsRow?> GetSeatsAsync(string classId, CancellationToken ct);

    /// <summary>All classes, oldest first.</summary>
    Task<IReadOnlyList<ClassSeatsRow>> ListSeatsAsync(CancellationToken ct);
}
