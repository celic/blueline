using System.Diagnostics;
using System.Net.Sockets;
using Blueline.Core.Entities;
using Blueline.Data;
using Microsoft.EntityFrameworkCore;

namespace Blueline.UiTests;

/// <summary>
/// Starts the real site once for the whole run, against a small database built here.
///
/// A real process rather than an in-memory test server: Blazor Server needs a genuine socket for
/// its circuit, and the point of these tests is the browser actually connecting to it. The
/// database is seeded rather than borrowed from the developer's machine, so a test never depends
/// on whichever season happens to be loaded there, and ingestion is switched off so nothing
/// reaches the league's API.
/// </summary>
[SetUpFixture]
public class BluelineAppFixture
{
    public static string BaseUrl { get; private set; } = "";

    /// <summary>Known values the tests assert against.</summary>
    public static class Seed
    {
        public const int SeasonId = 20252026;
        public const int TopScorerId = 100;
        public const string TopScorerName = "Alexis Topscorer";
        public const int GrinderId = 101;
        public const string GrinderName = "Boris Grinder";
        public const int GoalieId = 200;
        public const string GoalieName = "Casper Goalie";
        public const int HomeTeamId = 21;
        public const int AwayTeamId = 22;
        public const int GameCount = 10;
    }

    private Process? _app;
    private string _dataDir = "";

