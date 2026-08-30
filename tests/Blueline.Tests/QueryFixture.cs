using Blueline.Core.Entities;
using Blueline.Data;
using Blueline.Data.Queries;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Blueline.Tests;

/// <summary>
/// Base for query tests: a real SQLite database built directly from entities, so the queries
/// under test are exercised through actual SQL translation rather than LINQ-to-objects.
/// </summary>
public abstract class QueryFixture
{
    private SqliteConnection _connection = null!;
    protected BluelineDbContext Db = null!;
    protected StatsQueryService Queries = null!;

    protected const int SeasonId = 20252026;

    [SetUp]
    public void SetUpFixture()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        Db = new BluelineDbContext(new DbContextOptionsBuilder<BluelineDbContext>()
            .UseSqlite(_connection)
            .Options);
        Db.Database.EnsureCreated();

        Queries = new StatsQueryService(Db);
    }

    [TearDown]
    public void TearDownFixture()
    {
        Db.Dispose();
        _connection.Dispose();
    }

    protected Team AddTeam(int id, string abbrev)
    {
        var team = new Team { Id = id, Abbrev = abbrev, Name = $"{abbrev} Club" };
        Db.Teams.Add(team);
        return team;
    }

    protected Player AddPlayer(int id, string first, string last, string position = "C")
    {
        var player = new Player { Id = id, FirstName = first, LastName = last, Position = position };
        Db.Players.Add(player);
        return player;
    }

    protected Game AddGame(
        long id, int dayOffset, int homeTeamId, int awayTeamId,
        int gameType = GameTypes.Regular, int homeScore = 3, int awayScore = 2,
        string lastPeriodType = "REG")
    {
        var game = new Game
        {
            Id = id,
            SeasonId = SeasonId,
            GameType = gameType,
            GameDate = new DateOnly(2025, 10, 8).AddDays(dayOffset),
            HomeTeamId = homeTeamId,
            AwayTeamId = awayTeamId,
            HomeScore = homeScore,
            AwayScore = awayScore,
            LastPeriodType = lastPeriodType,
            GameState = "OFF",
        };
        Db.Games.Add(game);
        return game;
    }

    protected void AddSkaterLine(
        long gameId, int playerId, int teamId,
        int goals = 0, int assists = 0, int hits = 0, int shots = 0,
        int blockedShots = 0, int pim = 0, int plusMinus = 0, int toiSeconds = 1200)
    {
        Db.SkaterGameStats.Add(new SkaterGameStat
        {
            GameId = gameId,
            PlayerId = playerId,
            TeamId = teamId,
            Goals = goals,
            Assists = assists,
            Points = goals + assists,
            Hits = hits,
            Shots = shots,
            BlockedShots = blockedShots,
            Pim = pim,
            PlusMinus = plusMinus,
            TimeOnIceSeconds = toiSeconds,
        });
    }

    protected void AddGoalieLine(
        long gameId, int playerId, int teamId,
        int saves, int shotsAgainst, int toiSeconds = 3600, bool starter = true)
    {
        Db.GoalieGameStats.Add(new GoalieGameStat
        {
            GameId = gameId,
            PlayerId = playerId,
            TeamId = teamId,
            Saves = saves,
            ShotsAgainst = shotsAgainst,
            GoalsAgainst = shotsAgainst - saves,
            TimeOnIceSeconds = toiSeconds,
            Starter = starter,
        });
    }

    protected void AddTeamLine(long gameId, int teamId, int opponentId, bool isHome, string result, int points,
        int goalsFor = 3, int goalsAgainst = 2)
    {
        Db.TeamGameStats.Add(new TeamGameStat
        {
            GameId = gameId,
            TeamId = teamId,
            OpponentTeamId = opponentId,
            IsHome = isHome,
            Result = result,
            Points = points,
            GoalsFor = goalsFor,
            GoalsAgainst = goalsAgainst,
        });
    }

    protected Task SaveAsync() => Db.SaveChangesAsync();
}
