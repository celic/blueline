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
    IReadOnlyList<TrendPoint> Points)
{
    public double Total => Points.Count == 0 ? 0 : Points[^1].Cumulative;
    public double PerGame => Points.Count == 0 ? 0 : Total / Points.Count;
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

public record SeasonSummary(int SeasonId, string Label, int GameCount, DateOnly? FirstGame, DateOnly? LastGame);

public record LeaderRow(int Rank, int PlayerId, string FullName, string Position, string? TeamAbbrev, string? HeadshotUrl, int GamesPlayed, double Value);

public record IngestionStatusDto(
    string? LastRunKind,
    DateTimeOffset? LastRunStartedUtc,
    DateTimeOffset? LastRunCompletedUtc,
    string? LastRunStatus,
    string? LastRunError,
    int GamesInDatabase,
    DateOnly? LatestGameDate);
