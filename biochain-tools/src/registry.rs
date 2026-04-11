use std::collections::HashMap;

use serde::Deserialize;

use crate::models::{CascadeDetail, ReceptorEntry, TargetEntry};

// ── TOML schema ──

#[derive(Debug, Deserialize)]
struct BindingEntry {
    receptor: String,
    coupling: String,
}

#[derive(Debug, Deserialize)]
struct LigandBinding {
    receptors: Vec<BindingEntry>,
}

#[derive(Debug, Deserialize)]
struct DegradationEnzymes {
    enzymes: Vec<String>,
}

#[derive(Debug, Deserialize)]
struct RegistryToml {
    bindings: HashMap<String, LigandBinding>,
    coupling_to_2m: HashMap<String, Vec<String>>,
    sm_to_kinase: HashMap<String, Vec<String>>,
    kinase_targets: HashMap<String, Vec<String>>,
    nr_tf: HashMap<String, Vec<String>>,
    rtk_kinases: HashMap<String, Vec<String>>,
    jakstat_kinases: HashMap<String, Vec<String>>,
    enzyme_product: HashMap<String, String>,
    degradation_enzymes: DegradationEnzymes,
    transporter_substrate: HashMap<String, Vec<String>>,
}

// ── Runtime registry with reverse indexes ──

pub struct Registry {
    toml: RegistryToml,
    /// receptor → coupling (built at init from all bindings)
    receptor_coupling: HashMap<String, String>,
}

impl Registry {
    pub fn from_toml(raw: &str) -> Result<Self, toml::de::Error> {
        let toml: RegistryToml = toml::from_str(raw)?;

        // Build reverse index: receptor → coupling
        let mut receptor_coupling = HashMap::new();
        for binding in toml.bindings.values() {
            for entry in &binding.receptors {
                receptor_coupling
                    .entry(entry.receptor.clone())
                    .or_insert_with(|| entry.coupling.clone());
            }
        }

        Ok(Self {
            toml,
            receptor_coupling,
        })
    }

