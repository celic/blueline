using Blueline.Web.Components.Shared;

namespace Blueline.Tests;

/// <summary>
/// Tied leaderboard rows must rank identically on every request. Without a final tie-break the
/// database may return equal rows in any order, which also decides arbitrarily who survives the
/// Take cut-off.
/// </summary>
public class OrderingTests : QueryFixture
{
    private async Task SeedTiedSkatersAsync(int count)
    {
        AddTeam(21, "HME");
        AddTeam(22, "AWY");
        AddGame(2025020001, 0, 21, 22);

        // Deliberately inserted highest id first, so insertion order opposes the expected order.
        for (var id = count; id >= 1; id--)
        {
            AddPlayer(id, $"Player{id}", $"Tied{id}");
            AddSkaterLine(2025020001, id, 21, goals: 1, assists: 1);
        }

        await SaveAsync();
    }

    [Test]
    public async Task Tied_leaders_are_ranked_by_id_and_stay_stable_across_calls()
    {
        await SeedTiedSkatersAsync(5);

        var first = await Queries.GetLeadersAsync(SeasonId, "points");
        var second = await Queries.GetLeadersAsync(SeasonId, "points");

        Assert.Multiple(() =>
        {
            Assert.That(first.Select(l => l.PlayerId), Is.EqualTo(new[] { 1, 2, 3, 4, 5 }));
            Assert.That(second.Select(l => l.PlayerId), Is.EqualTo(first.Select(l => l.PlayerId)));
        });
    }

    [Test]
    public async Task A_take_cut_off_across_a_tie_selects_the_same_players_every_time()
    {
        await SeedTiedSkatersAsync(5);

        var first = await Queries.GetLeadersAsync(SeasonId, "points", take: 2);
        var second = await Queries.GetLeadersAsync(SeasonId, "points", take: 2);

        Assert.Multiple(() =>
        {
            Assert.That(first.Select(l => l.PlayerId), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(second.Select(l => l.PlayerId), Is.EqualTo(first.Select(l => l.PlayerId)));
        });
    }

    [Test]
    public async Task Tied_players_in_a_search_are_ordered_stably()
    {
        await SeedTiedSkatersAsync(4);

        var players = await Queries.SearchPlayersAsync(SeasonId, null);

        Assert.That(players.Select(p => p.Id), Is.EqualTo(new[] { 1, 2, 3, 4 }));
    }

    [Test]
    public async Task Teams_on_equal_points_and_wins_are_ordered_stably()
    {
        AddTeam(23, "CCC");
        AddTeam(22, "BBB");
        AddTeam(21, "AAA");
        AddGame(2025020001, 0, 21, 22);
        foreach (var teamId in new[] { 23, 22, 21 })
            AddTeamLine(2025020001, teamId, 99, true, "W", 2);
        await SaveAsync();

        var teams = await Queries.GetTeamsAsync(SeasonId);

        Assert.That(teams.Select(t => t.Id), Is.EqualTo(new[] { 21, 22, 23 }));
    }

    [Test]
    public async Task Tied_goalies_are_ordered_stably()
    {
        AddTeam(21, "HME");
        AddTeam(22, "AWY");
        AddGame(2025020001, 0, 21, 22);

        for (var id = 3; id >= 1; id--)
        {
            AddPlayer(id, $"Goalie{id}", $"Tied{id}", "G");
            AddGoalieLine(2025020001, id, 21, saves: 20, shotsAgainst: 22, toiSeconds: 1200);
        }
        await SaveAsync();

        var goalies = await Queries.SearchGoaliesAsync(SeasonId, stat: "saves");

        Assert.That(goalies.Select(g => g.Id), Is.EqualTo(new[] { 1, 2, 3 }));
    }
}

/// <summary>The comparison palette, which decides how overlaid series are told apart.</summary>
public class ChartPaletteTests
{
    [Test]
    public void Every_colour_is_a_distinct_six_digit_hex_value()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ChartPalette.Series, Is.Unique);
            foreach (var colour in ChartPalette.Series)
                Assert.That(colour, Does.Match("^#[0-9a-fA-F]{6}$"), colour);
        });
    }

    [Test]
    public void The_first_series_takes_the_first_colour() =>
        Assert.That(ChartPalette.For(0), Is.EqualTo(ChartPalette.Series[0]));

    [Test]
    public void Colours_wrap_rather_than_running_out()
    {
        // The comparison cap is below the palette length today, but wrapping keeps a raised cap
        // from throwing rather than merely repeating a colour.
        var length = ChartPalette.Series.Length;

        Assert.That(ChartPalette.For(length), Is.EqualTo(ChartPalette.For(0)));
    }

    [Test]
    public void The_palette_covers_the_full_comparison_set_without_repeating()
    {
        // One primary subject plus three comparisons.
        var used = Enumerable.Range(0, 4).Select(ChartPalette.For);

        Assert.That(used, Is.Unique);
    }
}
