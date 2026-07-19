use std::sync::Arc;
use serenity::all::{ChannelId, CreateEmbed, CreateMessage, EventHandler, GatewayIntents, GuildId, Http};
use serenity::Client;
use sqlx::{Pool, Sqlite};
use crate::handler::guestbook_bot_handler::GuestbookBotHandler;
use anyhow::Result;

pub struct DiscordService {
    client: Client
}
impl DiscordService {
    pub async fn create(token: &str, guild_id: u64, db_pool: Pool<Sqlite>) -> Result<Self> {
        let intents = GatewayIntents::GUILD_MESSAGES | GatewayIntents::MESSAGE_CONTENT;

        let handler = GuestbookBotHandler {
            db_pool,
            guild_id: GuildId::new(guild_id),
        };

        let client = Client::builder(token, intents)
            .event_handler(handler)
            .await?;

        Ok(DiscordService { client })
    }

    pub async fn start(&mut self) -> anyhow::Result<()> {
        self.client.start().await?;
        Ok(())
    }

    pub fn http(&self) -> Arc<Http> {
        self.client.http.clone()
    }

    pub async fn send_guestbook_entry_embed(
        http: &Http,
        channel_id: u64,
        id: &str,
        name: &str,
        message: &str,
        ip_hash: &str,
    ) -> Result<()> {
        let embed = CreateEmbed::new()
            .title("New Guestbook Entry")
            .field("Name", name, false)
            .field("Message", message, false)
            .field("IP Hash", ip_hash, false)
            .field("Entry ID", id, false)
            .footer(serenity::builder::CreateEmbedFooter::new(
                "Use /guestbook-accept or /guestbook-reject with this ID",
            ))
            .color(0xFFA500); // amber — "pending review"

        let builder = CreateMessage::new().embed(embed);

        ChannelId::new(channel_id)
            .send_message(http, builder)
            .await?;

        Ok(())
    }
}
