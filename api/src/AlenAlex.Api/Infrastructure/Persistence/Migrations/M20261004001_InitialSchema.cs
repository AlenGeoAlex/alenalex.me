using FluentMigrator;
using FluentMigrator.Postgres;

namespace AlenAlex.Api.Infrastructure.Persistence.Migrations;

/// <summary>The guestbook tables.</summary>
[Migration(20261004001, "Guestbook schema")]
public sealed class M20261004001_InitialSchema : Migration
{
    public override void Up()
    {
        Create.Table("guestbook_entries")
            .WithColumn("id").AsString().NotNullable().PrimaryKey("pk_guestbook_entries")
            // Shown on the site as gb·0042. Identity (BY DEFAULT) so imported rows keep their numbers.
            .WithColumn("seq").AsInt64().NotNullable().Identity(PostgresGenerationType.ByDefault).Unique("ux_guestbook_entries_seq")
            .WithColumn("name").AsString().NotNullable()
            .WithColumn("message").AsString().NotNullable()
            .WithColumn("status").AsString().NotNullable()
            .WithColumn("ip_hash").AsString().NotNullable()
            .WithColumn("rejection_reason").AsString().Nullable()
            .WithColumn("created_at").AsDateTimeOffset().NotNullable().WithDefaultValue(RawSql.Insert("now()"));

        Execute.Sql("ALTER TABLE guestbook_entries ADD CONSTRAINT ck_guestbook_entries_status CHECK (status IN ('pending_approval', 'accepted', 'rejected'))");

        Create.Index("ix_guestbook_entries_status_created").OnTable("guestbook_entries")
            .OnColumn("status").Ascending().OnColumn("created_at").Ascending();
        Create.Index("ix_guestbook_entries_ip_created").OnTable("guestbook_entries")
            .OnColumn("ip_hash").Ascending().OnColumn("created_at").Ascending();

        Create.Table("guestbook_likes")
            .WithColumn("entry_id").AsString().NotNullable().PrimaryKey("pk_guestbook_likes")
                .ForeignKey("fk_guestbook_likes_entry", "guestbook_entries", "id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("ip_hash").AsString().NotNullable().PrimaryKey("pk_guestbook_likes")
            .WithColumn("created_at").AsDateTimeOffset().NotNullable().WithDefaultValue(RawSql.Insert("now()"));
    }

    public override void Down()
    {
        Delete.Table("guestbook_likes");
        Delete.Table("guestbook_entries");
    }
}
