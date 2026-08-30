using Blueline.Core.Dtos;

namespace Blueline.Tests;

/// <summary>
/// The stat catalogue is what the API validates against and what the pages render, so a typo or
/// a duplicated key here surfaces as a stat that silently cannot be charted.
/// </summary>
public class StatDefinitionTests
{
    [Test]
    public void Every_catalogue_has_unique_keys()
    {
        Assert.Multiple(() =>
        {
            Assert.That(StatDefinition.Skater.Select(s => s.Key), Is.Unique);
            Assert.That(StatDefinition.Goalie.Select(s => s.Key), Is.Unique);
            Assert.That(StatDefinition.Team.Select(s => s.Key), Is.Unique);
        });
    }

    [Test]
    public void Every_definition_carries_a_label_and_a_unit()
    {
        var all = StatDefinition.Skater.Concat(StatDefinition.Goalie).Concat(StatDefinition.Team);

        Assert.Multiple(() =>
        {
            foreach (var stat in all)
            {
                Assert.That(stat.Label, Is.Not.Empty, $"{stat.Key} has no label");
                Assert.That(stat.Unit, Is.Not.Empty, $"{stat.Key} has no unit");
            }
        });
    }

    [TestCase("points")]
    [TestCase("POINTS")]
    [TestCase("Points")]
    public void Lookups_are_case_insensitive(string key) =>
        Assert.That(StatDefinition.FindSkater(key)?.Key, Is.EqualTo("points"));

    [Test]
    public void An_unknown_key_returns_null_rather_than_a_default()
    {
        Assert.Multiple(() =>
        {
            Assert.That(StatDefinition.FindSkater("nonsense"), Is.Null);
            Assert.That(StatDefinition.FindGoalie("nonsense"), Is.Null);
            Assert.That(StatDefinition.FindTeam("nonsense"), Is.Null);
        });
    }

    [Test]
    public void Lookups_do_not_cross_between_catalogues()
    {
        Assert.Multiple(() =>
        {
            // A goalie stat must not resolve as a skater stat, or a trend would chart the
            // wrong column entirely.
            Assert.That(StatDefinition.FindSkater("savePctg"), Is.Null);
            Assert.That(StatDefinition.FindGoalie("hits"), Is.Null);
            Assert.That(StatDefinition.FindTeam("goals"), Is.Null);
        });
    }

    [Test]
    public void Only_save_percentage_and_goals_against_average_are_rates()
    {
        var rates = StatDefinition.Goalie.Where(s => s.IsRate).Select(s => s.Key);

        Assert.That(rates, Is.EquivalentTo(new[] { "savePctg", "gaa" }));
    }

    [Test]
    public void No_skater_or_team_stat_is_a_rate()
    {
        // Both accumulate by summing; treating one as a rate would silently divide by a
        // denominator that was never supplied.
        Assert.Multiple(() =>
        {
            Assert.That(StatDefinition.Skater.Any(s => s.IsRate), Is.False);
            Assert.That(StatDefinition.Team.Any(s => s.IsRate), Is.False);
        });
    }

    [Test]
    public void The_rate_qualification_matches_the_leagues_own_threshold() =>
        Assert.That(StatDefinition.RateQualificationMinutes, Is.EqualTo(1500));
}
