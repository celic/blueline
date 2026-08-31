namespace Blueline.Core.Dtos;

/// <summary>How close to now the newest stored game is.</summary>
public enum SeasonFreshness
{
    /// <summary>Hockey was played in the last few days. Trailing windows describe current form.</summary>
    Current,

    /// <summary>
    /// Nothing for a week or two. Either the league is on a break or collection has stopped, and
    /// the site cannot tell those apart from the games alone.
    /// </summary>
    Behind,

    /// <summary>
    /// Nothing for weeks. The season being viewed is over, and its trailing windows describe how
    /// it finished rather than anything current.
    /// </summary>
    OffSeason,
}

/// <summary>
/// Whether what the site is showing is current, and the wording that depends on it.
///
/// A trailing window is silent about its own age: "most points in the last ten games" reads the
/// same in March as it does in August, when those ten games are four months old. Every page built
/// on a window therefore has to say which it is, or it presents a museum piece as current form —
/// and this is the season that gets it wrong, because 2025-26 is complete and 2026-27 does not
/// open until 2026-09-29.
/// </summary>
public static class SeasonFreshnessRules
{
    /// <summary>
    /// Within this, the league is playing. Clubs play every second or third night and thirty-two
    /// of them are doing it at once, so a day or two of silence is normal and a week is not.
    /// </summary>
    public const int CurrentWithinDays = 3;

    /// <summary>
    /// Past this, the season is over rather than paused. The longest breaks a season contains —
    /// an all-star weekend, an Olympic break — run to about a fortnight, so three weeks of nothing
    /// is not a gap in the schedule.
    /// </summary>
    public const int OffSeasonAfterDays = 21;

    public static SeasonFreshness Classify(DateOnly newestGame, DateOnly today)
    {
        var days = today.DayNumber - newestGame.DayNumber;

        // A game dated ahead of today means a clock that disagrees with the schedule, not a
        // problem worth announcing on the page.
        if (days <= CurrentWithinDays) return SeasonFreshness.Current;

        return days > OffSeasonAfterDays ? SeasonFreshness.OffSeason : SeasonFreshness.Behind;
    }

    /// <summary>Whole days between the newest game and today; never negative.</summary>
    public static int DaysSince(DateOnly newestGame, DateOnly today) =>
        Math.Max(0, today.DayNumber - newestGame.DayNumber);

    /// <summary>
    /// How long ago, in the units a reader would use. Exact days stop being informative once
    /// there are a hundred of them.
    /// </summary>
    public static string Describe(int days) => days switch
    {
        <= 0 => "today",
        1 => "yesterday",
        < 14 => $"{days} days ago",
        < 60 => $"{days / 7} weeks ago",
        _ => $"{days / 30} months ago",
    };
}
