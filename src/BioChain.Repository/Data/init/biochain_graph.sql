-- ═══════════════════════════════════════════════════════════
-- biochain_graph.sql — Graph Projection Layer
-- Signals Kernel v1.5
--
-- Run AFTER views.sql
--
-- Graph views + graph functions together.
-- Neo4j sync is application-layer — SQL provides read surfaces.
-- ═══════════════════════════════════════════════════════════


-- ═══════════════════════════════════════════════════════════
-- v_node — materialized snapshot of v_system for graph joins
-- ═══════════════════════════════════════════════════════════

CREATE MATERIALIZED VIEW v_node AS SELECT * FROM v_system;

CREATE UNIQUE INDEX idx_vnode_kind_id ON v_node(kind, id);
CREATE INDEX idx_vnode_entity ON v_node(entity_id);
CREATE INDEX idx_vnode_code ON v_node(code);
CREATE INDEX idx_vnode_kind ON v_node(kind);
CREATE INDEX idx_vnode_entity_kind ON v_node(entity_id, kind);
CREATE INDEX idx_vnode_state ON v_node(primary_state);


-- ═══════════════════════════════════════════════════════════
-- compare_states — normalized [-1, 1] scale centered on baseline
-- ═══════════════════════════════════════════════════════════

CREATE OR REPLACE FUNCTION compare_states(p_actual VARCHAR, p_op TEXT, p_threshold VARCHAR)
RETURNS BOOLEAN AS $$
DECLARE
    v_rank JSONB := '{"↓↓":-1,"↓":-0.5,"≈":0,"~":0,"↑":0.5,"↑↑":1,"●":1}';
    a NUMERIC := (v_rank->>p_actual)::NUMERIC;
    t NUMERIC := (v_rank->>p_threshold)::NUMERIC;
BEGIN
    -- ⊘ (absent) not in map → NULL → gate condition not met
    IF a IS NULL OR t IS NULL THEN RETURN false; END IF;
    RETURN CASE p_op
        WHEN '>=' THEN a >= t  WHEN '>' THEN a > t
        WHEN '<=' THEN a <= t  WHEN '<' THEN a < t
        WHEN '='  THEN a = t   WHEN '!=' THEN a != t
        ELSE a >= t END;
END; $$ LANGUAGE plpgsql IMMUTABLE;


-- ═══════════════════════════════════════════════════════════
-- evaluate_gate — live query against current signal state
-- ═══════════════════════════════════════════════════════════

CREATE OR REPLACE FUNCTION evaluate_gate(p_gate_id INT, p_entity_id UUID)
RETURNS BOOLEAN AS $$
DECLARE
    v_gate RECORD;
    v_expr JSONB;
    v_signal_code VARCHAR;
    v_region_code VARCHAR;
    v_op TEXT;
    v_threshold VARCHAR;
    v_actual_state VARCHAR;
    v_child RECORD;
    v_result BOOLEAN;
    v_count INT := 0;
    v_true_count INT := 0;
BEGIN
    -- NULL gate = always active
    IF p_gate_id IS NULL THEN RETURN true; END IF;

    SELECT * INTO v_gate FROM gate WHERE id = p_gate_id;
    IF NOT FOUND THEN RETURN true; END IF;

    -- Latch gates: return latched value directly
    IF v_gate.type = 'latch' THEN
        RETURN COALESCE(v_gate.latched, false);
    END IF;

    -- Threshold gates: evaluate expression against current signals
    IF v_gate.type = 'threshold' THEN
        -- expression is structured JSON: {"signal":"CORT","region":"ADR","op":">=","state":"↑"}
        BEGIN
            v_expr := v_gate.expression::JSONB;
        EXCEPTION WHEN OTHERS THEN
            RETURN true; -- unparseable expression = always active
        END;

        -- Raw fallback: unstructured expression
        IF v_expr ? 'raw' THEN RETURN true; END IF;

        v_signal_code := v_expr->>'signal';
        v_region_code := v_expr->>'region';
        v_op := COALESCE(v_expr->>'op', '>=');
        v_threshold := v_expr->>'state';

        IF v_signal_code IS NULL OR v_threshold IS NULL THEN RETURN true; END IF;

        -- Lookup current signal state
        IF v_region_code IS NOT NULL THEN
            SELECT s.state INTO v_actual_state
            FROM v_signal_current s
            JOIN v_region_current r ON s.region_id = r.id AND r.entity_id = s.entity_id
            WHERE s.entity_id = p_entity_id
              AND s.code = v_signal_code
              AND r.code = v_region_code
            LIMIT 1;
        ELSE
            SELECT s.state INTO v_actual_state
            FROM v_signal_current s
            WHERE s.entity_id = p_entity_id
              AND s.code = v_signal_code
            ORDER BY s.created_on_utc DESC
            LIMIT 1;
        END IF;

        -- Signal not found = gate not met
        IF v_actual_state IS NULL THEN RETURN false; END IF;

        RETURN compare_states(v_actual_state, v_op, v_threshold);
    END IF;

    -- Composite gates: and/or/not/xor — recurse into children via parent_id
    IF v_gate.type IN ('and', 'or', 'not', 'xor') THEN
        FOR v_child IN
            SELECT id FROM gate WHERE parent_id = p_gate_id
        LOOP
            v_count := v_count + 1;
            IF evaluate_gate(v_child.id, p_entity_id) THEN
                v_true_count := v_true_count + 1;
            END IF;
        END LOOP;

        IF v_count = 0 THEN RETURN true; END IF;

        RETURN CASE v_gate.type
            WHEN 'and' THEN v_true_count = v_count
            WHEN 'or'  THEN v_true_count > 0
            WHEN 'not' THEN v_true_count = 0
            WHEN 'xor' THEN v_true_count = 1
            ELSE true END;
    END IF;

    -- LLM gates: not evaluable in SQL, default to true
    IF v_gate.type = 'llm' THEN
        RETURN true;
    END IF;

    -- Other gate types (integrator, gain, novelty, splitter): not yet evaluable
    RETURN true;
