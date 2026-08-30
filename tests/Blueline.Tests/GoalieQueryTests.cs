using Blueline.Core.Dtos;

namespace Blueline.Tests;

/// <summary>
/// Goalie leaderboards and trends. The rate qualification and the appearance counting are the
/// parts that have already been wrong once, so they are pinned hardest here.
/// </summary>
public class GoalieQueryTests : QueryFixture
{
    /// <summary>
    /// A workhorse starter, a backup, and a third-stringer who was perfect in one short outing.
    /// </summary>
    private async Task SeedThreeGoaliesAsync()
    {
        AddTeam(21, "HME");
        AddTeam(22, "AWY");
        AddPlayer(1, "Starting", "Goalie", "G");
        AddPlayer(2, "Backup", "Goalie", "G");
        AddPlayer(3, "Third", "String", "G");

        // The starter: 30 full games at .900, comfortably over the minutes qualification.
        for (var i = 0; i < 30; i++)
        {
            AddGame(2025020000 + i, i, 21, 22);
            AddGoalieLine(2025020000 + i, playerId: 1, teamId: 21, saves: 27, shotsAgainst: 30);
        }

        // The backup: 5 full games at .800, below the qualification.
        for (var i = 30; i < 35; i++)
        {
            AddGame(2025020000 + i, i, 21, 22);
            AddGoalieLine(2025020000 + i, playerId: 2, teamId: 21, saves: 24, shotsAgainst: 30);
        }

        // The third-stringer: one perfect 10-minute relief appearance, plus 20 games on the
        // bench with no ice time at all.
        AddGame(2025020100, 40, 21, 22);
        AddGoalieLine(2025020100, playerId: 3, teamId: 21, saves: 4, shotsAgainst: 4, toiSeconds: 600, starter: false);
        for (var i = 0; i < 20; i++)
        {
            AddGame(2025020200 + i, 41 + i, 21, 22);
            AddGoalieLine(2025020200 + i, playerId: 3, teamId: 21, saves: 0, shotsAgainst: 0, toiSeconds: 0, starter: false);
        }

        await SaveAsync();
    }

    [Test]
    public async Task Games_played_counts_appearances_not_games_dressed()
    {
        await SeedThreeGoaliesAsync();

        var third = (await Queries.SearchGoaliesAsync(SeasonId, stat: "saves", take: 10)).Single(g => g.Id == 3);

        Assert.Multiple(() =>
        {
            // 21 rows exist for this goalie, but only one is an actual appearance.
            Assert.That(third.GamesPlayed, Is.EqualTo(1), "sitting on the bench is not a game played");
            Assert.That(third.MinutesPlayed, Is.EqualTo(10));
        });
    }

    [Test]
    public async Task A_goalie_who_never_played_is_absent_from_the_leaderboard()
    {
        AddTeam(21, "HME");
        AddTeam(22, "AWY");
        AddPlayer(4, "Never", "Played", "G");
        AddGame(2025020001, 0, 21, 22);
        AddGoalieLine(2025020001, playerId: 4, teamId: 21, saves: 0, shotsAgainst: 0, toiSeconds: 0, starter: false);
        await SaveAsync();

        var goalies = await Queries.SearchGoaliesAsync(SeasonId, stat: "saves");

        Assert.That(goalies, Is.Empty);
    }

    [Test]
    public async Task A_rate_leaderboard_excludes_goalies_below_the_minutes_qualification()
    {
        await SeedThreeGoaliesAsync();

        var leaders = await Queries.SearchGoaliesAsync(SeasonId, stat: "savePctg", take: 10);

        Assert.Multiple(() =>
        {
            // The third-stringer is a perfect 1.000 on four shots; without a floor he would top
            // the table ahead of a goalie who played 1,800 minutes.
            Assert.That(leaders.Select(g => g.Id), Does.Not.Contain(3));
            Assert.That(leaders[0].Id, Is.EqualTo(1), "the qualified starter leads");
        });
    }

