using System.Diagnostics;
using RecipeLibrary.Testing;

namespace RecipeLibrary.Web.E2E.Tests.Fixtures;

public sealed class E2eProcessHost : IAsyncDisposable
{
    private Process? _process;
    private readonly System.Collections.Concurrent.ConcurrentQueue<string> _output = new();

    public string Diagnostics => string.Join(Environment.NewLine, _output);

    public string BaseUrl { get; private set; } = string.Empty;

    public async Task StartAsync(string connectionString, CancellationToken ct = default, IReadOnlyDictionary<string, string>? settings = null)
    {
        BaseUrl = string.Empty;
        var listeningUrl = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        var webProject = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "Web", "RecipeLibrary.Web", "RecipeLibrary.Web.csproj"));

        if (!File.Exists(webProject))
        {
            throw new FileNotFoundException("Web project not found for E2E host.", webProject);
        }

        var uploadPath = Path.Combine(Path.GetTempPath(), "RecipeLibraryE2E", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(uploadPath);

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{webProject}\" -c Release --no-build --no-launch-profile --urls http://127.0.0.1:0",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Testing";
        psi.Environment["ConnectionStrings__RecipeDb"] = connectionString;
        psi.Environment["RecipeFileStorage__LocalBasePath"] = uploadPath;

        if (settings is not null)
            foreach (var setting in settings) psi.Environment[setting.Key] = setting.Value;

        psi.Environment["Logging__LogLevel__Microsoft.Hosting.Lifetime"] = "Information";

        _process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start the web application process.");

        _process.OutputDataReceived += (_, line) =>
        {
            Capture(line.Data);
            const string marker = "Now listening on: ";
            var markerIndex = line.Data?.IndexOf(marker, StringComparison.Ordinal) ?? -1;
            if (markerIndex >= 0
                && Uri.TryCreate(line.Data![(markerIndex + marker.Length)..].Trim(), UriKind.Absolute, out var address)
                && address.Scheme == Uri.UriSchemeHttp
                && address.Host == "127.0.0.1"
                && address.Port > 0)
            {
                listeningUrl.TrySetResult(address.GetLeftPart(UriPartial.Authority));
            }
        };
        _process.ErrorDataReceived += (_, line) => Capture(line.Data);
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTime.UtcNow.AddMinutes(2);
        Exception? lastError = null;

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            if (listeningUrl.Task.IsCompletedSuccessfully)
            {
                BaseUrl = await listeningUrl.Task;
                try
                {
                    using var response = await client.GetAsync($"{BaseUrl}/health/live", ct);
                    if (response.IsSuccessStatusCode)
                    {
                        return;
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }
            }

            if (_process.HasExited)
            {
                var stderr = string.Join(Environment.NewLine, _output);
                throw new InvalidOperationException(
                    $"Web application process exited with code {_process.ExitCode}. stderr: {stderr}",
                    lastError);
            }

            await Task.Delay(500, ct);
        }

        throw new InvalidOperationException($"Web application did not become reachable in time. {Diagnostics}", lastError);
    }

    private void Capture(string? line)
    {
        if (line is null) return;
        _output.Enqueue(line);
        while (_output.Count > 100) _output.TryDequeue(out _);
    }

    public async ValueTask DisposeAsync()
    {
        if (_process is { HasExited: false })
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }

        _process?.Dispose();
    }
}
