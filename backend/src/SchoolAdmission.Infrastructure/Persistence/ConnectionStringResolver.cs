using Microsoft.Extensions.Configuration;
using Npgsql;

namespace SchoolAdmission.Infrastructure.Persistence;

/// <summary>
/// Reads <c>ConnectionStrings__Default</c> first, then <c>DATABASE_URL</c>. DATABASE_URL may be a
/// <c>postgres://user:pass@host:port/db</c> URI or an Npgsql keyword string.
/// </summary>
public static class ConnectionStringResolver
{
    public const string DatabaseUrlKey = "DATABASE_URL";

    public static string Resolve(IConfiguration configuration)
    {
        var fromConnectionStrings = configuration.GetConnectionString("Default");
        if (!string.IsNullOrWhiteSpace(fromConnectionStrings))
        {
            return fromConnectionStrings;
        }

        var databaseUrl = configuration[DatabaseUrlKey];
        if (!string.IsNullOrWhiteSpace(databaseUrl))
        {
            return Normalize(databaseUrl);
        }

        throw new InvalidOperationException(
            "No database connection configured. Set ConnectionStrings__Default (Npgsql keyword form) " +
            "or DATABASE_URL (postgres://user:password@host:port/database).");
    }

    /// <summary>Turns a postgres:// or postgresql:// URI into an Npgsql keyword string; other values pass through.</summary>
    public static string Normalize(string value)
    {
        if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        var uri = new Uri(value);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
        };

        var userInfo = uri.UserInfo.Split(':', 2);
        if (userInfo.Length > 0 && userInfo[0].Length > 0)
        {
            builder.Username = Uri.UnescapeDataString(userInfo[0]);
        }

        if (userInfo.Length > 1)
        {
            builder.Password = Uri.UnescapeDataString(userInfo[1]);
        }

        // Pass through query options such as ?sslmode=require.
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            if (kv.Length == 2)
            {
                builder[Uri.UnescapeDataString(kv[0])] = Uri.UnescapeDataString(kv[1]);
            }
        }

        return builder.ConnectionString;
    }
}
