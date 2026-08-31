using Blueline.Web.Components.Shared;

namespace Blueline.Tests;

/// <summary>
/// Club colours, and the two things that make them safe: they are legible on the surface they are
/// drawn on, and two clubs that wear the same red never end up on one chart in it.
/// </summary>
public class TeamColourTests
{
    /// <summary>The card a chart sits on. Everything drawn on it is judged against this.</summary>
    private const string Surface = "#111c30";

    /// <summary>
    /// WCAG's floor for a graphical object — a chart line is not text, and 4.5:1 would rule out
    /// most of the league's identities for no gain in a chart that also distinguishes by shape.
    /// </summary>
    private const double MinimumContrast = 3.0;

    [Test]
    public void Every_club_in_the_league_has_a_colour() =>
        Assert.That(TeamColours.Count, Is.EqualTo(32));

    [Test]
    public void Every_colour_is_legible_on_the_surface_it_is_drawn_on()
    {
        Assert.Multiple(() =>
        {
            foreach (var abbrev in TeamColours.Abbrevs)
            {
                var contrast = Contrast(TeamColours.For(abbrev)!, Surface);
                Assert.That(contrast, Is.GreaterThanOrEqualTo(MinimumContrast),
                    $"{abbrev} is {contrast:F2}:1 against the card, which is not enough to see");
            }
        });
    }

    [Test]
    public void An_unknown_club_has_no_colour_rather_than_a_wrong_one()
    {
        // A relocation or an expansion team arrives in the data long before it arrives here.
        Assert.Multiple(() =>
        {
            Assert.That(TeamColours.For("ZZZ"), Is.Null);
            Assert.That(TeamColours.For(null), Is.Null);
            Assert.That(TeamColours.Pick("ZZZ", [], "#ffffff"), Is.EqualTo("#ffffff"));
        });
    }

    [Test]
    public void A_club_gets_its_own_colour_when_the_chart_is_clear()
    {
        Assert.That(TeamColours.Pick("EDM", [], "#ffffff"), Is.EqualTo(TeamColours.For("EDM")));
    }

    [Test]
    public void The_second_club_wearing_the_same_red_falls_back()
    {
        // Detroit and New Jersey are the same red to any eye. On one chart the second takes the
        // palette instead, because two indistinguishable lines are worse than one unfamiliar one.
        var detroit = TeamColours.For("DET")!;

        Assert.Multiple(() =>
        {
            Assert.That(TeamColours.Distance(detroit, TeamColours.For("NJD")!), Is.LessThan(60),
                "the premise: these two really are nearly the same colour");
            Assert.That(TeamColours.Pick("NJD", [detroit], "#ffffff"), Is.EqualTo("#ffffff"));
        });
    }

    [Test]
    public void A_club_that_looks_nothing_like_what_is_already_there_keeps_its_colour()
    {
        Assert.That(
            TeamColours.Pick("EDM", [TeamColours.For("TOR")!], "#ffffff"),
            Is.EqualTo(TeamColours.For("EDM")),
            "orange against blue needs no help");
    }

    [Test]
    public void Abbreviations_are_matched_regardless_of_case() =>
        Assert.That(TeamColours.For("edm"), Is.EqualTo(TeamColours.For("EDM")));

    private static double Contrast(string first, string second)
    {
        var a = Luminance(first);
        var b = Luminance(second);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private static double Luminance(string hex)
    {
        var (r, g, b) = TeamColours.Parse(hex);
        return 0.2126 * Channel(r) + 0.7152 * Channel(g) + 0.0722 * Channel(b);

        static double Channel(int value)
        {
            var v = value / 255d;
            return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }
    }
}
