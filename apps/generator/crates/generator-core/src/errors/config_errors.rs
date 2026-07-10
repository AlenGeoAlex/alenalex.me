#[derive(Debug, thiserror::Error)]
pub enum ConfigErrors{
    #[error("Missing env variable: {0}")]
    MissingVar(String)
}