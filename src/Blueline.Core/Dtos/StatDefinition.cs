namespace Blueline.Core.Dtos;

/// <summary>
/// The set of stats a trend can be plotted for. Keeping these in one place means the API,
/// the chart labels and the validation of user input never drift apart.
/// </summary>
public record StatDefinition(string Key, string Label, string Unit, bool CumulativeMakesSense)
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

    public static StatDefinition? FindSkater(string key) =>
        Skater.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase));

    public static StatDefinition? FindTeam(string key) =>
        Team.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase));
}
