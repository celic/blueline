using Blueline.Core.Entities;
using Blueline.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Blueline.Tests;

/// <summary>
/// SQLite connection settings for concurrent access. These need a real file on disk: an
/// in-memory database reports its journal mode as "memory" and cannot show contention at all.
/// </summary>
public class SqliteConcurrencyTests
{
    private string _dbPath = "";
    private string _directory = "";

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "blueline-wal", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _dbPath = Path.Combine(_directory, "blueline.db");
    }

    [TearDown]
    public void TearDown()
    {
        // Pooled connections keep a handle on the file, which would block deleting it.
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A stray handle is not worth failing a test over.
        }
    }

    private BluelineDbContext NewContext(bool withInterceptor = true, int busyTimeoutMs = 5000)
    {
        var builder = new DbContextOptionsBuilder<BluelineDbContext>()
            .UseSqlite($"Data Source={_dbPath}");

        if (withInterceptor) builder.AddInterceptors(new SqliteConnectionInterceptor(busyTimeoutMs));

        return new BluelineDbContext(builder.Options);
    }

    private static string ScalarPragma(BluelineDbContext db, string pragma)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA {pragma};";
        return command.ExecuteScalar()?.ToString() ?? "";
    }

    private void CreateSchemaWithOneTeam()
    {
        using var setup = NewContext();
        setup.Database.EnsureCreated();
        setup.Teams.Add(new Team { Id = 21, Abbrev = "HME", Name = "Home Club" });
        setup.SaveChanges();
    }

    [Test]
    public void A_file_database_uses_write_ahead_logging()
    {
        using var db = NewContext();
        db.Database.EnsureCreated();

        Assert.That(ScalarPragma(db, "journal_mode"), Is.EqualTo("wal").IgnoreCase);
    }

    [Test]
    public void Write_ahead_logging_is_the_providers_default_so_the_interceptor_only_confirms_it()
    {
        // Recorded deliberately: EF Core's SQLite provider already selects WAL, unlike raw
        // Microsoft.Data.Sqlite which defaults to the blocking rollback journal. If a future
        // provider version changes this, this test fails and the interceptor becomes load
        // bearing rather than belt and braces.
        using var db = NewContext(withInterceptor: false);
        db.Database.EnsureCreated();

        Assert.That(ScalarPragma(db, "journal_mode"), Is.EqualTo("wal").IgnoreCase);
    }

    [Test]
    public void Without_the_interceptor_a_connection_waits_no_time_at_all_for_a_lock()
    {
        // The gap the interceptor closes. Zero here does not mean "fail on contact" — the
        // provider still retries at its own level — it means SQLite never sleeps on the lock,
        // so the retrying happens as a busy spin rather than an efficient wait.
        using var db = NewContext(withInterceptor: false);
        db.Database.EnsureCreated();

        Assert.That(ScalarPragma(db, "busy_timeout"), Is.EqualTo("0"));
    }

    [Test]
    public void The_busy_timeout_is_applied_to_every_connection()
    {
        using var db = NewContext(busyTimeoutMs: 1234);
        db.Database.EnsureCreated();

        Assert.That(ScalarPragma(db, "busy_timeout"), Is.EqualTo("1234"));
    }

    [Test]
    public void Synchronous_is_set_to_normal()
    {
        using var db = NewContext();
        db.Database.EnsureCreated();

        // 1 is NORMAL; 2 would be the provider default FULL.
        Assert.That(ScalarPragma(db, "synchronous"), Is.EqualTo("1"));
    }

    [Test]
    public void Write_ahead_logging_survives_reopening_the_database()
    {
        using (var setup = NewContext()) setup.Database.EnsureCreated();

        // journal_mode is persisted in the file itself, so a fresh connection inherits it.
        using var reopened = NewContext();
        Assert.That(ScalarPragma(reopened, "journal_mode"), Is.EqualTo("wal").IgnoreCase);
    }

    [Test]
    public void A_page_read_is_not_blocked_by_an_uncommitted_ingestion_write()
    {
        // The site's real read path: a query with no explicit transaction, running while the
        // daily job holds a write open.
        CreateSchemaWithOneTeam();

        using var writer = NewContext();
        using var writerTransaction = writer.Database.BeginTransaction();
        writer.Teams.Add(new Team { Id = 22, Abbrev = "AWY", Name = "Away Club" });
        writer.SaveChanges();

        using var reader = NewContext(busyTimeoutMs: 1000);

        var visible = 0;
        Assert.DoesNotThrow(() => visible = reader.Teams.Count(),
            "a page load during an in-flight ingestion write must still render");

        // A write-ahead log reader sees the last committed snapshot, not the pending row.
        Assert.That(visible, Is.EqualTo(1));

        writerTransaction.Commit();
    }

    [Test]
    public void A_writer_blocked_indefinitely_eventually_errors_rather_than_hanging_forever()
    {
        // Contention does not fail on contact: Microsoft.Data.Sqlite retries a busy database
        // until the command timeout expires, which defaults to a full 30 seconds. busy_timeout
        // decides how each attempt waits — an efficient sleep rather than a spin — while the
        // command timeout is what actually bounds the total. Shortened here to keep the test
        // quick; the point is that a permanently held lock surfaces as an error, not a hang.
        CreateSchemaWithOneTeam();

        using var holder = NewContext();
        using var holderTransaction = holder.Database.BeginTransaction();
        holder.Teams.Add(new Team { Id = 22, Abbrev = "AWY", Name = "Away Club" });
        holder.SaveChanges();

        var builder = new DbContextOptionsBuilder<BluelineDbContext>()
            .UseSqlite($"Data Source={_dbPath}", o => o.CommandTimeout(1))
            .AddInterceptors(new SqliteConnectionInterceptor(busyTimeoutMilliseconds: 200));

        using var contender = new BluelineDbContext(builder.Options);
        contender.Teams.Add(new Team { Id = 23, Abbrev = "OTH", Name = "Other Club" });

        Assert.Throws<DbUpdateException>(() => contender.SaveChanges());

        holderTransaction.Commit();
    }

    [Test]
    public void A_second_writer_waits_out_a_brief_lock_when_the_busy_timeout_is_set()
    {
        CreateSchemaWithOneTeam();

        using var holder = NewContext();
        var holderTransaction = holder.Database.BeginTransaction();
        holder.Teams.Add(new Team { Id = 22, Abbrev = "AWY", Name = "Away Club" });
        holder.SaveChanges();

        // The lock clears shortly, as a real ingestion batch would.
        var release = Task.Run(async () =>
        {
            await Task.Delay(200);
            holderTransaction.Commit();
            holderTransaction.Dispose();
        });

        using var contender = NewContext(busyTimeoutMs: 5000);
        contender.Teams.Add(new Team { Id = 23, Abbrev = "OTH", Name = "Other Club" });

        Assert.DoesNotThrow(() => contender.SaveChanges(),
            "the timeout should absorb a short-lived lock instead of surfacing an error");

        release.GetAwaiter().GetResult();
        Assert.That(contender.Teams.Count(), Is.EqualTo(3));
    }
}
