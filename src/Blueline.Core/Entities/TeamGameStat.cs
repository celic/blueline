namespace Blueline.Core.Entities;

/// <summary>One team's result from one game, derived from the game record at ingestion time.</summary>
public class TeamGameStat
{
    public long GameId { get; set; }
    public Game? Game { get; set; }
    public int TeamId { get; set; }
    public Team? Team { get; set; }
    public int OpponentTeamId { get; set; }

    public bool IsHome { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }

    /// <summary>W, L or OTL.</summary>
    public string Result { get; set; } = "";

    /// <summary>Standings points earned: 2 for a win, 1 for an overtime/shootout loss.</summary>
    public int Points { get; set; }
}
