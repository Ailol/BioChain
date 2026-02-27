-- ===============================================================
-- views.sql
-- BioChain v5.0 — Analysis Views & Functions
--
-- Depends on: biochain_init.sql (9 core tables)
--
-- SECTIONS:
--   1. RECONSTRUCTION    data -> protocol -> components
--   2. PROBABILISTIC     state distributions, co-occurrence, confidence
--   3. PREDICTABILISTIC  temporal trajectories, phase progression
--   4. LOGICAL           gate networks, feedback loops, cascade walks
--   5. BEHAVIOUR         signal profiles, dimensional scoring
-- ===============================================================


-- ═══════════════════════════════════════════════════════════
-- 1. RECONSTRUCTION — data -> analysis pathway
-- ═══════════════════════════════════════════════════════════

-- Core view: one row per protocol with all resolved components
CREATE OR REPLACE VIEW v_analysis_pathway AS
SELECT
    -- Source data
    d.id                AS data_id,
    d.person_id,
    d.kind              AS data_kind,
    d.source_text,
    d.created_on_utc    AS data_created,

    -- Protocol
    p.id                AS protocol_id,
    p.tag,
    p.formula,
    p.status,
    p.phase,

    -- Signal source
    ss.code             AS signal_source_code,
    ss.type             AS signal_source_type,
    ss.state            AS signal_source_state,
    ss.region           AS signal_source_region,

    -- Signal target
    st.code             AS signal_target_code,
    st.type             AS signal_target_type,
    st.state            AS signal_target_state,
    st.region           AS signal_target_region,

    -- Receptor
    r.code              AS receptor_code,
    r.subtype           AS receptor_subtype,
    r.state             AS receptor_state,

    -- Gate
    g.code              AS gate_code,
    g.type              AS gate_type,
    g.expression        AS gate_expression,
    g.latched           AS gate_latched,

    -- Limiter
    l.code              AS limiter_code,
    l.activity          AS limiter_activity,
    l.rate_limiting     AS limiter_rate_limiting,
    l.reaction          AS limiter_reaction,

    -- Transporter
    tr.code             AS transporter_code,
    tr.state            AS transporter_state,
    tr.clearance        AS transporter_clearance,

    -- Interface
    i.source_region     AS interface_source,
    i.target_region     AS interface_target,
    i.pathway           AS interface_pathway

FROM protocol p
JOIN data d ON d.id = p.data_id
LEFT JOIN signal ss  ON ss.id  = p.signal_source_id
LEFT JOIN signal st  ON st.id  = p.signal_target_id
LEFT JOIN receptor r ON r.id   = p.receptor_id
LEFT JOIN gate g     ON g.id   = p.gate_id
LEFT JOIN limiter l  ON l.id   = p.limiter_id
LEFT JOIN transporter tr ON tr.id = p.transporter_id
LEFT JOIN interface i    ON i.id  = p.interface_id;

COMMENT ON VIEW v_analysis_pathway IS
'Full data -> analysis reconstruction.
 One row per protocol line, all linked components resolved.
 Filter by data_id or person_id to see complete analysis for an input.';


-- Compact per-data summary: how many protocols per tag type
CREATE OR REPLACE VIEW v_data_analysis_summary AS
SELECT
    d.id                AS data_id,
    d.person_id,
    d.kind,
    d.analyzed,
    d.created_on_utc,
    COUNT(p.id)                                         AS total_protocols,
    COUNT(p.id) FILTER (WHERE p.tag = 'SIGNAL')         AS signal_count,
    COUNT(p.id) FILTER (WHERE p.tag = 'STATE')          AS state_count,
    COUNT(p.id) FILTER (WHERE p.tag = 'GATE')           AS gate_count,
    COUNT(p.id) FILTER (WHERE p.tag = 'FEEDBACK')       AS feedback_count,
    COUNT(p.id) FILTER (WHERE p.tag = 'LIMITER')        AS limiter_count,
    COUNT(p.id) FILTER (WHERE p.tag = 'TRANSPORT')      AS transport_count,
    COUNT(p.id) FILTER (WHERE p.tag = 'RECEPTOR')       AS receptor_count,
    COUNT(p.id) FILTER (WHERE p.tag = 'DYSREG')         AS dysreg_count,
    COUNT(p.id) FILTER (WHERE p.tag = 'HYPOTHESIS')     AS hypothesis_count,
    COUNT(p.id) FILTER (WHERE p.tag = 'PREDICTION')     AS prediction_count,
    COUNT(p.id) FILTER (WHERE p.tag = 'INTERVENTION')   AS intervention_count,
    COUNT(p.signal_source_id)                           AS linked_signals,
    COUNT(p.gate_id)                                    AS linked_gates,
    COUNT(p.limiter_id)                                 AS linked_limiters,
    COUNT(p.transporter_id)                             AS linked_transporters,
    COUNT(p.receptor_id)                                AS linked_receptors
