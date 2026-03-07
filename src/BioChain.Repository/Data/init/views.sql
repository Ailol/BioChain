-- ═══════════════════════════════════════════════════════════
-- views.sql — Current-State & Region Views
-- Signals Kernel v1.5
--
-- Run AFTER biochain_init.sql, BEFORE biochain_graph.sql
-- ═══════════════════════════════════════════════════════════


-- CURRENT STATE — latest row per component

CREATE OR REPLACE VIEW v_signal_current AS
SELECT DISTINCT ON (entity_id, code, region_id)
    id, entity_id, type, code, region_id, module_id, state,
    value, unit, baseline, deviation_pct, range_low, range_high,
    confidence, distribution,
    tau_min_ms, tau_max_ms, trend, cause, analysis_id, created_on_utc
FROM signal
ORDER BY entity_id, code, region_id, created_on_utc DESC;

CREATE OR REPLACE VIEW v_receptor_current AS
SELECT DISTINCT ON (entity_id, code)
    id, entity_id, signal_id, signal_code, signal_type,
    code, subtype, module_id, state,
    cause, analysis_id, created_on_utc
FROM receptor
ORDER BY entity_id, code, created_on_utc DESC;

CREATE OR REPLACE VIEW v_transporter_current AS
SELECT DISTINCT ON (entity_id, code)
    id, entity_id, signal_id, signal_code, signal_type,
    code, module_id, state, clearance,
    cause, analysis_id, created_on_utc
FROM transporter
ORDER BY entity_id, code, created_on_utc DESC;

CREATE OR REPLACE VIEW v_gate_current AS
SELECT DISTINCT ON (entity_id, code)
    id, entity_id, code, type, module_id, threshold, expression,
    probability, parent_id, history, latched,
    prompt, model, parse_map, fallback_expr, timeout_ms, cache_ms,
    cause, analysis_id, created_on_utc
FROM gate
ORDER BY entity_id, code, created_on_utc DESC;

CREATE OR REPLACE VIEW v_limiter_current AS
SELECT DISTINCT ON (entity_id, code)
    id, entity_id, target_id, code, module_id, reaction,
    rate_limiting, activity,
    cause, analysis_id, created_on_utc
FROM limiter
ORDER BY entity_id, code, created_on_utc DESC;

CREATE OR REPLACE VIEW v_interface_current AS
SELECT DISTINCT ON (entity_id, code)
    id, entity_id, code, source_region_id, target_region_id,
    module_id, pathway, active,
    cause, analysis_id, created_on_utc
FROM interface
ORDER BY entity_id, code, created_on_utc DESC;

CREATE OR REPLACE VIEW v_region_current AS
SELECT DISTINCT ON (entity_id, code)
    id, entity_id, code, full_name, system, parent_id, module_id,
    activity_state, dominant_signal, stress_load,
    properties, cause, created_on_utc
FROM region
WHERE entity_id IS NOT NULL
ORDER BY entity_id, code, created_on_utc DESC;


-- v_system — full state (all component instances, regions deduplicated)
-- SELECT * FROM v_system WHERE entity_id = $1
-- NOTE: Only regions use v_*_current (deduplicated).
--       All other components use raw tables to preserve every instance.

CREATE OR REPLACE VIEW v_system AS
SELECT entity_id, 'signal'::VARCHAR(15) AS kind, id, code,
    state AS primary_state,
    jsonb_build_object('type', type, 'state', state,
        'value', value, 'unit', unit, 'baseline', baseline,
        'deviation_pct', deviation_pct,
        'range_low', range_low, 'range_high', range_high,
        'confidence', confidence, 'distribution', distribution,
        'trend', trend, 'region_id', region_id,
        'tau_min_ms', tau_min_ms, 'tau_max_ms', tau_max_ms) AS properties,
    created_on_utc
FROM signal
UNION ALL
SELECT entity_id, 'receptor', id, code, state,
    jsonb_build_object('state', state, 'subtype', subtype, 'signal_id', signal_id),
    created_on_utc FROM receptor