    /// Classify coupling into cascade type
    fn cascade_type(coupling: &str) -> &'static str {
        match coupling {
            "Gs" | "Gi" | "Gq" | "G12" => "GPCR",
            "ion_Ca" | "ion_Na" | "ion_Cl" => "ionotropic",
            "nuclear" => "nuclear",
            "RTK" => "RTK",
            "JAK-STAT" => "JAK-STAT",
            "cGMP" => "cGMP",
            _ => "unknown",
        }
    }

    // ── Query: receptors for ligand ──

    pub fn receptors_for_ligand(&self, ligand: &str) -> Option<Vec<ReceptorEntry>> {
        let binding = self.toml.bindings.get(ligand)?;
        Some(
            binding
                .receptors
                .iter()
                .map(|e| ReceptorEntry {
                    receptor: e.receptor.clone(),
                    coupling: e.coupling.clone(),
                    cascade_type: Self::cascade_type(&e.coupling).to_string(),
                })
                .collect(),
        )
    }

    // ── Query: cascade for receptor ──

    pub fn cascade_for_receptor(&self, receptor: &str) -> Option<CascadeDetail> {
        let coupling = self.receptor_coupling.get(receptor)?;
        let cascade_type = Self::cascade_type(coupling);

        match cascade_type {
            "GPCR" | "cGMP" => {
                let second_messengers = self
                    .toml
                    .coupling_to_2m
                    .get(coupling.as_str())
                    .cloned()
                    .unwrap_or_default();

                let mut kinases = Vec::new();
                for sm in &second_messengers {
                    if let Some(ks) = self.toml.sm_to_kinase.get(sm.as_str()) {
                        for k in ks {
                            if !kinases.contains(k) {
                                kinases.push(k.clone());
                            }
                        }
                    }
                }

                let mut tfs = Vec::new();
                for k in &kinases {
                    if let Some(targets) = self.toml.kinase_targets.get(k.as_str()) {
                        for t in targets {
                            if !tfs.contains(t) {
                                tfs.push(t.clone());
                            }
                        }
                    }
                }

                Some(CascadeDetail {
                    second_messengers,
                    kinases,
                    transcription_factors: tfs,
                    cascade_type: cascade_type.to_string(),
                })
            }
            "ionotropic" => {
                // Ion channels: coupling itself is the "second messenger" (ion flux)
                let ion = coupling.strip_prefix("ion_").unwrap_or(coupling);
                let kinases = self
                    .toml
                    .sm_to_kinase
                    .get(coupling.as_str())
                    .cloned()
                    .unwrap_or_default();

                let mut tfs = Vec::new();
                for k in &kinases {
                    if let Some(targets) = self.toml.kinase_targets.get(k.as_str()) {
                        for t in targets {
                            if !tfs.contains(t) {
                                tfs.push(t.clone());
                            }
                        }
                    }
                }

                Some(CascadeDetail {
                    second_messengers: vec![ion.to_string()],
                    kinases,
                    transcription_factors: tfs,
                    cascade_type: cascade_type.to_string(),
                })
            }
            "nuclear" => {
                let tfs = self
                    .toml
                    .nr_tf
                    .get(receptor)
                    .cloned()
                    .unwrap_or_default();

                Some(CascadeDetail {
                    second_messengers: vec![],
                    kinases: vec![],
                    transcription_factors: tfs,
                    cascade_type: cascade_type.to_string(),
                })
            }
            "RTK" => {
                let kinases = self
                    .toml
                    .rtk_kinases
                    .get(receptor)
                    .cloned()
                    .unwrap_or_default();

                let mut tfs = Vec::new();
                for k in &kinases {
                    if let Some(targets) = self.toml.kinase_targets.get(k.as_str()) {
                        for t in targets {
                            if !tfs.contains(t) {
                                tfs.push(t.clone());
                            }
                        }
                    }
                }

                Some(CascadeDetail {
                    second_messengers: vec![],
                    kinases,
                    transcription_factors: tfs,
                    cascade_type: cascade_type.to_string(),
                })
            }
            "JAK-STAT" => {
                let kinases = self
                    .toml
                    .jakstat_kinases
                    .get(receptor)
                    .cloned()
                    .unwrap_or_default();

                let mut tfs = Vec::new();
                for k in &kinases {
                    if let Some(targets) = self.toml.kinase_targets.get(k.as_str()) {
                        for t in targets {
                            if !tfs.contains(t) {
                                tfs.push(t.clone());
                            }
                        }
                    }
                }

                Some(CascadeDetail {
                    second_messengers: vec![],
                    kinases,
                    transcription_factors: tfs,
                    cascade_type: cascade_type.to_string(),
                })
            }
            _ => None,
        }
    }

    /// Get coupling for a receptor
    pub fn coupling_for_receptor(&self, receptor: &str) -> Option<String> {
        self.receptor_coupling.get(receptor).cloned()
    }

    // ── Query: downstream targets for kinase ──

    pub fn downstream_for_kinase(&self, kinase: &str) -> Option<Vec<TargetEntry>> {
        let targets = self.toml.kinase_targets.get(kinase)?;
        Some(
            targets
                .iter()
                .map(|t| {
                    // Classify target type
                    let target_type = if self.toml.kinase_targets.contains_key(t.as_str()) {
                        "kinase"
                    } else {
                        "transcription_factor"
                    };
                    TargetEntry {
                        target: t.clone(),
                        target_type: target_type.to_string(),
                    }
                })
                .collect(),
        )
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn test_registry() -> Registry {
        let raw = include_str!("../registry/unified_registry.toml");
        Registry::from_toml(raw).expect("Failed to parse registry TOML")
    }

    #[test]
    fn test_da_receptors() {
        let reg = test_registry();
        let receptors = reg.receptors_for_ligand("DA").unwrap();
        assert_eq!(receptors.len(), 6);
        assert_eq!(receptors[0].receptor, "D1");
        assert_eq!(receptors[0].coupling, "Gs");
        assert_eq!(receptors[0].cascade_type, "GPCR");
    }

    #[test]
    fn test_d1_cascade() {
        let reg = test_registry();
        let cascade = reg.cascade_for_receptor("D1").unwrap();
        assert_eq!(cascade.cascade_type, "GPCR");
        assert!(cascade.second_messengers.contains(&"cAMP".to_string()));
        assert!(cascade.kinases.contains(&"PKA".to_string()));
        assert!(cascade.transcription_factors.contains(&"CREB".to_string()));
    }

    #[test]
    fn test_trkb_cascade() {
        let reg = test_registry();
        let cascade = reg.cascade_for_receptor("TrkB").unwrap();
        assert_eq!(cascade.cascade_type, "RTK");
        assert!(cascade.kinases.contains(&"ERK".to_string()));
        assert!(cascade.kinases.contains(&"Akt".to_string()));
    }

    #[test]
    fn test_gr_nuclear() {
        let reg = test_registry();
        let cascade = reg.cascade_for_receptor("GR").unwrap();
        assert_eq!(cascade.cascade_type, "nuclear");
        assert!(cascade.transcription_factors.contains(&"NF-kB".to_string()));
    }

    #[test]
    fn test_il6r_jakstat() {
        let reg = test_registry();
        let cascade = reg.cascade_for_receptor("IL6R").unwrap();
        assert_eq!(cascade.cascade_type, "JAK-STAT");
        assert!(cascade.kinases.contains(&"JAK1".to_string()));
        assert!(cascade.kinases.contains(&"JAK2".to_string()));
    }

    #[test]
    fn test_pka_downstream() {
        let reg = test_registry();
        let targets = reg.downstream_for_kinase("PKA").unwrap();
        assert_eq!(targets.len(), 5);
        let names: Vec<&str> = targets.iter().map(|t| t.target.as_str()).collect();
        assert!(names.contains(&"CREB"));
        assert!(names.contains(&"DARPP-32"));
    }

    #[test]
    fn test_unknown_ligand() {
        let reg = test_registry();
        assert!(reg.receptors_for_ligand("NONEXISTENT").is_none());
    }
}