FROM data d
LEFT JOIN protocol p ON p.data_id = d.id
GROUP BY d.id, d.person_id, d.kind, d.analyzed, d.created_on_utc;

COMMENT ON VIEW v_data_analysis_summary IS
'Per-data-entry summary: tag counts + component link counts.
 Shows analysis richness per input. linked_* < tag counts = parser gaps.';


-- ═══════════════════════════════════════════════════════════
-- 2. PROBABILISTIC — state distributions, co-occurrence, confidence
-- ═══════════════════════════════════════════════════════════

-- Signal state distribution: how often each signal is in each state
CREATE OR REPLACE VIEW v_signal_state_distribution AS
SELECT
    s.person_id,
    s.type,
    s.code,
    s.region,
    p.tag,
    -- Count protocols referencing this signal with each state at time of observation
    COUNT(*)                                                AS evidence_count,
    -- Temporal range
    MIN(p.created_on_utc)                                   AS first_observed,
    MAX(p.created_on_utc)                                   AS last_observed,
    -- Current register state
    s.state                                                 AS current_state
FROM signal s
JOIN protocol p ON p.signal_source_id = s.id OR p.signal_target_id = s.id
GROUP BY s.person_id, s.type, s.code, s.region, s.state, p.tag;

COMMENT ON VIEW v_signal_state_distribution IS
'How often each signal appears in protocols, grouped by tag type.
 evidence_count = number of protocol lines referencing this signal.
 Higher count = more confidence in the signal state assessment.';


-- Signal co-occurrence: which signals appear together in the same phase
CREATE OR REPLACE VIEW v_signal_cooccurrence AS
SELECT
    p1.person_id,
    s1.code  AS signal_a,
    s1.state AS state_a,
    s2.code  AS signal_b,
    s2.state AS state_b,
    p1.phase,
    COUNT(*)  AS co_occurrence_count
FROM protocol p1
JOIN protocol p2 ON p2.person_id = p1.person_id
    AND p2.phase IS NOT DISTINCT FROM p1.phase
    AND p2.data_id = p1.data_id
    AND p2.id > p1.id
JOIN signal s1 ON s1.id = p1.signal_source_id
JOIN signal s2 ON s2.id = p2.signal_source_id
WHERE s1.code <> s2.code
GROUP BY p1.person_id, s1.code, s1.state, s2.code, s2.state, p1.phase;

COMMENT ON VIEW v_signal_cooccurrence IS
'Signal co-occurrence within same phase and data source.
 Use for: conditional probability P(signal_b=state_b | signal_a=state_a).
 Higher co_occurrence_count = stronger correlation.';


-- Evidence confidence: how well-supported is each signal state?
CREATE OR REPLACE VIEW v_evidence_confidence AS
SELECT
    s.person_id,
    s.code,
    s.region,
    s.state                                                AS current_state,
    COUNT(DISTINCT p.data_id)                              AS distinct_sources,
    COUNT(p.id)                                            AS total_evidence,
    COUNT(DISTINCT p.phase)                                AS distinct_phases,
    -- Confidence: sigmoid(evidence_count, threshold=3)
    1.0 / (1.0 + EXP(-(COUNT(p.id)::NUMERIC - 3)))        AS confidence,
    -- Recency weight: days since last evidence
    EXTRACT(EPOCH FROM (NOW() - MAX(p.created_on_utc))) / 86400.0  AS days_since_last
