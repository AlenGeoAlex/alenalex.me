use std::env;
use crate::errors::ConfigErrors;


#[derive(Debug, Clone)]
pub struct GeneratorConfig{
    pub posts_dir: String,
    pub template_path: String,
    pub blogspec_output: String,
    pub script_output: String,
    pub posts_output_dir: String,

    pub r2_account_id: String,
    pub r2_access_key: String,
    pub r2_secret_key: String,
    pub r2_bucket: String,
    pub r2_public_url_base: String,

    pub changed_post_dirs: Option<Vec<String>>,
}

impl GeneratorConfig {
    pub const ASSET_KEY_PREFIX: &str = "assets/hotlink-ok";

    pub fn from_env() -> Result<Self, ConfigErrors>{
        Ok(Self {
            posts_dir: required_env("POSTS_DIR")?,
            template_path: required_env("TEMPLATE_PATH")?,
            blogspec_output: required_env("BLOGSPEC_OUTPUT")?,
            posts_output_dir: required_env("POSTS_OUTPUT_DIR")?,
            r2_account_id: required_env("R2_ACCOUNT_ID")?,
            r2_access_key: required_env("R2_ACCESS_KEY")?,
            r2_secret_key: required_env("R2_SECRET_KEY")?,
            r2_bucket: required_env("R2_BUCKET")?,
            r2_public_url_base: required_env("R2_PUBLIC_URL_BASE")?,
            script_output: required_env("SCRIPT_OUTPUT")?,
            changed_post_dirs: optional_csv_env("CHANGED_POST_DIRS"),
        })
    }
}

/// Retrieves the value of the environment variable specified by `key`.
///
/// This function attempts to read the value of the environment variable using
/// `std::env::var`. If the environment variable is not set, it returns an
/// error of type `ConfigErrors::MissingVar` containing the missing variable's key.
///
/// # Arguments
///
/// * `key` - A string slice that holds the name of the environment variable to retrieve.
///
/// # Returns
///
/// * `Ok(String)` - The value of the environment variable if it is set.
/// * `Err(ConfigErrors::MissingVar)` - An error indicating the environment variable is missing.
///
/// # Errors
///
/// This function returns an ` ConfigErrors::MissingVar ` error if the environment variable with the
/// provided key does not exist or is inaccessible.
///
fn required_env(key: &str) -> Result<String, ConfigErrors> {
    env::var(key).map_err(|_| ConfigErrors::MissingVar(key.to_string()))
}

fn optional_csv_env(key: &str) -> Option<Vec<String>> {
    let raw = env::var(key).ok()?;
    if raw.trim().is_empty() {
        return None;
    }
    Some(raw.split(',').map(|s| s.trim().to_string()).collect())
}