namespace Blueline.Core.Entities;

/// <summary>An NHL franchise, keyed by the league's own team id.</summary>
public class Team
{
    public int Id { get; set; }
    public string Abbrev { get; set; } = "";
    public string Name { get; set; } = "";
    public string? LogoUrl { get; set; }

    public List<TeamGameStat> GameStats { get; set; } = [];
}
