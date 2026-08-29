namespace Blueline.Core.Entities;

/// <summary>One skater's line from one game. The atomic unit every trend is built from.</summary>
public class SkaterGameStat
{
    public long GameId { get; set; }
    public Game? Game { get; set; }
    public int PlayerId { get; set; }
    public Player? Player { get; set; }
    public int TeamId { get; set; }
    public Team? Team { get; set; }

    public int Goals { get; set; }
    public int Assists { get; set; }
    public int Points { get; set; }
    public int PlusMinus { get; set; }
    public int Pim { get; set; }
    public int Hits { get; set; }
    public int BlockedShots { get; set; }
    public int Shots { get; set; }
    public int PowerPlayGoals { get; set; }
    public int Giveaways { get; set; }
    public int Takeaways { get; set; }
    public int Shifts { get; set; }
    public int TimeOnIceSeconds { get; set; }
    public double? FaceoffWinPctg { get; set; }
}
