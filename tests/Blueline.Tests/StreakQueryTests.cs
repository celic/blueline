using Blueline.Core.Dtos;
using Blueline.Data.Queries;

namespace Blueline.Tests;

/// <summary>
/// Ranking runs by departure from a subject's own rate.
///
/// The behaviour worth protecting is that this board does *not* agree with the leaderboard. If a
/// star's twenty points always outranks a checker's eight, the page is a second copy of Leaders
/// and there was no reason to build it. The tests below are mostly about where that stops being
/// true: the floors that keep a fringe player's one good night off the board.
/// </summary>
public class StreakQueryTests : QueryFixture
{
    private StreaksQueryService Streaks => new(Db, Queries);

    private const int Star = 100;
    private const int Checker = 101;
    private const int Fringe = 102;

    /// <summary>
    /// A season where a star scores steadily and a checker is quiet all year, then explodes.
    /// </summary>
    private async Task SeedSeasonAsync(int games = 20, int recentGames = 5)
    {
        AddTeam(21, "HME");
        AddTeam(22, "AWY");
        AddPlayer(Star, "Alexis", "Star");
        AddPlayer(Checker, "Boris", "Checker");
        AddPlayer(Fringe, "Cy", "Fringe");

        for (var i = 0; i < games; i++)
        {
            var gameId = 2025020001 + i;
            AddGame(gameId, i * 2, 21, 22);

            var isRecent = i >= games - recentGames;

            // Two points a night, every night, all season.
            AddSkaterLine(gameId, Star, 21, goals: 1, assists: 1);

            // Nothing until the closing stretch, then two a night as well — the same output as
            // the star, from a player who has never produced it before.
            AddSkaterLine(gameId, Checker, 21, goals: isRecent ? 1 : 0, assists: isRecent ? 1 : 0);

            // A single assist, in the last game of the season.
            AddSkaterLine(gameId, Fringe, 21, assists: i == games - 1 ? 1 : 0);
        }

        await SaveAsync();
    }

    [Test]
    public async Task A_run_far_above_a_players_own_rate_outranks_the_same_run_from_a_star()
    {
        await SeedSeasonAsync();

        var board = await Streaks.GetSkaterStreaksAsync(SeasonId, "points", RollingWindow.Games(5), take: 5);

        Assert.Multiple(() =>
        {
            Assert.That(board!.Leaders[0].SubjectId, Is.EqualTo(Checker),
                "identical five-game totals, but only one of them is a departure");
            Assert.That(board.Leaders[0].Total, Is.EqualTo(board.Leaders[1].Total),
                "the raw totals really are the same, which is the whole point");
            Assert.That(board.Leaders[0].Lift, Is.GreaterThan(board.Leaders[1].Lift));
            Assert.That(board.Leaders[1].Lift, Is.EqualTo(1.0),
                "a player producing exactly their usual rate has a lift of one");
        });
    }

    [Test]
    public async Task A_single_good_night_from_a_fringe_player_is_kept_off_the_board()
    {
        await SeedSeasonAsync();

        var board = await Streaks.GetSkaterStreaksAsync(SeasonId, "points", RollingWindow.Games(5), take: 5);

        // One assist against a leader's ten is an enormous multiple of nothing, and the exact
        // failure the relative floor exists to prevent.
        Assert.That(board!.Leaders.Select(l => l.SubjectId), Does.Not.Contain(Fringe));
    }

    [Test]
    public async Task A_player_with_too_little_season_behind_them_has_no_baseline_to_depart_from()
    {
        // Eight games is under the minimum, so nobody qualifies however hot they look.
        await SeedSeasonAsync(games: 8, recentGames: 3);

        var board = await Streaks.GetSkaterStreaksAsync(SeasonId, "points", RollingWindow.Games(3), take: 5);

        Assert.That(board!.Leaders, Is.Empty,
            "a rate needs a season behind it before a week can be said to depart from it");
    }

