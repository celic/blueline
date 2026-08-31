namespace Blueline.Web.Components.Shared;

/// <summary>
/// A club's colour, for charts that plot that club or a player who plays for it.
///
/// Keyed on the abbreviation rather than the team id, deliberately: the league reissues ids — Utah
/// went from 59 to 68 on its rebrand while keeping "UTA" — so the abbreviation is the stable key
/// for a label, which is exactly what a colour is.
///
/// **These are brand colours adapted to a dark background, not brand colours.** Several clubs are
/// primarily black or navy, which is invisible here, so each entry is the colour that identifies
/// the club while still reading on the surface it is drawn on. Every one clears 3:1 against the
/// card, the contrast floor for a graphical object; `TeamColourTests` fails if a future edit drops
/// one below it.
///
/// Colour is never the only thing distinguishing a series — each carries its own marker shape as
/// well — which is what makes it safe to hand two clubs colours as close as Detroit's and New
/// Jersey's.
/// </summary>
public static class TeamColours
{
    private static readonly Dictionary<string, string> ByAbbrev = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ANA"] = "#F47A38", // orange
        ["BOS"] = "#FFB81C", // gold, not black
        ["BUF"] = "#2E86DE",
        ["CAR"] = "#E03A4C",
        ["CBJ"] = "#2A6EBB",
        ["CGY"] = "#E04A5F",
        ["CHI"] = "#E0424E",
        ["COL"] = "#B84361", // burgundy, lightened to clear the contrast floor
        ["DAL"] = "#00A159",
        ["DET"] = "#E03A3E",
        ["EDM"] = "#FF6A13",
        ["FLA"] = "#E0454A",
        ["LAK"] = "#A2AAAD", // silver, not black
        ["MIN"] = "#1F9E67",
        ["MTL"] = "#D62839",
        ["NJD"] = "#E23B4A",
        ["NSH"] = "#F2C230",
        ["NYI"] = "#F4783C",
        ["NYR"] = "#3D7BE8",
        ["OTT"] = "#E04452",
        ["PHI"] = "#F74902",
        ["PIT"] = "#FCB514", // gold, not black
        ["SEA"] = "#6FD3D8",
        ["SJS"] = "#00A0AA",
        ["STL"] = "#2B7CE0",
        ["TBL"] = "#5B9BF5",
        ["TOR"] = "#3E86F5",
        ["UTA"] = "#71AFE5",
        ["VAN"] = "#00A651",
        ["VGK"] = "#C9A227", // gold, not steel grey
        ["WPG"] = "#6E9BD8",
        ["WSH"] = "#E0303F",
    };

    /// <summary>How close two colours may be before they stop being tellable apart on one chart.</summary>
    private const double MinimumSeparation = 60;

    public static int Count => ByAbbrev.Count;

    public static IReadOnlyCollection<string> Abbrevs => ByAbbrev.Keys;

    /// <summary>The club's colour, or null for an abbreviation this build does not know.</summary>
    public static string? For(string? abbrev) =>
        abbrev is not null && ByAbbrev.TryGetValue(abbrev, out var colour) ? colour : null;

    /// <summary>
    /// The colour to draw a series in: the club's own, unless it is too close to one already on the
    /// chart, in which case the fallback.
    ///
    /// Half the league wears red. Two clubs an eye could not separate would make a comparison
    /// harder to read than the palette it replaced, and the point of using club colours is
    /// recognition, not decoration — so the second red gets a palette colour and stays distinct.
    /// </summary>
    public static string Pick(string? abbrev, IEnumerable<string> alreadyUsed, string fallback)
    {
        var colour = For(abbrev);
        if (colour is null) return fallback;

        return alreadyUsed.Any(used => Distance(used, colour) < MinimumSeparation) ? fallback : colour;
    }

    /// <summary>
    /// Straight-line distance in RGB. Crude next to a perceptual space, and enough for the question
    /// being asked — whether two colours are nearly the same, not how different they are.
    /// </summary>
    internal static double Distance(string first, string second)
    {
        var (r1, g1, b1) = Parse(first);
        var (r2, g2, b2) = Parse(second);

        return Math.Sqrt(Math.Pow(r1 - r2, 2) + Math.Pow(g1 - g2, 2) + Math.Pow(b1 - b2, 2));
    }

    internal static (int R, int G, int B) Parse(string hex)
    {
        var value = hex.TrimStart('#');
        return (
            Convert.ToInt32(value[..2], 16),
            Convert.ToInt32(value.Substring(2, 2), 16),
            Convert.ToInt32(value.Substring(4, 2), 16));
    }
}