FROM signal s
LEFT JOIN protocol p ON p.signal_source_id = s.id OR p.signal_target_id = s.id
GROUP BY s.person_id, s.code, s.region, s.state;

COMMENT ON VIEW v_evidence_confidence IS
'Confidence scoring per signal.
 distinct_sources: how many different inputs support this state.
 confidence: sigmoid(total_evidence, threshold=3) — 0.5 at 3 evidence.
 days_since_last: recency decay indicator.';


-- ═══════════════════════════════════════════════════════════
-- 3. PREDICTABILISTIC — temporal trajectories, phase progression
-- ═══════════════════════════════════════════════════════════

-- Phase timeline: ordered phase progression per analysis session
CREATE OR REPLACE VIEW v_phase_timeline AS
SELECT
    d.person_id,
    d.id                AS data_id,
    d.kind,
    p.phase,
    MIN(p.id)           AS first_protocol_id,
    MAX(p.id)           AS last_protocol_id,
    COUNT(p.id)         AS protocol_count,
    ARRAY_AGG(DISTINCT p.tag ORDER BY p.tag)  AS tags_in_phase,
    MIN(p.created_on_utc)  AS phase_start,
    MAX(p.created_on_utc)  AS phase_end
FROM data d
JOIN protocol p ON p.data_id = d.id
WHERE p.phase IS NOT NULL
GROUP BY d.person_id, d.id, d.kind, p.phase
ORDER BY d.person_id, d.id, MIN(p.id);

COMMENT ON VIEW v_phase_timeline IS
'Temporal ordering of phases within each analysis session.
 Shows: ONSET -> PROGRESSION -> RESISTANCE -> RESOLUTION.
 Use for phase transition probability: P(next_phase | current_phase).';


-- Signal trajectory: state changes across data entries over time
CREATE OR REPLACE VIEW v_signal_trajectory AS
SELECT
    s.person_id,
    s.code,
    s.region,
    s.type,
    d.id                    AS data_id,
    d.kind                  AS data_kind,
    d.created_on_utc        AS observed_at,
    p.phase,
    p.formula,
    s.state                 AS signal_state,
    -- Ordering for trajectory analysis
    ROW_NUMBER() OVER (
        PARTITION BY s.person_id, s.code, s.region
        ORDER BY d.created_on_utc, p.id
    ) AS observation_seq
FROM signal s
JOIN protocol p ON p.signal_source_id = s.id
JOIN data d ON d.id = p.data_id
ORDER BY s.person_id, s.code, d.created_on_utc;

COMMENT ON VIEW v_signal_trajectory IS
'Signal state changes over time.
 observation_seq: ordering for linear regression / trend detection.
 Use for: trajectory direction, rate of change, stability assessment.';


-- Cascade prediction: given a signal source, what signals follow as targets?
CREATE OR REPLACE VIEW v_cascade_edges AS
SELECT
    p.person_id,
    ss.code             AS source_code,
    ss.state            AS source_state,
    ss.region           AS source_region,
    st.code             AS target_code,
    st.state            AS target_state,
    st.region           AS target_region,
    p.tag,
    p.phase,
    COUNT(*)            AS edge_count,
    p.formula
FROM protocol p
JOIN signal ss ON ss.id = p.signal_source_id
JOIN signal st ON st.id = p.signal_target_id
WHERE p.signal_source_id IS NOT NULL
  AND p.signal_target_id IS NOT NULL
  AND p.signal_source_id <> p.signal_target_id
GROUP BY p.person_id, ss.code, ss.state, ss.region,
         st.code, st.state, st.region, p.tag, p.phase, p.formula;

COMMENT ON VIEW v_cascade_edges IS
'Directed edges in the cascade graph: signal_source -> signal_target.
 edge_count: how many protocols support this edge (weight for prediction).
 Use for: cascade simulation, "if DA↑↑ then what happens to 5HT?".';