END;
$$ LANGUAGE plpgsql STABLE;


-- ═══════════════════════════════════════════════════════════
-- v_graph — edges with resolved current-state nodes
-- ═══════════════════════════════════════════════════════════

CREATE OR REPLACE VIEW v_graph AS
SELECT
    e.id AS eid, e.entity_id,
    e.source_type, e.source_id,
    COALESCE(e.source_code, src.code) AS source_code,
    src.primary_state AS source_state,
    src.properties AS source_properties,
    e.operator, e.operator_class, e.properties AS edge_properties,
    e.gain, e.noise_sigma, e.transfer_fn, e.delay_ms,
    e.clamp_lo, e.clamp_hi,
    e.target_type, e.target_id,
    COALESCE(e.target_code, tgt.code) AS target_code,
    tgt.primary_state AS target_state,
    tgt.properties AS target_properties,
    e.gate_id,
    g.type AS gate_type,
    CASE WHEN e.gate_id IS NULL THEN true
         ELSE evaluate_gate(e.gate_id, e.entity_id) END AS gate_active,
    e.active, e.analysis_id, e.created_on_utc
FROM edge e
JOIN v_node src ON src.kind = e.source_type AND src.id = e.source_id
                AND src.entity_id = e.entity_id
JOIN v_node tgt ON tgt.kind = e.target_type AND tgt.id = e.target_id
                AND tgt.entity_id = e.entity_id
LEFT JOIN v_gate_current g ON g.id = e.gate_id
WHERE e.active = true;


-- ═══════════════════════════════════════════════════════════
-- NOTIFY — INSERT only (append-only model)
-- ═══════════════════════════════════════════════════════════

CREATE OR REPLACE FUNCTION notify_graph_insert()
RETURNS TRIGGER AS $$
BEGIN
    PERFORM pg_notify('graph_changed', json_build_object(
        'table', TG_TABLE_NAME,
        'id', NEW.id,
        'entity_id', NEW.entity_id
    )::Text);
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_signal_notify AFTER INSERT ON signal
    FOR EACH ROW EXECUTE FUNCTION notify_graph_insert();
CREATE TRIGGER trg_receptor_notify AFTER INSERT ON receptor
    FOR EACH ROW EXECUTE FUNCTION notify_graph_insert();
CREATE TRIGGER trg_transporter_notify AFTER INSERT ON transporter
    FOR EACH ROW EXECUTE FUNCTION notify_graph_insert();
CREATE TRIGGER trg_gate_notify AFTER INSERT ON gate
    FOR EACH ROW EXECUTE FUNCTION notify_graph_insert();
CREATE TRIGGER trg_limiter_notify AFTER INSERT ON limiter
    FOR EACH ROW EXECUTE FUNCTION notify_graph_insert();
CREATE TRIGGER trg_interface_notify AFTER INSERT ON interface
    FOR EACH ROW EXECUTE FUNCTION notify_graph_insert();
CREATE TRIGGER trg_region_notify AFTER INSERT ON region
    FOR EACH ROW EXECUTE FUNCTION notify_graph_insert();
CREATE TRIGGER trg_edge_notify AFTER INSERT ON edge
    FOR EACH ROW EXECUTE FUNCTION notify_graph_insert();


-- ═══════════════════════════════════════════════════════════
-- refresh_graph
-- ═══════════════════════════════════════════════════════════

CREATE OR REPLACE FUNCTION refresh_graph()
RETURNS VOID AS $$
BEGIN
    REFRESH MATERIALIZED VIEW CONCURRENTLY v_node;
END;
$$ LANGUAGE plpgsql;


