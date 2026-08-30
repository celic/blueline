using Blueline.Core.Dtos;
using Blueline.Core.Entities;

namespace Blueline.Tests;

/// <summary>
/// The computed properties on entities and DTOs. Small, but they encode judgements the site
/// depends on — chiefly that "no shots faced" is not the same as "a save percentage of zero".
/// </summary>
public class DtoAndEntityTests
{
    private static GoalieSummary Goalie(int minutes = 2000, int saves = 900, int shots = 1000, int goalsAgainst = 100) =>
        new(1, "A Goalie", null, "HME", GamesPlayed: 40, Starts: 38, minutes, saves, shots, goalsAgainst);

    [Test]
    public void Goalie_save_percentage_is_saves_over_shots() =>
        Assert.That(Goalie(saves: 912, shots: 1000).SavePctg, Is.EqualTo(0.912).Within(1e-9));

    [Test]
    public void A_goalie_who_faced_no_shots_has_no_save_percentage_rather_than_zero()
    {
        // .000 would rank them last on a leaderboard; null keeps them out of it entirely.
        Assert.That(Goalie(saves: 0, shots: 0).SavePctg, Is.Null);
    }

    [Test]
    public void Goals_against_average_is_per_sixty_minutes() =>
        Assert.That(Goalie(minutes: 3000, goalsAgainst: 100).GoalsAgainstAverage, Is.EqualTo(2.0).Within(1e-9));

    [Test]
    public void A_goalie_who_played_no_minutes_has_no_goals_against_average() =>
        Assert.That(Goalie(minutes: 0).GoalsAgainstAverage, Is.Null);

    [TestCase(1499, false)]
    [TestCase(1500, true)]
    [TestCase(1501, true)]
    public void Rate_qualification_is_inclusive_at_the_threshold(int minutes, bool expected) =>
        Assert.That(Goalie(minutes: minutes).QualifiesForRateTitle, Is.EqualTo(expected));

    // --- TrendSeries ---

    private static TrendSeries Series(bool isRate, params double[] cumulative) =>
        new("Subject", 1, "stat", "Stat", 20252026, 10,
            cumulative.Select((c, i) => new TrendPoint(i + 1, 1000 + i, new DateOnly(2025, 10, 8), "OPP", true, c, c, null)).ToList(),
            isRate);

    [Test]
    public void A_counting_series_reports_its_final_cumulative_as_the_total() =>
        Assert.That(Series(false, 1, 3, 6).Total, Is.EqualTo(6));

    [Test]
    public void A_counting_series_divides_the_total_by_games_for_a_per_game_figure() =>
        Assert.That(Series(false, 1, 3, 6).PerGame, Is.EqualTo(2));

    [Test]
    public void A_rate_series_reports_itself_per_game_because_it_is_already_normalised()
    {
        // Dividing a save percentage by games played would be meaningless.
        var series = Series(true, 0.9, 0.91, 0.912);

        Assert.Multiple(() =>
        {
            Assert.That(series.Total, Is.EqualTo(0.912));
            Assert.That(series.PerGame, Is.EqualTo(0.912));
        });
    }

    [Test]
    public void An_empty_series_reports_zero_rather_than_throwing()
    {
        var series = Series(false);

        Assert.Multiple(() =>
        {
            Assert.That(series.Total, Is.Zero);
            Assert.That(series.PerGame, Is.Zero);
        });
    }

    // --- entities ---

    [TestCase("G", true)]
    [TestCase("C", false)]
    [TestCase("D", false)]
    [TestCase("", false)]
    public void A_player_is_a_goalie_only_when_their_position_says_so(string position, bool expected) =>
        Assert.That(new Player { Position = position }.IsGoalie, Is.EqualTo(expected));

    [Test]
    public void A_players_full_name_joins_their_names_and_trims_a_missing_half()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new Player { FirstName = "Connor", LastName = "McDavid" }.FullName, Is.EqualTo("Connor McDavid"));
            Assert.That(new Player { FirstName = "", LastName = "McDavid" }.FullName, Is.EqualTo("McDavid"));
        });
    }

    [TestCase("OFF", true)]
    [TestCase("FINAL", true)]
    [TestCase("LIVE", false)]
    [TestCase("FUT", false)]
    [TestCase("", false)]
    public void Only_completed_games_count_as_final(string state, bool expected) =>
        Assert.That(new Game { GameState = state }.IsFinal, Is.EqualTo(expected));

    [Test]
    public void A_goalie_game_line_reports_no_save_percentage_when_no_shots_were_faced()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new GoalieGameStat { Saves = 0, ShotsAgainst = 0 }.SavePctg, Is.Null);
            Assert.That(new GoalieGameStat { Saves = 27, ShotsAgainst = 30 }.SavePctg, Is.EqualTo(0.9).Within(1e-9));
        });
    }
}
