namespace Blueline.Core.Dtos;

/// <summary>
/// The set of stats a trend can be plotted for. Keeping these in one place means the API,
/// the chart labels and the validation of user input never drift apart.
/// </summary>
/// <param name="IsRate">
/// True for stats that are a ratio rather than a count — save percentage, goals-against average.
/// These accumulate by summing numerators and denominators separately, never by averaging the
/// per-game figures, so a 40-shot night counts for more than a 10-shot night.
/// </param>
public record StatDefinition(
    string Key,
    string Label,
    string Unit,
    bool CumulativeMakesSense,
    bool IsRate = false)
{
    public static readonly StatDefinition[] Skater =
    [
        new("points", "Points", "P", true),
        new("goals", "Goals", "G", true),
        new("assists", "Assists", "A", true),
        new("shots", "Shots on Goal", "SOG", true),
        new("hits", "Hits", "H", true),
        new("blockedShots", "Blocked Shots", "BLK", true),
        new("pim", "Penalty Minutes", "PIM", true),
        new("plusMinus", "Plus/Minus", "+/-", true),
        new("takeaways", "Takeaways", "TK", true),
        new("giveaways", "Giveaways", "GV", true),
        new("toi", "Time on Ice", "min", false),
    ];

    public static readonly StatDefinition[] Team =
    [
        new("points", "Standings Points", "PTS", true),
        new("goalsFor", "Goals For", "GF", true),
        new("goalsAgainst", "Goals Against", "GA", true),
        new("goalDifferential", "Goal Differential", "DIFF", true),
    ];

    public static readonly StatDefinition[] Goalie =
    [
        new("savePctg", "Save Percentage", "SV%", false, IsRate: true),
        new("gaa", "Goals Against Average", "GAA", false, IsRate: true),
        new("saves", "Saves", "SV", true),
        new("shotsAgainst", "Shots Against", "SA", true),
        new("goalsAgainst", "Goals Against", "GA", true),
        new("toi", "Time on Ice", "min", true),
    ];

    /// <summary>
    /// Minutes a goalie must play before appearing on a rate leaderboard. Without a floor, a
    /// goalie who faced four shots in one relief appearance tops the save percentage table.
    /// 1,500 is the league's own threshold for the save percentage title over a full season.
    /// </summary>
    public const int RateQualificationMinutes = 1500;

    public static StatDefinition? FindSkater(string key) =>
        Skater.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase));

    public static StatDefinition? FindTeam(string key) =>
        Team.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase));

    public static StatDefinition? FindGoalie(string key) =>
        Goalie.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase));
}