-- ═══════════════════════════════════════════════════════════
-- export_graph_json
--
-- Nodes:  v_system (signal, receptor, transporter, gate,
--         limiter, interface, region) + module table
--         Each node carries a PascalCase 'label' for Neo4j.
--
-- Edges:  edge table (via v_graph)
--       + FK relationships:
--         LOCATED_IN   signal → region
--         BINDS        receptor → signal
--         EXPRESSED_IN receptor → region (via signal)
--         CLEARS       transporter → signal
--         MODULATES    limiter → signal
--         GATED_BY     source of gated edge → gate
--         BRIDGES_FROM interface → source region
--         BRIDGES_TO   interface → target region
--         MEMBER_OF    interface → pathway module
--         REALIZES     edge source → loop module
-- ═══════════════════════════════════════════════════════════

CREATE OR REPLACE FUNCTION export_graph_json(
    p_entity_id UUID
) RETURNS JSONB AS $$
BEGIN
    RETURN jsonb_build_object(
        'entity_id', p_entity_id,
        'exported_at', NOW(),

        -- ── NODES ────────────────────────────────────────────
        'nodes', COALESCE((
            SELECT jsonb_agg(n) FROM (
                -- v_system nodes with PascalCase label
                SELECT jsonb_build_object(
                    'kind', kind,
                    'label', INITCAP(kind),
                    'id', id, 'code', code,
                    'state', primary_state,
                    'properties', properties
                ) AS n FROM v_system
                WHERE entity_id = p_entity_id

                UNION ALL

                -- Module / Loop / Pathway nodes
                -- Loop and Pathway identity lives in module;
                -- label is derived from whether a loop/pathway references this module.
                SELECT jsonb_build_object(
                    'kind', 'module',
                    'label', CASE
                        WHEN EXISTS (SELECT 1 FROM loop WHERE module_id = m.id AND active = true)
                        THEN 'Loop'
                        WHEN EXISTS (SELECT 1 FROM pathway WHERE module_id = m.id AND active = true)
                        THEN 'Pathway'
                        ELSE 'Module'
                    END,
                    'id', m.id, 'code', m.code,
                    'state', COALESCE(m.agent_type, 'plain'),
                    'properties', COALESCE(m.properties, '{}'::JSONB)
                ) FROM module m
                WHERE m.entity_id = p_entity_id OR m.entity_id IS NULL
            ) sub
        ), '[]'::JSONB),

        -- ── EDGES ────────────────────────────────────────────
        -- Every edge includes source_id/target_id for unique nid matching
        'edges', COALESCE((
            SELECT jsonb_agg(e) FROM (
                -- ── edge table (signal processing edges) ─────
                SELECT jsonb_build_object(
                    'source', source_code, 'source_type', source_type,
                    'source_id', source_id,
                    'operator', operator, 'class', operator_class,
                    'target', target_code, 'target_type', target_type,
                    'target_id', target_id,
                    'properties', edge_properties,
                    'gain', gain, 'noise_sigma', noise_sigma,
                    'transfer_fn', transfer_fn, 'delay_ms', delay_ms,
                    'gate_id', gate_id, 'gate_type', gate_type,
                    'gate_active', gate_active
                ) AS e FROM v_graph
                WHERE entity_id = p_entity_id OR entity_id IS NULL

                -- ── LOCATED_IN: signal → region ──────────────
                UNION ALL
                SELECT jsonb_build_object(
                    'source', s.code, 'source_type', 'signal',
                    'source_id', s.id,
                    'operator', '@', 'class', 'located_in',
                    'target', r.code, 'target_type', 'region',
                    'target_id', r.id,
                    'properties', NULL::JSONB,
                    'gain', NULL, 'noise_sigma', NULL,
                    'transfer_fn', NULL, 'delay_ms', NULL,
                    'gate_id', NULL, 'gate_type', NULL,
                    'gate_active', NULL
                ) FROM signal s
                JOIN v_region_current r
                    ON s.region_id = r.id AND s.entity_id = r.entity_id
                WHERE s.entity_id = p_entity_id
                  AND s.region_id IS NOT NULL

                -- ── BINDS: receptor → signal ─────────────────
                -- FK-based (signal_id set)
                UNION ALL
                SELECT jsonb_build_object(
                    'source', rec.code, 'source_type', 'receptor',
                    'source_id', rec.id,
                    'operator', '⊕', 'class', 'binds',
                    'target', sig.code, 'target_type', 'signal',
                    'target_id', sig.id,
                    'properties', NULL::JSONB,
                    'gain', NULL, 'noise_sigma', NULL,
                    'transfer_fn', NULL, 'delay_ms', NULL,
                    'gate_id', NULL, 'gate_type', NULL,
                    'gate_active', NULL
                ) FROM receptor rec
                JOIN signal sig ON rec.signal_id = sig.id
                WHERE rec.entity_id = p_entity_id
                  AND rec.signal_id IS NOT NULL

                -- Code-based BINDS (signal_id NULL, signal_code set)
                UNION ALL
                SELECT jsonb_build_object(
                    'source', rec.code, 'source_type', 'receptor',
                    'source_id', rec.id,
                    'operator', '⊕', 'class', 'binds',
                    'target', sig.code, 'target_type', 'signal',
                    'target_id', sig.id,
                    'properties', NULL::JSONB,
                    'gain', NULL, 'noise_sigma', NULL,
                    'transfer_fn', NULL, 'delay_ms', NULL,
                    'gate_id', NULL, 'gate_type', NULL,
                    'gate_active', NULL
                ) FROM receptor rec
                CROSS JOIN LATERAL (
                    SELECT id, code FROM signal
                    WHERE code = rec.signal_code
                      AND entity_id = rec.entity_id
                    ORDER BY created_on_utc DESC LIMIT 1
                ) sig
                WHERE rec.entity_id = p_entity_id
                  AND rec.signal_id IS NULL
                  AND rec.signal_code IS NOT NULL

                -- ── EXPRESSED_IN: receptor → region (via signal) ─
                -- FK-based
                UNION ALL
                SELECT jsonb_build_object(
                    'source', rec.code, 'source_type', 'receptor',
                    'source_id', rec.id,
                    'operator', '@', 'class', 'expressed_in',
                    'target', reg.code, 'target_type', 'region',
                    'target_id', reg.id,
                    'properties', NULL::JSONB,
                    'gain', NULL, 'noise_sigma', NULL,
                    'transfer_fn', NULL, 'delay_ms', NULL,
                    'gate_id', NULL, 'gate_type', NULL,
                    'gate_active', NULL
                ) FROM receptor rec
                JOIN signal raw_sig ON rec.signal_id = raw_sig.id
                JOIN v_region_current reg
                    ON raw_sig.region_id = reg.id AND raw_sig.entity_id = reg.entity_id
                WHERE rec.entity_id = p_entity_id
                  AND rec.signal_id IS NOT NULL
                  AND raw_sig.region_id IS NOT NULL

                -- Code-based EXPRESSED_IN
                UNION ALL
                SELECT jsonb_build_object(
                    'source', rec.code, 'source_type', 'receptor',
                    'source_id', rec.id,
                    'operator', '@', 'class', 'expressed_in',
                    'target', reg.code, 'target_type', 'region',
                    'target_id', reg.id,
                    'properties', NULL::JSONB,
                    'gain', NULL, 'noise_sigma', NULL,
                    'transfer_fn', NULL, 'delay_ms', NULL,
                    'gate_id', NULL, 'gate_type', NULL,
                    'gate_active', NULL
                ) FROM receptor rec
                CROSS JOIN LATERAL (
                    SELECT s.region_id FROM signal s
                    WHERE s.code = rec.signal_code
                      AND s.entity_id = rec.entity_id
                      AND s.region_id IS NOT NULL
                    ORDER BY s.created_on_utc DESC LIMIT 1
                ) sig_region
                JOIN v_region_current reg
                    ON sig_region.region_id = reg.id AND reg.entity_id = rec.entity_id
                WHERE rec.entity_id = p_entity_id
                  AND rec.signal_id IS NULL
                  AND rec.signal_code IS NOT NULL

                -- ── CLEARS: transporter → signal ─────────────
                -- FK-based
                UNION ALL
                SELECT jsonb_build_object(
                    'source', t.code, 'source_type', 'transporter',
                    'source_id', t.id,
                    'operator', '⊖', 'class', 'clears',
                    'target', sig.code, 'target_type', 'signal',
                    'target_id', sig.id,
                    'properties', NULL::JSONB,
                    'gain', NULL, 'noise_sigma', NULL,
                    'transfer_fn', NULL, 'delay_ms', NULL,
                    'gate_id', NULL, 'gate_type', NULL,
                    'gate_active', NULL
                ) FROM transporter t
                JOIN signal sig ON t.signal_id = sig.id
                WHERE t.entity_id = p_entity_id
                  AND t.signal_id IS NOT NULL

                -- Code-based CLEARS
                UNION ALL
                SELECT jsonb_build_object(
                    'source', t.code, 'source_type', 'transporter',
                    'source_id', t.id,
                    'operator', '⊖', 'class', 'clears',
                    'target', sig.code, 'target_type', 'signal',
                    'target_id', sig.id,
                    'properties', NULL::JSONB,
                    'gain', NULL, 'noise_sigma', NULL,
                    'transfer_fn', NULL, 'delay_ms', NULL,
                    'gate_id', NULL, 'gate_type', NULL,
                    'gate_active', NULL
                ) FROM transporter t
                CROSS JOIN LATERAL (
                    SELECT id, code FROM signal
                    WHERE code = t.signal_code
                      AND entity_id = t.entity_id
                    ORDER BY created_on_utc DESC LIMIT 1
                ) sig
                WHERE t.entity_id = p_entity_id
                  AND t.signal_id IS NULL
                  AND t.signal_code IS NOT NULL

                -- ── MODULATES: limiter → signal ──────────────
                UNION ALL
                SELECT jsonb_build_object(
                    'source', lim.code, 'source_type', 'limiter',
                    'source_id', lim.id,
                    'operator', '⧫', 'class', 'modulates',
                    'target', sig.code, 'target_type', 'signal',
                    'target_id', sig.id,
                    'properties', NULL::JSONB,
                    'gain', NULL, 'noise_sigma', NULL,
                    'transfer_fn', NULL, 'delay_ms', NULL,
                    'gate_id', NULL, 'gate_type', NULL,
                    'gate_active', NULL
                ) FROM limiter lim
                JOIN signal sig ON lim.target_id = sig.id
                WHERE lim.entity_id = p_entity_id
                  AND lim.target_id IS NOT NULL

                -- ── GATED_BY: source of gated edge → gate ────
                -- Deduplicated: one rel per (source, gate) pair
                UNION ALL
                SELECT DISTINCT jsonb_build_object(
                    'source', src.code, 'source_type', src.kind,
                    'source_id', src.id,
                    'operator', '⊳', 'class', 'gated_by',
                    'target', g.code, 'target_type', 'gate',
                    'target_id', g.id,
                    'properties', NULL::JSONB,
                    'gain', NULL, 'noise_sigma', NULL,
                    'transfer_fn', NULL, 'delay_ms', NULL,
                    'gate_id', NULL, 'gate_type', NULL,
                    'gate_active', NULL
                ) FROM edge e
                JOIN v_node src ON src.kind = e.source_type AND src.id = e.source_id
                                AND src.entity_id = e.entity_id
                JOIN gate g ON e.gate_id = g.id
                WHERE (e.entity_id = p_entity_id OR e.entity_id IS NULL)
                  AND e.gate_id IS NOT NULL AND e.active = true

                -- ── BRIDGES_FROM: interface → source region ──
                UNION ALL
                SELECT jsonb_build_object(
                    'source', i.code, 'source_type', 'interface',
                    'source_id', i.id,
                    'operator', '←', 'class', 'bridges_from',
                    'target', r.code, 'target_type', 'region',
                    'target_id', r.id,
                    'properties', jsonb_build_object('pathway', i.pathway),
                    'gain', NULL, 'noise_sigma', NULL,
                    'transfer_fn', NULL, 'delay_ms', NULL,
                    'gate_id', NULL, 'gate_type', NULL,
                    'gate_active', NULL
                ) FROM interface i
                JOIN v_region_current r
                    ON i.source_region_id = r.id AND i.entity_id = r.entity_id
                WHERE i.entity_id = p_entity_id AND i.active = true

                -- ── BRIDGES_TO: interface → target region ────
                UNION ALL
                SELECT jsonb_build_object(
                    'source', i.code, 'source_type', 'interface',
                    'source_id', i.id,
                    'operator', '→', 'class', 'bridges_to',
                    'target', r.code, 'target_type', 'region',
                    'target_id', r.id,
                    'properties', jsonb_build_object('pathway', i.pathway),
                    'gain', NULL, 'noise_sigma', NULL,
                    'transfer_fn', NULL, 'delay_ms', NULL,
                    'gate_id', NULL, 'gate_type', NULL,
                    'gate_active', NULL
                ) FROM interface i
                JOIN v_region_current r
                    ON i.target_region_id = r.id AND i.entity_id = r.entity_id
                WHERE i.entity_id = p_entity_id AND i.active = true

                -- ── MEMBER_OF: interface → pathway module ────
                UNION ALL
                SELECT jsonb_build_object(
                    'source', ifc.code, 'source_type', 'interface',
                    'source_id', ifc.id,
                    'operator', '∈', 'class', 'member_of',
                    'target', m.code, 'target_type', 'module',
                    'target_id', m.id,
                    'properties', NULL::JSONB,
                    'gain', NULL, 'noise_sigma', NULL,
                    'transfer_fn', NULL, 'delay_ms', NULL,
                    'gate_id', NULL, 'gate_type', NULL,
                    'gate_active', NULL
                ) FROM (
                    SELECT DISTINCT ON (entity_id, code)
                        id, entity_id, code, pathway_id
                    FROM interface
                    WHERE pathway_id IS NOT NULL
                    ORDER BY entity_id, code, created_on_utc DESC
                ) ifc
                JOIN pathway p ON ifc.pathway_id = p.id
                JOIN module m ON p.module_id = m.id
                WHERE ifc.entity_id = p_entity_id

                -- ── REALIZES: edge source → loop module ──────
                UNION ALL
                SELECT DISTINCT jsonb_build_object(
                    'source', src.code, 'source_type', src.kind,
                    'source_id', src.id,
                    'operator', '∈', 'class', 'realizes',
                    'target', m.code, 'target_type', 'module',
                    'target_id', m.id,
                    'properties', jsonb_build_object('polarity', l.polarity),
                    'gain', NULL, 'noise_sigma', NULL,
                    'transfer_fn', NULL, 'delay_ms', NULL,
                    'gate_id', NULL, 'gate_type', NULL,
                    'gate_active', NULL
                ) FROM edge e
                JOIN v_node src ON src.kind = e.source_type AND src.id = e.source_id
                JOIN loop l ON e.loop_id = l.id
                JOIN module m ON l.module_id = m.id
                WHERE (e.entity_id = p_entity_id OR e.entity_id IS NULL)
                  AND e.loop_id IS NOT NULL AND e.active = true
            ) sub
        ), '[]'::JSONB)
    );
