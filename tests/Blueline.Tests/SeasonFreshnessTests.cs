using Blueline.Core.Dtos;

namespace Blueline.Tests;

/// <summary>
/// Deciding whether what the site shows is current.
///
/// A trailing window is silent about its own age, so this is what stops "most points in the last
/// ten games" being presented as current form when those ten games are four months old.
/// </summary>
public class SeasonFreshnessTests
{
    private static readonly DateOnly Today = new(2026, 4, 16);

    [TestCase(0, SeasonFreshness.Current, TestName = "Hockey_tonight_is_current")]
    [TestCase(3, SeasonFreshness.Current, TestName = "Three_days_is_a_normal_gap")]
    [TestCase(4, SeasonFreshness.Behind, TestName = "Four_days_is_not")]
    [TestCase(21, SeasonFreshness.Behind, TestName = "A_three_week_break_is_still_a_break")]
    [TestCase(22, SeasonFreshness.OffSeason, TestName = "Longer_than_any_break_means_the_season_ended")]
    [TestCase(140, SeasonFreshness.OffSeason, TestName = "A_summer_is_plainly_the_off_season")]
    public void Silence_is_classified_by_how_long_it_has_lasted(int daysAgo, SeasonFreshness expected) =>
        Assert.That(SeasonFreshnessRules.Classify(Today.AddDays(-daysAgo), Today), Is.EqualTo(expected));

    [Test]
    public void A_game_dated_ahead_of_today_is_current_rather_than_a_problem()
    {
        // A clock disagreeing with the schedule is not something to announce on the page.
        Assert.Multiple(() =>
        {
            Assert.That(SeasonFreshnessRules.Classify(Today.AddDays(2), Today), Is.EqualTo(SeasonFreshness.Current));
            Assert.That(SeasonFreshnessRules.DaysSince(Today.AddDays(2), Today), Is.Zero);
        });
    }

    [TestCase(0, "today")]
    [TestCase(1, "yesterday")]
    [TestCase(9, "9 days ago")]
    [TestCase(21, "3 weeks ago")]
    [TestCase(136, "4 months ago")]
    public void Age_is_described_in_the_units_a_reader_would_use(int days, string expected) =>
        Assert.That(SeasonFreshnessRules.Describe(days), Is.EqualTo(expected));
}
