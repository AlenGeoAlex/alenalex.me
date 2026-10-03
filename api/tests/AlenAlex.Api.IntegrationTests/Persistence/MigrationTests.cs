using AlenAlex.Api.Infrastructure.Persistence;
using AlenAlex.Api.IntegrationTests.Support;

namespace AlenAlex.Api.IntegrationTests.Persistence;

public sealed class MigrationTests
{
    [Fact]
    public void Migrations_are_listed_explicitly_with_versions()
    {
        var versions = DatabaseMigrator.All()
            .Select(m => ((FluentMigrator.MigrationAttribute)Attribute.GetCustomAttribute(m.GetType(), typeof(FluentMigrator.MigrationAttribute))!).Version);
        Assert.Equal([20261004001L], versions);
    }

    [Fact]
    public async Task Fresh_database_gets_the_full_schema()
    {
        await using var db = TestDatabase.Create();

        Assert.Equal(1L, await db.ScalarAsync("SELECT COUNT(*) FROM \"VersionInfo\""));
        Assert.Equal("bigint",
            await db.ScalarAsync("SELECT data_type FROM information_schema.columns WHERE table_name = 'guestbook_entries' AND column_name = 'seq'"));
        // identity BY DEFAULT ('d'), so imported rows can keep their own seq
        Assert.Equal("d",
            (await db.ScalarAsync("SELECT attidentity::text FROM pg_attribute WHERE attrelid = 'guestbook_entries'::regclass AND attname = 'seq'"))?.ToString());
        Assert.Equal("timestamp with time zone",
            await db.ScalarAsync("SELECT data_type FROM information_schema.columns WHERE table_name = 'guestbook_entries' AND column_name = 'created_at'"));
        Assert.Equal(4L, await db.ScalarAsync("SELECT COUNT(*) FROM pg_indexes WHERE tablename = 'guestbook_entries'"));
        Assert.Equal(1L, await db.ScalarAsync("SELECT COUNT(*) FROM pg_constraint WHERE conname = 'ck_guestbook_entries_status'"));
        Assert.Equal("c", (await db.ScalarAsync("SELECT confdeltype FROM pg_constraint WHERE conname = 'fk_guestbook_likes_entry'"))?.ToString());
        Assert.Equal(2L, await db.ScalarAsync("SELECT COUNT(*) FROM information_schema.key_column_usage WHERE constraint_name = 'pk_guestbook_likes'"));
    }

    [Fact]
    public async Task Running_the_migrations_again_is_a_no_op()
    {
        await using var db = TestDatabase.Create();
        await db.ExecuteAsync("INSERT INTO guestbook_entries (id, name, message, status, ip_hash) VALUES ('x', 'Ada', 'hello', 'accepted', 'h')");

        db.Migrate();
        db.Migrate();

        Assert.Equal(1L, await db.ScalarAsync("SELECT COUNT(*) FROM \"VersionInfo\""));
        Assert.Equal(1L, await db.ScalarAsync("SELECT COUNT(*) FROM guestbook_entries"));
    }
}