END;
$$ LANGUAGE plpgsql STABLE;


-- ═══════════════════════════════════════════════════════════
-- serialize_profile_dsl — compact DSL for LLM context
-- ═══════════════════════════════════════════════════════════

CREATE OR REPLACE FUNCTION serialize_profile_dsl(
    p_entity_id UUID
) RETURNS TEXT AS $$
DECLARE
    v_out TEXT := '#PROFILE: ' || p_entity_id || E'\n';
    v_rec RECORD;
BEGIN
    -- Regions (non-homeostatic)
    v_out := v_out || E'\n#PHASE: regions\n';
    FOR v_rec IN
        SELECT code, activity_state, stress_load
        FROM v_region_current
        WHERE entity_id = p_entity_id
          AND activity_state NOT IN ('homeostatic', 'unknown')
        ORDER BY CASE activity_state
            WHEN 'dysregulated' THEN 0 WHEN 'elevated' THEN 1
            WHEN 'depleted' THEN 2 ELSE 3 END
    LOOP
        v_out := v_out || 'REGION: ' || v_rec.code
            || ' ' || v_rec.activity_state
            || ' stress:' || COALESCE(v_rec.stress_load, '~') || E'\n';
    END LOOP;

    -- Signals (non-homeostatic first)
    v_out := v_out || E'\n#PHASE: baseline\n';
    FOR v_rec IN
        SELECT s.type, s.code, r.code AS region, s.state,
               s.value, s.unit, s.baseline, s.confidence, s.trend
        FROM v_signal_current s
        LEFT JOIN v_region_current r ON s.region_id = r.id
        WHERE s.entity_id = p_entity_id
        ORDER BY CASE s.state WHEN '≈' THEN 1 ELSE 0 END, s.type, s.code
    LOOP
        v_out := v_out || 'SIGNAL: ' || v_rec.type || ':' || v_rec.code;
        IF v_rec.region IS NOT NULL THEN v_out := v_out || '@' || v_rec.region; END IF;
        v_out := v_out || ' ' || v_rec.state;
        IF v_rec.value IS NOT NULL THEN
            v_out := v_out || ' =' || v_rec.value;
            IF v_rec.unit IS NOT NULL THEN v_out := v_out || v_rec.unit; END IF;
        END IF;
        IF v_rec.trend IS NOT NULL THEN v_out := v_out || ' ^' || v_rec.trend; END IF;
        IF v_rec.baseline IS NOT NULL THEN
            v_out := v_out || ' — baseline:' || v_rec.baseline;
        END IF;
        IF v_rec.confidence IS NOT NULL AND v_rec.confidence < 1.0 THEN
            v_out := v_out || ' conf:' || v_rec.confidence;
        END IF;
        v_out := v_out || E'\n';
    END LOOP;

    -- Receptors (non-active)
    FOR v_rec IN
        SELECT code, state FROM v_receptor_current
        WHERE entity_id = p_entity_id AND state != 'active'
    LOOP
        v_out := v_out || 'RECEPTOR: ' || v_rec.code
            || ' — status: ' || v_rec.state || E'\n';
    END LOOP;

    -- Transporters (non-default)
    FOR v_rec IN
        SELECT code, state, clearance FROM v_transporter_current
        WHERE entity_id = p_entity_id AND (state != 'active' OR clearance != '≈')
    LOOP
        v_out := v_out || 'TRANSPORT: ' || v_rec.code
            || ' — status: ' || v_rec.state
            || ', clearance:' || v_rec.clearance || E'\n';
    END LOOP;

    -- Gates (latched or LLM)
    FOR v_rec IN
        SELECT code, type FROM v_gate_current
        WHERE entity_id = p_entity_id AND (latched = true OR type = 'llm')
    LOOP
        v_out := v_out || 'GATE: ' || v_rec.code || ' — status: '
            || CASE WHEN v_rec.type = 'llm' THEN 'llm' ELSE 'latched' END || E'\n';
    END LOOP;

    -- Limiters (bottleneck or non-default)
    FOR v_rec IN
        SELECT code, activity, rate_limiting FROM v_limiter_current
        WHERE entity_id = p_entity_id AND (activity != '≈' OR rate_limiting = true)
    LOOP
        v_out := v_out || 'LIMITER: ' || v_rec.code;
        IF v_rec.rate_limiting THEN v_out := v_out || ' ⧫'; END IF;
        v_out := v_out || ' ' || v_rec.activity || E'\n';
    END LOOP;

    -- Active edges (dysreg + feedback first)
    v_out := v_out || E'\n#PHASE: active_edges\n';
    FOR v_rec IN
        SELECT source_code, operator, operator_class, target_code,
               edge_properties, gain, noise_sigma, transfer_fn, delay_ms,
               gate_id, gate_type, gate_active
        FROM v_graph WHERE entity_id = p_entity_id
        ORDER BY CASE operator_class
            WHEN 'dysreg' THEN 0 WHEN 'feedback' THEN 1
            WHEN 'gate' THEN 2 WHEN 'causal' THEN 3 ELSE 4 END
    LOOP
        v_out := v_out || 'EDGE: ' || v_rec.source_code
            || ' ' || v_rec.operator || ' ' || v_rec.target_code;
        IF v_rec.gain IS NOT NULL THEN
            v_out := v_out || ' gain:' || v_rec.gain;
        END IF;
        IF v_rec.delay_ms IS NOT NULL THEN
            v_out := v_out || ' delay:' || v_rec.delay_ms || 'ms';
        END IF;
        IF v_rec.gate_id IS NOT NULL THEN
            v_out := v_out || ' ['
                || CASE WHEN v_rec.gate_active THEN 'ACTIVE' ELSE 'DORMANT' END
                || ':' || COALESCE(v_rec.gate_type, 'unknown') || ']';
        END IF;
        IF v_rec.edge_properties != '{}'::JSONB THEN
            v_out := v_out || ' ' || v_rec.edge_properties::TEXT;
        END IF;
        v_out := v_out || E'\n';
    END LOOP;

    -- Inter-region traffic
    v_out := v_out || E'\n#PHASE: region_traffic\n';
    FOR v_rec IN
        SELECT source_region, target_region, edge_count, edge_classes
        FROM v_region_traffic WHERE entity_id = p_entity_id
        ORDER BY edge_count DESC
    LOOP
        v_out := v_out || 'INTERFACE: ' || v_rec.source_region
            || ' → ' || v_rec.target_region
            || ' edges:' || v_rec.edge_count
            || ' types:' || v_rec.edge_classes::TEXT || E'\n';
    END LOOP;

    RETURN v_out;
