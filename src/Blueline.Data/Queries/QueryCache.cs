using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Blueline.Data.Queries;

/// <summary>
/// Holds the aggregates — leaderboards, streak boards, standings — and nothing else.
///
/// The split is not arbitrary. Measured against the real two-season database, every leaderboard and
/// every streak board for both seasons and both scopes comes to about 0.7 MB of JSON, while each
/// costs 40-90 ms to compute. Caching every player's trend would take 0.56 GB to speed up the
/// cheapest queries in the system — one player, eighty-two rows, no aggregation. So the expensive
/// things are small and the big things are cheap, which decides what belongs here.
///
/// **Invalidation cannot rely on being told.** Ingestion runs in a separate process now, so nothing
/// writing to the database can notify a cache living in the web app. Instead every key carries a
/// version token read from the database itself — the newest ingestion run and the newest game.
/// A run that revises yesterday's box scores changes the first; an archive import changes the
/// second. When either moves, every old key is unreachable and falls out on its own.
/// </summary>
public sealed class QueryCache : IDisposable
{
    /// <summary>
    /// Entries, not bytes. A few hundred aggregates at a few KB each is single-digit MB, so the
    /// limit is here to bound a mistake rather than to manage memory.
    /// </summary>
    public const int MaxEntries = 500;

    /// <summary>
    /// How long a cached figure may outlive the write that changed it.
    ///
    /// The version token is itself read from the database, and reading it on every call would put
    /// two queries in front of every cache hit. Ten seconds keeps that lookup shared across the
    /// dashboard's panels while bounding staleness to less than a page reload.
    /// </summary>
    private static readonly TimeSpan DefaultVersionWindow = TimeSpan.FromSeconds(10);

    /// <summary>Rarely-touched entries fall out by themselves, which is what "older seasons are rarely retrieved" wanted.</summary>
    private static readonly TimeSpan IdleExpiry = TimeSpan.FromMinutes(30);

    private const string VersionKey = "data-version";

    private readonly MemoryCache _entries = new(new MemoryCacheOptions { SizeLimit = MaxEntries });
    private readonly TimeSpan _versionWindow;

    public QueryCache() : this(DefaultVersionWindow)
    {
    }

    /// <summary>
    /// Tests pass <see cref="TimeSpan.Zero"/> so the token is re-read every call. Otherwise a test
    /// that writes and immediately re-reads would be asserting on the holding window rather than on
    /// invalidation, and would have to sleep through it to find out.
    /// </summary>
    internal QueryCache(TimeSpan versionWindow) => _versionWindow = versionWindow;

    /// <summary>
    /// Hits and misses since start. Nothing displays them; they exist so a test can assert that a
    /// second identical request did not reach the database, which is otherwise invisible.
    /// </summary>
    public long Hits { get; private set; }

    public long Misses { get; private set; }

    public async Task<T> GetOrCreateAsync<T>(
        BluelineDbContext db, string key, Func<Task<T>> load, CancellationToken ct = default)
    {
        var versioned = $"{await VersionAsync(db, ct)}|{key}";

        if (_entries.TryGetValue(versioned, out T? cached) && cached is not null)
        {
            Hits++;
            return cached;
        }

        Misses++;
        var value = await load();

        _entries.Set(versioned, value, new MemoryCacheEntryOptions
        {
            Size = 1,
            SlidingExpiration = IdleExpiry,
        });

        return value;
    }

    /// <summary>
    /// What the database looks like right now, cheaply: the newest ingestion run and the newest
    /// game, both indexed lookups.
    ///
    /// Deliberately one token for the whole database rather than one per season. A finished season
    /// cannot change, so a per-season token would keep its entries across a nightly run — but it
    /// would also mean reasoning about which seasons are finished, and being wrong about that
    /// serves stale figures. One token costs a recomputation per stat after each run, which is
    /// 40-90 ms once, and is impossible to get wrong.
    /// </summary>
    private async Task<string> VersionAsync(BluelineDbContext db, CancellationToken ct)
    {
        var holding = _versionWindow > TimeSpan.Zero;

        if (holding && _entries.TryGetValue(VersionKey, out string? cached) && cached is not null) return cached;

        var lastRun = await db.IngestionRuns.MaxAsync(r => (int?)r.Id, ct) ?? 0;
        var lastGame = await db.Games.MaxAsync(g => (long?)g.Id, ct) ?? 0;

        var version = $"{lastRun}-{lastGame}";

        // A zero window means "hold nothing" — MemoryCache rejects a zero expiry rather than
        // treating it as immediate, so the entry is simply not written.
        if (holding)
        {
            _entries.Set(VersionKey, version, new MemoryCacheEntryOptions
            {
                Size = 1,
                AbsoluteExpirationRelativeToNow = _versionWindow,
            });
        }

        return version;
    }

    public void Dispose() => _entries.Dispose();
}
