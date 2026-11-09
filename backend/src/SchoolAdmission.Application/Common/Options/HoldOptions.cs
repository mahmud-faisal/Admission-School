namespace SchoolAdmission.Application.Common.Options;

/// <summary>
/// Hold timing settings. Bound in Infrastructure from the env vars HOLD_DURATION_SECONDS (default 600),
/// HOLD_SWEEP_INTERVAL_SECONDS (10), HOLD_SWEEP_BATCH_SIZE (500) and HOLD_SWEEPER_ENABLED (true).
/// </summary>
public sealed class HoldOptions
{
    public const int DefaultHoldDurationSeconds = 600;

    public int HoldDurationSeconds { get; set; } = DefaultHoldDurationSeconds;

    public int SweepIntervalSeconds { get; set; } = 10;

    public int SweepBatchSize { get; set; } = 500;

    public bool SweeperEnabled { get; set; } = true;
}
