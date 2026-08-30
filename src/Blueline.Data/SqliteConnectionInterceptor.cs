using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Blueline.Data;

/// <summary>
/// Configures every SQLite connection to tolerate contention.
///
/// Blueline writes from a background ingestion job while Blazor circuits read on request
/// threads. The important setting here is <c>busy_timeout</c>: EF Core leaves it at zero, so a
/// statement that meets a held lock fails immediately with "database is locked" rather than
/// waiting the moment out. Plain reads do not contend with a writer under write-ahead logging,
/// but writers still serialise against each other — a manual refresh overlapping the daily job,
/// or either overlapping a WAL checkpoint — and with no timeout those lose instantly.
///
/// <c>journal_mode=WAL</c> is set here too. EF Core's SQLite provider already enables it, so
/// this is belt and braces rather than a change: it keeps the guarantee explicit and local
/// instead of resting on a provider default that could shift underneath us.
///
/// Applied per connection rather than once at startup because only one of these settings is
/// persistent: <c>journal_mode</c> is stored in the database file, but <c>busy_timeout</c> and
/// <c>synchronous</c> reset every time a connection is opened.
/// </summary>
public class SqliteConnectionInterceptor(int busyTimeoutMilliseconds = SqliteConnectionInterceptor.DefaultBusyTimeoutMilliseconds)
    : DbConnectionInterceptor
{
    /// <summary>
    /// How long a blocked statement waits for the lock before giving up. EF Core's default is
    /// zero, meaning no wait at all. Writes here are short — a batch of games, not a bulk load —
    /// so a few seconds is far more than any of them needs, while still failing rather than
    /// hanging if something genuinely deadlocks.
    /// </summary>
    public const int DefaultBusyTimeoutMilliseconds = 5000;

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) =>
        Configure(connection);

    public override Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Configure(connection);
        return Task.CompletedTask;
    }

    private void Configure(DbConnection connection)
    {
        // The connection string is overridable, so this must no-op against another provider.
        if (connection is not SqliteConnection) return;

        using var command = connection.CreateCommand();

        // journal_mode is a no-op once the file is already in WAL — which the provider has
        // usually done already — and in-memory databases report "memory" rather than failing.
        //
        // synchronous=NORMAL is the usual companion to WAL: it cannot corrupt the database, and
        // the worst a power loss costs is the last transaction or two. That is an easy trade
        // here because every row is re-derivable from the league API, and the daily job already
        // re-reads a lookback window.
        command.CommandText =
            $"""
            PRAGMA journal_mode=WAL;
            PRAGMA busy_timeout={busyTimeoutMilliseconds};
            PRAGMA synchronous=NORMAL;
            """;

        command.ExecuteNonQuery();
    }
}
