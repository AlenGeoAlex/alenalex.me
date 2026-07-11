use std::net::SocketAddr;
use std::sync::Arc;
use axum::{Error, Router, ServiceExt};
use axum::routing::get;
use crate::config::AppConfig;
use crate::state::root_state::RootState;

pub mod db;
mod config;
mod state;
pub mod routes;
pub mod extractors;
pub mod models;
pub mod service;

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

    let root_state = RootState::new(
        app_config.clone(),
        pool);

    let app : Router = Router::new()
        .route("/_health", get(get_health))
        .with_state(root_state);


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