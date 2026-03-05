-- ═══════════════════════════════════════════════════════════
-- biochain_functions.sql — Temporal & Helper Functions
-- Signals Kernel v1.5
--
-- Run AFTER biochain_graph.sql (or views.sql — no graph dependency)
-- ═══════════════════════════════════════════════════════════


-- ═══════════════════════════════════════════════════════════
-- system_at — full state at any point in time
-- ═══════════════════════════════════════════════════════════

CREATE OR REPLACE FUNCTION system_at(
    p_entity_id UUID,
    p_at TIMESTAMPTZ
) RETURNS TABLE (
    kind VARCHAR(15), code VARCHAR, primary_state VARCHAR,
    properties JSONB, as_of TIMESTAMPTZ
) AS $$
BEGIN
    RETURN QUERY

    SELECT 'signal'::VARCHAR(15), s.code::VARCHAR, s.state::VARCHAR,
        jsonb_build_object('type', s.type, 'state', s.state,
            'value', s.value, 'unit', s.unit, 'baseline', s.baseline,
            'deviation_pct', s.deviation_pct,
            'range_low', s.range_low, 'range_high', s.range_high,
            'confidence', s.confidence, 'distribution', s.distribution,
            'trend', s.trend, 'region_id', s.region_id),
        s.created_on_utc
    FROM (SELECT DISTINCT ON (entity_id, code, region_id) *
          FROM signal WHERE entity_id = p_entity_id AND created_on_utc <= p_at
          ORDER BY entity_id, code, region_id, created_on_utc DESC) s

    UNION ALL
    SELECT 'receptor', r.code, r.state,
        jsonb_build_object('state', r.state, 'subtype', r.subtype), r.created_on_utc
    FROM (SELECT DISTINCT ON (entity_id, code) *
          FROM receptor WHERE entity_id = p_entity_id AND created_on_utc <= p_at
          ORDER BY entity_id, code, created_on_utc DESC) r

    UNION ALL
    SELECT 'transporter', t.code, t.state,
        jsonb_build_object('state', t.state, 'clearance', t.clearance), t.created_on_utc
    FROM (SELECT DISTINCT ON (entity_id, code) *
          FROM transporter WHERE entity_id = p_entity_id AND created_on_utc <= p_at
          ORDER BY entity_id, code, created_on_utc DESC) t

    UNION ALL
    SELECT 'gate', g.code, CASE WHEN g.latched THEN 'latched' ELSE g.type END,
        jsonb_build_object('type', g.type, 'threshold', g.threshold, 'latched', g.latched,
            'probability', g.probability, 'prompt', g.prompt, 'model', g.model),
        g.created_on_utc
    FROM (SELECT DISTINCT ON (entity_id, code) *
          FROM gate WHERE entity_id = p_entity_id AND created_on_utc <= p_at
          ORDER BY entity_id, code, created_on_utc DESC) g

    UNION ALL
    SELECT 'limiter', l.code, l.activity,
        jsonb_build_object('activity', l.activity, 'rate_limiting', l.rate_limiting),
        l.created_on_utc
    FROM (SELECT DISTINCT ON (entity_id, code) *
          FROM limiter WHERE entity_id = p_entity_id AND created_on_utc <= p_at
          ORDER BY entity_id, code, created_on_utc DESC) l

    UNION ALL
    SELECT 'interface', i.code, CASE WHEN i.active THEN 'active' ELSE 'inactive' END,
        jsonb_build_object('source_region_id', i.source_region_id, 'target_region_id', i.target_region_id),
        i.created_on_utc
    FROM (SELECT DISTINCT ON (entity_id, code) *
          FROM interface WHERE entity_id = p_entity_id AND created_on_utc <= p_at
          ORDER BY entity_id, code, created_on_utc DESC) i

    UNION ALL
    SELECT 'region', rg.code, rg.activity_state,
        jsonb_build_object('full_name', rg.full_name, 'system', rg.system, 'stress_load', rg.stress_load),
        rg.created_on_utc
    FROM (SELECT DISTINCT ON (entity_id, code) *
          FROM region WHERE entity_id = p_entity_id AND created_on_utc <= p_at
          ORDER BY entity_id, code, created_on_utc DESC) rg;
END;
$$ LANGUAGE plpgsql STABLE;


-- ═══════════════════════════════════════════════════════════
-- system_diff — what changed between two timepoints
-- ═══════════════════════════════════════════════════════════

CREATE OR REPLACE FUNCTION system_diff(
    p_entity_id UUID,
    p_from TIMESTAMPTZ,
    p_to TIMESTAMPTZ
) RETURNS TABLE (
    kind VARCHAR(15), code VARCHAR,
    state_before VARCHAR, state_after VARCHAR
) AS $$
BEGIN
    RETURN QUERY
    SELECT COALESCE(a.kind, b.kind), COALESCE(a.code, b.code),
        a.primary_state, b.primary_state
    FROM system_at(p_entity_id, p_from) a
    FULL OUTER JOIN system_at(p_entity_id, p_to) b
        ON a.kind = b.kind AND a.code = b.code
    WHERE a.primary_state IS DISTINCT FROM b.primary_state;
END;
$$ LANGUAGE plpgsql STABLE;


-- ═══════════════════════════════════════════════════════════
-- upsert_region — parser helper
-- ═══════════════════════════════════════════════════════════

CREATE OR REPLACE FUNCTION upsert_region(
    p_entity_id UUID,
    p_code VARCHAR(30),
    p_full_name VARCHAR(100) DEFAULT NULL,
    p_system VARCHAR(30) DEFAULT NULL,
    p_parent_code VARCHAR(30) DEFAULT NULL
) RETURNS INT AS $$
DECLARE
    v_id INT;
    v_parent_id INT;
BEGIN
    IF p_parent_code IS NOT NULL THEN
        SELECT id INTO v_parent_id FROM region
        WHERE code = p_parent_code
          AND (entity_id = p_entity_id OR entity_id IS NULL)
        ORDER BY entity_id NULLS LAST LIMIT 1;
    END IF;

    INSERT INTO region (entity_id, code, full_name, system, parent_id)
    VALUES (p_entity_id, p_code, p_full_name, p_system, v_parent_id)
    RETURNING id INTO v_id;

    RETURN v_id;
END;
$$ LANGUAGE plpgsql;
