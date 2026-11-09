namespace SchoolAdmission.Application.Abstractions.Persistence;

public interface IUnitOfWork
{
    /// <summary>Starts a READ COMMITTED transaction. Repository calls made before it ends run inside it.</summary>
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct);

    Task<int> SaveChangesAsync(CancellationToken ct);
}

public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct);

    /// <summary>Disposing without committing also rolls back.</summary>
    Task RollbackAsync(CancellationToken ct);
}
