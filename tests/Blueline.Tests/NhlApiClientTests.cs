using System.Net;
using System.Text;
using Blueline.Ingestion.Nhl;
using Microsoft.Extensions.Logging.Abstractions;

namespace Blueline.Tests;

/// <summary>
/// The client's contract is that a bad response never throws — a single unreachable game must
/// not abandon a 1,400-game backfill. These pin the failure paths, which are otherwise only
/// exercised when the league's API misbehaves.
/// </summary>
public class NhlApiClientTests
{
    private sealed class Responder(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(respond(request));
        }
    }

    private static NhlApiClient Client(Responder responder) =>
        new(new HttpClient(responder) { BaseAddress = new Uri(NhlApiClient.DefaultBaseAddress) },
            NullLogger<NhlApiClient>.Instance);

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Test]
    public async Task A_successful_response_is_deserialised()
    {
        var client = Client(new Responder(_ => Json("""{"currentDate":"2026-01-15","games":[]}""")));

        var score = await client.GetScoreAsync(new DateOnly(2026, 1, 15), default);

        Assert.That(score!.CurrentDate, Is.EqualTo("2026-01-15"));
    }

    [Test]
    public async Task A_404_returns_null_rather_than_throwing()
    {
        // Routine: the league 404s for dates and seasons that do not exist.
        var client = Client(new Responder(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));

        Assert.That(await client.GetBoxscoreAsync(2025020001, default), Is.Null);
    }

    [Test]
    public async Task A_server_error_returns_null_rather_than_throwing()
    {
        var client = Client(new Responder(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        Assert.That(await client.GetBoxscoreAsync(2025020001, default), Is.Null);
    }

    [Test]
    public async Task Malformed_json_returns_null_rather_than_throwing()
    {
        var client = Client(new Responder(_ => Json("{ this is not json")));

        Assert.That(await client.GetScoreAsync(new DateOnly(2026, 1, 15), default), Is.Null);
    }

    [Test]
    public async Task A_response_of_the_wrong_shape_returns_null_rather_than_throwing()
    {
        // Valid JSON, but not the object the caller expects — what an API redesign looks like.
        var client = Client(new Responder(_ => Json("""[1, 2, 3]""")));

        Assert.That(await client.GetScoreAsync(new DateOnly(2026, 1, 15), default), Is.Null);
    }

    [Test]
    public async Task A_transport_failure_returns_null_rather_than_throwing()
    {
        var client = Client(new Responder(_ => throw new HttpRequestException("connection reset")));

        Assert.That(await client.GetBoxscoreAsync(2025020001, default), Is.Null);
    }

    [Test]
    public void Cancellation_propagates_instead_of_being_swallowed()
    {
        // A shutdown must actually stop the run, not be mistaken for a bad response.
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var client = Client(new Responder(_ => throw new OperationCanceledException()));

        // CatchAsync, not ThrowsAsync: HttpClient surfaces this as TaskCanceledException, a
        // subclass, and the point is that cancellation escapes at all rather than its exact type.
        Assert.CatchAsync<OperationCanceledException>(
            async () => await client.GetBoxscoreAsync(2025020001, cts.Token));
    }

    [Test]
    public async Task Requests_are_addressed_relative_to_the_league_api()
    {
        HttpRequestMessage? seen = null;
        var responder = new Responder(r => { seen = r; return Json("""{"standings":[]}"""); });

        await Client(responder).GetStandingsAsync(new DateOnly(2026, 4, 1), default);

        Assert.That(seen!.RequestUri!.ToString(), Is.EqualTo("https://api-web.nhle.com/v1/standings/2026-04-01"));
    }

    [Test]
    public async Task Dates_are_formatted_the_way_the_league_expects()
    {
        HttpRequestMessage? seen = null;
        var responder = new Responder(r => { seen = r; return Json("""{"games":[]}"""); });

        // A single-digit month and day must still be zero padded.
        await Client(responder).GetScoreAsync(new DateOnly(2026, 3, 7), default);

        Assert.That(seen!.RequestUri!.AbsolutePath, Is.EqualTo("/v1/score/2026-03-07"));
    }
}
