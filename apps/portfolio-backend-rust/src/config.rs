use std::env::VarError;
use anyhow::{Context, Result};

#[derive(Debug, Clone, Hash, Eq, PartialEq)]
pub struct AppConfig {
    pub database_url: String,
    pub ip_header: Option<String>,
    pub hashing_salt : String
}

impl AppConfig {
    pub fn init() -> anyhow::Result<Self> {
        Ok(Self {
            database_url: get_string_or_default("DATABASE_URL", "sqlite://guestbook.db?mode=rwc".to_string())?,
            ip_header: get_optional_string("IP_HEADER")?,
            hashing_salt : get_required_string("HASHING_SALT")?
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