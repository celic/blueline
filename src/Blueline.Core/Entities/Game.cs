namespace Blueline.Core.Entities;

public static class GameTypes
{
    public const int Preseason = 1;
    public const int Regular = 2;
    public const int Playoffs = 3;
}

/// <summary>A single game, keyed by the league's own game id (e.g. 2025020001).</summary>
public class Game
{
    public long Id { get; set; }

    /// <summary>Season in the league's format, e.g. 20252026.</summary>
    public int SeasonId { get; set; }
    public int GameType { get; set; }
    public DateOnly GameDate { get; set; }

    public int HomeTeamId { get; set; }
    public Team? HomeTeam { get; set; }
    public int AwayTeamId { get; set; }
    public Team? AwayTeam { get; set; }

    public int HomeScore { get; set; }
    public int AwayScore { get; set; }

    /// <summary>REG, OT or SO — decides whether the loser banks a point.</summary>
    public string LastPeriodType { get; set; } = "REG";

    /// <summary>League game state. Only OFF/FINAL games are treated as complete.</summary>
    public string GameState { get; set; } = "";

    public bool IsFinal => GameState is "OFF" or "FINAL";

    public List<SkaterGameStat> SkaterGameStats { get; set; } = [];
    public List<GoalieGameStat> GoalieGameStats { get; set; } = [];
    public List<TeamGameStat> TeamGameStats { get; set; } = [];
}
