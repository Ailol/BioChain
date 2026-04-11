use axum::extract::State;
use axum::http::StatusCode;
use axum::Json;

use crate::cache::AppState;
use crate::models::*;

pub async fn health() -> Json<serde_json::Value> {
    Json(serde_json::json!({ "status": "ok" }))
}

pub async fn receptors(
    State(state): State<AppState>,
    Json(query): Json<ReceptorQuery>,
) -> Result<Json<ReceptorResponse>, (StatusCode, Json<ErrorResponse>)> {
    // Try cache first, fall back to registry
    let ligand = &query.ligand;

    if let Some(receptors) = state.registry.receptors_for_ligand(ligand) {
        Ok(Json(ReceptorResponse {
            ligand: ligand.clone(),
            receptors,
            source: "registry".to_string(),
        }))
    } else {
        Err((
            StatusCode::NOT_FOUND,
            Json(ErrorResponse {
                error: format!("Unknown ligand: {}", ligand),
            }),
        ))
    }
}

pub async fn cascade(
    State(state): State<AppState>,
    Json(query): Json<CascadeQuery>,
) -> Result<Json<CascadeResponse>, (StatusCode, Json<ErrorResponse>)> {
    let receptor = &query.receptor;

    let coupling = state.registry.coupling_for_receptor(receptor).ok_or_else(|| {
        (
            StatusCode::NOT_FOUND,
            Json(ErrorResponse {
                error: format!("Unknown receptor: {}", receptor),
            }),
        )
    })?;

    let cascade = state
        .registry
        .cascade_for_receptor(receptor)
        .ok_or_else(|| {
            (
                StatusCode::NOT_FOUND,
                Json(ErrorResponse {
                    error: format!("No cascade data for receptor: {}", receptor),
                }),
            )
        })?;

    Ok(Json(CascadeResponse {
        receptor: receptor.clone(),
        coupling,
        cascade,
        source: "registry".to_string(),
    }))
}

pub async fn downstream(
    State(state): State<AppState>,
    Json(query): Json<DownstreamQuery>,
) -> Result<Json<DownstreamResponse>, (StatusCode, Json<ErrorResponse>)> {
    let kinase = &query.kinase;

    if let Some(targets) = state.registry.downstream_for_kinase(kinase) {
        Ok(Json(DownstreamResponse {
            kinase: kinase.clone(),
            targets,
            source: "registry".to_string(),
        }))
    } else {
        Err((
            StatusCode::NOT_FOUND,
            Json(ErrorResponse {
                error: format!("Unknown kinase: {}", kinase),
            }),
        ))
    }
}
