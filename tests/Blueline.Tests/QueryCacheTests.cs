using Blueline.Core.Dtos;
using Blueline.Core.Entities;
using Blueline.Data.Queries;

namespace Blueline.Tests;

/// <summary>
/// The aggregate cache, and the one property that matters more than speed: it must not serve a
/// figure the database has since changed.
///
/// Ingestion runs in a separate process, so nothing writing can tell the cache to drop anything.
/// Every key therefore carries a version token read from the database itself, and these tests are
/// mostly about that token moving when it should.
/// </summary>
public class QueryCacheTests : QueryFixture
{
    private QueryCache _cache = null!;
    private StatsQueryService _cached = null!;

    [SetUp]
    public void SetUpCache()
    {
        // Zero holding window: the token is re-read on every call, so these tests assert on
        // invalidation rather than sleeping through the window that exists to share one lookup
        // across a page's panels.
        _cache = new QueryCache(TimeSpan.Zero);
        _cached = new StatsQueryService(Db, _cache);
    }

    [TearDown]
    public void TearDownCache() => _cache.Dispose();

    private async Task SeedAsync(int games = 4, int goals = 1)
    {
        AddTeam(21, "HME");
        AddTeam(22, "AWY");
        AddPlayer(100, "Alexis", "Star");

        for (var i = 0; i < games; i++)
        {
            var gameId = 2025020001 + i;
            AddGame(gameId, i * 2, 21, 22);
            AddSkaterLine(gameId, 100, 21, goals: goals);
        }

        await SaveAsync();
    }

    [Test]
    public async Task The_second_identical_request_does_not_touch_the_database()
    {
        await SeedAsync();

        var first = await _cached.GetLeadersAsync(SeasonId, "goals");
        var second = await _cached.GetLeadersAsync(SeasonId, "goals");

        Assert.Multiple(() =>
        {
            Assert.That(second[0].Value, Is.EqualTo(first[0].Value));
            Assert.That(_cache.Hits, Is.EqualTo(1));
            Assert.That(_cache.Misses, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Different_arguments_are_different_entries()
    {
        await SeedAsync();

        await _cached.GetLeadersAsync(SeasonId, "goals");
        await _cached.GetLeadersAsync(SeasonId, "points");
        await _cached.GetLeadersAsync(SeasonId, "goals", take: 5);
        await _cached.GetTeamsAsync(SeasonId);

        Assert.Multiple(() =>
        {
            Assert.That(_cache.Misses, Is.EqualTo(4), "four questions, four answers");
            Assert.That(_cache.Hits, Is.Zero);
        });
    }

    [Test]
    public async Task A_new_game_makes_the_old_answer_unreachable()
    {
        await SeedAsync(games: 4);
        var before = await _cached.GetLeadersAsync(SeasonId, "goals");

        // What an archive import looks like: rows arriving with no ingestion run behind them.
        AddGame(2025020099, 40, 21, 22);
        AddSkaterLine(2025020099, 100, 21, goals: 3);
        await SaveAsync();

        var after = await _cached.GetLeadersAsync(SeasonId, "goals");

        Assert.Multiple(() =>
        {
            Assert.That(before[0].Value, Is.EqualTo(4));
            Assert.That(after[0].Value, Is.EqualTo(7), "the new game is counted, not the cached total");
        });
    }

    [Test]
    public async Task An_ingestion_run_makes_the_old_answer_unreachable()
    {
        // A nightly run usually revises games already stored rather than adding any, so the newest
        // game id does not move. The run itself is the signal.
        await SeedAsync(games: 4);
        var before = await _cached.GetLeadersAsync(SeasonId, "goals");

        var line = Db.SkaterGameStats.Single(s => s.GameId == 2025020001);
        line.Goals = 5;
        Db.IngestionRuns.Add(new IngestionRun
        {
            Kind = "daily",
            StartedUtc = DateTimeOffset.UtcNow,
            CompletedUtc = DateTimeOffset.UtcNow,
            Status = IngestionStatus.Succeeded,
        });
        await SaveAsync();

        var after = await _cached.GetLeadersAsync(SeasonId, "goals");

        Assert.Multiple(() =>
        {
            Assert.That(before[0].Value, Is.EqualTo(4));
            Assert.That(after[0].Value, Is.EqualTo(8), "a stat correction reaches the leaderboard");
        });
    }

    [Test]
    public async Task Without_a_cache_the_service_behaves_exactly_as_before()
    {
        await SeedAsync();

        var uncached = new StatsQueryService(Db);
        var first = await uncached.GetLeadersAsync(SeasonId, "goals");

        AddGame(2025020099, 40, 21, 22);
        AddSkaterLine(2025020099, 100, 21, goals: 3);
        await SaveAsync();

        var second = await uncached.GetLeadersAsync(SeasonId, "goals");

        Assert.Multiple(() =>
        {
            Assert.That(first[0].Value, Is.EqualTo(4));
            Assert.That(second[0].Value, Is.EqualTo(7), "no cache, no staleness, ever");
        });
    }

    [Test]
    public async Task Streak_boards_are_cached_too()
    {
        await SeedAsync(games: 12);

        var streaks = new StreaksQueryService(Db, _cached, _cache);
        await streaks.GetSkaterStreaksAsync(SeasonId, "goals", RollingWindow.Games(5));
        var hitsBefore = _cache.Hits;
        await streaks.GetSkaterStreaksAsync(SeasonId, "goals", RollingWindow.Games(5));

        Assert.That(_cache.Hits, Is.GreaterThan(hitsBefore));
    }
}