END;
$$ LANGUAGE plpgsql STABLE;


-- ═══════════════════════════════════════════════════════════
-- walk_edges
-- ═══════════════════════════════════════════════════════════

CREATE OR REPLACE FUNCTION walk_edges(
    p_entity_id UUID,
    p_source_type VARCHAR(15),
    p_source_id INT,
    p_max_depth INT DEFAULT 6,
    p_gated BOOLEAN DEFAULT false
) RETURNS TABLE (
    depth INT, edge_id INT,
    source_type VARCHAR(15), source_id INT,
    target_type VARCHAR(15), target_id INT,
    operator VARCHAR(20), operator_class VARCHAR(15),
    properties JSONB,
    gate_id INT, gate_active BOOLEAN
) AS $$
BEGIN
    RETURN QUERY
    WITH RECURSIVE walk AS (
        SELECT 1 AS depth, e.id AS edge_id,
            e.source_type, e.source_id, e.target_type, e.target_id,
            e.operator, e.operator_class, e.properties,
            e.gate_id,
            CASE WHEN e.gate_id IS NULL THEN true
                 ELSE evaluate_gate(e.gate_id, p_entity_id) END AS gate_active
        FROM edge e
        WHERE (e.entity_id = p_entity_id OR e.entity_id IS NULL)
          AND e.source_type = p_source_type AND e.source_id = p_source_id
          AND e.active = true
          AND (NOT p_gated OR e.gate_id IS NULL OR evaluate_gate(e.gate_id, p_entity_id))
        UNION ALL
        SELECT w.depth + 1, e.id,
            e.source_type, e.source_id, e.target_type, e.target_id,
            e.operator, e.operator_class, e.properties,
            e.gate_id,
            CASE WHEN e.gate_id IS NULL THEN true
                 ELSE evaluate_gate(e.gate_id, p_entity_id) END
        FROM walk w
        JOIN edge e ON e.source_type = w.target_type AND e.source_id = w.target_id
            AND e.active = true AND (e.entity_id = p_entity_id OR e.entity_id IS NULL)
            AND (NOT p_gated OR e.gate_id IS NULL OR evaluate_gate(e.gate_id, p_entity_id))
        WHERE w.depth < p_max_depth
    )
    SELECT * FROM walk;