    /// <summary>
    /// Downloads the browser these tests drive, if it is not already there.
    ///
    /// Playwright keeps browsers in a per-user cache outside the repository, so a checkout that
    /// builds and passes every other suite still cannot run these until someone runs an install
    /// command they have to know about. What it says when they have not is "Executable doesn't
    /// exist at ...chrome-headless-shell.exe", which names a file rather than a cause.
    ///
    /// Installing here is a no-op once the browser is present — it checks and returns — so the
    /// cost is a fraction of a second per run against a first-run download of a few hundred MB.
    /// Set BLUELINE_SKIP_PLAYWRIGHT_INSTALL where that download is not wanted, such as an air-
    /// gapped build that provisions the cache itself.
    /// </summary>
    private static void EnsureBrowserInstalled()
    {
        if (Environment.GetEnvironmentVariable("BLUELINE_SKIP_PLAYWRIGHT_INSTALL") is { Length: > 0 }) return;

        // Chromium and its headless shell are separate downloads, and headless runs need the
        // shell — installing "chromium" alone was enough in older versions and is not now.
        var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium", "chromium-headless-shell"]);

        if (exitCode != 0)
        {
            throw new InvalidOperationException(
                $"Playwright could not install its browsers (exit code {exitCode}). Run this once by hand: " +
                @"powershell -File tests\Blueline.UiTestsin\Debug
et10.0\playwright.ps1 install chromium chromium-headless-shell");
        }
    }

    [OneTimeSetUp]
    public async Task StartAsync()
    {
        EnsureBrowserInstalled();

        _dataDir = Path.Combine(Path.GetTempPath(), "blueline-ui", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dataDir);

        SeedDatabase(Path.Combine(_dataDir, "blueline.db"));

        var port = FreePort();
        BaseUrl = $"http://127.0.0.1:{port}";

        var webDll = Path.Combine(RepositoryRoot(), "src", "Blueline.Web", "bin", "Debug", "net10.0", "Blueline.Web.dll");
        if (!File.Exists(webDll))
            throw new FileNotFoundException($"Build Blueline.Web before running the UI tests. Expected {webDll}.");

        var startInfo = new ProcessStartInfo("dotnet", $"\"{webDll}\"")
        {
            // Run from the web project's own output so wwwroot and the static asset manifest resolve.
            WorkingDirectory = Path.GetDirectoryName(webDll)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        startInfo.Environment["ASPNETCORE_URLS"] = BaseUrl;
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        startInfo.Environment[BluelineDbPath.DataDirectoryVariable] = _dataDir;

        // No background ingestion and no self-seeding: the tests supply their own data.
        startInfo.Environment["Ingestion__DailyJobEnabled"] = "false";
        startInfo.Environment["Ingestion__RunOnStartup"] = "false";
        startInfo.Environment["Ingestion__SeedSeasonId"] = "0";

        _app = Process.Start(startInfo)
               ?? throw new InvalidOperationException("Could not start the site.");

        // Drain the pipes so a chatty log cannot fill the buffer and stall the process.
        _app.OutputDataReceived += (_, _) => { };
        _app.ErrorDataReceived += (_, _) => { };
        _app.BeginOutputReadLine();
        _app.BeginErrorReadLine();

        await WaitUntilReadyAsync();
    }

    [OneTimeTearDown]
    public void Stop()
    {
        try
        {
            if (_app is { HasExited: false }) _app.Kill(entireProcessTree: true);
            _app?.Dispose();
        }
        catch (InvalidOperationException)
        {
            // Already gone.
        }

        try
        {
            if (Directory.Exists(_dataDir)) Directory.Delete(_dataDir, recursive: true);
        }
        catch (IOException)
        {
            // A stray file handle is not worth failing the run over.
        }
    }

    private async Task WaitUntilReadyAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTime.UtcNow.AddSeconds(90);

        while (DateTime.UtcNow < deadline)
        {
            if (_app!.HasExited)
                throw new InvalidOperationException($"The site exited during startup with code {_app.ExitCode}.");

            try
            {
                // Readiness, not liveness: it also confirms the seeded data is visible.
                var response = await http.GetAsync($"{BaseUrl}/health/ready");
                if (response.IsSuccessStatusCode) return;
            }
            catch (HttpRequestException)
            {
                // Not listening yet.
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"The site did not become ready at {BaseUrl}.");
    }

    /// <summary>
    /// Builds the database through migrations rather than EnsureCreated: the app runs
    /// <c>MigrateAsync</c> at startup, and without a migrations history it would try to create
    /// tables that already exist and fail to boot.
    /// </summary>
    private static void SeedDatabase(string path)
    {
        using var db = new BluelineDbContext(new DbContextOptionsBuilder<BluelineDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options);

        db.Database.Migrate();

        db.Teams.Add(new Team { Id = Seed.HomeTeamId, Abbrev = "HME", Name = "Hometown Heroes" });
        db.Teams.Add(new Team { Id = Seed.AwayTeamId, Abbrev = "AWY", Name = "Awayville Wanderers" });

        db.Players.Add(new Player { Id = Seed.TopScorerId, FirstName = "Alexis", LastName = "Topscorer", Position = "C" });
        db.Players.Add(new Player { Id = Seed.GrinderId, FirstName = "Boris", LastName = "Grinder", Position = "L" });
        db.Players.Add(new Player { Id = Seed.GoalieId, FirstName = "Casper", LastName = "Goalie", Position = "G" });

        for (var i = 0; i < Seed.GameCount; i++)
        {
            var gameId = 2025020001 + i;
            var homeWin = i % 2 == 0;

            db.Games.Add(new Game
            {
                Id = gameId,
                SeasonId = Seed.SeasonId,
                GameType = GameTypes.Regular,
                // Deliberately uneven spacing, so the date axis has something to show.
                GameDate = new DateOnly(2025, 10, 8).AddDays(i < 5 ? i * 2 : i * 2 + 20),
                HomeTeamId = Seed.HomeTeamId,
                AwayTeamId = Seed.AwayTeamId,
                HomeScore = homeWin ? 4 : 1,
                AwayScore = homeWin ? 2 : 3,
                GameState = "OFF",
            });

            db.SkaterGameStats.Add(new SkaterGameStat
            {
                GameId = gameId, PlayerId = Seed.TopScorerId, TeamId = Seed.HomeTeamId,
                Goals = 2, Assists = 1, Points = 3, Shots = 6, Hits = 1, TimeOnIceSeconds = 1200,
            });

            db.SkaterGameStats.Add(new SkaterGameStat
            {
                GameId = gameId, PlayerId = Seed.GrinderId, TeamId = Seed.HomeTeamId,
                Goals = 0, Assists = 0, Points = 0, Shots = 1, Hits = 7, TimeOnIceSeconds = 900,
            });

            db.GoalieGameStats.Add(new GoalieGameStat
            {
                GameId = gameId, PlayerId = Seed.GoalieId, TeamId = Seed.HomeTeamId,
                Saves = 28, ShotsAgainst = 30, GoalsAgainst = 2, TimeOnIceSeconds = 3600, Starter = true,
            });

            db.TeamGameStats.Add(new TeamGameStat
            {
                GameId = gameId, TeamId = Seed.HomeTeamId, OpponentTeamId = Seed.AwayTeamId, IsHome = true,
                GoalsFor = homeWin ? 4 : 1, GoalsAgainst = homeWin ? 2 : 3,
                Result = homeWin ? "W" : "L", Points = homeWin ? 2 : 0,
            });

            db.TeamGameStats.Add(new TeamGameStat
            {
                GameId = gameId, TeamId = Seed.AwayTeamId, OpponentTeamId = Seed.HomeTeamId, IsHome = false,
                GoalsFor = homeWin ? 2 : 3, GoalsAgainst = homeWin ? 4 : 1,
                Result = homeWin ? "L" : "W", Points = homeWin ? 0 : 2,
            });
        }

        db.SaveChanges();
    }

    private static int FreePort()
    {
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>
    /// Walks up from the test binaries looking for the web project, rather than for a solution
    /// file: the solution's extension has already changed once (.sln to .slnx), and the project
    /// being launched is the thing actually needed here.
    /// </summary>
    private static string RepositoryRoot()
    {
        var marker = Path.Combine("src", "Blueline.Web", "Blueline.Web.csproj");
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, marker)))
            directory = directory.Parent;

        return directory?.FullName
               ?? throw new DirectoryNotFoundException(
                   $"Could not locate the repository root from {AppContext.BaseDirectory}; expected to find {marker}.");
    }
}
