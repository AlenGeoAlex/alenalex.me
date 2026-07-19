use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use sqlx::{Error, FromRow, Row};
use sqlx::sqlite::SqliteRow;

#[derive(Debug, Clone, Serialize)]
pub struct Guestbook {
    pub id: String,
    pub name: String,
    pub message: String,
    pub ip_hash: String,
    pub created_at: DateTime<Utc>,
    pub status: GuestbookStatus,
}

impl FromRow<'_, SqliteRow> for Guestbook {
    fn from_row(row: &'_ SqliteRow) -> Result<Self, Error> {
        let status_str: String = row.try_get("status")?;
        let reason: Option<String> = row.try_get("rejection_reason")?;

        let status = match status_str.as_str() {
            "pending_approval" => GuestbookStatus::PendingApproval,
            "accepted" => GuestbookStatus::Accepted,
            "rejected" => GuestbookStatus::Rejected { reason },
            other => {
                return Err(Error::Decode(
                    format!("unknown guestbook status: {other}").into(),
                ))
            }
        };

        Ok(Guestbook {
            id: row.try_get("id")?,
            name: row.try_get("name")?,
            message: row.try_get("message")?,
            ip_hash: row.try_get("ip_hash")?,
            created_at: row.try_get("created_at")?,
            status,
        })
    }
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]

pub enum GuestbookStatus {
    PendingApproval,
    Accepted,
    Rejected { reason: Option<String> },
}

impl GuestbookStatus {
    pub fn as_db_str(&self) -> &'static str {
        match self {
            GuestbookStatus::PendingApproval => "pending_approval",
            GuestbookStatus::Accepted => "accepted",
            GuestbookStatus::Rejected { .. } => "rejected",
        }
    }
}

impl TryFrom<(&str, Option<String>)> for GuestbookStatus {
    type Error = String;

    fn try_from((status_str, reason): (&str, Option<String>)) -> Result<Self, Self::Error> {
        match status_str {
            "pending_approval" => Ok(GuestbookStatus::PendingApproval),
            "accepted" => Ok(GuestbookStatus::Accepted),
            "rejected" => Ok(GuestbookStatus::Rejected { reason }),
            other => Err(format!("unknown guestbook status: {other}")),
        }
    }
}