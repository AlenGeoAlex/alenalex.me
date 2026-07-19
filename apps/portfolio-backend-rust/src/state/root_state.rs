use std::sync::Arc;
use serenity::all::Http;
use sqlx::{Pool, Sqlite};
use crate::config::AppConfig;
use crate::state::app_state::AppState;
use crate::state::discord_state::DiscordState;

#[derive(Debug, Clone)]
pub struct RootState{
    pub app : AppState,
    pub discord : DiscordState
}

impl RootState {
    pub fn new(
        app_config: Arc<AppConfig>,
        db: Pool<Sqlite>,
        discord_http: Arc<Http>
    ) -> Self {
        Self {
            app: AppState::new(app_config, db),
            discord: DiscordState::new(discord_http)
        }
    }
}