using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace SchoolAdmission.Infrastructure.Persistence;

/// <summary>
/// Applies EF migrations at startup, retrying while PostgreSQL is still starting (docker compose).
/// EF Core 9+ takes a migration lock, so two instances starting together do not migrate concurrently.
/// </summary>
public static class DatabaseMigrator
{
    public static async Task MigrateDatabaseWithRetryAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseMigrator).FullName!);

        if (!ReadBool(configuration["DB_MIGRATE_ON_STARTUP"], defaultValue: true))
        {
            logger.LogInformation("DB_MIGRATE_ON_STARTUP=false: skipping migrations.");
            return;
        }

        var maxAttempts = int.TryParse(configuration["DB_MIGRATE_MAX_ATTEMPTS"], out var n) && n > 0 ? n : 15;

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var scope = services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AdmissionDbContext>();
                await db.Database.MigrateAsync(ct);
                logger.LogInformation("Database migrations applied (attempt {Attempt}).", attempt);
                return;
            }
            catch (Exception ex) when (IsTransient(ex) && attempt < maxAttempts)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(10, Math.Pow(2, attempt - 1)));
                logger.LogWarning(
                    "Database not ready (attempt {Attempt}/{Max}): {Message}. Retrying in {Delay}s.",
                    attempt, maxAttempts, ex.Message, delay.TotalSeconds);
                await Task.Delay(delay, ct);
            }
        }
    }

    private static bool IsTransient(Exception ex) =>
        ex is NpgsqlException or SocketException or TimeoutException ||
        (ex.InnerException is not null && IsTransient(ex.InnerException));

    private static bool ReadBool(string? value, bool defaultValue) =>
        bool.TryParse(value, out var parsed) ? parsed : defaultValue;
}
