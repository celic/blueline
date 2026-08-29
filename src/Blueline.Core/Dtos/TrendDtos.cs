namespace Blueline.Core.Dtos;

/// <summary>A single point on a trend line: one game, in season order.</summary>
public record TrendPoint(
    int GameNumber,
    long GameId,
    DateOnly Date,
    string Opponent,
    bool IsHome,
    double Value,
    double Cumulative,
    double? RollingAverage);

/// <summary>A full trend series for one subject (player or team) and one stat.</summary>
public record TrendSeries(
    string SubjectName,
    int SubjectId,
    string Stat,
    string StatLabel,
    int SeasonId,
    int RollingWindow,
    IReadOnlyList<TrendPoint> Points,
    bool IsRate = false)
{
    /// <summary>
    /// For a counting stat this is the season total. For a rate it is the season rate — the
    /// final cumulative figure, already weighted by every game's denominator.
    /// </summary>
    public double Total => Points.Count == 0 ? 0 : Points[^1].Cumulative;

    /// <summary>
    /// Dividing a rate by games played would be meaningless, so a rate reports itself here: it
    /// is already normalised.
    /// </summary>
    public double PerGame => Points.Count == 0 ? 0 : IsRate ? Total : Total / Points.Count;
}

public record PlayerSummary(
    int Id,
    string FullName,
    string Position,
    string? HeadshotUrl,
    string? TeamAbbrev,
    int GamesPlayed,
    int Goals,
    int Assists,
    int Points);

public record GoalieSummary(
    int Id,
    string FullName,
    string? HeadshotUrl,
    string? TeamAbbrev,
    int GamesPlayed,
    int Starts,
    int MinutesPlayed,
    int Saves,
    int ShotsAgainst,
    int GoalsAgainst)
{
    /// <summary>Null rather than zero when no shots were faced, so it never reads as .000.</summary>
    public double? SavePctg => ShotsAgainst > 0 ? (double)Saves / ShotsAgainst : null;

    /// <summary>Goals against per 60 minutes played.</summary>
    public double? GoalsAgainstAverage => MinutesPlayed > 0 ? GoalsAgainst * 60d / MinutesPlayed : null;

    /// <summary>Whether the goalie has played enough to appear on a rate leaderboard.</summary>
    public bool QualifiesForRateTitle => MinutesPlayed >= StatDefinition.RateQualificationMinutes;
}

public record TeamSummary(
    int Id,
    string Abbrev,
    string Name,
    string? LogoUrl,
    int GamesPlayed,
    int Wins,
    int Losses,
    int OvertimeLosses,
    int StandingsPoints);

/// <param name="GameCount">Every stored game, regular season and playoffs together.</param>
/// <param name="RegularSeasonGames">
/// Games counted by the default scope. Reported separately because a leaderboard covering only
/// the regular season should not sit next to a total that silently includes playoff games.
/// </param>
public record SeasonSummary(
    int SeasonId,
    string Label,
    int GameCount,
    int RegularSeasonGames,
    int PlayoffGames,
    DateOnly? FirstGame,
    DateOnly? LastGame);

public record LeaderRow(int Rank, int PlayerId, string FullName, string Position, string? TeamAbbrev, string? HeadshotUrl, int GamesPlayed, double Value);

public record IngestionStatusDto(
    string? LastRunKind,
    DateTimeOffset? LastRunStartedUtc,
    DateTimeOffset? LastRunCompletedUtc,
    string? LastRunStatus,
    string? LastRunError,
    int GamesInDatabase,
    DateOnly? LatestGameDate);
