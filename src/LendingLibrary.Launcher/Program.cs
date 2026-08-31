using System.Diagnostics;
using System.Net;

Console.Title = "Library Lending - Launcher";
const string AppUrl = "http://localhost:8080";
const string HealthUrl = AppUrl + "/health";

PrintHeader("Library Lending launcher");

var repoRoot = FindRepoRoot();
if (repoRoot is null)
{
    Fail("Could not find docker-compose.yml above this launcher's folder.");
    return;
}

Console.WriteLine($"Project folder: {repoRoot}");

if (!await EnsureDockerRunningAsync())
{
    Fail("Docker Desktop did not start in time. Start it manually and try again.");
    return;
}

Console.WriteLine();
Console.WriteLine("Starting containers (docker compose up -d)...");
var composeExit = RunStreamed("docker", "compose up -d", repoRoot);
if (composeExit != 0)
{
    Fail($"'docker compose up -d' exited with code {composeExit}. See output above.");
    return;
}

Console.WriteLine();
Console.Write("Waiting for the app to respond");
if (!await WaitForHealthyAsync(HealthUrl, TimeSpan.FromSeconds(90)))
{
    Console.WriteLine();
    Fail("The app didn't become healthy in time. Check 'docker compose logs web'.");
    return;
}

Console.WriteLine();
Console.WriteLine($"App is up: {AppUrl}");
Console.WriteLine("Opening your default browser...");

try
{
    Process.Start(new ProcessStartInfo(AppUrl) { UseShellExecute = true });
}
catch (Exception ex)
{
    Console.WriteLine($"Couldn't open a browser automatically ({ex.Message}). Open {AppUrl} manually.");
}

Console.WriteLine();
Console.WriteLine("Done. The app keeps running in Docker after you close this window.");
WaitForKeyPress();
return;

static void PrintHeader(string title)
{
    Console.WriteLine(new string('=', title.Length));
    Console.WriteLine(title);
    Console.WriteLine(new string('=', title.Length));
    Console.WriteLine();
}

static void Fail(string message)
{
    Console.WriteLine();
    Console.WriteLine($"ERROR: {message}");
    WaitForKeyPress();
}

static void WaitForKeyPress()
{
    Console.WriteLine("Press any key to close this window...");
    if (Console.IsInputRedirected)
    {
        return;
    }

    try
    {
        Console.ReadKey(intercept: true);
    }
    catch (InvalidOperationException)
    {
        // No console attached to read from; nothing more to do.
    }
}

static string? FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    for (var i = 0; i < 10 && dir is not null; i++, dir = dir.Parent)
    {
        if (File.Exists(Path.Combine(dir.FullName, "docker-compose.yml")))
        {
            return dir.FullName;
        }
    }

    return null;
}

static async Task<bool> EnsureDockerRunningAsync()
{
    Console.Write("Checking Docker Desktop");
    if (RunSilent("docker", "info") == 0)
    {
        Console.WriteLine(" - already running.");
        return true;
    }

    Console.WriteLine(" - not running, starting it...");

    var dockerDesktopExe = @"C:\Program Files\Docker\Docker\Docker Desktop.exe";
    if (File.Exists(dockerDesktopExe))
    {
        Process.Start(new ProcessStartInfo(dockerDesktopExe) { UseShellExecute = true });
    }
    else
    {
        Console.WriteLine($"Could not find Docker Desktop at '{dockerDesktopExe}'. Start it manually.");
    }

    var deadline = DateTime.UtcNow.AddMinutes(2);
    while (DateTime.UtcNow < deadline)
    {
        Console.Write(".");
        if (RunSilent("docker", "info") == 0)
        {
            Console.WriteLine(" ready.");
            return true;
        }

        await Task.Delay(TimeSpan.FromSeconds(3));
    }

    return false;
}

static async Task<bool> WaitForHealthyAsync(string healthUrl, TimeSpan timeout)
{
    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    var deadline = DateTime.UtcNow.Add(timeout);

    while (DateTime.UtcNow < deadline)
    {
        try
        {
            var response = await client.GetAsync(healthUrl);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                return true;
            }
        }
        catch
        {
            // App not accepting connections yet; keep polling.
        }

        Console.Write(".");
        await Task.Delay(TimeSpan.FromSeconds(2));
    }

    return false;
}

static int RunSilent(string fileName, string arguments)
{
    try
    {
        using var process = Process.Start(new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        });
        process!.WaitForExit();
        return process.ExitCode;
    }
    catch
    {
        return -1;
    }
}

static int RunStreamed(string fileName, string arguments, string workingDirectory)
{
    try
    {
        using var process = Process.Start(new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true
        });
        process!.WaitForExit();
        return process.ExitCode;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to run '{fileName} {arguments}': {ex.Message}");
        return -1;
    }
}
