using Blueline.Core.Entities;

namespace Blueline.Core.Dtos;

/// <summary>
/// Which games a query counts. Every stat query takes one of these, so a page and its API
/// equivalent can never disagree about what "the season" means.
/// </summary>
public enum GameScope
{
    /// <summary>Regular season only — the default, and what every published stat line means.</summary>
    RegularSeason,

    /// <summary>Playoff games only.</summary>
    Playoffs,

    /// <summary>Regular season and playoffs combined.</summary>
    All,
}

public static class GameScopes
{
    /// <summary>
    /// The game types a scope admits. Preseason is never included: it is not ingested, and its
    /// stats do not count towards anything.
    /// </summary>
    public static int[] GameTypes(this GameScope scope) => scope switch
    {
        GameScope.Playoffs => [Entities.GameTypes.Playoffs],
        GameScope.All => [Entities.GameTypes.Regular, Entities.GameTypes.Playoffs],
        _ => [Entities.GameTypes.Regular],
    };

    public static string Label(this GameScope scope) => scope switch
    {
        GameScope.Playoffs => "Playoffs",
        GameScope.All => "Regular season + playoffs",
        _ => "Regular season",
    };

    /// <summary>Short form for table headers and chart axes, where the full label is too long.</summary>
    public static string ShortLabel(this GameScope scope) => scope switch
    {
        GameScope.Playoffs => "Playoffs",
        GameScope.All => "Combined",
        _ => "Regular",
    };

    /// <summary>
    /// Standings points only exist in the regular season, so a scope that includes playoff games
    /// cannot present a meaningful points total or points percentage.
    /// </summary>
    public static bool HasStandingsPoints(this GameScope scope) => scope == GameScope.RegularSeason;

    /// <summary>
    /// Parses a scope from a query string. Unrecognised values fall back to the regular season
    /// rather than erroring, so a stale bookmark still renders something sensible.
    /// </summary>
    public static GameScope Parse(string? value) =>
        Enum.TryParse<GameScope>(value, ignoreCase: true, out var scope) ? scope : GameScope.RegularSeason;
}