-- ═══════════════════════════════════════════════════════════
-- 4. LOGICAL — gate networks, feedback loops, cascade walks
-- ═══════════════════════════════════════════════════════════

-- Gate evaluation network: all gates with their conditions and effects
CREATE OR REPLACE VIEW v_gate_network AS
SELECT
    g.person_id,
    g.code              AS gate_code,
    g.type              AS gate_type,
    g.threshold,
    g.expression,
    g.latched,
    pg.code             AS parent_gate,
    p.formula           AS protocol_formula,
    p.phase,
    p.status,
    ss.code             AS affected_signal,
    ss.state            AS signal_state
FROM gate g
LEFT JOIN gate pg ON pg.id = g.parent_id
LEFT JOIN protocol p ON p.gate_id = g.id
LEFT JOIN signal ss ON ss.id = p.signal_source_id;

COMMENT ON VIEW v_gate_network IS
'Gate logic network. Shows gate → signal relationships.
 parent_gate: for nested/hierarchical gate trees.
 latched: bistable gates that lock state.
 Use for: evaluating conditional logic, finding gate cascades.';


-- Feedback loops: protocols tagged FEEDBACK with source → target chain
CREATE OR REPLACE VIEW v_feedback_loops AS
SELECT
    p.person_id,
    p.id                AS protocol_id,
    p.formula,
    p.phase,
    ss.code             AS loop_from,
    ss.state            AS from_state,
    ss.region           AS from_region,
    st.code             AS loop_to,
    st.state            AS to_state,
    st.region           AS to_region,
    CASE
        WHEN p.formula LIKE '%\u27F3\u207B%' THEN 'negative'
        WHEN p.formula LIKE '%\u27F3\u207A%' THEN 'positive'
        ELSE 'unknown'
    END                 AS loop_type
FROM protocol p
LEFT JOIN signal ss ON ss.id = p.signal_source_id
LEFT JOIN signal st ON st.id = p.signal_target_id
WHERE p.tag IN ('FEEDBACK', 'FORMULA')
  AND (p.formula LIKE '%\u27F3%' OR p.tag = 'FEEDBACK');

COMMENT ON VIEW v_feedback_loops IS
'Feedback loops: negative (⟳⁻) and positive (⟳⁺).
 loop_from → loop_to: the feedback cycle direction.
 Negative: homeostatic (DA.D2 autoreceptor → DA release↓).
 Positive: amplifying (DA↑↑ → PEA↑ → DA release↑↑).
 Use for: stability analysis, runaway detection.';


-- Dysregulation chains: all DYSREG tagged protocols with signal context
CREATE OR REPLACE VIEW v_dysreg_chain AS
SELECT
    p.person_id,
    p.formula                   AS dysreg_formula,
    p.phase,
    p.status,
    ss.code                     AS affected_signal,
    ss.state                    AS signal_state,
    ss.region                   AS signal_region,
    p.created_on_utc
FROM protocol p
LEFT JOIN signal ss ON ss.id = p.signal_source_id
WHERE p.tag = 'DYSREG'
ORDER BY p.person_id, p.created_on_utc;

COMMENT ON VIEW v_dysreg_chain IS
'Dysregulation events (⚡ marker in BioChain notation).
 Chains of maladaptive states that may compound.
 Use for: identifying intervention targets, risk assessment.';


