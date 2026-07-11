use axum::Router;
use chrono::{DateTime, NaiveDateTime, Utc};
use serde::{Deserialize, Serialize};
use crate::state::root_state::RootState;

#[derive(Debug, Clone, Deserialize)]
pub struct CreateEntryRequest {
    pub name: String,
    pub message: String,
}

#[derive(Debug, Clone, Serialize)]
pub struct CreateEntryResponse {
    pub id: String,
    pub name: String,
    pub message: String,
    pub likes: i64,
    pub created_at: DateTime<Utc>
}

// pub fn router() -> Router<RootState> {
//     Router::new()
//         .route()
// }