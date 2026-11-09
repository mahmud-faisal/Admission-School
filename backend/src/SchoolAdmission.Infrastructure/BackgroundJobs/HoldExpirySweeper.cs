using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchoolAdmission.Application.Common.Options;
using SchoolAdmission.Application.Holds.ExpireStaleHolds;

namespace SchoolAdmission.Infrastructure.BackgroundJobs;

/// <summary>
/// Periodically writes EXPIRED onto stale HELD rows. Seat release does NOT depend on this job: every read and
/// count already treats <c>expires_at &lt;= now()</c> as expired. Safe to run in several processes at once
/// (idempotent UPDATE + FOR UPDATE SKIP LOCKED), so no leader election is needed.
/// </summary>
internal sealed partial class HoldExpirySweeper(
    IServiceScopeFactory scopeFactory,
    IOptions<HoldOptions> options,
    ILogger<HoldExpirySweeper> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.SweeperEnabled)
        {
            LogDisabled(logger);
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(settings.SweepIntervalSeconds));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await SweepOnceAsync(settings.SweepBatchSize, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }

    private async Task SweepOnceAsync(int batchSize, CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var expired = await sender.Send(new ExpireStaleHoldsCommand(batchSize), ct);
            if (expired > 0)
            {
                LogExpired(logger, expired);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Never crash the host; the next tick tries again.
            LogSweepFailed(logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Hold expiry sweeper is disabled (HOLD_SWEEPER_ENABLED=false).")]
    private static partial void LogDisabled(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sweeper marked {Count} stale holds as EXPIRED.")]
    private static partial void LogExpired(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Hold expiry sweep failed; will retry on the next tick.")]
    private static partial void LogSweepFailed(ILogger logger, Exception exception);
}
