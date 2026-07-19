use std::sync::Arc;
use axum::extract::FromRef;
use serenity::all::Http;
use crate::state::root_state::RootState;

#[derive(Debug, Clone)]
pub struct DiscordState {
    pub http: Arc<Http>,
}

impl DiscordState {
    pub fn new(discord_http: Arc<Http>) -> Self {
        return Self {
            http: discord_http
        }
    }
}

impl FromRef<RootState> for DiscordState {
    fn from_ref(input: &RootState) -> Self {
        return input.discord.clone()
    }
}