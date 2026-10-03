using AlenAlex.Api.Features.Guestbook.ListEntries;
using AlenAlex.Api.Features.Guestbook.CreateEntry;
using AlenAlex.Api.Infrastructure.Persistence;
using AlenAlex.Api.IntegrationTests.Support;

namespace AlenAlex.Api.IntegrationTests.Persistence;

public sealed class MigrationTests
{
    // The production database as first created, including its _sqlx_migrations table.
    private const string LegacySchema =
        """
        CREATE TABLE _sqlx_migrations (
            version BIGINT PRIMARY KEY, description TEXT NOT NULL,
            installed_on TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            success BOOLEAN NOT NULL, checksum BLOB NOT NULL, execution_time BIGINT NOT NULL);
        CREATE TABLE guestbook_entries (
            id TEXT PRIMARY KEY, name TEXT NOT NULL, message TEXT NOT NULL,
            status TEXT NOT NULL DEFAULT 'pending', likes INTEGER NOT NULL DEFAULT 0,
            ip_hash TEXT NOT NULL, rejection_reason TEXT,
            created_at TEXT NOT NULL DEFAULT (datetime('now')));
        CREATE TABLE guestbook_likes (
            entry_id TEXT NOT NULL REFERENCES guestbook_entries(id) ON DELETE CASCADE,
            ip_hash TEXT NOT NULL, created_at TEXT NOT NULL DEFAULT (datetime('now')),
            PRIMARY KEY (entry_id, ip_hash));
        INSERT INTO _sqlx_migrations VALUES (20260711001, 'initial migration', '2026-07-15 17:55:01', 1, x'00', 1);
        """;

    [Fact]
    public void Migrations_are_listed_explicitly_with_versions()
    {
        var versions = DatabaseMigrator.All()
            .Select(m => ((FluentMigrator.MigrationAttribute)Attribute.GetCustomAttribute(m.GetType(), typeof(FluentMigrator.MigrationAttribute))!).Version);
        Assert.Equal([20260711001L, 20261002001L], versions);
    }

    [Fact]
    public async Task Fresh_database_gets_the_full_schema_and_rerun_is_a_no_op()
    {
        await using var db = TestDatabase.Create();
        db.Migrate();

        Assert.Equal(2L, await db.ScalarAsync("SELECT COUNT(*) FROM VersionInfo"));
        Assert.Equal(1L, await db.ScalarAsync("SELECT COUNT(*) FROM pragma_table_info('guestbook_entries') WHERE name = 'seq'"));
        Assert.Equal(3L, await db.ScalarAsync("SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name LIKE 'idx_guestbook_entries_%'"));
        Assert.Equal(1L, await db.ScalarAsync("SELECT COUNT(*) FROM pragma_foreign_key_list('guestbook_likes') WHERE \"table\" = 'guestbook_entries' AND on_delete = 'CASCADE'"));
        Assert.Equal(2L, await db.ScalarAsync("SELECT COUNT(*) FROM pragma_table_info('guestbook_likes') WHERE pk > 0"));
    }

    [Fact]
    public async Task Upgrades_the_original_database()
    {
        await using var db = TestDatabase.Create(migrate: false);
        await db.ExecuteAsync(LegacySchema);
        await db.ExecuteAsync(
            """
            INSERT INTO guestbook_entries (id, name, message, status, ip_hash, created_at) VALUES
              ('a', 'Ada', 'first', 'pending', 'h1', '2026-07-15T17:55:09.519545+00:00'),
              ('b', 'Bob', 'second', 'accepted', 'h2', '2026-07-15 18:00:00'),
              ('c', 'Cy', 'third', '', 'h3', '2026-07-15T18:05:00.000000+00:00');
            INSERT INTO guestbook_likes (entry_id, ip_hash) VALUES ('b', 'h1');
            """);

        db.Migrate();

        Assert.Equal(0L, await db.ScalarAsync("SELECT COUNT(*) FROM guestbook_entries WHERE status IN ('pending', '')"));
        Assert.Equal(3L, await db.ScalarAsync("SELECT seq FROM guestbook_entries WHERE id = 'c'"));

        var asH1 = await new ListEntriesHandler(db.UnitOfWork).HandleAsync("h1", TestContext.Current.CancellationToken);
        Assert.Equal(["b", "a"], asH1.Select(e => e.Id));
        Assert.Equal((1L, true), (asH1[0].LikeCount, asH1[0].Liked));

        // New entries continue the sequence.
        var created = await new CreateEntryHandler(db.UnitOfWork, TimeProvider.System).HandleAsync(new CreateEntryRequest("Dee", "fourth"), "h4", TestContext.Current.CancellationToken);
        Assert.Equal(4, created.Seq);
    }

    [Fact]
    public async Task Accepts_a_database_with_an_existing_migration_table()
    {
        await using var db = TestDatabase.Create(migrate: false);
        await db.ExecuteAsync(LegacySchema);
        await db.ExecuteAsync(
            """
            ALTER TABLE guestbook_entries ADD COLUMN seq INTEGER;
            CREATE UNIQUE INDEX idx_guestbook_entries_seq ON guestbook_entries (seq);
            CREATE INDEX idx_guestbook_entries_status_created ON guestbook_entries (status, created_at);
            CREATE INDEX idx_guestbook_entries_ip_created ON guestbook_entries (ip_hash, created_at);
            INSERT INTO _sqlx_migrations VALUES (20261002001, 'normalize status and seq', '2026-10-02 20:24:35', 1, x'00', 1);
            INSERT INTO guestbook_entries (id, seq, name, message, status, ip_hash) VALUES ('x', 7, 'Ada', 'hello', 'accepted', 'h');
            """);

        db.Migrate();

        Assert.Equal(2L, await db.ScalarAsync("SELECT COUNT(*) FROM VersionInfo"));
        Assert.Equal(7L, await db.ScalarAsync("SELECT seq FROM guestbook_entries WHERE id = 'x'"));
    }

    [Fact]
    public async Task Tolerates_an_upgraded_schema_without_any_bookkeeping()
    {
        await using var db = TestDatabase.Create(migrate: false);
        await db.ExecuteAsync(
            """
            CREATE TABLE guestbook_entries (
                id TEXT PRIMARY KEY, name TEXT NOT NULL, message TEXT NOT NULL,
                status TEXT NOT NULL DEFAULT 'pending', likes INTEGER NOT NULL DEFAULT 0,
                ip_hash TEXT NOT NULL, rejection_reason TEXT,
                created_at TEXT NOT NULL DEFAULT (datetime('now')), seq INTEGER);
            """);

        db.Migrate();

        Assert.Equal(2L, await db.ScalarAsync("SELECT COUNT(*) FROM VersionInfo"));
        Assert.Equal(1L, await db.ScalarAsync("SELECT COUNT(*) FROM sqlite_master WHERE name = 'guestbook_likes'"));
    }
}