    [Test]
    public async Task A_games_window_is_only_offered_to_players_who_filled_it()
    {
        await SeedSeasonAsync();

        // One extra player, called up for the last two games of the season.
        AddPlayer(200, "Dana", "Callup");
        AddSkaterLine(2025020001 + 18, 200, 21, goals: 3);
        AddSkaterLine(2025020001 + 19, 200, 21, goals: 3);
        await SaveAsync();

        var board = await Streaks.GetSkaterStreaksAsync(SeasonId, "goals", RollingWindow.Games(5), take: 5);

        Assert.That(board!.Leaders.Select(l => l.SubjectId), Does.Not.Contain(200),
            "two games is not a five-game run, however good the two were");
    }

    [Test]
    public async Task A_days_window_counts_whatever_games_fell_inside_it()
    {
        await SeedSeasonAsync();

        // Games sit two days apart, so a nine-day window holds five of them.
        var board = await Streaks.GetSkaterStreaksAsync(SeasonId, "points", RollingWindow.Days(9), take: 5);

        Assert.Multiple(() =>
        {
            Assert.That(board!.WindowUnit, Is.EqualTo(WindowUnit.Days));
            Assert.That(board.Leaders[0].GamesInWindow, Is.EqualTo(5));
            Assert.That(board.Leaders[0].SubjectId, Is.EqualTo(Checker));
        });
    }

    [Test]
    public async Task A_days_window_ignores_a_subject_with_barely_any_games_in_it()
    {
        await SeedSeasonAsync();

        // Three days reaches two games, one short of what a days window will report on.
        var board = await Streaks.GetSkaterStreaksAsync(SeasonId, "points", RollingWindow.Days(3), take: 5);

        Assert.That(board!.Leaders, Is.Empty,
            "two games inside a window is a couple of games, not a stretch of form");
    }

    [Test]
    public async Task The_window_ends_on_the_last_day_of_hockey_rather_than_today()
    {
        await SeedSeasonAsync();

        var board = await Streaks.GetSkaterStreaksAsync(SeasonId, "points", RollingWindow.Games(5));

        Assert.That(board!.AsOf, Is.EqualTo(new DateOnly(2025, 10, 8).AddDays(38)),
            "measured from the newest game stored, so the board still works in the off-season");
    }

    [Test]
    public async Task An_unknown_stat_is_not_a_board()
    {
        await SeedSeasonAsync();

        Assert.That(await Streaks.GetSkaterStreaksAsync(SeasonId, "corsi"), Is.Null);
    }

    [Test]
    public async Task A_season_with_no_games_has_nothing_to_measure()
    {
        Assert.That(await Streaks.GetSkaterStreaksAsync(19992000, "points"), Is.Null);
    }

    [Test]
    public async Task Leaders_carry_the_names_and_clubs_a_panel_needs()
    {
        await SeedSeasonAsync();

        var board = await Streaks.GetSkaterStreaksAsync(SeasonId, "points", RollingWindow.Games(5));

        Assert.Multiple(() =>
        {
            Assert.That(board!.Leaders[0].SubjectName, Is.EqualTo("Boris Checker"));
            Assert.That(board.Leaders[0].TeamAbbrev, Is.EqualTo("HME"));
            Assert.That(board.StatLabel, Is.EqualTo("Points"));
            Assert.That(board.WindowLabel, Is.EqualTo("last 5 games"));
        });
    }

