use std::env::VarError;
use anyhow::{Context, Result};

#[derive(Debug, Clone, Hash, Eq, PartialEq)]
pub struct AppConfig {
    pub database_url: String,
    pub ip_header: Option<String>,
    pub hashing_salt : String,
    pub enable_scalar: bool,
    pub discord_bot_token: String,
    pub discord_guild_id: String,
    pub discord_channel_id: String,
    pub allowed_origins: Vec<String>,
}

impl AppConfig {
    pub fn init() -> anyhow::Result<Self> {
        Ok(Self {
            database_url: get_string_or_default("DATABASE_URL", "sqlite://guestbook.db?mode=rwc".to_string())?,
            ip_header: get_optional_string("IP_HEADER")?,
            hashing_salt : get_required_string("HASHING_SALT")?,
            enable_scalar: get_bool_or_default("ENABLE_SCALAR", false)?,
            discord_bot_token: get_required_string("DISCORD_BOT_TOKEN")?,
            discord_guild_id: get_required_string("DISCORD_GUILD_ID")?,
            discord_channel_id: get_required_string("DISCORD_CHANNEL_ID")?,
            allowed_origins: get_list_of_strings("ALLOWED_ORIGINS", ",")?,
        })
    }
}

fn get_required_string(key: &str) -> Result<String>{
    std::env::var(key)
        .with_context(|| format!("Failed to get required string for key: {}", key))
}

fn get_optional_string(key: &str) -> Result<Option<String>> {
    match std::env::var(key) {
        Ok(val) => Ok(Some(val)),
        Err(err) => {
            match err {
                VarError::NotPresent => {
                    Ok(None)
                }
                _ => Err(err.into())
            }
        }
    }
}

fn get_string_or_default(key: &str, default: String) -> anyhow::Result<String> {
    match std::env::var(key) {
        Ok(val) => Ok(val),
        Err(err) => {
            match err {
                VarError::NotPresent => Ok(default),
                _ => Err(err.into())
            }
        }
    }
}

fn get_bool_or_default(key: &str, default: bool) -> Result<bool> {
    match std::env::var(key) {
        Ok(val) => Ok(val.parse().unwrap_or(default)),
        Err(err) => {
            match err {
                VarError::NotPresent => Ok(default),
                _ => Err(err.into())
            }
        }
    }
}

fn get_list_of_strings(key: &str, sep: &str) -> Result<Vec<String>>
{
    match std::env::var(key) {
        Ok(val) => {
            Ok(val.split(sep).map(|s| s.to_string()).collect())
        }
        Err(err) => Err(err.into())
    }
        
}