END;
$$ LANGUAGE plpgsql STABLE;


-- ═══════════════════════════════════════════════════════════
-- find_feedback_loops
-- ═══════════════════════════════════════════════════════════

CREATE OR REPLACE FUNCTION find_feedback_loops(
    p_entity_id UUID,
    p_gated BOOLEAN DEFAULT false
) RETURNS TABLE (
    loop_path TEXT[], operators TEXT[], is_positive BOOLEAN
) AS $$
BEGIN
    RETURN QUERY
    WITH RECURSIVE loop_walk AS (
        SELECT
            e.source_type || ':' || e.source_id AS start_node,
            ARRAY[e.source_type || ':' || e.source_id]::TEXT[] AS path,
            ARRAY[e.operator::TEXT] AS ops,
            e.target_type, e.target_id,
            e.target_type || ':' || e.target_id AS current_node,
            1 AS depth
        FROM edge e
        WHERE e.entity_id = p_entity_id AND e.active = true
          AND e.operator_class = 'feedback'
          AND (NOT p_gated OR e.gate_id IS NULL OR evaluate_gate(e.gate_id, p_entity_id))
        UNION ALL
        SELECT lw.start_node,
            lw.path || (e.source_type || ':' || e.source_id),
            lw.ops || e.operator,
            e.target_type, e.target_id,
            e.target_type || ':' || e.target_id,
            lw.depth + 1
        FROM loop_walk lw
        JOIN edge e ON e.source_type = lw.target_type AND e.source_id = lw.target_id
            AND e.active = true AND (e.entity_id = p_entity_id OR e.entity_id IS NULL)
            AND (NOT p_gated OR e.gate_id IS NULL OR evaluate_gate(e.gate_id, p_entity_id))
        WHERE lw.depth < 8
          AND NOT (e.target_type || ':' || e.target_id) = ANY(lw.path[2:])
    )
    SELECT lw.path, lw.ops, NOT ('⟳⁻' = ANY(lw.ops))
    FROM loop_walk lw
    WHERE lw.current_node = lw.start_node AND lw.depth > 1;
