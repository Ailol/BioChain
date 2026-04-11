use std::sync::Arc;
use std::time::{Duration, Instant};

use dashmap::DashMap;
use rusqlite::Connection;
use tokio::sync::Mutex;

use crate::models::CacheValue;
use crate::registry::Registry;

const TIER1_TTL: Duration = Duration::from_secs(300); // 5 min
const TIER2_TTL_SECONDS: i64 = 86400; // 24h

/// Shared application state
#[derive(Clone)]
pub struct AppState {
    pub registry: Arc<Registry>,
    pub cache: Arc<Cache>,
}

pub struct Cache {
    tier1: DashMap<String, (Instant, CacheValue)>,
    tier2: Mutex<Connection>,
}

impl Cache {
    pub fn new(db_path: &str) -> Result<Self, rusqlite::Error> {
        let conn = Connection::open(db_path)?;
        conn.execute_batch(
            "PRAGMA journal_mode=WAL;
             PRAGMA synchronous=NORMAL;
             CREATE TABLE IF NOT EXISTS cache (
                 key TEXT PRIMARY KEY,
                 value TEXT NOT NULL,
                 fetched_at INTEGER NOT NULL,
                 source TEXT NOT NULL,
                 ttl_seconds INTEGER NOT NULL
             );",
        )?;
        Ok(Self {
            tier1: DashMap::new(),
            tier2: Mutex::new(conn),
        })
    }

    /// Get from tier 1 (in-memory DashMap)
    pub fn get_tier1(&self, key: &str) -> Option<CacheValue> {
        let entry = self.tier1.get(key)?;
        let (ts, val) = entry.value();
        if ts.elapsed() < TIER1_TTL {
            Some(val.clone())
        } else {
            drop(entry);
            self.tier1.remove(key);
            None
        }
    }

    /// Get from tier 2 (SQLite WAL)
    pub async fn get_tier2(&self, key: &str) -> Option<CacheValue> {
        let conn = self.tier2.lock().await;
        let mut stmt = conn
            .prepare_cached("SELECT value, fetched_at, ttl_seconds FROM cache WHERE key = ?1")
            .ok()?;
        let result = stmt
            .query_row([key], |row| {
                let value: String = row.get(0)?;
                let fetched_at: i64 = row.get(1)?;
                let ttl: i64 = row.get(2)?;
                Ok((value, fetched_at, ttl))
            })
            .ok()?;

        let now = std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .unwrap()
            .as_secs() as i64;

        if now - result.1 > result.2 {
            // Expired
            return None;
        }

        let val: CacheValue = serde_json::from_str(&result.0).ok()?;

        // Promote to tier 1
        self.tier1.insert(key.to_string(), (Instant::now(), val.clone()));

        Some(val)
    }

    /// Store in both tier 1 and tier 2
    pub async fn set(&self, key: &str, value: &CacheValue, source: &str) {
        // Tier 1
        self.tier1
            .insert(key.to_string(), (Instant::now(), value.clone()));

        // Tier 2
        let json = match serde_json::to_string(value) {
            Ok(j) => j,
            Err(_) => return,
        };
        let now = std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .unwrap()
            .as_secs() as i64;

        let conn = self.tier2.lock().await;
        let _ = conn.execute(
            "INSERT OR REPLACE INTO cache (key, value, fetched_at, source, ttl_seconds) VALUES (?1, ?2, ?3, ?4, ?5)",
            rusqlite::params![key, json, now, source, TIER2_TTL_SECONDS],
        );
    }
}