-- Cascade walk function: starting from a signal, find all reachable signals
CREATE OR REPLACE FUNCTION f_cascade_walk(
    p_person_id UUID,
    p_start_signal_code TEXT,
    p_max_depth INT DEFAULT 5
)
RETURNS TABLE (
    depth       INT,
    source_code TEXT,
    source_state TEXT,
    target_code TEXT,
    target_state TEXT,
    tag         TEXT,
    formula     TEXT,
    phase       TEXT
) AS $$
WITH RECURSIVE cascade AS (
    -- Base: all protocols with our starting signal as source
    SELECT
        1 AS depth,
        ss.code AS source_code,
        ss.state AS source_state,
        st.code AS target_code,
        st.state AS target_state,
        p.tag,
        p.formula,
        p.phase,
        ARRAY[ss.code] AS visited
    FROM protocol p
    JOIN signal ss ON ss.id = p.signal_source_id
    JOIN signal st ON st.id = p.signal_target_id
    WHERE p.person_id = p_person_id
      AND ss.code = p_start_signal_code
      AND p.signal_source_id <> p.signal_target_id

    UNION ALL

    -- Recursive: follow target → next source
    SELECT
        c.depth + 1,
        ss.code,
        ss.state,
        st.code,
        st.state,
        p.tag,
        p.formula,
        p.phase,
        c.visited || ss.code
    FROM cascade c
    JOIN signal ss ON ss.code = c.target_code AND ss.person_id = p_person_id
    JOIN protocol p ON p.signal_source_id = ss.id AND p.person_id = p_person_id
    JOIN signal st ON st.id = p.signal_target_id
    WHERE c.depth < p_max_depth
      AND NOT (ss.code = ANY(c.visited))  -- prevent cycles
      AND p.signal_source_id <> p.signal_target_id
)
SELECT c.depth, c.source_code, c.source_state,
       c.target_code, c.target_state, c.tag, c.formula, c.phase
FROM cascade c
ORDER BY c.depth, c.source_code;
$$ LANGUAGE SQL STABLE;

COMMENT ON FUNCTION f_cascade_walk IS
'Walk the cascade graph from a starting signal.
 Returns all reachable signals up to max_depth hops.
 Prevents cycles via visited array.
 Usage: SELECT * FROM f_cascade_walk(person_id, ''DA'', 5);';


-- ═══════════════════════════════════════════════════════════
-- 5. BEHAVIOUR & PERSONALITY — signal profiles, dimensional scoring
-- ═══════════════════════════════════════════════════════════

-- Person signal profile: aggregate view of all signals for a person
CREATE OR REPLACE VIEW v_person_signal_profile AS
SELECT
    s.person_id,
    s.type,
    s.code,
    s.region,
    s.state,
    s.baseline,

    -- Evidence counts
    COUNT(DISTINCT p.id)      AS protocol_mentions,
    COUNT(DISTINCT p.data_id) AS data_sources,
    COUNT(DISTINCT p.phase)   AS phases_active,

    -- Component network
    (SELECT COUNT(*) FROM receptor rc WHERE rc.signal_id = s.id)     AS receptor_count,
    (SELECT COUNT(*) FROM transporter tp WHERE tp.signal_id = s.id)  AS transporter_count,
    (SELECT COUNT(*) FROM limiter lm WHERE lm.target_id = s.id)     AS limiter_count,

    -- Gate involvement
    COUNT(DISTINCT p.gate_id) AS gate_involvements,

    -- Temporal
    MIN(p.created_on_utc)     AS first_seen,
    MAX(p.created_on_utc)     AS last_seen

FROM signal s
LEFT JOIN protocol p ON p.signal_source_id = s.id OR p.signal_target_id = s.id
GROUP BY s.id, s.person_id, s.type, s.code, s.region, s.state, s.baseline;

COMMENT ON VIEW v_person_signal_profile IS
'Complete signal profile per person.
 Each row = one molecule in one region.
 Use for: personality fingerprinting, comparing persons.
 Higher protocol_mentions + data_sources = more reliable assessment.';


-- Layer analysis: signals grouped by BioChain layer (NT / H / P / eCB / NI / NS)
CREATE OR REPLACE VIEW v_layer_analysis AS
SELECT
    s.person_id,
    s.type                                          AS layer,
    COUNT(DISTINCT s.id)                            AS signal_count,
    COUNT(DISTINCT s.code)                          AS unique_chemicals,
    COUNT(DISTINCT p.id)                            AS total_evidence,
    ARRAY_AGG(DISTINCT s.code ORDER BY s.code)      AS chemicals,

    -- State distribution within layer
    COUNT(*) FILTER (WHERE s.state IN ('\u2191\u2191', '\u2191'))  AS elevated_count,
    COUNT(*) FILTER (WHERE s.state = '\u2248')                     AS baseline_count,
    COUNT(*) FILTER (WHERE s.state IN ('\u2193\u2193', '\u2193'))  AS depressed_count,
    COUNT(*) FILTER (WHERE s.state IN ('\u2298', '~'))             AS disrupted_count

