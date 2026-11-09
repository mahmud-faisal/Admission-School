using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolAdmission.Application.Abstractions;
using SchoolAdmission.Application.Abstractions.Persistence;
using SchoolAdmission.Application.Common.Options;
using SchoolAdmission.Infrastructure.BackgroundJobs;
using SchoolAdmission.Infrastructure.Ids;
using SchoolAdmission.Infrastructure.Persistence;
using SchoolAdmission.Infrastructure.Persistence.Repositories;

namespace SchoolAdmission.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = ConnectionStringResolver.Resolve(configuration);

        // No EnableRetryOnFailure: it does not work with user-started transactions (DESIGN §4).
        services.AddDbContext<AdmissionDbContext>(o => o.UseNpgsql(connectionString));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IClassRepository, ClassRepository>();
        services.AddScoped<IHoldRepository, HoldRepository>();
        services.AddSingleton<IIdGenerator, GuidV7IdGenerator>();

        services.AddOptions<HoldOptions>()
            .Configure(o =>
            {
                o.HoldDurationSeconds = ReadInt(configuration, "HOLD_DURATION_SECONDS", HoldOptions.DefaultHoldDurationSeconds);
                o.SweepIntervalSeconds = ReadInt(configuration, "HOLD_SWEEP_INTERVAL_SECONDS", 10);
                o.SweepBatchSize = ReadInt(configuration, "HOLD_SWEEP_BATCH_SIZE", 500);
                o.SweeperEnabled = !bool.TryParse(configuration["HOLD_SWEEPER_ENABLED"], out var enabled) || enabled;
            })
            .Validate(o => o.HoldDurationSeconds >= 1, "HOLD_DURATION_SECONDS must be a whole number >= 1.")
            .Validate(o => o.SweepIntervalSeconds >= 1, "HOLD_SWEEP_INTERVAL_SECONDS must be a whole number >= 1.")
            .Validate(o => o.SweepBatchSize is >= 1 and <= 10_000, "HOLD_SWEEP_BATCH_SIZE must be 1..10000.")
            .ValidateOnStart();

        services.AddHostedService<HoldExpirySweeper>();

        return services;
    }

    /// <summary>Missing → default; present but not an integer → -1 so options validation fails loudly at startup.</summary>
    private static int ReadInt(IConfiguration configuration, string key, int defaultValue)
    {
        var raw = configuration[key];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        return int.TryParse(raw, out var value) ? value : -1;
    }
}
