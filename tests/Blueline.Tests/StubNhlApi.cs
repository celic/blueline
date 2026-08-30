using System.Net;
using System.Text;

namespace Blueline.Tests;

/// <summary>
/// Serves canned league API responses by path so ingestion can be exercised without a network.
/// Anything not registered comes back as 404, which the client treats as "nothing there".
/// </summary>
public class StubNhlApi : HttpMessageHandler
{
    private readonly Dictionary<string, string> _responses = new(StringComparer.OrdinalIgnoreCase);

    public List<string> RequestedPaths { get; } = [];

    public StubNhlApi Add(string path, string json)
    {
        _responses[path] = json;
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri!.AbsolutePath.TrimStart('/');
        if (path.StartsWith("v1/", StringComparison.OrdinalIgnoreCase)) path = path[3..];

        RequestedPaths.Add(path);

        if (!_responses.TryGetValue(path, out var json))
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });
    }

    /// <summary>A game the away side wins in regulation, with two skaters and a goalie per side.</summary>
    public static string Boxscore(
        long gameId,
        string gameDate,
        int homeTeamId,
        string homeAbbrev,
        int awayTeamId,
        string awayAbbrev,
        int homeScore,
        int awayScore,
        string lastPeriodType = "REG",
        int homeSkaterGoals = 1,
        int gameType = 2) => $$"""
        {
          "id": {{gameId}},
          "season": 20252026,
          "gameType": {{gameType}},
          "gameDate": "{{gameDate}}",
          "gameState": "OFF",
          "awayTeam": { "id": {{awayTeamId}}, "commonName": { "default": "Away Club" },
                        "placeName": { "default": "Awayville" }, "abbrev": { "default": "{{awayAbbrev}}" },
                        "score": {{awayScore}}, "sog": 30, "logo": "https://example.test/{{awayAbbrev}}.svg" },
          "homeTeam": { "id": {{homeTeamId}}, "commonName": { "default": "Home Club" },
                        "placeName": { "default": "Hometown" }, "abbrev": { "default": "{{homeAbbrev}}" },
                        "score": {{homeScore}}, "sog": 28, "logo": "https://example.test/{{homeAbbrev}}.svg" },
          "gameOutcome": { "lastPeriodType": "{{lastPeriodType}}" },
          "playerByGameStats": {
            "awayTeam": {
              "forwards": [ { "playerId": 100, "name": { "default": "A. Forward" }, "position": "C",
                              "goals": 1, "assists": 1, "points": 2, "plusMinus": 1, "pim": 0, "hits": 3,
                              "blockedShots": 1, "sog": 4, "powerPlayGoals": 0, "giveaways": 1,
                              "takeaways": 2, "shifts": 22, "toi": "18:30", "faceoffWinningPctg": 0.55 } ],
              "defense": [],
              "goalies": [ { "playerId": 200, "name": { "default": "A. Goalie" }, "shotsAgainst": 28,
                             "saves": 27, "goalsAgainst": 1, "pim": 0, "toi": "60:00", "starter": true } ]
            },
            "homeTeam": {
              "forwards": [ { "playerId": 101, "name": { "default": "H. Forward" }, "position": "L",
                              "goals": {{homeSkaterGoals}}, "assists": 0, "points": {{homeSkaterGoals}},
                              "plusMinus": -1, "pim": 2, "hits": 1, "blockedShots": 0, "sog": 3,
                              "powerPlayGoals": 0, "giveaways": 0, "takeaways": 1, "shifts": 20,
                              "toi": "16:00", "faceoffWinningPctg": 0.0 } ],
              "defense": [],
              "goalies": [ { "playerId": 201, "name": { "default": "H. Goalie" }, "shotsAgainst": 30,
                             "saves": 28, "goalsAgainst": 2, "pim": 0, "toi": "59:00", "starter": true } ]
            }
          }
        }
        """;

    /// <summary>The shape of /v1/player/{playerId}/landing.</summary>
    public static string PlayerLanding(int playerId, string first, string last, string position = "C") => $$"""
        {
          "playerId": {{playerId}},
          "firstName": { "default": "{{first}}" },
          "lastName": { "default": "{{last}}" },
          "position": "{{position}}",
          "headshot": "https://example.test/landing/{{playerId}}.png"
        }
        """;

    /// <summary>The shape of /v1/standings/{date}, which is only read for team abbreviations.</summary>
    public static string Standings(params string[] abbrevs)
    {
        var rows = abbrevs.Select(a => $$"""
            { "teamAbbrev": { "default": "{{a}}" }, "teamName": { "default": "{{a}} Club" },
              "teamLogo": "https://example.test/{{a}}.svg" }
            """);

        return $$"""{ "standings": [{{string.Join(",", rows)}}] }""";
    }

    /// <summary>
    /// The shape of /v1/club-schedule-season/{team}/{season}. Like /score, abbrev here is a bare
    /// string rather than a locale object.
    /// </summary>
    public static string ClubSchedule(params (long GameId, int GameType, string Date, int HomeId, string HomeAbbrev, int AwayId, string AwayAbbrev)[] games)
    {
        var entries = games.Select(g => $$"""
            { "id": {{g.GameId}}, "season": 20252026, "gameType": {{g.GameType}},
              "gameDate": "{{g.Date}}", "gameState": "OFF",
              "homeTeam": { "id": {{g.HomeId}}, "abbrev": "{{g.HomeAbbrev}}" },
              "awayTeam": { "id": {{g.AwayId}}, "abbrev": "{{g.AwayAbbrev}}" } }
            """);

        return $$"""{ "games": [{{string.Join(",", entries)}}] }""";
    }

    /// <summary>
    /// A club's season roster, which is where full first and last names come from. Box scores
    /// only ever carry an abbreviated "A. Forward".
    /// </summary>
    public static string ClubStats(params (int PlayerId, string First, string Last, string Position)[] players)
    {
        static string Entry((int PlayerId, string First, string Last, string Position) p) => $$"""
            { "playerId": {{p.PlayerId}}, "firstName": { "default": "{{p.First}}" },
              "lastName": { "default": "{{p.Last}}" }, "positionCode": "{{p.Position}}",
              "headshot": "https://example.test/{{p.PlayerId}}.png" }
            """;

        var skaters = players.Where(p => p.Position != "G").Select(Entry);
        var goalies = players.Where(p => p.Position == "G").Select(Entry);

        return $$"""
            { "skaters": [{{string.Join(",", skaters)}}], "goalies": [{{string.Join(",", goalies)}}] }
            """;
    }

    /// <summary>The shape of /v1/score/{date}, where abbrev is a bare string rather than an object.</summary>
    public static string Score(string date, params long[] gameIds) => ScoreOfType(date, 2, gameIds);

    /// <summary>
    /// Deliberately not an overload of <see cref="Score"/>: game ids fit in an int, so
    /// <c>Score(date, 2025020001)</c> would silently bind the id as the game type.
    /// </summary>
    public static string ScoreOfType(string date, int gameType, params long[] gameIds)
    {
        var games = string.Join(",", gameIds.Select(id => $$"""
            { "id": {{id}}, "season": 20252026, "gameType": {{gameType}}, "gameDate": "{{date}}", "gameState": "OFF",
              "awayTeam": { "id": 22, "abbrev": "AWY" }, "homeTeam": { "id": 21, "abbrev": "HME" } }
            """));

        return $$"""{ "currentDate": "{{date}}", "games": [{{games}}] }""";
    }
}
