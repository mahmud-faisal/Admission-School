using MediatR;
using SchoolAdmission.Application.Abstractions.Persistence;

namespace SchoolAdmission.Application.Holds.ExpireStaleHolds;

/// <summary>
/// Writes EXPIRED onto stale HELD rows in batches. Correctness never depends on this (expiry is computed on
/// read with the DB clock); it keeps stored state tidy. Safe to run in several processes at once.
/// </summary>
public sealed class ExpireStaleHoldsHandler(IHoldRepository holds) : IRequestHandler<ExpireStaleHoldsCommand, int>
{
    public async Task<int> Handle(ExpireStaleHoldsCommand request, CancellationToken cancellationToken)
    {
        var total = 0;
        int batch;
        do
        {
            batch = await holds.ExpireStaleBatchAsync(request.BatchSize, cancellationToken);
            total += batch;
        }
        while (batch >= request.BatchSize && !cancellationToken.IsCancellationRequested);

        return total;
    }
}
