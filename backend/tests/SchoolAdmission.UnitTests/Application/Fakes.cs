using SchoolAdmission.Application.Abstractions;
using SchoolAdmission.Application.Abstractions.Persistence;
using SchoolAdmission.Domain.Classes;
using SchoolAdmission.Domain.Holds;

namespace SchoolAdmission.UnitTests.Application;

/// <summary>Shared call log so tests can assert the ORDER of repository and transaction calls.</summary>
public sealed class CallLog
{
    public List<string> Calls { get; } = [];

    public void Add(string call) => Calls.Add(call);
}

public sealed class FakeUnitOfWork(CallLog log) : IUnitOfWork
{
    public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct)
    {
        log.Add("Begin");
        return Task.FromResult<IUnitOfWorkTransaction>(new FakeTransaction(log));
    }

    public Task<int> SaveChangesAsync(CancellationToken ct)
    {
        log.Add("SaveChanges");
        return Task.FromResult(1);
    }

    private sealed class FakeTransaction(CallLog log) : IUnitOfWorkTransaction
    {
        private bool _done;

        public Task CommitAsync(CancellationToken ct)
        {
            _done = true;
            log.Add("Commit");
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken ct)
        {
            _done = true;
            log.Add("Rollback");
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            if (!_done)
            {
                log.Add("Rollback(dispose)");
            }

            return ValueTask.CompletedTask;
        }
    }
}

public sealed class FakeClassRepository(CallLog log) : IClassRepository
{
    public ClassLockRow? LockRow { get; set; }

    public ClassSeatsRow? SeatsRow { get; set; }

    public List<Class> Added { get; } = [];

    public void Add(Class entity)
    {
        log.Add("AddClass");
        Added.Add(entity);
    }

    public Task<ClassLockRow?> LockForHoldCreationAsync(string classId, CancellationToken ct)
    {
        log.Add("Lock");
        return Task.FromResult(LockRow);
    }

    public Task<ClassSeatsRow?> GetSeatsAsync(string classId, CancellationToken ct)
    {
        log.Add("GetSeats");
        return Task.FromResult(SeatsRow);
    }

    public Task<IReadOnlyList<ClassSeatsRow>> ListSeatsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ClassSeatsRow>>(SeatsRow is null ? [] : [SeatsRow]);
}

public sealed class FakeHoldRepository(CallLog log) : IHoldRepository
{
    public bool HasActive { get; set; }

    public ConfirmedHoldRow? ConfirmedHold { get; set; }

    public int Occupied { get; set; }

    public bool ThrowDuplicateOnInsert { get; set; }

    /// <summary>Results returned by successive TryConfirmAsync calls (null = 0 rows matched).</summary>
    public Queue<HoldRow?> ConfirmResults { get; } = new();

    public Queue<bool> CancelResults { get; } = new();

    public Queue<HoldSnapshot?> Snapshots { get; } = new();

    public HoldRow? View { get; set; }

    public int? InsertedDuration { get; private set; }

    public Task<int> ExpireStaleForClassAsync(string classId, DateTime dbNow, CancellationToken ct)
    {
        log.Add("ExpireStale");
        return Task.FromResult(0);
    }

    public Task<bool> HasActiveOrConfirmedAsync(string classId, string studentId, DateTime dbNow, CancellationToken ct)
    {
        log.Add("HasActive");
        return Task.FromResult(HasActive);
    }

    public Task<ConfirmedHoldRow?> GetConfirmedHoldAsync(string studentId, CancellationToken ct)
    {
        log.Add("GetConfirmedHold");
        return Task.FromResult(ConfirmedHold);
    }

    public Task<int> CountOccupiedSeatsAsync(string classId, DateTime dbNow, CancellationToken ct)
    {
        log.Add("Count");
        return Task.FromResult(Occupied);
    }

    public Task<HoldRow> InsertHeldAsync(string holdId, string classId, string studentId, DateTime dbNow, int holdDurationSeconds, CancellationToken ct)
    {
        log.Add("Insert");
        if (ThrowDuplicateOnInsert)
        {
            throw new DuplicateHoldException("ux_holds_class_student_active");
        }

        InsertedDuration = holdDurationSeconds;
        return Task.FromResult(new HoldRow(holdId, classId, "Class", studentId, HoldStatus.Held, dbNow,
            dbNow.AddSeconds(holdDurationSeconds), null, null, dbNow));
    }

    public Task<HoldRow?> TryConfirmAsync(string holdId, string studentId, CancellationToken ct)
    {
        log.Add("TryConfirm");
        return Task.FromResult(ConfirmResults.Count > 0 ? ConfirmResults.Dequeue() : null);
    }

    public Task<bool> TryCancelAsync(string holdId, string studentId, CancellationToken ct)
    {
        log.Add("TryCancel");
        return Task.FromResult(CancelResults.Count > 0 && CancelResults.Dequeue());
    }

    public Task<HoldSnapshot?> GetSnapshotAsync(string holdId, CancellationToken ct)
    {
        log.Add("Snapshot");
        return Task.FromResult(Snapshots.Count > 0 ? Snapshots.Dequeue() : null);
    }

    public Task<HoldRow?> GetViewAsync(string holdId, CancellationToken ct) => Task.FromResult(View);

    public Task<IReadOnlyList<HoldRow>> ListByStudentAsync(string studentId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<HoldRow>>(View is null ? [] : [View]);

    public Task<int> ExpireStaleBatchAsync(int batchSize, CancellationToken ct) => Task.FromResult(0);
}

public sealed class FakeIds : IIdGenerator
{
    public string NewClassId() => "c_00000000000000000000000000000001";

    public string NewHoldId() => "h_00000000000000000000000000000001";
}
