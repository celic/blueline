using Blueline.Data.Queries;

namespace Blueline.Tests;

/// <summary>
/// Covers the fold that turns raw game values into a trend line. This is the one piece of
/// arithmetic the whole site depends on being right.
/// </summary>
public class TrendCalculationTests
{
    private static List<StatsQueryService.GameRow> Rows(params double[] values) =>
        values.Select((v, i) => new StatsQueryService.GameRow(
            GameId: 1000 + i,
            Date: new DateOnly(2025, 10, 8).AddDays(i),
            IsHome: i % 2 == 0,
            Opponent: "OPP",
            Value: v)).ToList();

    [Test]
    public void Cumulative_totals_run_as_a_running_sum()
    {
        var points = StatsQueryService.BuildPoints(Rows(1, 0, 2, 3), rollingWindow: 2);

        Assert.That(points.Select(p => p.Cumulative), Is.EqualTo(new[] { 1d, 1d, 3d, 6d }));
    }

    [Test]
    public void Game_numbers_are_sequential_from_one()
    {
        var points = StatsQueryService.BuildPoints(Rows(1, 1, 1), rollingWindow: 2);

        Assert.That(points.Select(p => p.GameNumber), Is.EqualTo(new[] { 1, 2, 3 }));
    }

    [Test]
    public void Rolling_average_is_null_until_a_full_window_of_games_exists()
    {
        var points = StatsQueryService.BuildPoints(Rows(2, 4, 6, 8), rollingWindow: 3);

        Assert.Multiple(() =>
        {
            Assert.That(points[0].RollingAverage, Is.Null);
            Assert.That(points[1].RollingAverage, Is.Null);
            // (2 + 4 + 6) / 3 and (4 + 6 + 8) / 3
            Assert.That(points[2].RollingAverage, Is.EqualTo(4d));
            Assert.That(points[3].RollingAverage, Is.EqualTo(6d));
        });
    }

    [Test]
    public void Rolling_average_only_looks_backwards()
    {
        // A late burst must not lift the average of earlier games.
        var points = StatsQueryService.BuildPoints(Rows(0, 0, 0, 9), rollingWindow: 2);

        Assert.Multiple(() =>
        {
            Assert.That(points[2].RollingAverage, Is.EqualTo(0d));
            Assert.That(points[3].RollingAverage, Is.EqualTo(4.5d));
        });
    }

    [Test]
    public void Negative_values_accumulate_correctly()
    {
        // Plus/minus is the one stat that can fall as the season goes on.
        var points = StatsQueryService.BuildPoints(Rows(-1, 2, -3), rollingWindow: 1);

        Assert.That(points.Select(p => p.Cumulative), Is.EqualTo(new[] { -1d, 1d, -2d }));
    }

    [Test]
    public void A_window_larger_than_the_season_yields_no_rolling_average()
    {
        var points = StatsQueryService.BuildPoints(Rows(1, 2), rollingWindow: 10);

        Assert.That(points.Select(p => p.RollingAverage), Is.All.Null);
    }

    [Test]
    public void A_window_of_zero_is_treated_as_one_rather_than_dividing_by_zero()
    {
        var points = StatsQueryService.BuildPoints(Rows(3, 5), rollingWindow: 0);

        Assert.That(points.Select(p => p.RollingAverage), Is.EqualTo(new double?[] { 3d, 5d }));
    }

    [Test]
    public void An_empty_season_produces_no_points()
    {
        Assert.That(StatsQueryService.BuildPoints(Rows(), rollingWindow: 5), Is.Empty);
    }

    [Test]
    public void Opponent_and_venue_carry_through_to_each_point()
    {
        var points = StatsQueryService.BuildPoints(Rows(1, 1), rollingWindow: 1);

        Assert.Multiple(() =>
        {
            Assert.That(points[0].IsHome, Is.True);
            Assert.That(points[1].IsHome, Is.False);
            Assert.That(points[0].Opponent, Is.EqualTo("OPP"));
            Assert.That(points[0].Date, Is.EqualTo(new DateOnly(2025, 10, 8)));
        });
    }

    [TestCase(20252026, "2025-26")]
    [TestCase(20232024, "2023-24")]
    [TestCase(19992000, "1999-00")]
    public void Season_ids_format_as_readable_labels(int seasonId, string expected) =>
        Assert.That(StatsQueryService.FormatSeason(seasonId), Is.EqualTo(expected));
}
