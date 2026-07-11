use std::sync::Arc;
use axum::extract::FromRef;
use sqlx::{Pool, Sqlite};
use crate::config::AppConfig;
use crate::state::root_state::RootState;

#[derive(Debug, Clone)]
pub struct AppState {
    pub db_pool : Pool<Sqlite>,
    pub app_config : Arc<AppConfig>
}

impl AppState {

    pub fn new(
        app_config: Arc<AppConfig>,
        db_pool: Pool<Sqlite>) -> Self {
        Self {
            db_pool,
            app_config
        }
    }

}

impl FromRef<RootState> for AppState {
    fn from_ref(root_state: &RootState) -> Self {
        root_state.app.clone()
    }
}