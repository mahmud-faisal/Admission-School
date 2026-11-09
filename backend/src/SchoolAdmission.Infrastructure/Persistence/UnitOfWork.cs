using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SchoolAdmission.Application.Abstractions.Persistence;

namespace SchoolAdmission.Infrastructure.Persistence;

/// <summary>
/// Wraps the scoped <see cref="AdmissionDbContext"/>. Repositories share that context, so raw SQL issued while a
/// transaction is open runs on the transaction's connection.
/// </summary>
internal sealed class UnitOfWork(AdmissionDbContext db) : IUnitOfWork
{
    /// <summary>
    /// READ COMMITTED on purpose: each statement after the class lock takes a fresh snapshot and sees holds that
    /// competing transactions committed while we waited. REPEATABLE READ would count from a stale snapshot.
    /// </summary>
    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct) =>
        new EfTransaction(await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct));

    public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    private sealed class EfTransaction(IDbContextTransaction inner) : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken ct) => inner.CommitAsync(ct);

        public Task RollbackAsync(CancellationToken ct) => inner.RollbackAsync(ct);

        // Disposing an uncommitted EF transaction rolls it back.
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
