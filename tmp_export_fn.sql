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
                ) FROM v_signal_current s
                JOIN v_region_current r
                    ON s.region_id = r.id AND s.entity_id = r.entity_id
                WHERE s.entity_id = p_entity_id
                  AND s.region_id IS NOT NULL

                -- ── BINDS: receptor → signal ─────────────────
                -- raw_sig resolves FK; cur_sig gives current nid
                UNION ALL
                SELECT jsonb_build_object(
                    'source', rec.code, 'source_type', 'receptor',
                    'source_id', rec.id,
                    'operator', '⊕', 'class', 'binds',
                    'target', cur_sig.code, 'target_type', 'signal',
                    'target_id', cur_sig.id,
                    'properties', NULL::JSONB,
                    'gain', NULL, 'noise_sigma', NULL,
                    'transfer_fn', NULL, 'delay_ms', NULL,
                    'gate_id', NULL, 'gate_type', NULL,
                    'gate_active', NULL
                ) FROM v_receptor_current rec
                JOIN signal raw_sig ON rec.signal_id = raw_sig.id
                JOIN v_signal_current cur_sig
                    ON cur_sig.entity_id = raw_sig.entity_id
                    AND cur_sig.code = raw_sig.code
                    AND cur_sig.region_id IS NOT DISTINCT FROM raw_sig.region_id
                WHERE rec.entity_id = p_entity_id
                  AND rec.signal_id IS NOT NULL

                -- ── EXPRESSED_IN: receptor → region (via signal) ─
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
                ) FROM v_receptor_current rec
                JOIN signal raw_sig ON rec.signal_id = raw_sig.id
                JOIN v_region_current reg
                    ON raw_sig.region_id = reg.id AND raw_sig.entity_id = reg.entity_id
                WHERE rec.entity_id = p_entity_id
                  AND rec.signal_id IS NOT NULL
                  AND raw_sig.region_id IS NOT NULL

                -- ── CLEARS: transporter → signal ─────────────
                UNION ALL
                SELECT jsonb_build_object(
                    'source', t.code, 'source_type', 'transporter',
                    'source_id', t.id,
                    'operator', '⊖', 'class', 'clears',
                    'target', cur_sig.code, 'target_type', 'signal',
                    'target_id', cur_sig.id,
                    'properties', NULL::JSONB,
                    'gain', NULL, 'noise_sigma', NULL,
                    'transfer_fn', NULL, 'delay_ms', NULL,
                    'gate_id', NULL, 'gate_type', NULL,
                    'gate_active', NULL
                ) FROM v_transporter_current t
                JOIN signal raw_sig ON t.signal_id = raw_sig.id
                JOIN v_signal_current cur_sig
                    ON cur_sig.entity_id = raw_sig.entity_id
                    AND cur_sig.code = raw_sig.code
                    AND cur_sig.region_id IS NOT DISTINCT FROM raw_sig.region_id
                WHERE t.entity_id = p_entity_id
                  AND t.signal_id IS NOT NULL

                -- ── MODULATES: limiter → signal ──────────────
                UNION ALL
                SELECT jsonb_build_object(
                    'source', lim.code, 'source_type', 'limiter',
                    'source_id', lim.id,
                    'operator', '⧫', 'class', 'modulates',
                    'target', cur_sig.code, 'target_type', 'signal',
                    'target_id', cur_sig.id,
                    'properties', NULL::JSONB,
                    'gain', NULL, 'noise_sigma', NULL,
                    'transfer_fn', NULL, 'delay_ms', NULL,
                    'gate_id', NULL, 'gate_type', NULL,
                    'gate_active', NULL
                ) FROM v_limiter_current lim
                JOIN signal raw_sig ON lim.target_id = raw_sig.id
                JOIN v_signal_current cur_sig
                    ON cur_sig.entity_id = raw_sig.entity_id
                    AND cur_sig.code = raw_sig.code
                    AND cur_sig.region_id IS NOT DISTINCT FROM raw_sig.region_id
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
                JOIN v_gate_current g ON e.gate_id = g.id
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
                ) FROM v_interface_current i
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
                ) FROM v_interface_current i
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
