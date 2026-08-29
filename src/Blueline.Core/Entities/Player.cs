namespace Blueline.Core.Entities;

/// <summary>A skater or goalie, keyed by the league's own player id.</summary>
public class Player
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";

    /// <summary>Position code as reported by the league: C, L, R, D or G.</summary>
    public string Position { get; set; } = "";
    public string? HeadshotUrl { get; set; }

    public bool IsGoalie => Position == "G";
    public string FullName => $"{FirstName} {LastName}".Trim();

    public List<SkaterGameStat> SkaterGameStats { get; set; } = [];
    public List<GoalieGameStat> GoalieGameStats { get; set; } = [];
}
