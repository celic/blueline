using System.Text.Json.Serialization;

namespace Blueline.Ingestion.Nhl;

/// <summary>The league returns display strings as an object of locale keys; we only ever want the default.</summary>
public record LocalizedText([property: JsonPropertyName("default")] string Default = "")
{
    public override string ToString() => Default;
}

// --- /v1/standings/{date} ---
public record StandingsResponse(List<StandingsRow> Standings);
public record StandingsRow(
    LocalizedText? TeamAbbrev,
    LocalizedText? TeamName,
    string? TeamLogo,
    string? ConferenceName,
    string? DivisionName);

// --- /v1/club-schedule-season/{team}/{season} ---
public record ClubScheduleResponse(List<ScheduleGame> Games);
public record ScheduleGame(
    long Id,
    int Season,
    int GameType,
    string GameDate,
    string GameState,
    ScheduleTeam? AwayTeam,
    ScheduleTeam? HomeTeam);
public record ScheduleTeam(int Id, LocalizedText? Abbrev);

// --- /v1/score/{date} ---
public record ScoreResponse(string? CurrentDate, List<ScheduleGame> Games);

// --- /v1/gamecenter/{gameId}/boxscore ---
public record BoxscoreResponse(
    long Id,
    int Season,
    int GameType,
    string GameDate,
    string GameState,
    BoxscoreTeam? AwayTeam,
    BoxscoreTeam? HomeTeam,
    GameOutcome? GameOutcome,
    PlayerByGameStats? PlayerByGameStats);

public record BoxscoreTeam(int Id, LocalizedText? CommonName, LocalizedText? Abbrev, LocalizedText? PlaceName, int Score, int Sog, string? Logo);
public record GameOutcome(string? LastPeriodType);
public record PlayerByGameStats(TeamPlayers? AwayTeam, TeamPlayers? HomeTeam);
public record TeamPlayers(List<BoxSkater> Forwards, List<BoxSkater> Defense, List<BoxGoalie> Goalies)
{
    public IEnumerable<BoxSkater> AllSkaters => (Forwards ?? []).Concat(Defense ?? []);
}

public record BoxSkater(
    int PlayerId,
    LocalizedText? Name,
    string? Position,
    int Goals,
    int Assists,
    int Points,
    int PlusMinus,
    int Pim,
    int Hits,
    int BlockedShots,
    int Sog,
    int PowerPlayGoals,
    int Giveaways,
    int Takeaways,
    int Shifts,
    string? Toi,
    double? FaceoffWinningPctg);

public record BoxGoalie(
    int PlayerId,
    LocalizedText? Name,
    int ShotsAgainst,
    int Saves,
    int GoalsAgainst,
    int Pim,
    string? Toi,
    bool Starter);

// --- /v1/club-stats/{team}/{season}/{gameType} ---
public record ClubStatsResponse(List<ClubStatsPlayer> Skaters, List<ClubStatsPlayer> Goalies);
public record ClubStatsPlayer(
    int PlayerId,
    LocalizedText? FirstName,
    LocalizedText? LastName,
    string? PositionCode,
    string? Headshot);
