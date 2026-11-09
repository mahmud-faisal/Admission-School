using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace SchoolAdmission.IntegrationTests.Infrastructure;

/// <summary>
/// Starts the real API as a separate OS process (<c>dotnet SchoolAdmission.Api.dll</c>) on a free port.
/// Used by the bonus test: two independent processes on one database must never oversell.
/// </summary>
public sealed class ApiProcess : IAsyncDisposable
{
    private readonly Process _process;
    private readonly ConcurrentQueue<string> _output = new();

    private ApiProcess(Process process, Uri baseAddress)
    {
        _process = process;
        BaseAddress = baseAddress;
    }

    public Uri BaseAddress { get; }

    /// <summary>API_DLL_PATH, or the Api dll copied next to the test assembly (project reference).</summary>
    public static string? FindApiDll()
    {
        var fromEnv = Environment.GetEnvironmentVariable("API_DLL_PATH");
        if (!string.IsNullOrWhiteSpace(fromEnv) && File.Exists(fromEnv))
        {
            return fromEnv;
        }

        var local = Path.Combine(AppContext.BaseDirectory, "SchoolAdmission.Api.dll");
        var runtimeConfig = Path.Combine(AppContext.BaseDirectory, "SchoolAdmission.Api.runtimeconfig.json");
        return File.Exists(local) && File.Exists(runtimeConfig) ? local : null;
    }

    public static async Task<ApiProcess> StartAsync(string apiDll, string connectionString, IReadOnlyDictionary<string, string> extraEnv)
    {
        var port = FreePort();
        var dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } host ? host : "dotnet";
        var info = new ProcessStartInfo(dotnet)
        {
            WorkingDirectory = Path.GetDirectoryName(apiDll)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        info.ArgumentList.Add(apiDll);
        info.ArgumentList.Add("--urls");
        info.ArgumentList.Add($"http://127.0.0.1:{port}");
        info.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        info.Environment["ConnectionStrings__Default"] = connectionString;
        foreach (var (key, value) in extraEnv)
        {
            info.Environment[key] = value;
        }

        var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start the API process.");
        var api = new ApiProcess(process, new Uri($"http://127.0.0.1:{port}"));
        process.OutputDataReceived += (_, e) => api.Record(e.Data);
        process.ErrorDataReceived += (_, e) => api.Record(e.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await api.WaitUntilReadyAsync(TimeSpan.FromSeconds(60));
        return api;
    }

    public HttpClient CreateClient() => new() { BaseAddress = BaseAddress, Timeout = TimeSpan.FromSeconds(60) };

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync();
            }
        }
        finally
        {
            _process.Dispose();
        }
    }

    private async Task WaitUntilReadyAsync(TimeSpan timeout)
    {
        using var client = new HttpClient { BaseAddress = BaseAddress, Timeout = TimeSpan.FromSeconds(2) };
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (_process.HasExited)
            {
                throw new InvalidOperationException($"API process exited early:\n{string.Join('\n', _output)}");
            }

            try
            {
                var response = await client.GetAsync("/health/ready");
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // not listening yet
            }
            catch (TaskCanceledException)
            {
                // request timed out; retry
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"API process did not become ready:\n{string.Join('\n', _output)}");
    }

    private void Record(string? line)
    {
        if (line is null)
        {
            return;
        }

        _output.Enqueue(line);
        while (_output.Count > 200 && _output.TryDequeue(out _))
        {
        }
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
