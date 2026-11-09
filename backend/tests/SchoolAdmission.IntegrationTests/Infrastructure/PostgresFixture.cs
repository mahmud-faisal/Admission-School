using Microsoft.EntityFrameworkCore;
using Npgsql;
using SchoolAdmission.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(SchoolAdmission.IntegrationTests.Infrastructure.PostgresFixture))]

namespace SchoolAdmission.IntegrationTests.Infrastructure;

/// <summary>
/// One real PostgreSQL per test run:
/// - TEST_DATABASE_URL set (docker compose `test` profile) → use that server;
/// - otherwise start postgres:16-alpine with Testcontainers (needs Docker locally).
/// The schema is migrated once into a template database; every test gets its own fresh copy
/// (<c>CREATE DATABASE ... TEMPLATE</c>), so tests never share state and can run in parallel.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly string _template = "adm_tpl_" + Guid.NewGuid().ToString("N")[..12];

    // Test-only: PostgreSQL refuses concurrent CREATE DATABASE from the same template.
    private readonly SemaphoreSlim _createLock = new(1, 1);

    private PostgreSqlContainer? _container;
    private string _adminConnectionString = string.Empty;

    public async ValueTask InitializeAsync()
    {
        var url = Environment.GetEnvironmentVariable("TEST_DATABASE_URL");
        if (!string.IsNullOrWhiteSpace(url))
        {
            _adminConnectionString = ConnectionStringResolver.Normalize(url);
        }
        else
        {
            _container = new PostgreSqlBuilder("postgres:16-alpine").Build();
            await _container.StartAsync();
            _adminConnectionString = _container.GetConnectionString();
        }

        await ExecuteAdminAsync($"CREATE DATABASE {_template}");

        var options = new DbContextOptionsBuilder<AdmissionDbContext>().UseNpgsql(ForDatabase(_template)).Options;
        await using (var db = new AdmissionDbContext(options))
        {
            await db.Database.MigrateAsync();
        }

        // A template must have no open connections when it is copied.
        NpgsqlConnection.ClearAllPools();
    }

    public async Task<string> CreateDatabaseAsync()
    {
        var name = "adm_test_" + Guid.NewGuid().ToString("N");
        await _createLock.WaitAsync();
        try
        {
            await ExecuteAdminAsync($"CREATE DATABASE {name} TEMPLATE {_template}");
        }
        finally
        {
            _createLock.Release();
        }

        return ForDatabase(name);
    }

    public async Task DropDatabaseAsync(string connectionString)
    {
        var name = new NpgsqlConnectionStringBuilder(connectionString).Database!;
        NpgsqlConnection.ClearAllPools();
        await ExecuteAdminAsync($"DROP DATABASE IF EXISTS {name} WITH (FORCE)");
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_container is null)
            {
                await ExecuteAdminAsync($"DROP DATABASE IF EXISTS {_template} WITH (FORCE)");
            }
        }
        finally
        {
            if (_container is not null)
            {
                await _container.DisposeAsync();
            }

            _createLock.Dispose();
        }
    }

    private string ForDatabase(string database) =>
        new NpgsqlConnectionStringBuilder(_adminConnectionString) { Database = database, MaxPoolSize = 200 }.ConnectionString;

    private async Task ExecuteAdminAsync(string sql)
    {
        // Database names are generated above (hex only), never user input.
        await using var connection = new NpgsqlConnection(
            new NpgsqlConnectionStringBuilder(_adminConnectionString) { Pooling = false }.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
