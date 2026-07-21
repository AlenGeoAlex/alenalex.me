use axum::extract::{Path, State};
use axum::http::StatusCode;
use axum::response::IntoResponse;
use axum::Router;
use crate::extractors::client_ip::ClientIp;
use crate::service::guestbook_service::GuestbookLikeError;
use crate::state::app_state::AppState;
use crate::state::root_state::RootState;
use crate::utils::hash_ip;

pub fn router() -> Router<RootState> {
    Router::new()
        .route("/", axum::routing::post(create_like))
        .route("/", axum::routing::delete(delete_like))
}

#[utoipa::path(
    delete,
    path = "/{guestbook_id}/like",
    responses(
        (status = 200, description = "Like deleted"),
        (status = 400, description = "Bad request"),
        (status = 404, description = "Guestbook not found"),
    ),
    params(
        ("guestbook_id" = String, Path, description = "The ID of the guestbook"),
    ),
)]
async fn delete_like(
    State(state): State<AppState>,
    Path(guestbook_id): Path<String>,
    ClientIp(ip): ClientIp,
) -> impl IntoResponse {
    let ip_hash = hash_ip(&state.app_config.hashing_salt, ip);

    match crate::service::guestbook_service::unlike(
        &state.db_pool,
        &guestbook_id,
        ip_hash.as_str()
    ).await {
        Ok(_) => {
            (StatusCode::NO_CONTENT, "Like deleted").into_response()
        }
        Err(err) => {
            tracing::error!("Error deleting like: {}", err);
            match err {
                GuestbookLikeError::LikeNotFound => {
                    (StatusCode::NOT_FOUND, "Like not found").into_response()
                }
                _ => {
                    (StatusCode::INTERNAL_SERVER_ERROR, "Internal server error").into_response()
                }
            }
        }
    }
}

#[utoipa::path(
    post,
    path = "/{guestbook_id}/like",
    responses(
        (status = 200, description = "Like created"),
        (status = 400, description = "Bad request"),
        (status = 404, description = "Guestbook not found"),
    ),
    params(
        ("guestbook_id" = String, Path, description = "The ID of the guestbook"),
    ),
)]
async fn create_like(
    State(state): State<AppState>,
    Path(guestbook_id): Path<String>,
    ClientIp(ip): ClientIp,
) -> impl  IntoResponse {
    let hash_ip = hash_ip(&state.app_config.hashing_salt, ip);

    match crate::service::guestbook_service::like(&state.db_pool, &guestbook_id, hash_ip.as_str()).await {
        Ok(_) => {
            (StatusCode::CREATED, "Like created").into_response()
        }
        Err(GuestbookLikeError::LikeAlreadyExists) => {
            (StatusCode::CONFLICT, "Like already exists").into_response()
        },
        _ => {
            (StatusCode::INTERNAL_SERVER_ERROR, "Internal server error").into_response()
        }
    }
}