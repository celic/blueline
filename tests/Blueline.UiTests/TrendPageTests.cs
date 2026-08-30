using Microsoft.Playwright;

namespace Blueline.UiTests;

/// <summary>
/// The interaction wiring on a trend page: controls re-query and repaint, and the chart is
/// actually built. Chart arithmetic is covered properly by the unit suite; what cannot be
/// covered there is whether any of this reaches the browser at all.
/// </summary>
[Parallelizable(ParallelScope.Self)]
public class TrendPageTests : BluelinePageTest
{
    private static string PlayerUrl => $"/players/{BluelineAppFixture.Seed.TopScorerId}";

    [Test]
    public async Task A_player_page_draws_a_chart_of_their_season()
    {
        await GoToAsync(PlayerUrl);
        await WaitForChartAsync();

        var points = await ReadChartAsync<int>("chart.data.datasets[0].data.length");

        Assert.That(points, Is.EqualTo(BluelineAppFixture.Seed.GameCount));
        AssertNoConsoleErrors();
    }

    [Test]
    public async Task The_summary_tiles_show_the_seeded_totals()
    {
        await GoToAsync(PlayerUrl);

        // Three points a game across ten games.
        await Expect(Page.Locator(".stat-row .value").First).ToHaveTextAsync("30");
        AssertNoConsoleErrors();
    }

    [Test]
    public async Task Changing_the_stat_re_queries_and_repaints()
    {
        await GoToAsync(PlayerUrl);
        await WaitForChartAsync();

        await Page.Locator("#stat").SelectOptionAsync("hits");

        // One hit a game rather than three points.
        await Expect(Page.Locator(".stat-row .value").First).ToHaveTextAsync("10");
        Assert.That(await ReadChartAsync<double>("chart.data.datasets[0].data.at(-1)"), Is.EqualTo(10));
        AssertNoConsoleErrors();
    }

    [Test]
    public async Task Switching_to_per_game_adds_the_rolling_average_series()
    {
        await GoToAsync(PlayerUrl);
        await WaitForChartAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Per game", Exact = true }).ClickAsync();

        await Expect(Page.Locator("#window")).ToBeVisibleAsync();
        await Page.WaitForFunctionAsync(
            "() => Object.values(Chart.instances)[0].data.datasets.length === 2");

        var labels = await ReadChartAsync<string[]>("chart.data.datasets.map(d => d.label)");
        Assert.That(labels, Does.Contain("Per game"));
        AssertNoConsoleErrors();
    }

    [Test]
    public async Task Switching_the_x_axis_to_dates_rebuilds_it_as_a_time_scale()
    {
        await GoToAsync(PlayerUrl);
        await WaitForChartAsync();

        Assert.That(await ReadChartAsync<string>("chart.scales.x.type"), Is.EqualTo("category"));

        await Page.GetByRole(AriaRole.Button, new() { Name = "Date", Exact = true }).ClickAsync();
        await Page.WaitForFunctionAsync("() => Object.values(Chart.instances)[0].scales.x.type === 'time'");

        // The seeded season deliberately contains an uneven gap, so a genuine time scale must
        // span the real calendar range rather than collapsing onto the game numbers.
        var span = await ReadChartAsync<double>("chart.scales.x.max - chart.scales.x.min");
        Assert.That(span, Is.GreaterThan(TimeSpan.FromDays(30).TotalMilliseconds),
            "a time scale should cover the season's actual dates");
        AssertNoConsoleErrors();
    }

    [Test]
    public async Task The_games_filter_changes_what_is_counted()
    {
        await GoToAsync(PlayerUrl);
        await WaitForChartAsync();

        // The seed holds regular-season games only, so a playoff view has nothing to show.
        await Page.Locator("select").Filter(new() { HasText = "Regular season" }).First
            .SelectOptionAsync("Playoffs");

        await Expect(Page.Locator(".empty")).ToBeVisibleAsync();
        AssertNoConsoleErrors();
    }

    [Test]
    public async Task A_team_page_draws_its_points_pace()
    {
        await GoToAsync($"/teams/{BluelineAppFixture.Seed.HomeTeamId}");
        await WaitForChartAsync();

        // Five wins across ten games, two points each.
        Assert.That(await ReadChartAsync<double>("chart.data.datasets[0].data.at(-1)"), Is.EqualTo(10));
        AssertNoConsoleErrors();
    }

    [Test]
    public async Task A_goalie_page_charts_a_shot_weighted_save_percentage()
    {
        await GoToAsync($"/goalies/{BluelineAppFixture.Seed.GoalieId}");
        await WaitForChartAsync();

        // 28 saves on 30 shots every night.
        var final = await ReadChartAsync<double>("chart.data.datasets[0].data.at(-1)");
        Assert.That(final, Is.EqualTo(28d / 30).Within(0.0005));
        AssertNoConsoleErrors();
    }
}
