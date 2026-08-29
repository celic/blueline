namespace Blueline.Core.Dtos;

/// <summary>Presentation defaults. Each page still lets the reader override them.</summary>
public class DisplayOptions
{
    public const string SectionName = "Display";

    /// <summary>
    /// Which games are counted before the reader picks otherwise.
    ///
    /// Defaults to the regular season, because that is what a published stat line means: a
    /// player's "42 goals" never silently includes playoff goals. Set to <c>All</c> to have the
    /// site count both by default, or <c>Playoffs</c> for a playoff-only view.
    /// </summary>
    public GameScope DefaultGameScope { get; set; } = GameScope.RegularSeason;
}