    [Test]
    public async Task A_counting_leaderboard_applies_no_qualification()
    {
        await SeedThreeGoaliesAsync();

        var leaders = await Queries.SearchGoaliesAsync(SeasonId, stat: "saves", take: 10);

        // Total saves is not distorted by a small sample, so everyone belongs on it.
        Assert.That(leaders.Select(g => g.Id), Does.Contain(3));
    }

    [Test]
    public async Task The_qualification_is_dropped_when_nobody_clears_it()
    {
        // A part-loaded season: nobody has 1,500 minutes yet.
        AddTeam(21, "HME");
        AddTeam(22, "AWY");
        AddPlayer(1, "Early", "Season", "G");
        for (var i = 0; i < 3; i++)
        {
            AddGame(2025020000 + i, i, 21, 22);
            AddGoalieLine(2025020000 + i, playerId: 1, teamId: 21, saves: 28, shotsAgainst: 30);
        }
        await SaveAsync();

        var leaders = await Queries.SearchGoaliesAsync(SeasonId, stat: "savePctg");

        Assert.That(leaders, Has.Count.EqualTo(1),
            "an empty leaderboard would read as a bug rather than as an unmet threshold");
    }

    [Test]
    public async Task Goals_against_average_ranks_ascending_because_lower_is_better()
    {
        AddTeam(21, "HME");
        AddTeam(22, "AWY");
        AddPlayer(1, "Stingy", "Goalie", "G");
        AddPlayer(2, "Leaky", "Goalie", "G");

        for (var i = 0; i < 30; i++)
        {
            AddGame(2025020000 + i, i, 21, 22);
            AddGoalieLine(2025020000 + i, playerId: 1, teamId: 21, saves: 29, shotsAgainst: 30);   // 1 GA
            AddGame(2025021000 + i, i, 22, 21);
            AddGoalieLine(2025021000 + i, playerId: 2, teamId: 22, saves: 26, shotsAgainst: 30);   // 4 GA
        }
        await SaveAsync();

        var leaders = await Queries.SearchGoaliesAsync(SeasonId, stat: "gaa", take: 10);

        Assert.That(leaders[0].Id, Is.EqualTo(1), "the lower goals-against average leads");
    }

    [Test]
    public async Task Search_matches_on_either_half_of_the_name()
    {
        await SeedThreeGoaliesAsync();

        var byFirst = await Queries.SearchGoaliesAsync(SeasonId, "Backup", "saves");
        var byLast = await Queries.SearchGoaliesAsync(SeasonId, "String", "saves");

        Assert.Multiple(() =>
        {
            Assert.That(byFirst.Select(g => g.Id), Is.EqualTo(new[] { 2 }));
            Assert.That(byLast.Select(g => g.Id), Is.EqualTo(new[] { 3 }));
        });
    }

    [Test]
    public async Task A_goalie_trend_skips_appearances_with_no_ice_time()
    {
        await SeedThreeGoaliesAsync();

        var trend = await Queries.GetGoalieTrendAsync(3, SeasonId, "savePctg");

        Assert.That(trend!.Points, Has.Count.EqualTo(1),
            "20 bench rows would otherwise flatten the chart with meaningless zeroes");
    }

    [Test]
    public async Task A_goalie_trend_weights_save_percentage_by_shots_faced()
    {
        AddTeam(21, "HME");
        AddTeam(22, "AWY");
        AddPlayer(1, "Test", "Goalie", "G");

        AddGame(2025020001, 0, 21, 22);
        AddGoalieLine(2025020001, 1, 21, saves: 10, shotsAgainst: 10);   // 1.000 on 10 shots
        AddGame(2025020002, 1, 21, 22);
        AddGoalieLine(2025020002, 1, 21, saves: 20, shotsAgainst: 40);   // .500 on 40 shots
        await SaveAsync();

        var trend = await Queries.GetGoalieTrendAsync(1, SeasonId, "savePctg");

        Assert.Multiple(() =>
        {
            Assert.That(trend!.IsRate, Is.True);
            Assert.That(trend.Total, Is.EqualTo(0.6), "30 saves on 50 shots, not the mean of 1.000 and .500");
        });
    }

