namespace Blueline.Ingestion;

public class IngestionOptions
{
    public const string SectionName = "Ingestion";

    /// <summary>
    /// Whether the site schedules its own daily ingestion pass.
    ///
    /// Off by default: collecting data is a scheduled job, and a scheduled job belongs outside the
    /// web app, where it can be run, watched and retried without a request pipeline in front of it.
    /// The README gives the recipe — a cron entry or scheduled task invoking the CLI's
    /// <c>daily</c> verb against the same database.
    ///
    /// Turn it on for a deployment that would rather carry the schedule in-process, or for local
    /// development. Seeding an empty database is unaffected either way.
    /// </summary>
    public bool DailyJobEnabled { get; set; }

    /// <summary>
    /// When the daily job runs, in UTC. Defaults to 11:00 UTC (roughly 07:00 Eastern), by which
    /// point every North American game from the previous night has finished and been scored.
    /// </summary>
    public TimeOnly DailyRunTimeUtc { get; set; } = new(11, 0);

    /// <summary>
    /// How many days back each daily run re-reads. The league revises box scores after the fact,
    /// so re-reading a short window is what keeps stored stats correct.
    /// </summary>
    public int LookbackDays { get; set; } = 3;

    /// <summary>Run one ingestion pass at startup rather than waiting for the next scheduled time.</summary>
    public bool RunOnStartup { get; set; } = true;

    /// <summary>Season the site seeds itself with when the database is empty. Zero disables seeding.</summary>
    public int SeedSeasonId { get; set; } = 20252026;

    /// <summary>
    /// Directory searched for season archives when the database is empty. Every archive found is
    /// loaded, so a deployment can ship several past seasons rather than just one.
    ///
    /// Relative paths resolve against the application directory. Set to an empty string to ignore
    /// archives entirely and always ingest from the league.
    /// </summary>
    public string? SeedArchiveDirectory { get; set; } = "seed";
}
