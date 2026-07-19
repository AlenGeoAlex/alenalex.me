use crate::models::guestbook::{Guestbook, GuestbookStatus};
use anyhow::{anyhow, Result};
use chrono::{DateTime, Utc};
use nanoid::nanoid;
use sqlx::{Pool, Sqlite};
use sqlx::query::Query;
use sqlx::sqlite::SqliteArguments;

pub async  fn create_entry(
    db_pool: &Pool<Sqlite>,
    message: String,
    hash: String,
    name: String,
) -> Result<Guestbook>
{
    let created : DateTime<Utc> = DateTime::from_naive_utc_and_offset(chrono::Utc::now().naive_utc(), chrono::Utc);
    let guestbook: Guestbook = sqlx::query_as(
        "INSERT INTO guestbook_entries (id, name, message, status, ip_hash, created_at, rejection_reason)
     VALUES (?, ?, ?, ?, ?, ?, null)
     RETURNING id, name, message, status, ip_hash, created_at, rejection_reason",
    )
        .bind(nanoid!())
        .bind(&name)
        .bind(&message)
        .bind(GuestbookStatus::PendingApproval.as_db_str())
        .bind(&hash)
        .bind(&created)
        .fetch_one(db_pool)
        .await?;

    Ok(guestbook)
}

pub async fn list_entries(
    db_pool: &Pool<Sqlite>,
    ip_hash: &str
) -> Result<Vec<Guestbook>> {
    let guestbooks : Vec<Guestbook> = sqlx::query_as("SELECT * FROM guestbook_entries WHERE (status = 'accepted') OR (status = '' AND ip_hash = ?)")
        .bind(ip_hash)
        .fetch_all(db_pool)
        .await?;

    Ok(guestbooks)
}

pub async fn list_pending_entries(
    db_pool: &Pool<Sqlite>,
) -> Result<Vec<Guestbook>> {
    let guestbooks : Vec<Guestbook> = sqlx::query_as(
        "SELECT id, name, message, status, ip_hash, created_at, rejection_reason FROM guestbook_entries WHERE status = 'pending'"
    ).fetch_all(db_pool).await?;

    Ok(guestbooks)
}

pub async fn approve_entry(db_pool: &Pool<Sqlite>, id: &str) -> Result<()> {
    update_entry(db_pool, id.to_string(), None, GuestbookStatus::Accepted).await
}

pub async fn reject_entry(db_pool: &Pool<Sqlite>, id: &str, reason: Option<String>) -> Result<()> {
    update_entry(db_pool, id.to_string(), None, GuestbookStatus::Rejected { reason }).await
}

async fn update_entry(
    db_pool: &Pool<Sqlite>,
    id: String,
    message: Option<String>,
    status: GuestbookStatus,
) -> Result<()>
{
    let mut query : Query<Sqlite, SqliteArguments> = match &status {
        GuestbookStatus::Rejected { reason } => {
            Ok(sqlx::query(
                "UPDATE guestbook_entries SET
                             message = coalesce(?, message),
                             status = ?,
                             rejection_reason = ?
                         WHERE id = ? AND status = 'pending_approval';"
            ))
        },
        GuestbookStatus::Accepted => Ok(sqlx::query(
            "UPDATE guestbook_entries SET message = coalesce(?, message), status = ? WHERE id = ? AND status = 'pending_approval';"
        )),
        _ => Err(anyhow!("Unknown state provided"))
    }?;

    query = query
        .bind(&message)
        .bind(&status.as_db_str());

    if let GuestbookStatus::Rejected { reason } = &status {
        query = query.bind(reason);
    }

    let res = query
        .bind(&id)
        .execute(db_pool)
        .await?;

    if res.rows_affected() == 0 {
        return Err(anyhow!("No status update has been made"));
    }

    Ok(())
}