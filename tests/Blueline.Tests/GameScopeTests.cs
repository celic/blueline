using Blueline.Core.Dtos;
using Blueline.Core.Entities;

namespace Blueline.Tests;

public class GameScopeTests
{
    [Test]
    public void The_regular_season_scope_admits_only_regular_season_games() =>
        Assert.That(GameScope.RegularSeason.GameTypes(), Is.EqualTo(new[] { GameTypes.Regular }));

    [Test]
    public void The_playoff_scope_admits_only_playoff_games() =>
        Assert.That(GameScope.Playoffs.GameTypes(), Is.EqualTo(new[] { GameTypes.Playoffs }));

    [Test]
    public void The_combined_scope_admits_both_but_never_preseason()
    {
        var types = GameScope.All.GameTypes();

        Assert.Multiple(() =>
        {
            Assert.That(types, Does.Contain(GameTypes.Regular));
            Assert.That(types, Does.Contain(GameTypes.Playoffs));
            Assert.That(types, Does.Not.Contain(GameTypes.Preseason),
                "preseason stats count towards nothing and are never ingested");
        });
    }

    [Test]
    public void Standings_points_exist_only_in_the_regular_season()
    {
        Assert.Multiple(() =>
        {
            Assert.That(GameScope.RegularSeason.HasStandingsPoints(), Is.True);
            Assert.That(GameScope.Playoffs.HasStandingsPoints(), Is.False);
            Assert.That(GameScope.All.HasStandingsPoints(), Is.False);
        });
    }

    [TestCase("Playoffs", GameScope.Playoffs)]
    [TestCase("playoffs", GameScope.Playoffs)]
    [TestCase("ALL", GameScope.All)]
    [TestCase("RegularSeason", GameScope.RegularSeason)]
    public void Parse_accepts_the_scope_names_case_insensitively(string input, GameScope expected) =>
        Assert.That(GameScopes.Parse(input), Is.EqualTo(expected));

    [TestCase(null)]
    [TestCase("")]
    [TestCase("nonsense")]
    [TestCase("preseason")]
    public void Parse_falls_back_to_the_regular_season_rather_than_throwing(string? input) =>
        Assert.That(GameScopes.Parse(input), Is.EqualTo(GameScope.RegularSeason),
            "a stale bookmark should still render something sensible");
}
