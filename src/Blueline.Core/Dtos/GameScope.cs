using Blueline.Core.Entities;

namespace Blueline.Core.Dtos;

/// <summary>
/// Which games a query counts. Every stat query takes one of these, so a page and its API
/// equivalent can never disagree about what "the season" means.
///
/// The two never merge, deliberately. A combined scope existed and was removed: the regular
/// season and the playoffs are scored differently enough that totalling them produces figures
/// nobody quotes. Standings points, overtime losses and points percentage are all regular-season
/// concepts, so a combined view had to hide three columns to avoid stating something false —
/// and a combined game count sits beside a points total that ignores half those games.
/// </summary>
public enum GameScope
{
    /// <summary>Regular season only — the default, and what every published stat line means.</summary>
    RegularSeason,

    /// <summary>Playoff games only, numbered from a club's first playoff game rather than their 83rd.</summary>
    Playoffs,
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
        _ => [Entities.GameTypes.Regular],
    };

    public static string Label(this GameScope scope) => scope switch
    {
        GameScope.Playoffs => "Playoffs",
        _ => "Regular season",
    };

    /// <summary>Short form for table headers and chart axes, where the full label is too long.</summary>
    public static string ShortLabel(this GameScope scope) => scope switch
    {
        GameScope.Playoffs => "Playoffs",
        _ => "Regular",
    };

    /// <summary>
    /// Standings points only exist in the regular season, so a scope that includes playoff games
    /// cannot present a meaningful points total or points percentage.
    /// </summary>
    public static bool HasStandingsPoints(this GameScope scope) => scope == GameScope.RegularSeason;

    /// <summary>
    /// Parses a scope from a query string. Unrecognised values fall back to the regular season
    /// rather than erroring, so a stale bookmark still renders something sensible — including
    /// <c>?scope=All</c>, which this build no longer offers.
    ///
    /// <see cref="Enum.IsDefined{T}(T)"/> guards the numeric case: <see cref="Enum.TryParse{T}(string, bool, out T)"/>
    /// also accepts digits and hands back whatever number it is given, defined or not, so
    /// <c>?scope=7</c> would otherwise succeed and produce a scope no switch has an arm for.
    /// </summary>
    public static GameScope Parse(string? value) =>
        Enum.TryParse<GameScope>(value, ignoreCase: true, out var scope) && Enum.IsDefined(scope)
            ? scope
            : GameScope.RegularSeason;
}
