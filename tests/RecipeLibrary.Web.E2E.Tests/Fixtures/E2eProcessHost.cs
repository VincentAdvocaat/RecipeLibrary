using System.Diagnostics;
using RecipeLibrary.Testing;

namespace RecipeLibrary.Web.E2E.Tests.Fixtures;

public sealed class E2eProcessHost : IAsyncDisposable
{
    private Process? _process;

    public string BaseUrl { get; private set; } = string.Empty;

    public async Task StartAsync(string connectionString, CancellationToken ct = default)
    {
        var port = Random.Shared.Next(10000, 60000);
        BaseUrl = $"http://127.0.0.1:{port}";

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
            Arguments = $"run --project \"{webProject}\" -c Release --no-build --no-launch-profile --urls {BaseUrl}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Testing";
        psi.Environment["ConnectionStrings__RecipeDb"] = connectionString;
        psi.Environment["RecipeFileStorage__LocalBasePath"] = uploadPath;

        _process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start the web application process.");

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTime.UtcNow.AddMinutes(2);
        Exception? lastError = null;

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var response = await client.GetAsync($"{BaseUrl}/recipes", ct);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            if (_process.HasExited)
            {
                var stderr = await _process.StandardError.ReadToEndAsync(ct);
                throw new InvalidOperationException(
                    $"Web application process exited with code {_process.ExitCode}. stderr: {stderr}",
                    lastError);
            }

            await Task.Delay(500, ct);
        }

        throw new InvalidOperationException("Web application did not become reachable in time.", lastError);
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
