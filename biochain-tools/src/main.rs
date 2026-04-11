mod cache;
mod handlers;
mod models;
mod reactome_api;
mod registry;

use std::sync::Arc;

use axum::routing::{get, post};
use axum::Router;
use tower_http::cors::CorsLayer;
use tower_http::trace::TraceLayer;
use tracing::info;

use cache::{AppState, Cache};
use registry::Registry;

const REGISTRY_TOML: &str = include_str!("../registry/unified_registry.toml");

#[tokio::main]
async fn main() {
    tracing_subscriber::fmt()
        .with_env_filter(
            tracing_subscriber::EnvFilter::try_from_default_env()
                .unwrap_or_else(|_| "biochain_tools=info,tower_http=info".into()),
        )
        .init();

    let port: u16 = std::env::var("BIOCHAIN_TOOLS_PORT")
        .ok()
        .and_then(|p| p.parse().ok())
        .unwrap_or(8002);

    let db_path = std::env::var("BIOCHAIN_TOOLS_DB").unwrap_or_else(|_| "./cache.db".to_string());

    let registry = Registry::from_toml(REGISTRY_TOML).expect("Failed to parse registry TOML");
    info!("Registry loaded from embedded TOML");

    let cache = Cache::new(&db_path).expect("Failed to initialize SQLite cache");
    info!("SQLite cache initialized at {}", db_path);

    let state = AppState {
        registry: Arc::new(registry),
        cache: Arc::new(cache),
    };

    let app = Router::new()
        .route("/health", get(handlers::health))
        .route("/api/receptors", post(handlers::receptors))
        .route("/api/cascade", post(handlers::cascade))
        .route("/api/downstream", post(handlers::downstream))
        .layer(CorsLayer::permissive())
        .layer(TraceLayer::new_for_http())
        .with_state(state);

    let listener = tokio::net::TcpListener::bind(format!("0.0.0.0:{}", port))
        .await
        .expect("Failed to bind");

    info!("biochain-tools listening on 0.0.0.0:{}", port);

    axum::serve(listener, app).await.expect("Server error");
}
