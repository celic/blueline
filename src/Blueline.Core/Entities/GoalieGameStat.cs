namespace Blueline.Core.Entities;

/// <summary>One goalie's line from one game.</summary>
public class GoalieGameStat
{
    public long GameId { get; set; }
    public Game? Game { get; set; }
    public int PlayerId { get; set; }
    public Player? Player { get; set; }
    public int TeamId { get; set; }
    public Team? Team { get; set; }

    public bool Starter { get; set; }
    public int ShotsAgainst { get; set; }
    public int Saves { get; set; }
    public int GoalsAgainst { get; set; }
    public int Pim { get; set; }
    public int TimeOnIceSeconds { get; set; }

    /// <summary>Null rather than zero when the goalie faced no shots, so averages stay honest.</summary>
    public double? SavePctg => ShotsAgainst > 0 ? (double)Saves / ShotsAgainst : null;
}
