-- ═══════════════════════════════════════════════════════════
-- backfill-code-columns.sql
-- Backfill code-based columns from existing FK relationships
-- Run ONCE after schema migration. Idempotent (WHERE ... IS NULL).
-- ═══════════════════════════════════════════════════════════

-- Backfill receptors: copy signal code/type from parent signal
UPDATE receptor r
SET signal_code = s.code,
    signal_type = s.type
FROM signal s
WHERE r.signal_id = s.id
  AND r.signal_code IS NULL;

-- Backfill transporters: copy signal code/type from parent signal
UPDATE transporter t
SET signal_code = s.code,
    signal_type = s.type
FROM signal s
WHERE t.signal_id = s.id
  AND t.signal_code IS NULL;

-- Backfill edges: copy source/target codes from signal table
UPDATE edge e
SET source_code = ss.code,
    source_signal_type = ss.type,
    source_region = sr.code,
    target_code = ts.code,
    target_signal_type = ts.type,
    target_region = tr.code,
    relationship_kind = CASE
        WHEN e.operator_class = 'feedback' THEN 'negative_feedback'
        WHEN e.operator_class = 'dysreg' THEN 'dysregulation'
        WHEN e.operator_class = 'causal' THEN 'causal'
        ELSE e.operator_class
    END
FROM signal ss
LEFT JOIN region sr ON ss.region_id = sr.id
JOIN signal ts ON e.target_id = ts.id
LEFT JOIN region tr ON ts.region_id = tr.id
WHERE e.source_id = ss.id
  AND e.source_code IS NULL;

-- Backfill gate info on edges that have gate_id
UPDATE edge e
SET gate_code = g.code,
    gate_type = g.type,
    gate_condition = g.expression
FROM gate g
WHERE e.gate_id = g.id
  AND e.gate_code IS NULL;

-- ═══════════════════════════════════════
-- Verification queries (run manually)
-- ═══════════════════════════════════════
-- Should return 0 rows after backfill:
-- SELECT count(*) FROM receptor WHERE signal_id IS NOT NULL AND signal_code IS NULL;
-- SELECT count(*) FROM transporter WHERE signal_id IS NOT NULL AND signal_code IS NULL;
-- SELECT count(*) FROM edge WHERE source_id IS NOT NULL AND source_code IS NULL;