UNION ALL
SELECT entity_id, 'transporter', id, code, state,
    jsonb_build_object('state', state, 'clearance', clearance, 'signal_id', signal_id),
    created_on_utc FROM transporter
UNION ALL
SELECT entity_id, 'gate', id, code,
    CASE WHEN latched THEN 'latched' ELSE type END,
    jsonb_build_object('type', type, 'threshold', threshold, 'latched', latched,
        'expression', expression, 'probability', probability,
        'prompt', prompt, 'model', model),
    created_on_utc FROM gate
UNION ALL
SELECT entity_id, 'limiter', id, code, activity,
    jsonb_build_object('activity', activity, 'rate_limiting', rate_limiting, 'reaction', reaction),
    created_on_utc FROM limiter
UNION ALL
SELECT entity_id, 'interface', id, code,
    CASE WHEN active THEN 'active' ELSE 'inactive' END,
    jsonb_build_object('source_region_id', source_region_id, 'target_region_id', target_region_id, 'pathway', pathway),
    created_on_utc FROM interface
UNION ALL
SELECT entity_id, 'region', id, code, activity_state,
    jsonb_build_object('full_name', full_name, 'system', system, 'stress_load', stress_load, 'dominant_signal', dominant_signal),
    created_on_utc FROM v_region_current;


-- REGION ACTIVITY — computed from components

CREATE OR REPLACE VIEW v_region_activity AS
WITH region_signals AS (
    SELECT entity_id, region_id,
        COUNT(*) AS total,
        COUNT(*) FILTER (WHERE state IN ('↑','↑↑')) AS elevated,
        COUNT(*) FILTER (WHERE state IN ('↓','↓↓')) AS depleted,
        COUNT(*) FILTER (WHERE state = '≈') AS homeostatic,
        COUNT(*) FILTER (WHERE state IN ('~','⊘','●')) AS abnormal,
        jsonb_agg(jsonb_build_object(
            'code', code, 'type', type, 'state', state, 'trend', trend
        ) ORDER BY CASE state WHEN '≈' THEN 1 ELSE 0 END, code) AS signal_detail
    FROM v_signal_current WHERE region_id IS NOT NULL
    GROUP BY entity_id, region_id
),
region_receptors AS (
    SELECT r.entity_id, s.region_id,
        COUNT(*) AS total,
        COUNT(*) FILTER (WHERE r.state != 'active') AS impaired,
        jsonb_agg(jsonb_build_object('code', r.code, 'state', r.state))
            FILTER (WHERE r.state != 'active') AS impaired_detail
    FROM v_receptor_current r
    JOIN v_signal_current s ON r.signal_id = s.id
    WHERE s.region_id IS NOT NULL
    GROUP BY r.entity_id, s.region_id
),
region_dysreg AS (
    SELECT e.entity_id, s.region_id,
        COUNT(*) AS total, jsonb_agg(DISTINCT e.operator) AS types
    FROM edge e
    JOIN v_signal_current s ON e.source_type = 'signal' AND e.source_id = s.id
    WHERE e.operator_class = 'dysreg' AND e.active = true AND s.region_id IS NOT NULL
    GROUP BY e.entity_id, s.region_id
)
SELECT
    r.id AS region_id, r.entity_id, r.code AS region_code, r.full_name, r.system,
    CASE
        WHEN COALESCE(rd.total, 0) > 0 THEN 'dysregulated'
        WHEN COALESCE(rs.abnormal, 0) > 0 THEN 'abnormal'
        WHEN COALESCE(rs.elevated, 0) > COALESCE(rs.depleted, 0) THEN 'elevated'
        WHEN COALESCE(rs.depleted, 0) > COALESCE(rs.elevated, 0) THEN 'depleted'
        WHEN COALESCE(rs.homeostatic, 0) = COALESCE(rs.total, 0) AND rs.total > 0 THEN 'homeostatic'
        WHEN rs.total IS NULL THEN 'unknown'
        ELSE 'mixed'
    END AS computed_activity,
    COALESCE(rs.total, 0) AS signal_count,
    COALESCE(rs.elevated, 0) AS signals_elevated,
    COALESCE(rs.depleted, 0) AS signals_depleted,
    COALESCE(rs.signal_detail, '[]'::JSONB) AS signals,
    COALESCE(rr.total, 0) AS receptor_count,
    COALESCE(rr.impaired, 0) AS receptors_impaired,
    COALESCE(rr.impaired_detail, '[]'::JSONB) AS receptors_impaired_detail,
    COALESCE(rd.total, 0) AS dysreg_count,
    COALESCE(rd.types, '[]'::JSONB) AS dysreg_types
