use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};

#[derive(Debug, Serialize, Deserialize, Clone)]
pub struct GuestbookLikes {
    pub entry_id: String,
    pub ip_hash: String,
    pub created_at: DateTime<Utc>,
}