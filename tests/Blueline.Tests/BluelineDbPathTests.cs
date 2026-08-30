using Blueline.Data;

namespace Blueline.Tests;

/// <summary>
/// Where the database file lands. Worth pinning because the web host and the CLI are separate
/// processes: if they ever disagree here, they silently operate on two different databases.
/// </summary>
/// <remarks>
/// Mutates a process-wide environment variable, so this fixture must not run beside others.
/// </remarks>
[NonParallelizable]
public class BluelineDbPathTests
{
    private string? _original;
    private string _tempDir = "";

    [SetUp]
    public void SetUp()
    {
        _original = Environment.GetEnvironmentVariable(BluelineDbPath.DataDirectoryVariable);
        _tempDir = Path.Combine(Path.GetTempPath(), "blueline-tests", Guid.NewGuid().ToString("N"));
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable(BluelineDbPath.DataDirectoryVariable, _original);
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public void The_data_directory_honours_the_environment_variable()
    {
        Environment.SetEnvironmentVariable(BluelineDbPath.DataDirectoryVariable, _tempDir);

        Assert.That(BluelineDbPath.DataDirectory, Is.EqualTo(_tempDir));
    }

    [Test]
    public void The_database_file_sits_inside_the_data_directory()
    {
        Environment.SetEnvironmentVariable(BluelineDbPath.DataDirectoryVariable, _tempDir);

        Assert.That(BluelineDbPath.DatabaseFile, Is.EqualTo(Path.Combine(_tempDir, "blueline.db")));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void A_blank_environment_variable_falls_back_to_a_per_user_folder(string? value)
    {
        Environment.SetEnvironmentVariable(BluelineDbPath.DataDirectoryVariable, value);

        Assert.Multiple(() =>
        {
            Assert.That(BluelineDbPath.DataDirectory, Is.Not.Empty);
            Assert.That(BluelineDbPath.DataDirectory, Does.EndWith("Blueline"));
        });
    }

    [Test]
    public void An_explicit_connection_string_always_wins()
    {
        Environment.SetEnvironmentVariable(BluelineDbPath.DataDirectoryVariable, _tempDir);

        // This is what lets a deployment point at Postgres without touching code.
        const string configured = "Host=localhost;Database=blueline";
        Assert.That(BluelineDbPath.ResolveConnectionString(configured), Is.EqualTo(configured));
    }

    [Test]
    public void An_explicit_connection_string_does_not_create_any_directory()
    {
        Environment.SetEnvironmentVariable(BluelineDbPath.DataDirectoryVariable, _tempDir);

        BluelineDbPath.ResolveConnectionString("Host=localhost;Database=blueline");

        Assert.That(Directory.Exists(_tempDir), Is.False);
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void An_absent_connection_string_resolves_to_the_sqlite_file(string? configured)
    {
        Environment.SetEnvironmentVariable(BluelineDbPath.DataDirectoryVariable, _tempDir);

        var resolved = BluelineDbPath.ResolveConnectionString(configured);

        Assert.Multiple(() =>
        {
            Assert.That(resolved, Is.EqualTo($"Data Source={Path.Combine(_tempDir, "blueline.db")}"));
            Assert.That(Directory.Exists(_tempDir), Is.True, "the directory must exist before SQLite opens the file");
        });
    }

    [Test]
    public void Resolving_twice_is_harmless_when_the_directory_already_exists()
    {
        Environment.SetEnvironmentVariable(BluelineDbPath.DataDirectoryVariable, _tempDir);

        var first = BluelineDbPath.ResolveConnectionString(null);
        var second = BluelineDbPath.ResolveConnectionString(null);

        Assert.That(second, Is.EqualTo(first));
    }
}
