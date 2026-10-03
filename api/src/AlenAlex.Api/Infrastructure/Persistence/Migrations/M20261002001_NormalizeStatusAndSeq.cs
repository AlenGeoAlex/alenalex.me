using FluentMigrator;

namespace AlenAlex.Api.Infrastructure.Persistence.Migrations;

/// <summary>
/// Canonical statuses (<c>pending_approval | accepted | rejected</c>), a stable <c>seq</c> per
/// entry and indexes. Each step checks the schema first, because an existing database may
/// already have these changes or may predate them (no <c>seq</c> column).
/// </summary>
[Migration(20261002001, "Normalize status, add seq and indexes")]
public sealed class M20261002001_NormalizeStatusAndSeq : Migration
{
    public override void Up()
    {
        // Older rows may hold 'pending' or ''. The column default is still 'pending', but the
        // app always sets status explicitly.
        Execute.Sql("UPDATE guestbook_entries SET status = 'pending_approval' WHERE status IN ('pending', '')");

        if (!Schema.Table("guestbook_entries").Column("seq").Exists())
        {
            Alter.Table("guestbook_entries").AddColumn("seq").AsInt64().Nullable();
            // Backfill from rowid (insertion order). It needs its own column because VACUUM
            // may renumber rowids.
            IfDatabase(ProcessorIdConstants.SQLite).Execute.Sql("UPDATE guestbook_entries SET seq = rowid");
        }

        if (!Schema.Table("guestbook_entries").Index("idx_guestbook_entries_seq").Exists())
        {
            Create.Index("idx_guestbook_entries_seq").OnTable("guestbook_entries").OnColumn("seq").Unique();
        }
        if (!Schema.Table("guestbook_entries").Index("idx_guestbook_entries_status_created").Exists())
        {
            Create.Index("idx_guestbook_entries_status_created").OnTable("guestbook_entries")
                .OnColumn("status").Ascending().OnColumn("created_at").Ascending();
        }
        if (!Schema.Table("guestbook_entries").Index("idx_guestbook_entries_ip_created").Exists())
        {
            Create.Index("idx_guestbook_entries_ip_created").OnTable("guestbook_entries")
                .OnColumn("ip_hash").Ascending().OnColumn("created_at").Ascending();
        }
    }

    public override void Down()
    {
        Delete.Index("idx_guestbook_entries_ip_created").OnTable("guestbook_entries");
        Delete.Index("idx_guestbook_entries_status_created").OnTable("guestbook_entries");
        Delete.Index("idx_guestbook_entries_seq").OnTable("guestbook_entries");
        Delete.Column("seq").FromTable("guestbook_entries");
    }
}
