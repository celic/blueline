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
    public void No_scope_ever_admits_preseason_or_merges_the_two()
    {
        Assert.Multiple(() =>
        {
            foreach (var scope in Enum.GetValues<GameScope>())
            {
                Assert.That(scope.GameTypes(), Does.Not.Contain(GameTypes.Preseason),
                    "preseason stats count towards nothing and are never ingested");
                Assert.That(scope.GameTypes(), Has.Length.EqualTo(1),
                    "the regular season and the playoffs are never counted together");
            }
        });
    }

    [Test]
    public void Standings_points_exist_only_in_the_regular_season()
    {
        Assert.Multiple(() =>
        {
            Assert.That(GameScope.RegularSeason.HasStandingsPoints(), Is.True);
            Assert.That(GameScope.Playoffs.HasStandingsPoints(), Is.False);
        });
    }

    [TestCase("Playoffs", GameScope.Playoffs)]
    [TestCase("playoffs", GameScope.Playoffs)]
    [TestCase("RegularSeason", GameScope.RegularSeason)]
    public void Parse_accepts_the_scope_names_case_insensitively(string input, GameScope expected) =>
        Assert.That(GameScopes.Parse(input), Is.EqualTo(expected));

    [TestCase(null)]
    [TestCase("")]
    [TestCase("nonsense")]
    [TestCase("preseason")]
    [TestCase("All", Description = "the combined scope earlier builds offered")]
    [TestCase("7", Description = "TryParse accepts digits and does not check they name anything")]
    public void Parse_falls_back_to_the_regular_season_rather_than_throwing(string? input) =>
        Assert.That(GameScopes.Parse(input), Is.EqualTo(GameScope.RegularSeason),
            "a stale bookmark should still render something sensible");
}
