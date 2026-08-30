using Blueline.Ingestion;

namespace Blueline.Tests;

/// <summary>Which archives a deployment seeds from, and in what order.</summary>
[NonParallelizable]
public class SeedArchiveDiscoveryTests
{
    private string _directory = "";

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "blueline-seed", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private void WriteArchive(string name) => File.WriteAllText(Path.Combine(_directory, name), "not-real");

    private IngestionOptions Options() => new() { SeedArchiveDirectory = _directory };

    [Test]
    public void Every_archive_in_the_directory_is_found()
    {
        WriteArchive($"20252026{DailyIngestionWorker.ArchiveExtension}");
        WriteArchive($"20242025{DailyIngestionWorker.ArchiveExtension}");

        Assert.That(DailyIngestionWorker.FindSeedArchives(Options()), Has.Count.EqualTo(2),
            "a deployment can carry several past seasons");
    }

    [Test]
    public void Archives_are_ordered_oldest_first_so_the_newest_wins_any_overlap()
    {
        WriteArchive($"20252026{DailyIngestionWorker.ArchiveExtension}");
        WriteArchive($"20232024{DailyIngestionWorker.ArchiveExtension}");
        WriteArchive($"20242025{DailyIngestionWorker.ArchiveExtension}");

        var found = DailyIngestionWorker.FindSeedArchives(Options()).Select(Path.GetFileName).ToList();

        Assert.That(found, Is.EqualTo(new[]
        {
            $"20232024{DailyIngestionWorker.ArchiveExtension}",
            $"20242025{DailyIngestionWorker.ArchiveExtension}",
            $"20252026{DailyIngestionWorker.ArchiveExtension}",
        }));
    }

    [Test]
    public void Unrelated_files_are_ignored()
    {
        WriteArchive($"20252026{DailyIngestionWorker.ArchiveExtension}");
        WriteArchive("manifest.json");
        WriteArchive("20242025.blueline.gz.partial");
        WriteArchive("notes.txt");

        Assert.That(DailyIngestionWorker.FindSeedArchives(Options()), Has.Count.EqualTo(1),
            "a half-downloaded archive must not be imported");
    }

    [Test]
    public void An_empty_directory_setting_disables_archives_entirely()
    {
        WriteArchive($"20252026{DailyIngestionWorker.ArchiveExtension}");

        var options = new IngestionOptions { SeedArchiveDirectory = "" };

        Assert.That(DailyIngestionWorker.FindSeedArchives(options), Is.Empty,
            "an explicit empty setting means always ingest from the league");
    }

    [Test]
    public void A_missing_directory_is_not_an_error()
    {
        var options = new IngestionOptions
        {
            SeedArchiveDirectory = Path.Combine(_directory, "does-not-exist"),
        };

        Assert.That(DailyIngestionWorker.FindSeedArchives(options), Is.Empty);
    }

    [Test]
    public void A_directory_with_no_archives_falls_back_to_ingesting()
    {
        WriteArchive("manifest.json");

        Assert.That(DailyIngestionWorker.FindSeedArchives(Options()), Is.Empty);
    }
}