    [Test]
    public async Task A_goalie_trend_computes_goals_against_average_per_sixty_minutes()
    {
        AddTeam(21, "HME");
        AddTeam(22, "AWY");
        AddPlayer(1, "Test", "Goalie", "G");

        AddGame(2025020001, 0, 21, 22);
        AddGoalieLine(2025020001, 1, 21, saves: 28, shotsAgainst: 30, toiSeconds: 3600);  // 2 GA in 60 min
        AddGame(2025020002, 1, 21, 22);
        AddGoalieLine(2025020002, 1, 21, saves: 29, shotsAgainst: 30, toiSeconds: 1800);  // 1 GA in 30 min
        await SaveAsync();

        var trend = await Queries.GetGoalieTrendAsync(1, SeasonId, "gaa");

        // Three goals across 90 minutes is a 2.00 pace.
        Assert.That(trend!.Total, Is.EqualTo(2.0).Within(1e-6));
    }

    [Test]
    public async Task An_unknown_goalie_stat_yields_no_series()
    {
        await SeedThreeGoaliesAsync();

        Assert.That(await Queries.GetGoalieTrendAsync(1, SeasonId, "hits"), Is.Null,
            "a skater stat must not resolve against goalie rows");
    }

    [Test]
    public async Task An_unknown_goalie_yields_no_series()
    {
        await SeedThreeGoaliesAsync();

        Assert.That(await Queries.GetGoalieTrendAsync(999, SeasonId, "savePctg"), Is.Null);
    }

    [Test]
    public async Task A_traded_goalie_is_labelled_by_the_club_they_played_most_for()
    {
        AddTeam(21, "OLD");
        AddTeam(22, "NEW");
        AddPlayer(1, "Traded", "Goalie", "G");

        AddGame(2025020001, 0, 21, 22);
        AddGoalieLine(2025020001, 1, teamId: 21, saves: 28, shotsAgainst: 30);
        for (var i = 1; i <= 3; i++)
        {
            AddGame(2025020000 + i + 1, i, 22, 21);
            AddGoalieLine(2025020000 + i + 1, 1, teamId: 22, saves: 28, shotsAgainst: 30);
        }
        await SaveAsync();

        var goalie = (await Queries.SearchGoaliesAsync(SeasonId, stat: "saves")).Single();

        Assert.That(goalie.TeamAbbrev, Is.EqualTo("NEW"), "three games for the new club against one for the old");
    }

    [Test]
    public async Task Scope_narrows_a_goalie_leaderboard_to_the_chosen_games()
    {
        AddTeam(21, "HME");
        AddTeam(22, "AWY");
        AddPlayer(1, "Test", "Goalie", "G");

        AddGame(2025020001, 0, 21, 22);
        AddGoalieLine(2025020001, 1, 21, saves: 20, shotsAgainst: 20);
        AddGame(2025030001, 200, 21, 22, gameType: Core.Entities.GameTypes.Playoffs);
        AddGoalieLine(2025030001, 1, 21, saves: 10, shotsAgainst: 20);
        await SaveAsync();

        var regular = (await Queries.SearchGoaliesAsync(SeasonId, stat: "saves", scope: GameScope.RegularSeason)).Single();
        var playoffs = (await Queries.SearchGoaliesAsync(SeasonId, stat: "saves", scope: GameScope.Playoffs)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(regular.Saves, Is.EqualTo(20));
            Assert.That(regular.SavePctg, Is.EqualTo(1.0).Within(1e-9), "20 saves on 20 shots");
            Assert.That(playoffs.Saves, Is.EqualTo(10));
            Assert.That(playoffs.SavePctg, Is.EqualTo(0.5).Within(1e-9),
                "10 saves on 20 shots, with the regular season's 20 left out entirely");
        });
    }
}
