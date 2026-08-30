using Blueline.Core.Entities;
using Blueline.Ingestion;
using Blueline.Ingestion.Nhl;

namespace Blueline.Tests;

/// <summary>
/// The small rules that decide what gets stored and how it is labelled. Each one guards against
/// a specific way the league's data misleads.
/// </summary>
public class IngestionRuleTests
{
    private static ScheduleGame Game(int gameType, string state) =>
        new(2025020001, 20252026, gameType, "2026-01-15", state, null, null);

    [TestCase(GameTypes.Regular, "OFF")]
    [TestCase(GameTypes.Regular, "FINAL")]
    [TestCase(GameTypes.Playoffs, "OFF")]
    [TestCase(GameTypes.Playoffs, "FINAL")]
    public void Completed_regular_and_playoff_games_are_ingested(int gameType, string state) =>
        Assert.That(NhlIngestionService.IsIngestableGame(Game(gameType, state)), Is.True);

    [Test]
    public void Preseason_games_are_never_ingested()
    {
        // They appear on club schedules but their stats count towards nothing.
        Assert.That(NhlIngestionService.IsIngestableGame(Game(GameTypes.Preseason, "OFF")), Is.False);
    }

    [TestCase("LIVE")]
    [TestCase("FUT")]
    [TestCase("PRE")]
    [TestCase("")]
    public void A_game_that_is_not_over_is_not_ingested(string state)
    {
        // Ingesting a live game would store a partial box score as though it were the result.
        Assert.That(NhlIngestionService.IsIngestableGame(Game(GameTypes.Regular, state)), Is.False);
    }

    // --- placeholder-name detection ---

    [TestCase("D.", true)]
    [TestCase("C.", true)]
    [TestCase("", true)]
    [TestCase("   ", true)]
    [TestCase("Daniil", false)]
    [TestCase("Jean-Gabriel", false)]
    public void A_name_still_abbreviated_to_an_initial_needs_resolving(string firstName, bool expected) =>
        Assert.That(NhlIngestionService.NeedsRealName(new Player { FirstName = firstName }), Is.EqualTo(expected));

    // --- box score name splitting ---

    [Test]
    public void A_box_score_name_splits_on_the_first_space()
    {
        var (first, last) = NhlIngestionService.SplitBoxscoreName("D. Tarasov");

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo("D."));
            Assert.That(last, Is.EqualTo("Tarasov"));
        });
    }

    [Test]
    public void A_multi_part_surname_stays_whole()
    {
        // Only the first space separates the initial; the rest is all surname.
        var (first, last) = NhlIngestionService.SplitBoxscoreName("K. Van Riemsdyk");

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo("K."));
            Assert.That(last, Is.EqualTo("Van Riemsdyk"));
        });
    }

    [Test]
    public void A_single_word_name_is_treated_as_a_surname()
    {
        var (first, last) = NhlIngestionService.SplitBoxscoreName("Tarasov");

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.Empty);
            Assert.That(last, Is.EqualTo("Tarasov"));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void An_absent_name_yields_two_empty_halves_rather_than_throwing(string? name)
    {
        var (first, last) = NhlIngestionService.SplitBoxscoreName(name);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.Empty);
            Assert.That(last, Is.Empty);
        });
    }

    // --- daily schedule arithmetic ---

    [Test]
    public void The_next_run_is_later_today_when_the_time_has_not_passed()
    {
        var delay = DailyIngestionWorker.TimeUntilNextRun(
            new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc), new TimeOnly(11, 0));

        Assert.That(delay, Is.EqualTo(TimeSpan.FromHours(2)));
    }

    [Test]
    public void The_next_run_rolls_to_tomorrow_once_todays_slot_has_passed()
    {
        var delay = DailyIngestionWorker.TimeUntilNextRun(
            new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc), new TimeOnly(11, 0));

        Assert.That(delay, Is.EqualTo(TimeSpan.FromHours(23)));
    }

    [Test]
    public void Landing_exactly_on_the_run_time_waits_a_full_day_rather_than_running_twice()
    {
        var delay = DailyIngestionWorker.TimeUntilNextRun(
            new DateTime(2026, 1, 15, 11, 0, 0, DateTimeKind.Utc), new TimeOnly(11, 0));

        Assert.That(delay, Is.EqualTo(TimeSpan.FromHours(24)));
    }

    [Test]
    public void The_delay_is_never_negative_across_a_day_of_possible_start_times()
    {
        var runTime = new TimeOnly(11, 0);

        Assert.Multiple(() =>
        {
            for (var hour = 0; hour < 24; hour++)
            {
                var delay = DailyIngestionWorker.TimeUntilNextRun(
                    new DateTime(2026, 1, 15, hour, 30, 0, DateTimeKind.Utc), runTime);

                Assert.That(delay, Is.GreaterThan(TimeSpan.Zero), $"at {hour}:30");
                Assert.That(delay, Is.LessThanOrEqualTo(TimeSpan.FromHours(24)), $"at {hour}:30");
            }
        });
    }
}
