using Blueline.Core.Dtos;
using Blueline.Data.Queries;

namespace Blueline.Tests;

/// <summary>
/// Windows counted in days rather than games.
///
/// The two are not interchangeable, and the case that shows why is a layoff: ten games spanning
/// five weeks and ten games spanning two are the same window by one measure and nothing like each
/// other by the other. A goalie makes the point more sharply still — a fortnight is four starts
/// for one and eight for another, so only a calendar window compares them on equal terms.
/// </summary>
public class RollingWindowTests
{
    /// <summary>Rows on given dates, so a window can be tested against gaps rather than a tidy run.</summary>
    private static List<StatsQueryService.GameRow> RowsOn(params (int DayOffset, double Value)[] games) =>
        games.Select((g, i) => new StatsQueryService.GameRow(
            GameId: 1000 + i,
            Date: new DateOnly(2025, 10, 8).AddDays(g.DayOffset),
            IsHome: i % 2 == 0,
            Opponent: "OPP",
            Value: g.Value)).ToList();

    [TestCase("10", 10, WindowUnit.Games, TestName = "Parse_a_bare_number_as_games")]
    [TestCase("15g", 15, WindowUnit.Games, TestName = "Parse_an_explicit_games_token")]
    [TestCase("14d", 14, WindowUnit.Days, TestName = "Parse_a_days_token")]
    [TestCase("14D", 14, WindowUnit.Days, TestName = "Parse_is_case_insensitive")]
    public void Parse_reads_both_units(string input, int size, WindowUnit unit)
    {
        var window = RollingWindow.Parse(input);

        Assert.Multiple(() =>
        {
            Assert.That(window.Size, Is.EqualTo(size));
            Assert.That(window.Unit, Is.EqualTo(unit));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("nonsense")]
    [TestCase("0")]
    [TestCase("-5")]
    [TestCase("d")]
    public void Parse_falls_back_to_the_default_rather_than_throwing(string? input) =>
        Assert.That(RollingWindow.Parse(input), Is.EqualTo(RollingWindow.Default),
            "a stale bookmark should still render something sensible");

    [Test]
    public void Sizes_are_clamped_to_what_a_season_can_support()
    {
        Assert.Multiple(() =>
        {
            Assert.That(RollingWindow.Games(500).Size, Is.EqualTo(RollingWindow.MaxGames));
            Assert.That(RollingWindow.Days(500).Size, Is.EqualTo(RollingWindow.MaxDays));
            Assert.That(RollingWindow.Parse("999d").Size, Is.EqualTo(RollingWindow.MaxDays));
        });
    }

    [Test]
    public void A_bare_int_still_means_games()
    {
        // Every call site predating days passes a number, and must keep meaning what it meant.
        RollingWindow window = 10;

        Assert.That(window, Is.EqualTo(RollingWindow.Games(10)));
    }

    [Test]
    public void Tokens_round_trip_through_parse()
    {
        Assert.Multiple(() =>
        {
            foreach (var window in RollingWindow.Choices)
                Assert.That(RollingWindow.Parse(window.Token), Is.EqualTo(window), window.Token);
        });
    }

    [Test]
    public void A_days_window_counts_the_games_that_fell_inside_it()
    {
        // An opening game, then three inside one week, then one after a fortnight off.
        var rows = RowsOn((0, 0), (8, 2), (10, 4), (12, 6), (26, 10));

        var points = StatsQueryService.BuildPoints(rows, RollingWindow.Days(7));

        Assert.Multiple(() =>
        {
            Assert.That(points[3].RollingAverage, Is.EqualTo(4.0),
                "days 8, 10 and 12 sit inside the week ending on day 12: (2+4+6)/3");
            Assert.That(points[4].RollingAverage, Is.EqualTo(10.0),
                "the week ending on day 26 contains that game alone, so the layoff is not averaged away");
        });
    }

    [Test]
    public void The_same_layoff_leaves_a_games_window_unaware_of_it()
    {
        var rows = RowsOn((0, 2), (2, 4), (4, 6), (18, 10));

        var byGames = StatsQueryService.BuildPoints(rows, RollingWindow.Games(3));
        var byDays = StatsQueryService.BuildPoints(rows, RollingWindow.Days(7));

        Assert.Multiple(() =>
        {
            Assert.That(byGames[3].RollingAverage, Is.EqualTo(20d / 3).Within(0.001),
                "three games back reaches over a fortnight's absence as though it were not there");
            Assert.That(byDays[3].RollingAverage, Is.EqualTo(10.0),
                "seven days back does not");
        });
    }

    [Test]
    public void A_days_window_reports_nothing_until_the_season_spans_it()
    {
        var rows = RowsOn((0, 2), (3, 4), (9, 6), (11, 8));

        var points = StatsQueryService.BuildPoints(rows, RollingWindow.Days(7));

        Assert.Multiple(() =>
        {
            Assert.That(points[0].RollingAverage, Is.Null,
                "one game is not a week of form, however it is labelled");
            Assert.That(points[1].RollingAverage, Is.Null, "day 3 is still short of a full week");
            Assert.That(points[2].RollingAverage, Is.EqualTo(5.0),
                "day 9 looks back to day 3: (4+6)/2");
        });
    }

    [Test]
    public void A_days_window_totals_what_the_window_holds_rather_than_scaling_the_average()
    {
        var rows = RowsOn((0, 0), (8, 2), (10, 4), (12, 6), (26, 10));

        var points = StatsQueryService.BuildPoints(rows, RollingWindow.Days(7));

        Assert.Multiple(() =>
        {
            Assert.That(points[3].RollingTotal, Is.EqualTo(12.0), "2 + 4 + 6 across the week");
            Assert.That(points[4].RollingTotal, Is.EqualTo(10.0),
                "the average times seven days would claim 70, a figure nobody scored");
        });
    }

    [Test]
    public void A_games_window_totals_its_games()
    {
        var rows = RowsOn((0, 2), (1, 4), (2, 6));

        var points = StatsQueryService.BuildPoints(rows, RollingWindow.Games(2));

        Assert.Multiple(() =>
        {
            Assert.That(points[0].RollingTotal, Is.Null, "the window is not full yet");
            Assert.That(points[1].RollingTotal, Is.EqualTo(6.0));
            Assert.That(points[2].RollingTotal, Is.EqualTo(10.0));
        });
    }

    [Test]
    public void Rates_carry_no_rolling_total()
    {
        // 20 saves on 20 shots, then 10 on 20: adding percentages would produce a meaningless 1.5.
        // Six days apart, so a seven-day window ending on the second reaches the first exactly —
        // the boundary is inclusive.
        var rows = new List<StatsQueryService.GameRow>
        {
            new(1000, new DateOnly(2025, 10, 8), true, "OPP", 20, 20),
            new(1001, new DateOnly(2025, 10, 14), false, "OPP", 10, 20),
        };

        var points = StatsQueryService.BuildPoints(rows, RollingWindow.Days(7));

        Assert.Multiple(() =>
        {
            Assert.That(points[1].RollingTotal, Is.Null);
            Assert.That(points[1].RollingAverage, Is.EqualTo(0.75),
                "30 saves on 40 shots, weighted by shots rather than averaged");
        });
    }

    [Test]
    public void Games_on_the_same_day_all_belong_to_the_window()
    {
        // Two subjects can share a date on a team page comparison, and a window boundary landing
        // between them would drop one silently.
        var rows = RowsOn((0, 1), (7, 2), (7, 4));

        var points = StatsQueryService.BuildPoints(rows, RollingWindow.Days(7));

        Assert.That(points[2].RollingAverage, Is.EqualTo(3.0), "both day-7 games, and not day 0");
    }
}
