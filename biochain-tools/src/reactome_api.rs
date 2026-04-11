use std::time::Duration;

use reqwest::Client;
use tracing::{info, warn};

const REACTOME_BASE: &str = "https://reactome.org/ContentService";

pub struct ReactomeClient {
    client: Client,
    rate_limit_ms: u64,
}

impl ReactomeClient {
    pub fn new(rate_limit_ms: u64) -> Self {
        let client = Client::builder()
            .timeout(Duration::from_secs(3))
            .build()
            .expect("Failed to build HTTP client");
        Self {
            client,
            rate_limit_ms,
        }
    }

    /// Query a Reactome entity by identifier (e.g., "R-HSA-109582")
    pub async fn query_entity(&self, identifier: &str) -> Option<serde_json::Value> {
        let url = format!("{}/data/query/{}", REACTOME_BASE, identifier);
        info!("Reactome API: GET {}", url);

        tokio::time::sleep(Duration::from_millis(self.rate_limit_ms)).await;

        match self.client.get(&url).header("Accept", "application/json").send().await {
            Ok(resp) if resp.status().is_success() => {
                resp.json::<serde_json::Value>().await.ok()
            }
            Ok(resp) => {
                warn!("Reactome API returned {}: {}", resp.status(), identifier);
                None
            }
            Err(e) => {
                warn!("Reactome API error for {}: {}", identifier, e);
                None
            }
        }
    }

    /// Query participants of a pathway
    pub async fn query_participants(&self, pathway_id: &str) -> Option<serde_json::Value> {
        let url = format!(
            "{}/data/participants/{}/participatingPhysicalEntities",
            REACTOME_BASE, pathway_id
        );
        info!("Reactome API: GET {}", url);

        tokio::time::sleep(Duration::from_millis(self.rate_limit_ms)).await;

        match self.client.get(&url).header("Accept", "application/json").send().await {
            Ok(resp) if resp.status().is_success() => {
                resp.json::<serde_json::Value>().await.ok()
            }
            Ok(resp) => {
                warn!(
                    "Reactome API returned {} for participants: {}",
                    resp.status(),
                    pathway_id
                );
                None
            }
            Err(e) => {
                warn!("Reactome API error for participants {}: {}", pathway_id, e);
                None
            }
        }
    }
}
