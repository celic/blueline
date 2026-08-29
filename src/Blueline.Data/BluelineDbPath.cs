namespace Blueline.Data;

/// <summary>
/// Works out where the SQLite file lives.
///
/// The web host and the CLI are separate processes started from different working directories,
/// so a relative path would silently give them two different databases. Everything therefore
/// resolves to one explicit directory: <c>BLUELINE_DATA_DIR</c> if set (point this at the mounted
/// volume when deploying), otherwise a per-user application data folder.
/// </summary>
public static class BluelineDbPath
{
    public const string DataDirectoryVariable = "BLUELINE_DATA_DIR";
    private const string FileName = "blueline.db";

    public static string DataDirectory
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable(DataDirectoryVariable);
            if (!string.IsNullOrWhiteSpace(configured)) return configured;

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            // On a container with no profile this comes back empty; fall back to the app's own folder.
            if (string.IsNullOrWhiteSpace(appData)) appData = AppContext.BaseDirectory;

            return Path.Combine(appData, "Blueline");
        }
    }

    public static string DatabaseFile => Path.Combine(DataDirectory, FileName);

    /// <summary>
    /// Returns the connection string to use. An explicit configured value always wins, so a
    /// deployment can point at Postgres or a different file without code changes.
    /// </summary>
    public static string ResolveConnectionString(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured)) return configured;

        Directory.CreateDirectory(DataDirectory);
        return $"Data Source={DatabaseFile}";
    }
}
