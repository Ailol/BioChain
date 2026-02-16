# Signal Ablation Report

Date: 2026-02-15 20:03 UTC
Reference profiles: 9

## Leave-One-Out Ablation

| Signal | Kendall's Tau | Shapley Value | Recommendation |
|--------|--------------|---------------|----------------|
| Semantic | 0.9971 | 0.118057 | Redundant |
| Affinity | 0.9911 | 0.116762 | Redundant |
| LayerCoverage | 0.9056 | 0.116762 | Moderate |
| Diversity | 0.9905 | 0.118058 | Redundant |
| Density | 1.0000 | 0.118140 | Redundant |

## Interpretation

- Tau > 0.95: Signal is redundant (removing it barely changes rankings)
- Tau < 0.80: Signal is important
- Tau < 0.60: Signal is critically important
