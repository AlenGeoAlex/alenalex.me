using FluentMigrator;
using FluentMigrator.Postgres;

namespace AlenAlex.Api.Infrastructure.Persistence.Migrations;

/// <summary>The owner's reactions on guestbook entries, and the last few Spotify tracks.</summary>
[Migration(20261005001, "Guestbook reactions and listening history")]
public sealed class M20261005001_ReactionsAndListening : Migration
{
    public override void Up()
    {
        // No CHECK on the key: the list of reactions lives in code (GuestbookReactions) so it can grow.
        Create.Table("guestbook_reactions")
            .WithColumn("entry_id").AsString().NotNullable().PrimaryKey("pk_guestbook_reactions")
                .ForeignKey("fk_guestbook_reactions_entry", "guestbook_entries", "id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("reaction").AsString().NotNullable().PrimaryKey("pk_guestbook_reactions")
            .WithColumn("created_at").AsDateTimeOffset().NotNullable().WithDefaultValue(RawSql.Insert("now()"));

        Create.Table("listening_history")
            .WithColumn("id").AsInt64().NotNullable().PrimaryKey("pk_listening_history").Identity(PostgresGenerationType.Always)
            .WithColumn("track").AsString().NotNullable()
            .WithColumn("artist").AsString().NotNullable()
            .WithColumn("album").AsString().Nullable()
            .WithColumn("art_url").AsString().Nullable()
            .WithColumn("played_at").AsDateTimeOffset().NotNullable();

        Create.Index("ix_listening_history_played").OnTable("listening_history").OnColumn("played_at").Descending();
    }

    public override void Down()
    {
        Delete.Table("listening_history");
        Delete.Table("guestbook_reactions");
    }
}
