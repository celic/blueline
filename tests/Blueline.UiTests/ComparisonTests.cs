using Microsoft.Playwright;

namespace Blueline.UiTests;

/// <summary>
/// Comparison, which is where the interactive failures in this project have actually occurred:
/// a picker that kept a stale selection after choosing, and a chart that did not repaint.
/// </summary>
[Parallelizable(ParallelScope.Self)]
public class ComparisonTests : BluelinePageTest
{
    private static string PlayerUrl => $"/players/{BluelineAppFixture.Seed.TopScorerId}";

    private async Task AddComparisonAsync(string search, string name)
    {
        await Page.GetByPlaceholder("Search players…").FillAsync(search);
        await Page.GetByRole(AriaRole.Button, new() { Name = name }).ClickAsync();
    }

    [Test]
    public async Task Searching_finds_a_player_and_adding_them_draws_a_second_series()
    {
        await GoToAsync(PlayerUrl);
        await WaitForChartAsync();

        await AddComparisonAsync("grinder", BluelineAppFixture.Seed.GrinderName);

        await Page.WaitForFunctionAsync("() => Object.values(Chart.instances)[0].data.datasets.length === 2");

        var labels = await ReadChartAsync<string[]>("chart.data.datasets.map(d => d.label)");
        Assert.That(labels, Does.Contain(BluelineAppFixture.Seed.GrinderName));
        AssertNoConsoleErrors();
    }

    [Test]
    public async Task The_chosen_player_becomes_a_chip_and_the_search_box_clears()
    {
        // The stale-selection bug: the picker used to keep showing a name that had already moved
        // into a chip, so it looked as though the wrong player had been chosen.
        await GoToAsync(PlayerUrl);
        await WaitForChartAsync();

        await AddComparisonAsync("grinder", BluelineAppFixture.Seed.GrinderName);

        await Expect(Page.Locator(".chip")).ToContainTextAsync(BluelineAppFixture.Seed.GrinderName);
        await Expect(Page.GetByPlaceholder("Search players…")).ToHaveValueAsync("");
        AssertNoConsoleErrors();
    }

    [Test]
    public async Task Removing_a_comparison_drops_its_chip_and_its_series()
    {
        await GoToAsync(PlayerUrl);
        await WaitForChartAsync();
        await AddComparisonAsync("grinder", BluelineAppFixture.Seed.GrinderName);
        await Expect(Page.Locator(".chip")).ToHaveCountAsync(1);

        await Page.Locator(".chip button").ClickAsync();

        await Expect(Page.Locator(".chip")).ToHaveCountAsync(0);
        await Page.WaitForFunctionAsync("() => Object.values(Chart.instances)[0].data.datasets.length === 1");
        AssertNoConsoleErrors();
    }

    [Test]
    public async Task A_player_outside_the_top_scorers_can_still_be_found()
    {
        // The defect this replaced: a fixed list of the leading scorers, which silently excluded
        // most of the league. The grinder has no points at all.
        await GoToAsync(PlayerUrl);
        await WaitForChartAsync();

        await Page.GetByPlaceholder("Search players…").FillAsync("grinder");

        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = BluelineAppFixture.Seed.GrinderName }))
            .ToBeVisibleAsync();
        AssertNoConsoleErrors();
    }

    [Test]
    public async Task A_search_matching_nobody_says_so()
    {
        await GoToAsync(PlayerUrl);
        await WaitForChartAsync();

        await Page.GetByPlaceholder("Search players…").FillAsync("zzzzzz");

        await Expect(Page.Locator(".compare-results")).ToContainTextAsync("Nothing matches");
        AssertNoConsoleErrors();
    }

    [Test]
    public async Task A_comparison_survives_changing_the_stat()
    {
        await GoToAsync(PlayerUrl);
        await WaitForChartAsync();
        await AddComparisonAsync("grinder", BluelineAppFixture.Seed.GrinderName);
        await Page.WaitForFunctionAsync("() => Object.values(Chart.instances)[0].data.datasets.length === 2");

        await Page.Locator("#stat").SelectOptionAsync("hits");

        // The comparison is held by id and re-fetched against the new stat, not discarded.
        await Expect(Page.Locator(".chip")).ToContainTextAsync(BluelineAppFixture.Seed.GrinderName);
        await Page.WaitForFunctionAsync(
            "() => Object.values(Chart.instances)[0].data.datasets.some(d => d.data.at(-1) === 70)");
        AssertNoConsoleErrors();
    }

    [Test]
    public async Task Teams_can_be_compared_too()
    {
        await GoToAsync($"/teams/{BluelineAppFixture.Seed.HomeTeamId}");
        await WaitForChartAsync();

        await Page.GetByPlaceholder("Search teams…").FillAsync("Awayville");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Awayville" }).ClickAsync();

        await Page.WaitForFunctionAsync("() => Object.values(Chart.instances)[0].data.datasets.length === 2");
        await Expect(Page.Locator(".chip")).ToContainTextAsync("Awayville");
        AssertNoConsoleErrors();
    }
}