    [Test]
    public async Task Time_on_ice_compares_minutes_against_minutes()
    {
        AddTeam(21, "HME");
        AddTeam(22, "AWY");
        AddPlayer(Star, "Alexis", "Star");

        for (var i = 0; i < 20; i++)
        {
            var gameId = 2025020001 + i;
            AddGame(gameId, i * 2, 21, 22);
            // Fifteen minutes a night all year, twenty in the closing stretch.
            AddSkaterLine(gameId, Star, 21, toiSeconds: i >= 15 ? 1200 : 900);
        }

        await SaveAsync();

        var board = await Streaks.GetSkaterStreaksAsync(SeasonId, "toi", RollingWindow.Games(5));
        var leader = board!.Leaders.Single();

        Assert.Multiple(() =>
        {
            Assert.That(leader.PerGame, Is.EqualTo(20).Within(0.01), "minutes, not seconds");
            Assert.That(leader.Baseline, Is.EqualTo(16.25).Within(0.01),
                "the season average in minutes: fifteen games at 15 and five at 20");
            Assert.That(leader.Lift, Is.EqualTo(20 / 16.25).Within(0.01));
        });
    }

    [Test]
    public async Task A_goalie_board_reads_lift_as_a_difference_in_save_percentage()
    {
        AddTeam(21, "HME");
        AddTeam(22, "AWY");
        AddPlayer(Star, "Alexis", "Starter", "G");

        for (var i = 0; i < 20; i++)
        {
            var gameId = 2025020001 + i;
            AddGame(gameId, i * 2, 21, 22);
            // .900 all season, .950 across the closing stretch.
            AddGoalieLine(gameId, Star, 21, saves: i >= 15 ? 38 : 36, shotsAgainst: 40);
        }

        await SaveAsync();

        var board = await Streaks.GetGoalieStreaksAsync(SeasonId, RollingWindow.Games(5));
        var leader = board!.Leaders.Single();

        Assert.Multiple(() =>
        {
            Assert.That(board.IsRate, Is.True);
            Assert.That(leader.Total, Is.EqualTo(0.95).Within(1e-6), "shot-weighted, not averaged");
            Assert.That(leader.Baseline, Is.EqualTo(0.9125).Within(1e-6));
            Assert.That(leader.Lift, Is.EqualTo(0.0375).Within(1e-6),
                "a difference in save percentage, since a ratio of 1.04 would mean nothing to anyone");
        });
    }

    [Test]
    public async Task A_goalie_board_carries_the_club_a_goalie_actually_played_for()
    {
        AddTeam(21, "HME");
        AddTeam(22, "AWY");
        AddPlayer(Star, "Alexis", "Starter", "G");

        for (var i = 0; i < 20; i++)
        {
            var gameId = 2025020001 + i;
            AddGame(gameId, i * 2, 21, 22);
            AddGoalieLine(gameId, Star, 21, saves: i >= 15 ? 38 : 36, shotsAgainst: 40);
        }

        await SaveAsync();

        var board = await Streaks.GetGoalieStreaksAsync(SeasonId, RollingWindow.Games(5));

        Assert.That(board!.Leaders.Single().TeamAbbrev, Is.EqualTo("HME"),
            "read from goalie appearances; the skater lookup would leave this empty");
    }

    [Test]
    public async Task A_backup_with_one_quiet_night_does_not_top_the_goalie_board()
    {
        AddTeam(21, "HME");
        AddTeam(22, "AWY");
        AddPlayer(Star, "Alexis", "Starter", "G");
        AddPlayer(Checker, "Boris", "Backup", "G");

        for (var i = 0; i < 20; i++)
        {
            var gameId = 2025020001 + i;
            AddGame(gameId, i * 2, 21, 22);
            AddGoalieLine(gameId, Star, 21, saves: i >= 16 ? 38 : 36, shotsAgainst: 40);

            // The backup faces a handful of shots on three nights, stopping all of them.
            if (i >= 17) AddGoalieLine(gameId, Checker, 21, saves: 6, shotsAgainst: 6, toiSeconds: 600);
        }

        await SaveAsync();

        var board = await Streaks.GetGoalieStreaksAsync(SeasonId, RollingWindow.Days(9));

        Assert.That(board!.Leaders.Select(l => l.SubjectId), Does.Not.Contain(Checker),
            "a perfect .1000 over eighteen shots is noise presented as a finding");
    }
}
