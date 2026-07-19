use std::net::SocketAddr;
use std::str::FromStr;
use std::sync::Arc;
use axum::Router;
use axum::ServiceExt;
use axum::routing::get;
use utoipa::OpenApi;
use utoipa_scalar::{Scalar, Servable};
use crate::api_doc::ApiDoc;
use crate::config::AppConfig;
use crate::service::discord_service::DiscordService;
use crate::state::root_state::RootState;

pub mod db;
mod config;
mod state;
pub mod routes;
pub mod extractors;
pub mod models;
pub mod service;
pub mod api_doc;
pub mod handler;

#[tokio::main]
async fn main()  {
    dotenv::dotenv().ok();

    let app_config = Arc::new(AppConfig::init()
        .expect("Failed to initialize app config"));

    tracing_subscriber::fmt()
        .with_env_filter(
            tracing_subscriber::EnvFilter::try_from_default_env()
                .unwrap_or_else(|_| "portfolio_backend_rust=debug,tower_http=debug,axum=debug".into()),
        )
        .init();

    let pool = sqlx::sqlite::SqlitePoolOptions::new()
        .max_connections(10)
        .connect(&app_config.database_url)
        .await
        .expect("Failed to connect to database");

    sqlx::migrate!("src/migrations")
        .run(&pool)
        .await
        .expect("Failed to migrate database");

    tracing::info!("Database migrated successfully");
    
    let mut discord_service = DiscordService::create(
        &app_config.discord_bot_token,
        u64::from_str(&app_config.discord_guild_id).unwrap(),
        pool.clone()
    ).await.expect("Failed to create Discord service");

    let discord_http = discord_service.http();

    tokio::spawn(async move {
        if let Err(err) = discord_service.start().await {
            tracing::error!("discord bot exited with error: {err:?}");
        }
    });

    let root_state = RootState::new(
        app_config.clone(),
        pool,
        discord_http
    );

    let api_router = Router::new()
        .route("/_health", get(get_health))
        .nest("/api/guestbook", routes::guestbook::router())
        .with_state(root_state);

    let mut app = api_router;

    if app_config.enable_scalar {
        app = app.merge(Scalar::with_url("/scalar", ApiDoc::openapi()));
    }


    let listener = tokio::net::TcpListener::bind("0.0.0.0:8080")
        .await
        .expect("failed to bind to port 8080");

    tracing::info!("listening on {}", listener.local_addr().unwrap());

    axum::serve(listener,
                app.into_make_service_with_connect_info::<SocketAddr>(),
    ).await.unwrap();
}

async fn get_health() -> &'static str {
    "OK"
}