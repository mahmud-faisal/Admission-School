using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace SchoolAdmission.IntegrationTests.Infrastructure;

/// <summary>
/// Base class: every test gets a fresh database and an in-process API (xUnit creates one instance per test).
/// Override <see cref="Settings"/> for env-style settings, or call <see cref="StartApiAsync"/> again.
/// </summary>
public abstract class IntegrationTest(PostgresFixture postgres) : IAsyncLifetime
{
    public const string StudentHeader = "X-Student-Id";

    private readonly List<AdmissionApiFactory> _factories = [];

    protected PostgresFixture Postgres { get; } = postgres;

    protected string ConnectionString { get; private set; } = string.Empty;

    protected HttpClient Client { get; private set; } = null!;

    protected virtual IReadOnlyDictionary<string, string?> Settings => new Dictionary<string, string?>();

    public async ValueTask InitializeAsync()
    {
        ConnectionString = await Postgres.CreateDatabaseAsync();
        Client = StartApiAsync(Settings);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var factory in _factories)
        {
            await factory.DisposeAsync();
        }

        await Postgres.DropDatabaseAsync(ConnectionString);
        GC.SuppressFinalize(this);
    }

    /// <summary>Starts another in-process API instance on the same test database.</summary>
    protected HttpClient StartApiAsync(
        IReadOnlyDictionary<string, string?> settings,
        Action<IServiceCollection>? configureServices = null,
        string environment = "Testing")
    {
        var factory = new AdmissionApiFactory(ConnectionString, settings, configureServices, environment);
        _factories.Add(factory);
        return factory.CreateClient();
    }

    protected static string Sid(int n) => (20150000000000000L + n).ToString(System.Globalization.CultureInfo.InvariantCulture);

    protected async Task<string> CreateClassAsync(string name = "Class 6 - Morning Shift", int seats = 10, HttpClient? client = null)
    {
        var response = await (client ?? Client).PostAsJsonAsync("/classes", new { name, totalSeats = seats });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await ReadJsonAsync(response)).GetProperty("id").GetString()!;
    }

    protected static Task<HttpResponseMessage> ApplyAsync(HttpClient client, string classId, string? studentId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/classes/{classId}/holds");
        if (studentId is not null)
        {
            request.Headers.Add(StudentHeader, studentId);
        }

        return client.SendAsync(request);
    }

    protected Task<HttpResponseMessage> ApplyAsync(string classId, string? studentId) => ApplyAsync(Client, classId, studentId);

    protected async Task<string> ApplyOkAsync(string classId, string studentId)
    {
        var response = await ApplyAsync(classId, studentId);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await ReadJsonAsync(response)).GetProperty("holdId").GetString()!;
    }

    protected Task<HttpResponseMessage> SendAsStudentAsync(HttpMethod method, string url, string? studentId, HttpClient? client = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (studentId is not null)
        {
            request.Headers.Add(StudentHeader, studentId);
        }

        return (client ?? Client).SendAsync(request);
    }

    protected Task<HttpResponseMessage> ConfirmAsync(string holdId, string studentId, HttpClient? client = null) =>
        SendAsStudentAsync(HttpMethod.Post, $"/holds/{holdId}/confirm", studentId, client);

    protected Task<HttpResponseMessage> CancelAsync(string holdId, string studentId) =>
        SendAsStudentAsync(HttpMethod.Delete, $"/holds/{holdId}", studentId);

    protected async Task<JsonElement> GetClassAsync(string classId)
    {
        var response = await Client.GetAsync($"/classes/{classId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await ReadJsonAsync(response);
    }

    protected static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    protected static async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
    {
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var json = await ReadJsonAsync(response);
        return json.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    /// <summary>Runs SQL directly against this test's database, bypassing the API.</summary>
    protected async Task<T> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        var result = await command.ExecuteScalarAsync();
        return (T)Convert.ChangeType(result!, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }

    protected async Task ExecuteSqlAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync();
    }
}
