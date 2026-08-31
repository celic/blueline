using Microsoft.Playwright;

namespace Blueline.UiTests;

/// <summary>
/// The affordances that are invisible when they work and unnoticed when they break: a skip link,
/// pressed state on the toggles, a chart that is more than a picture, and series told apart by
/// something other than colour.
/// </summary>
[Parallelizable(ParallelScope.Self)]
public class AccessibilityTests : BluelinePageTest
{
    private static string PlayerUrl => $"/players/{BluelineAppFixture.Seed.TopScorerId}";

    [Test]
    public async Task The_first_stop_on_the_keyboard_is_a_way_past_the_navigation()
    {
        await GoToAsync("/");

        // Asserted through the DOM rather than by pressing Tab: where the browser puts focus on a
        // freshly loaded page is its own business, and a test that depends on it is testing
        // Chromium. What matters is that the link comes first and appears when focused.
        var firstFocusable = await Page.EvaluateAsync<string>(
            "() => document.querySelector('a, button, input, select, [tabindex]:not([tabindex=\"-1\"])').className");
        Assert.That(firstFocusable, Does.Contain("skip-link"));

        var skip = Page.Locator(".skip-link");
        await Expect(skip).ToHaveAttributeAsync("href", "#content");

        var hiddenTop = await skip.EvaluateAsync<double>("el => el.getBoundingClientRect().top");
        Assert.That(hiddenTop, Is.LessThan(0), "out of the way until it is wanted");

        await skip.FocusAsync();

        // Polled rather than measured once: the link slides down over 150ms, so reading its box
        // the instant focus lands catches it mid-transition, still above the top of the page.
        await Page.WaitForFunctionAsync(
            "() => document.querySelector('.skip-link').getBoundingClientRect().top >= 0");
        AssertNoConsoleErrors();
    }

    [Test]
    public async Task A_chart_is_more_than_a_picture()
    {
        // A canvas tells a screen reader nothing at all, so the chart carries its own summary of
        // what is plotted and where each line ends up.
        await GoToAsync(PlayerUrl);
        await WaitForChartAsync();

        var canvas = Page.Locator("canvas");

        await Expect(canvas).ToHaveAttributeAsync("role", "img");
        await Expect(canvas).ToHaveAttributeAsync(
            "aria-label", new System.Text.RegularExpressions.Regex(
                $"Cumulative points by game number\\. {BluelineAppFixture.Seed.TopScorerName} ends at 30\\."));
        AssertNoConsoleErrors();
    }

    [Test]
    public async Task The_view_toggles_say_which_one_is_on()
    {
        await GoToAsync(PlayerUrl);

        var cumulative = Page.GetByRole(AriaRole.Button, new() { Name = "Cumulative", Exact = true });
        var perGame = Page.GetByRole(AriaRole.Button, new() { Name = "Per game", Exact = true });

        await Expect(cumulative).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(perGame).ToHaveAttributeAsync("aria-pressed", "false");

        await perGame.ClickAsync();

        // Written as words rather than bound to a bool: Blazor renders a true bool as a valueless
        // attribute and drops a false one, and either loses the state this is here to carry.
        await Expect(perGame).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(cumulative).ToHaveAttributeAsync("aria-pressed", "false");
        AssertNoConsoleErrors();
    }

    [Test]
    public async Task Compared_subjects_are_told_apart_by_shape_as_well_as_colour()
    {
        await GoToAsync(PlayerUrl);
        await WaitForChartAsync();

        await Page.Locator(".compare-picker input").FillAsync(BluelineAppFixture.Seed.GrinderName[..5]);
        await Page.Locator(".compare-results button").First.ClickAsync();
        await WaitForChartAsync("chart.data.datasets.length === 2");

        var styles = await ReadChartAsync<string[]>("chart.data.datasets.map(d => d.pointStyle)");

        Assert.Multiple(async () =>
        {
            Assert.That(styles, Is.Unique, "colour cannot be the only thing separating the lines");

            // The chip repeats the mark the chart draws, so the two can be matched up away from
            // the legend.
            await Expect(Page.Locator(".chip .swatch")).ToHaveTextAsync("▲");
        });
        AssertNoConsoleErrors();
    }

    [Test]
    public async Task A_search_that_finds_nothing_says_so_out_loud()
    {
        await GoToAsync(PlayerUrl);

        await Page.Locator(".compare-picker input").FillAsync("zzzznobody");

        await Expect(Page.Locator(".compare-picker [role='status']"))
            .ToHaveTextAsync("No matches for zzzznobody");
        AssertNoConsoleErrors();
    }

    [Test]
    public async Task A_team_page_offers_its_games_as_a_table_and_not_only_as_a_chart()
    {
        // Chart.js tooltips answer only to a pointer, so the table is the whole of a keyboard
        // reader's access to the game-by-game figures. The player and goalie pages already had one.
        await GoToAsync($"/teams/{BluelineAppFixture.Seed.HomeTeamId}");

        var log = Page.Locator(".card").Filter(new() { HasText = "Game log" });
        await Expect(log.Locator("tbody tr")).ToHaveCountAsync(BluelineAppFixture.Seed.GameCount);

        // Ten games at two points a win, five of them wins, newest first — so the top row carries
        // the closing total.
        await Expect(log.Locator("tbody tr").First.Locator("td").Last).ToHaveTextAsync("10");
        AssertNoConsoleErrors();
    }

    [Test]
    public async Task Table_headers_are_marked_as_headers_for_their_column()
    {
        await GoToAsync("/leaders");

        var headers = Page.Locator("table th");

        await Expect(headers.First).ToHaveAttributeAsync("scope", "col");
        Assert.That(await headers.CountAsync(), Is.GreaterThan(0));
        AssertNoConsoleErrors();
    }
}
