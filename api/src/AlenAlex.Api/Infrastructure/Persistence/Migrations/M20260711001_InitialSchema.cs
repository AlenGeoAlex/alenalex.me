using FluentMigrator;

namespace AlenAlex.Api.Infrastructure.Persistence.Migrations;

/// <summary>
/// The production database already has these tables, so each one is only created if missing.
/// </summary>
[Migration(20260711001, "Initial guestbook schema")]
public sealed class M20260711001_InitialSchema : Migration
{
    public override void Up()
    {
        if (!Schema.Table("guestbook_entries").Exists())
        {
            Create.Table("guestbook_entries")
                .WithColumn("id").AsString().NotNullable().PrimaryKey()
                .WithColumn("name").AsString().NotNullable()
                .WithColumn("message").AsString().NotNullable()
                .WithColumn("status").AsString().NotNullable().WithDefaultValue("pending")
                .WithColumn("likes").AsInt32().NotNullable().WithDefaultValue(0)
                .WithColumn("ip_hash").AsString().NotNullable()
                .WithColumn("rejection_reason").AsString().Nullable()
                .WithColumn("created_at").AsString().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);
        }

        if (!Schema.Table("guestbook_likes").Exists())
        {
            Create.Table("guestbook_likes")
                .WithColumn("entry_id").AsString().NotNullable().PrimaryKey()
                    .ForeignKey("fk_guestbook_likes_entry", "guestbook_entries", "id").OnDelete(System.Data.Rule.Cascade)
                .WithColumn("ip_hash").AsString().NotNullable().PrimaryKey()
                .WithColumn("created_at").AsString().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);
        }
    }

    public override void Down()
    {
        Delete.Table("guestbook_likes");
        Delete.Table("guestbook_entries");
    }
}
