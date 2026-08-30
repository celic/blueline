using Blueline.Core.Dtos;
using Blueline.Web.Components.Shared;

namespace Blueline.Tests;

/// <summary>
/// The two axes need different data shapes, and getting it wrong is invisible until a chart
/// silently misaligns two players.
/// </summary>
public class TrendDatasetTests
{
    private static List<TrendPoint> Points(params (int Day, double Value)[] entries) =>
        entries.Select((e, i) => new TrendPoint(
            i + 1, 1000 + i, new DateOnly(2025, 10, 8).AddDays(e.Day), "OPP", true,
            e.Value, e.Value, e.Value)).ToList();

    [Test]
    public void A_category_series_is_padded_so_shorter_seasons_stay_aligned()
    {
        var dataset = TrendDatasets.From(
            "Short season", Points((0, 1), (1, 2)), p => p.Cumulative, "#fff", timeAxis: false, categoryLength: 5);

        Assert.Multiple(() =>
        {
            Assert.That(dataset.Data, Has.Count.EqualTo(5));
            Assert.That(dataset.Data.Skip(2), Is.All.Null, "the tail is padding, not data");
            Assert.That(dataset.Dates, Is.Null, "a category axis aligns by index, not by date");
        });
    }

    [Test]
    public void A_time_series_carries_its_own_dates_and_is_never_padded()
    {
        var dataset = TrendDatasets.From(
            "Short season", Points((0, 1), (1, 2)), p => p.Cumulative, "#fff", timeAxis: true, categoryLength: 5);

        Assert.Multiple(() =>
        {
            Assert.That(dataset.Data, Has.Count.EqualTo(2), "padding a time axis would invent points with no date");
            Assert.That(dataset.Dates, Is.EqualTo(new[] { "2025-10-08", "2025-10-09" }));
        });
    }

    [Test]
    public void A_gap_between_appearances_survives_into_the_dates()
    {
        // Six weeks out injured. On a category axis these are adjacent; the dates are what let
        // the chart show the layoff for what it was.
        var dataset = TrendDatasets.From(
            "Injured", Points((0, 1), (42, 2)), p => p.Cumulative, "#fff", timeAxis: true, categoryLength: 2);

        Assert.That(dataset.Dates, Is.EqualTo(new[] { "2025-10-08", "2025-11-19" }));
    }

    [Test]
    public void Dates_are_written_in_a_form_the_chart_adapter_can_parse()
    {
        var dataset = TrendDatasets.From(
            "Any", Points((0, 1)), p => p.Value, "#fff", timeAxis: true, categoryLength: 1);

        Assert.That(dataset.Dates![0], Does.Match(@"^\d{4}-\d{2}-\d{2}$"));
    }

    [Test]
    public void Nulls_from_a_rolling_average_are_preserved_rather_than_dropped()
    {
        // The first games of a season have no rolling average yet; the gap is meaningful.
        var points = Points((0, 1), (1, 2), (2, 3));
        var dataset = TrendDatasets.From(
            "Rolling", points, _ => null, "#fff", timeAxis: true, categoryLength: 3);

        Assert.Multiple(() =>
        {
            Assert.That(dataset.Data, Has.Count.EqualTo(3));
            Assert.That(dataset.Data, Is.All.Null);
            Assert.That(dataset.Dates, Has.Count.EqualTo(3), "a gap still needs its x position");
        });
    }

    [Test]
    public void The_kind_and_fill_flags_pass_through_on_both_axes()
    {
        var bar = TrendDatasets.From(
            "Bars", Points((0, 1)), p => p.Value, "#fff", timeAxis: true, categoryLength: 1, kind: "bar");
        var filled = TrendDatasets.From(
            "Filled", Points((0, 1)), p => p.Value, "#fff", timeAxis: false, categoryLength: 1, fill: true);

        Assert.Multiple(() =>
        {
            Assert.That(bar.Kind, Is.EqualTo("bar"));
            Assert.That(filled.Fill, Is.True);
        });
    }
}
