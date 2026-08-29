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

    private static List<StatsQueryService.GameRow> RateRows(params (double Numerator, double Denominator)[] values) =>
        values.Select((v, i) => new StatsQueryService.GameRow(
            GameId: 2000 + i,
            Date: new DateOnly(2025, 10, 8).AddDays(i),
            IsHome: true,
            Opponent: "OPP",
            Value: v.Numerator,
            RateDenominator: v.Denominator)).ToList();

    [Test]
    public void A_rate_weights_each_game_by_its_denominator_rather_than_averaging()
    {
        // A perfect 10-shot night followed by a poor 40-shot night. Averaging the two game
        // percentages gives .750; the honest combined figure is 30 saves on 50 shots.
        var points = StatsQueryService.BuildPoints(RateRows((10, 10), (20, 40)), rollingWindow: 1);

        Assert.Multiple(() =>
        {
            Assert.That(points[0].Value, Is.EqualTo(1.0));
            Assert.That(points[1].Value, Is.EqualTo(0.5));
            Assert.That(points[1].Cumulative, Is.EqualTo(0.6), "must be 30/50, not the mean of 1.000 and .500");
        });
    }

    [Test]
    public void A_rolling_rate_is_also_denominator_weighted()
    {
        var points = StatsQueryService.BuildPoints(RateRows((10, 10), (20, 40), (9, 10)), rollingWindow: 2);

        Assert.Multiple(() =>
        {
            // Games 1-2: 30 saves on 50 shots.
            Assert.That(points[1].RollingAverage, Is.EqualTo(0.6));
            // Games 2-3: 29 saves on 50 shots.
            Assert.That(points[2].RollingAverage, Is.EqualTo(0.58));
        });
    }

    [Test]
    public void A_game_facing_no_shots_does_not_divide_by_zero_or_distort_the_rate()
    {
        // A goalie pulled in early relief can face nothing at all.
        var points = StatsQueryService.BuildPoints(RateRows((0, 0), (18, 20)), rollingWindow: 1);

        Assert.Multiple(() =>
        {
            Assert.That(points[0].Value, Is.Zero);
            Assert.That(points[0].Cumulative, Is.Zero);
            Assert.That(points[1].Cumulative, Is.EqualTo(0.9), "the empty appearance must not dilute the rate");
        });
    }

    [Test]
    public void Goals_against_average_accumulates_per_sixty_minutes()
    {
        // Two goals in a full 60, then one in 30 minutes: 3 goals across 90 minutes is 2.00.
        var points = StatsQueryService.BuildPoints(RateRows((2 * 60, 60), (1 * 60, 30)), rollingWindow: 1);

        Assert.Multiple(() =>
        {
            Assert.That(points[0].Cumulative, Is.EqualTo(2.0));
            Assert.That(points[1].Value, Is.EqualTo(2.0), "one goal in 30 minutes is a 2.00 pace");
            Assert.That(points[1].Cumulative, Is.EqualTo(2.0));
        });
    }

    [Test]
    public void Counting_stats_are_unaffected_by_the_rate_support()
    {
        // Rows with no denominator must still behave exactly as before.
        var points = StatsQueryService.BuildPoints(Rows(2, 3, 4), rollingWindow: 2);

        Assert.Multiple(() =>
        {
            Assert.That(points.Select(p => p.Cumulative), Is.EqualTo(new[] { 2d, 5d, 9d }));
            Assert.That(points[2].RollingAverage, Is.EqualTo(3.5));
        });
    }

    [TestCase(20252026, "2025-26")]
    [TestCase(20232024, "2023-24")]
    [TestCase(19992000, "1999-00")]
    public void Season_ids_format_as_readable_labels(int seasonId, string expected) =>
        Assert.That(StatsQueryService.FormatSeason(seasonId), Is.EqualTo(expected));
}
