use std::sync::Arc;
use sqlx::{Pool, Sqlite};
use crate::config::AppConfig;
use crate::state::app_state::AppState;

#[derive(Debug, Clone)]
pub struct RootState{
    pub app : AppState
}

impl RootState {
    pub fn new(
        app_config: Arc<AppConfig>,
        db: Pool<Sqlite>
    ) -> Self {
        Self {
            app: AppState::new(app_config, db)
        }
    }
}