END;
$$ LANGUAGE plpgsql STABLE;


-- ═══════════════════════════════════════════════════════════
-- find_dysreg_cascades
-- ═══════════════════════════════════════════════════════════

CREATE OR REPLACE FUNCTION find_dysreg_cascades(
    p_entity_id UUID,
    p_max_depth INT DEFAULT 6,
    p_gated BOOLEAN DEFAULT false
) RETURNS TABLE (
    root_code TEXT, dysreg_type TEXT,
    cascade_depth INT, affected_path TEXT[]
) AS $$
BEGIN
    RETURN QUERY
    WITH RECURSIVE dysreg_roots AS (
        SELECT src.code::TEXT AS root_code, e.operator::TEXT AS dysreg_type,
            e.target_type, e.target_id
        FROM edge e
        JOIN v_node src ON src.kind = e.source_type AND src.id = e.source_id
        WHERE e.entity_id = p_entity_id AND e.active = true AND e.operator_class = 'dysreg'
          AND (NOT p_gated OR e.gate_id IS NULL OR evaluate_gate(e.gate_id, p_entity_id))
    ),
    cascades AS (
        SELECT dr.root_code, dr.dysreg_type, 1 AS depth,
            ARRAY[tgt.code::TEXT] AS path, e.target_type AS next_type, e.target_id AS next_id
        FROM dysreg_roots dr
        JOIN v_node tgt ON tgt.kind = dr.target_type AND tgt.id = dr.target_id
        JOIN edge e ON e.source_type = dr.target_type AND e.source_id = dr.target_id
            AND e.active = true AND (e.entity_id = p_entity_id OR e.entity_id IS NULL)
            AND (NOT p_gated OR e.gate_id IS NULL OR evaluate_gate(e.gate_id, p_entity_id))
        UNION ALL
        SELECT c.root_code, c.dysreg_type, c.depth + 1,
            c.path || tgt.code, e.target_type, e.target_id
        FROM cascades c
        JOIN v_node tgt ON tgt.kind = c.next_type AND tgt.id = c.next_id
        JOIN edge e ON e.source_type = c.next_type AND e.source_id = c.next_id
            AND e.active = true AND (e.entity_id = p_entity_id OR e.entity_id IS NULL)
            AND (NOT p_gated OR e.gate_id IS NULL OR evaluate_gate(e.gate_id, p_entity_id))
        WHERE c.depth < p_max_depth AND NOT tgt.code = ANY(c.path)
    )
    SELECT c.root_code, c.dysreg_type, MAX(c.depth), c.path
    FROM cascades c
    GROUP BY c.root_code, c.dysreg_type, c.path
    ORDER BY MAX(c.depth) DESC;
END;
$$ LANGUAGE plpgsql STABLE;


-- ═══════════════════════════════════════════════════════════
-- NEO4J SYNC (application-layer)
--
-- Read surfaces: v_system (nodes), edge (relationships)
-- App: LISTEN graph_changed → debounce → refresh_graph()
--      → sync changed entity_ids via Bolt driver
-- ═══════════════════════════════════════════════════════════