FROM v_region_current r
LEFT JOIN region_signals rs ON rs.region_id = r.id AND rs.entity_id = r.entity_id
LEFT JOIN region_receptors rr ON rr.region_id = r.id AND rr.entity_id = r.entity_id
LEFT JOIN region_dysreg rd ON rd.region_id = r.id AND rd.entity_id = r.entity_id;


-- REGION TRAFFIC — inter-region edge flow

CREATE OR REPLACE VIEW v_region_traffic AS
SELECT
    e.entity_id,
    sr.id AS source_region_id, sr.code AS source_region,
    tr.id AS target_region_id, tr.code AS target_region,
    COUNT(*) AS edge_count,
    jsonb_agg(DISTINCT e.operator_class) AS edge_classes,
    jsonb_agg(jsonb_build_object(
        'source', src_s.code, 'operator', e.operator,
        'target', tgt_s.code, 'class', e.operator_class
    ) ORDER BY e.operator_class) AS edges
FROM edge e
JOIN v_signal_current src_s ON e.source_type = 'signal' AND e.source_id = src_s.id
JOIN v_region_current sr ON src_s.region_id = sr.id AND sr.entity_id = e.entity_id
JOIN v_signal_current tgt_s ON e.target_type = 'signal' AND e.target_id = tgt_s.id
JOIN v_region_current tr ON tgt_s.region_id = tr.id AND tr.entity_id = e.entity_id
WHERE e.active = true AND e.entity_id IS NOT NULL AND sr.id != tr.id
GROUP BY e.entity_id, sr.id, sr.code, tr.id, tr.code;


-- REGION TREE — global hierarchy

CREATE OR REPLACE VIEW v_region_tree AS
WITH RECURSIVE tree AS (
    SELECT id, code, full_name, system, parent_id, 0 AS depth
    FROM region WHERE parent_id IS NULL AND entity_id IS NULL
    UNION ALL
    SELECT r.id, r.code, r.full_name, r.system, r.parent_id, t.depth + 1
    FROM region r JOIN tree t ON r.parent_id = t.id
    WHERE r.entity_id IS NULL
)
SELECT * FROM tree ORDER BY system, depth, code;


-- CODE-BASED VIEWS — graph queries using code columns

CREATE OR REPLACE VIEW v_edges_by_code AS
SELECT
    entity_id,
    source_code,
    source_signal_type,
    source_region,
    target_code,
    target_signal_type,
    target_region,
    relationship_kind,
    operator,
    operator_class,
    gate_code,
    gate_type,
    gate_condition,
    active
FROM edge
WHERE source_code IS NOT NULL;

CREATE OR REPLACE VIEW v_subject_graph AS
SELECT
    'signal' AS node_type,
    s.code,
    s.type AS signal_type,
    r.code AS region,
    s.state,
    s.entity_id
FROM signal s
LEFT JOIN region r ON s.region_id = r.id
UNION ALL
SELECT
    'receptor',
    rec.code,
    rec.signal_type,
    NULL,
    rec.state,
    rec.entity_id
FROM receptor rec
UNION ALL
SELECT
    'transporter',
    t.code,
    t.signal_type,
    NULL,
    t.state,
    t.entity_id
FROM transporter t;