FROM signal s
LEFT JOIN protocol p ON p.signal_source_id = s.id
GROUP BY s.person_id, s.type;

COMMENT ON VIEW v_layer_analysis IS
'BioChain layer (NT/H/P/eCB/NI/NS) aggregate analysis.
 NT layer = fast neurotransmitters (DA, 5HT, NE, GABA, GLU).
 H layer  = hormones (cortisol, testosterone, etc.).
 P layer  = neuropeptides (OXT, dynorphin, BDNF, etc.).
 elevated/depressed/disrupted counts = layer health indicators.';


-- Behavioral pattern: recurring signal combinations across multiple data sources
CREATE OR REPLACE VIEW v_behavioral_patterns AS
SELECT
    p1.person_id,
    s1.code         AS signal_a,
    s1.state        AS state_a,
    s2.code         AS signal_b,
    s2.state        AS state_b,
    COUNT(DISTINCT p1.data_id)    AS occurrence_count,
    COUNT(DISTINCT p1.phase)      AS phase_count,
    ARRAY_AGG(DISTINCT p1.phase ORDER BY p1.phase)
        FILTER (WHERE p1.phase IS NOT NULL)  AS phases
FROM protocol p1
JOIN protocol p2 ON p2.person_id = p1.person_id
    AND p2.data_id = p1.data_id
    AND p2.id > p1.id
JOIN signal s1 ON s1.id = p1.signal_source_id
JOIN signal s2 ON s2.id = p2.signal_source_id
WHERE s1.code <> s2.code
GROUP BY p1.person_id, s1.code, s1.state, s2.code, s2.state
HAVING COUNT(DISTINCT p1.data_id) >= 2;

COMMENT ON VIEW v_behavioral_patterns IS
'Signal pairs that co-occur across 2+ different data inputs.
 occurrence_count >= 2: pattern is consistent, not one-off.
 High occurrence + multi-phase = stable behavioral trait.
 Low occurrence + single phase = situational response.
 Use for: personality trait extraction, behavioral predictions.';


-- Intervention targets: limiters and gates that could shift the profile
CREATE OR REPLACE VIEW v_intervention_targets AS
SELECT
    l.person_id,
    'limiter' AS component_type,
    l.code,
    l.activity,
    l.rate_limiting,
    l.reaction,
    s.code   AS target_signal,
    s.state  AS target_signal_state,
    -- Rate-limiting enzymes with abnormal activity = high-impact targets
    CASE
        WHEN l.rate_limiting AND l.activity IN ('\u2193', '\u2193\u2193') THEN 'high'
        WHEN l.rate_limiting THEN 'medium'
        ELSE 'low'
    END      AS intervention_priority
FROM limiter l
LEFT JOIN signal s ON s.id = l.target_id
WHERE l.activity <> '\u2248'

UNION ALL

SELECT
    g.person_id,
    'gate' AS component_type,
    g.code,
    g.type   AS activity,
    g.latched AS rate_limiting,
    g.expression AS reaction,
    ss.code  AS target_signal,
    ss.state AS target_signal_state,
    CASE
        WHEN g.latched THEN 'high'
        WHEN g.type IN ('threshold', 'integrator') THEN 'medium'
        ELSE 'low'
    END      AS intervention_priority
FROM gate g
LEFT JOIN protocol p ON p.gate_id = g.id
LEFT JOIN signal ss ON ss.id = p.signal_source_id
WHERE g.latched = true
   OR g.type IN ('threshold', 'latch');

COMMENT ON VIEW v_intervention_targets IS
'Components where intervention would have the most impact.
 Rate-limiting enzymes with low activity = synthesis bottleneck.
 Latched gates = stuck states that need unlocking.
 Use for: clinical recommendations, optimization targets.';
