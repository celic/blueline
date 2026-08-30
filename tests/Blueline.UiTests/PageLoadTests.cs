using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Blueline.UiTests;

/// <summary>Every page renders, connects, and reports nothing broken underneath.</summary>
[Parallelizable(ParallelScope.Self)]
public class PageLoadTests : BluelinePageTest
{
    [TestCase("/", "Blueline")]
    [TestCase("/leaders", "Season leaders")]
    [TestCase("/players", "Players")]
    [TestCase("/goalies", "Goalies")]
    [TestCase("/teams", "Teams")]
    [TestCase("/data", "Data")]
    public async Task A_page_loads_with_its_heading_and_no_console_errors(string path, string heading)
    {
        await GoToAsync(path);

        await Expect(Page.Locator("h1")).ToHaveTextAsync(heading);
        AssertNoConsoleErrors();
    }

    [Test]
    public async Task The_landing_page_reaches_the_leaders_it_used_to_be()
    {
        // Leaders moved off "/" and the root must still resolve: an old bookmark landing on a
        // 404 is the visible cost of that move, and the only part of it a reader would notice.
        await GoToAsync("/");

        await Page.GetByRole(AriaRole.Link, new() { Name = "Leaders", Exact = true }).First.ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex("/leaders"));
        await Expect(Page.Locator("h1")).ToHaveTextAsync("Season leaders");
        AssertNoConsoleErrors();
    }

    [Test]
    public async Task The_leaders_page_lists_the_seeded_players()
    {
        await GoToAsync("/leaders");

        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = BluelineAppFixture.Seed.TopScorerName }))
            .ToBeVisibleAsync();
        AssertNoConsoleErrors();
    }

    [Test]
    public async Task Navigation_moves_between_sections_without_a_full_reload()
    {
        await GoToAsync("/leaders");

        await Page.GetByRole(AriaRole.Link, new() { Name = "Goalies", Exact = true }).ClickAsync();

        await Expect(Page.Locator("h1")).ToHaveTextAsync("Goalies");
        await Expect(Page.GetByText(BluelineAppFixture.Seed.GoalieName)).ToBeVisibleAsync();
        AssertNoConsoleErrors();
    }

    [Test]
    public async Task A_goalie_opened_by_player_url_is_redirected_to_the_goalie_page()
    {
        // Goalies record none of the skater stats that page charts, so it redirects rather than
        // rendering an empty season.
        await GoToAsync($"/players/{BluelineAppFixture.Seed.GoalieId}");

        await Expect(Page).ToHaveURLAsync(new Regex($"/goalies/{BluelineAppFixture.Seed.GoalieId}"));
        await Expect(Page.Locator("h1")).ToHaveTextAsync(BluelineAppFixture.Seed.GoalieName);
        AssertNoConsoleErrors();
    }

    [Test]
    public async Task A_season_with_no_data_shows_an_empty_state_rather_than_an_error()
    {
        await GoToAsync($"/players/{BluelineAppFixture.Seed.TopScorerId}?season=19992000");

        await Expect(Page.Locator(".empty")).ToBeVisibleAsync();
        AssertNoConsoleErrors();
    }
}
