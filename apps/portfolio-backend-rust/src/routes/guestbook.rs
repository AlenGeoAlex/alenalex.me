use std::net::IpAddr;
use std::str::FromStr;
use std::time::Duration;
use axum::extract::State;
use axum::{Json, Router};
use axum::http::{Method, StatusCode};
use axum::response::IntoResponse;
use axum::routing::{get, post};
use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use utoipa::ToSchema;
use crate::extractors::client_ip::ClientIp;
use crate::models::guestbook::GuestbookStatus;
use crate::service::discord_service::DiscordService;
use crate::service::guestbook_service::create_entry;
use crate::state::app_state::AppState;
use crate::state::discord_state::DiscordState;
use crate::state::root_state::RootState;
use crate::utils::hash_ip;

#[derive(Debug, Clone, Deserialize, ToSchema)]
pub struct CreateEntryRequest {
    pub name: String,
    pub message: String,
}

#[derive(Debug, Clone, Serialize, ToSchema)]
pub struct CreateEntryResponse {
    pub id: String,
    pub name: String,
    pub message: String
}

#[derive(Debug, Clone, Serialize, ToSchema)]
pub struct ListEntriesResponse {
    entries: Vec<Entries>
}

#[derive(Debug, Clone, Serialize, ToSchema)]
pub struct Entries {
    pub id: String,
    pub name: String,
    pub message: String,
    pub is_pending: bool,
    pub created_at: chrono::DateTime<chrono::Utc>,
    pub like_count: u32,
    pub like_from_ip: bool,
}

pub fn router() -> Router<RootState> {
    Router::new()
        .route("/", post(create_entry_handler))
        .route("/", get(list_entries))
        .nest("/{guestbook_id}/likes", crate::routes::guestbook_likes::router())
}

#[utoipa::path(
    get,
    path = "/api/guestbook",
    responses(
        (status = 200, description = "List of entries", body = ListEntriesResponse),
        (status = 500, description = "Internal error")
    )
)]
async fn list_entries(
    State(state): State<AppState>,
    ClientIp(ip): ClientIp,
) -> impl IntoResponse {
    let ip_hash = hash_ip(&state.app_config.hashing_salt, ip);

    match crate::service::guestbook_service::list_entries(&state.db_pool, &ip_hash).await {
        Ok(entries) => {
            let response = ListEntriesResponse {
                entries: entries.iter().map(|entry| {
                    let is_pending = entry.status == GuestbookStatus::PendingApproval;
                    Entries {
                        id: entry.id.clone(),
                        name: entry.name.clone(),
                        message: entry.message.clone(),
                        is_pending,
                        created_at: entry.created_at,
                        like_count: 0,
                        like_from_ip: false,
                    }
                }).collect(),
            };
            (StatusCode::OK, Json(response)).into_response()
        },
        Err(err) => {
            tracing::error!("Error listing guestbook entries: {:?}", err);
            (StatusCode::INTERNAL_SERVER_ERROR, "failed to create entry").into_response()

        }
    }
}

#[utoipa::path(
    post,
    path = "/api/guestbook",
    request_body = CreateEntryRequest,
    responses(
        (status = 201, description = "Entry created", body = CreateEntryResponse),
        (status = 500, description = "Internal error")
    )
)]
async fn create_entry_handler(
    State(state): State<AppState>,
    State(discord_state) : State<DiscordState>,
    ClientIp(ip): ClientIp,
    Json(payload): Json<CreateEntryRequest>,
) -> impl IntoResponse {
    let ip_hash = hash_ip(&state.app_config.hashing_salt, ip);

    match create_entry(&state.db_pool, payload.message, ip_hash.clone(), payload.name).await {
        Ok(entry) => {
            let response = CreateEntryResponse {
                id: entry.id,
                name: entry.name,
                message: entry.message
            };


            DiscordService::send_guestbook_entry_embed(
                &discord_state.http,
                u64::from_str(&state.app_config.discord_channel_id).unwrap(),
                &response.id.to_string(),
                &response.name,
                &response.message,
                &ip_hash,
            )
            .await
            .unwrap();

            (StatusCode::CREATED, Json(response)).into_response()
        }
        Err(err) => {
            tracing::error!("failed to create guestbook entry: {err:?}");
            (StatusCode::INTERNAL_SERVER_ERROR, "failed to create entry").into_response()
        }
    }
}


