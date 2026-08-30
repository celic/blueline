namespace Blueline.Core.Dtos;

/// <summary>Presentation defaults. Each page still lets the reader override them.</summary>
public class DisplayOptions
{
    public const string SectionName = "Display";

    /// <summary>
    /// Which games are counted before the reader picks otherwise.
    ///
    /// Defaults to the regular season, because that is what a published stat line means: a
    /// player's "42 goals" never silently includes playoff goals. Set to <c>Playoffs</c> for a
    /// playoff-only view. There is no combined setting — see <see cref="GameScope"/> for why.
    /// </summary>
    public GameScope DefaultGameScope { get; set; } = GameScope.RegularSeason;
}
