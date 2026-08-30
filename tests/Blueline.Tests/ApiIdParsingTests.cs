using Blueline.Web.Api;
using Blueline.Web.Components.Shared;

namespace Blueline.Tests;

/// <summary>The id list the comparison endpoints accept.</summary>
public class ApiIdParsingTests
{
    [Test]
    public void A_comma_separated_list_is_parsed_in_order() =>
        Assert.That(StatsEndpoints.ParseIds("1,2,3"), Is.EqualTo(new[] { 1, 2, 3 }));

    [Test]
    public void Surrounding_whitespace_is_tolerated() =>
        Assert.That(StatsEndpoints.ParseIds(" 1 , 2 "), Is.EqualTo(new[] { 1, 2 }));

    [Test]
    public void Duplicates_collapse_so_one_subject_is_not_drawn_twice() =>
        Assert.That(StatsEndpoints.ParseIds("7,7,8"), Is.EqualTo(new[] { 7, 8 }));

    [Test]
    public void Unparseable_entries_are_dropped_rather_than_failing_the_whole_request()
    {
        // A caller with one bad id still gets the subjects they asked for.
        Assert.That(StatsEndpoints.ParseIds("1,banana,3"), Is.EqualTo(new[] { 1, 3 }));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase(",,,")]
    [TestCase("banana")]
    public void Nothing_usable_yields_an_empty_list(string? ids) =>
        Assert.That(StatsEndpoints.ParseIds(ids), Is.Empty);

    [Test]
    public void The_list_is_capped_at_what_a_chart_can_carry()
    {
        var many = string.Join(",", Enumerable.Range(1, ChartPalette.MaxSeries + 5));

        Assert.That(StatsEndpoints.ParseIds(many), Has.Count.EqualTo(ChartPalette.MaxSeries));
    }
}
