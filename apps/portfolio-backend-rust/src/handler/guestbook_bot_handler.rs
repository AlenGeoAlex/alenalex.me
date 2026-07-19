use serenity::all::{Context, CreateEmbed, CreateInteractionResponse, EventHandler, GuildId, Interaction, InteractionResponseFlags};
use serenity::async_trait;
use sqlx::{Pool, Sqlite};

pub struct GuestbookBotHandler {
    pub db_pool: Pool<Sqlite>,
    pub guild_id: GuildId,
}

#[async_trait]
impl EventHandler for GuestbookBotHandler {
    async fn ready(&self, ctx: Context, _ready: serenity::all::Ready) {
        tracing::info!("Discord bot connected, registering guild commands");

        let commands = self.guild_id
            .set_commands(&ctx.http, vec![
                build_accept_command(),
                build_reject_command(),
                build_pending_command(),
            ])
            .await;

        if let Err(err) = commands {
            tracing::error!("failed to register guild commands: {err:?}");
        }
    }

    async fn interaction_create(&self, ctx: Context, interaction: serenity::all::Interaction) {
        let Some(command) = interaction.command() else {
            return;
        };

        match command.data.name.as_str() {
            "guestbook-accept" => self.handle_accept(&ctx, &command).await,
            "guestbook-reject" => self.handle_reject(&ctx, &command).await,
            "guestbook-pending" => self.handle_pending(&ctx, &command).await,
            other => tracing::warn!("unknown command: {other}"),
        }
    }
}

use serenity::builder::{CreateCommand, CreateCommandOption, CreateEmbedFooter, CreateInteractionResponseMessage};
use serenity::all::CommandOptionType;

fn build_accept_command() -> CreateCommand {
    CreateCommand::new("guestbook-accept")
        .description("Approve a pending guestbook entry")
        .add_option(
            CreateCommandOption::new(CommandOptionType::String, "id", "The entry ID")
                .required(true),
        )
}

fn build_reject_command() -> CreateCommand {
    CreateCommand::new("guestbook-reject")
        .description("Reject a pending guestbook entry")
        .add_option(
            CreateCommandOption::new(CommandOptionType::String, "id", "The entry ID")
                .required(true),
        )
        .add_option(
            CreateCommandOption::new(CommandOptionType::String, "reason", "Reason for rejection")
                .required(false),
        )
}

fn build_pending_command() -> CreateCommand {
    CreateCommand::new("guestbook-pending")
        .description("List pending guestbook entries")
}

impl GuestbookBotHandler {
    async fn handle_accept(&self, ctx: &Context, command: &serenity::all::CommandInteraction) {
        let Some(id) = get_string_option(command, "id") else {
            respond_error(ctx, command, "Missing `id` option.").await;
            return;
        };

        match crate::service::guestbook_service::approve_entry(&self.db_pool, &id).await {
            Ok(_) => respond(ctx, command, &format!("Entry `{id}` approved.")).await,
            Err(err) => {
                tracing::error!("approve failed: {err:?}");
                respond_error(ctx, command, format!("Failed to approve entry: {err}").as_str()).await;
            }
        }
    }

    async fn handle_reject(&self, ctx: &Context, command: &serenity::all::CommandInteraction) {
        let Some(id) = get_string_option(command, "id") else {
            respond_error(ctx, command, "Missing `id` option.").await;
            return;
        };
        let reason = get_string_option(command, "reason"); // Option<String>, fine if absent

        match crate::service::guestbook_service::reject_entry(&self.db_pool, &id, reason).await {
            Ok(_) => respond(ctx, command, &format!("Entry `{id}` rejected.")).await,
            Err(err) => {
                tracing::error!("reject failed: {err:?}");
                respond_error(ctx, command, format!("Failed to approve entry: {err}").as_str()).await;
            }
        }
    }

    async fn handle_pending(&self, ctx: &Context, command: &serenity::all::CommandInteraction) {
        let result = {
            match crate::service::guestbook_service::list_pending_entries(&self.db_pool).await{
                Ok(entry) => entry,
                Err(err) => {
                    tracing::error!("list_entries failed: {err:?}");
                    respond(ctx, command, "Failed to list entries.").await;
                    return;
                }
            }
        };

        if result.len() == 0 {
            respond(ctx, command, "No pending entries.").await;
            return;
        }

        let mut embed = CreateEmbed::new()
            .title("Pending Entry")
            .footer(CreateEmbedFooter::new("Please accept or reject the entry with /guestbook-entry accept or /guestbook-entry reject."));

        for each in result {
            embed = embed.field(&each.id, &each.message, true);
        }

        if let Err(err) = command.create_response(&ctx.http, CreateInteractionResponse::Message(CreateInteractionResponseMessage::new().add_embed(embed))).await {
            tracing::error!("failed to respond to interaction: {err:?}");
            respond_error(ctx, command, "Failed to respond to interaction.").await;
            return;
        }
    }
}

fn get_string_option(command: &serenity::all::CommandInteraction, name: &str) -> Option<String> {
    command.data.options.iter()
        .find(|opt| opt.name == name)
        .and_then(|opt| opt.value.as_str())
        .map(|s| s.to_string())
}

async fn respond(ctx: &Context, command: &serenity::all::CommandInteraction, message: &str) {
    use serenity::builder::{CreateInteractionResponse, CreateInteractionResponseMessage};

    let data = CreateInteractionResponseMessage::new()
        .content(message);
    let builder = CreateInteractionResponse::Message(data);

    if let Err(err) = command.create_response(&ctx.http, builder).await {
        tracing::error!("failed to respond to interaction: {err:?}");
    }
}

async fn respond_error(ctx: &Context, command: &serenity::all::CommandInteraction, message: &str) {
    use serenity::builder::{CreateInteractionResponse, CreateInteractionResponseMessage};

    let data = CreateInteractionResponseMessage::new()
        .content(message);

    let builder = CreateInteractionResponse::Message(data);

    if let Err(err) = command.create_response(&ctx.http, builder).await {
        tracing::error!("failed to respond to interaction: {err:?}");
    }
}