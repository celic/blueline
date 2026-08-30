using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Blueline.UiTests;

/// <summary>
/// The landing page: panels of runs, each linking through to the trend behind it.
/// </summary>
[Parallelizable(ParallelScope.Self)]
public class DashboardTests : BluelinePageTest
{
    [Test]
    public async Task The_dashboard_shows_a_panel_for_each_board()
    {
        await GoToAsync("/");

        await Expect(Page.Locator(".panel")).ToHaveCountAsync(5);
        await Expect(Page.Locator(".panel-head h2").First).ToHaveTextAsync("Points");
        AssertNoConsoleErrors();
    }

    [Test]
    public async Task A_run_is_drawn_as_well_as_counted()
    {
        await GoToAsync("/");

        var row = Page.Locator(".streak-list li").First;

        await Expect(row.Locator(".streak-name")).ToHaveTextAsync(BluelineAppFixture.Seed.TopScorerName);
        await Expect(row.Locator(".streak-detail")).ToContainTextAsync("30 in 10 games");

        // Counted rather than asserted visible. The seeded player scores three every night, so
        // the line is perfectly flat — a zero-height box that Playwright rightly calls invisible,
        // even though it is on screen and correct. The point count is the real claim anyway: one
        // vertex per game in the window.
        var points = await row.Locator("svg.sparkline polyline").GetAttributeAsync("points");
        Assert.That(points!.Split(' '), Has.Length.EqualTo(BluelineAppFixture.Seed.GameCount));
        AssertNoConsoleErrors();
    }

    [Test]
    public async Task A_panel_nobody_qualifies_for_says_so_rather_than_showing_nothing()
    {
        // The seeded season is ten games long, so a twenty-game window cannot be filled. An empty
        // panel is a normal answer — in a quiet week most of them are — and has to read as one.
        await GoToAsync("/");

        var goals = Page.Locator(".panel").Filter(new() { Has = Page.GetByText("Goals", new() { Exact = true }) });

        await Expect(goals.Locator(".panel-empty")).ToBeVisibleAsync();
        AssertNoConsoleErrors();
    }

    [Test]
    public async Task A_row_links_through_to_the_trend_that_produced_it()
    {
        await GoToAsync("/");

        await Page.Locator(".streak-list li a").First.ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex($"/players/{BluelineAppFixture.Seed.TopScorerId}"));
        await Expect(Page.Locator("h1")).ToHaveTextAsync(BluelineAppFixture.Seed.TopScorerName);
        AssertNoConsoleErrors();
    }

    [Test]
    public async Task The_window_travels_with_the_link()
    {
        // Landing on a ten-game average after clicking a fourteen-day run would show different
        // numbers from the ones that were clicked.
        await GoToAsync($"/goalies/{BluelineAppFixture.Seed.GoalieId}?window=14d");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Per game", Exact = true }).ClickAsync();

        await Expect(Page.Locator("#window")).ToHaveValueAsync("14d");
        await WaitForChartAsync("chart.data.datasets.some(d => d.label === '14-day average')");
        AssertNoConsoleErrors();
    }

    [Test]
    public async Task The_page_says_what_day_its_windows_end_on()
    {
        // Not today: in the off-season these are the closing weeks of the last season played, and
        // the page has to say so rather than presenting them as current form.
        await GoToAsync("/");

        await Expect(Page.GetByText("Windows end on")).ToBeVisibleAsync();
        AssertNoConsoleErrors();
    }
}
