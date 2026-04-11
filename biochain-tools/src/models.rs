use serde::{Deserialize, Serialize};

// ── Request types (POST JSON body) ──

#[derive(Debug, Deserialize)]
pub struct ReceptorQuery {
    pub ligand: String,
}

#[derive(Debug, Deserialize)]
pub struct CascadeQuery {
    pub receptor: String,
}

#[derive(Debug, Deserialize)]
pub struct DownstreamQuery {
    pub kinase: String,
}

// ── Response types ──

#[derive(Debug, Serialize, Deserialize, Clone)]
pub struct ReceptorEntry {
    pub receptor: String,
    pub coupling: String,
    pub cascade_type: String,
}

#[derive(Debug, Serialize)]
pub struct ReceptorResponse {
    pub ligand: String,
    pub receptors: Vec<ReceptorEntry>,
    pub source: String,
}

#[derive(Debug, Serialize, Deserialize, Clone)]
pub struct CascadeDetail {
    pub second_messengers: Vec<String>,
    pub kinases: Vec<String>,
    pub transcription_factors: Vec<String>,
    pub cascade_type: String,
}

#[derive(Debug, Serialize)]
pub struct CascadeResponse {
    pub receptor: String,
    pub coupling: String,
    pub cascade: CascadeDetail,
    pub source: String,
}

#[derive(Debug, Serialize, Deserialize, Clone)]
pub struct TargetEntry {
    pub target: String,
    pub target_type: String,
}

#[derive(Debug, Serialize)]
pub struct DownstreamResponse {
    pub kinase: String,
    pub targets: Vec<TargetEntry>,
    pub source: String,
}

#[derive(Debug, Serialize)]
pub struct ErrorResponse {
    pub error: String,
}

// ── Cache entry (serialized to JSON for SQLite tier) ──

#[derive(Debug, Serialize, Deserialize, Clone)]
pub enum CacheValue {
    Receptors {
        ligand: String,
        receptors: Vec<ReceptorEntry>,
    },
    Cascade {
        receptor: String,
        coupling: String,
        cascade: CascadeDetail,
    },
    Downstream {
        kinase: String,
        targets: Vec<TargetEntry>,
    },
}
