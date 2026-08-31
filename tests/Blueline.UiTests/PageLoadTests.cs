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
    public async Task A_page_that_fails_explains_itself_instead_of_killing_the_session()
    {
        // /dev/throw exists only in Development and does nothing but fail, so the state a reader
        // meets on the worst day the site has is something that has actually been looked at.
        await GoToAsync("/dev/throw");

        await Expect(Page.Locator(".error-state h2")).ToHaveTextAsync("This page could not be loaded");
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Try again" })).ToBeVisibleAsync();

        // Blazor's own error strip is what this replaces; it must stay hidden.
        await Expect(Page.Locator("#blazor-error-ui")).Not.ToBeVisibleAsync();

        // No console assertion here: the handled exception is reported to the browser by design.
    }

    [Test]
    public async Task One_broken_page_does_not_take_the_rest_of_the_site_with_it()
    {
        await GoToAsync("/dev/throw");
        await Expect(Page.Locator(".error-state")).ToBeVisibleAsync();

        await Page.GetByRole(AriaRole.Link, new() { Name = "Leaders", Exact = true }).ClickAsync();

        // The boundary is reset on navigation. Without that it stays broken for the rest of the
        // session, and one bad page would blank every page visited afterwards.
        await Expect(Page.Locator("h1")).ToHaveTextAsync("Season leaders");
        await Expect(Page.Locator(".error-state")).Not.ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = BluelineAppFixture.Seed.TopScorerName }))
            .ToBeVisibleAsync();
    }

    [Test]
    public async Task A_season_with_no_data_shows_an_empty_state_rather_than_an_error()
    {
        await GoToAsync($"/players/{BluelineAppFixture.Seed.TopScorerId}?season=19992000");

        await Expect(Page.Locator(".empty")).ToBeVisibleAsync();
        AssertNoConsoleErrors();
    }
}
