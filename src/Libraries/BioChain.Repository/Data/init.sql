-- ═══════════════════════════════════════════════════════════
-- BioChain v5.0 — Core Schema
-- 7 tables. 3 domains. Full notation coverage.
--
-- GRAPH   (static knowledge — the physics engine)
--   node     anything: signal, receptor, enzyme, region, gate, substance
--   edge     any relationship between nodes (formulas)
--   path     named sequences: pathways, circuits, mechanisms
--
-- PERSON  (dynamic, per-individual)
--   person   who
--   event    what happened (append-only evidence)
--   profile  who they are now (versioned state)
--
-- DERIVED (computed by ModulMathematics)
--   trace    what we understand: hypotheses, loops, trajectories
--
-- Design: every table shares the same bones.
-- id | kind | identity | relationships | data | embedding | time
-- If it's not a FK or filtered in WHERE → it lives in data JSONB.
-- ═══════════════════════════════════════════════════════════

CREATE EXTENSION IF NOT EXISTS vector;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS pg_trgm;


-- ═══════════════════════════════════════════════════════════
-- GRAPH DOMAIN
-- The knowledge base. Static, person-independent.
-- Add a chemical = add a node + edges. Schema never changes.
-- ═══════════════════════════════════════════════════════════

-- ── NODE ─────────────────────────────────────────────────
-- Everything in the lexicon. Signals, receptors, enzymes,
-- transporters, regions, gates, substances. One table.
-- parent_id gives hierarchy: DA.D1→DA, PVN→HYP, DAT→DA.

CREATE TABLE node (
    id          SERIAL PRIMARY KEY,
    kind        VARCHAR(12) NOT NULL,               -- signal, receptor, enzyme, transporter, region, gate, substance
    code        VARCHAR(20) NOT NULL UNIQUE,         -- NT:DA, DA.D1, TH, DAT, VTA, SSRI
    label       VARCHAR(100) NOT NULL,               -- Dopamine, D1 Receptor, Tyrosine Hydroxylase
    parent_id   INT REFERENCES node(id),             -- hierarchy: receptor→signal, enzyme→substrate, subregion→region
    data        JSONB DEFAULT '{}',                  -- kind-specific payload:
                                                     --   signal:      {layer, unit, normal_range}
                                                     --   receptor:    {g_protein, ion_channel, location}
                                                     --   enzyme:      {function, is_rate_limiting, product}
                                                     --   transporter: {transport_type, location}
                                                     --   region:      {region_type}
                                                     --   gate:        {gate_type, symbol, rule}
                                                     --   substance:   {class, dosing}
    embedding   vector(1536)
);

CREATE INDEX idx_node_kind     ON node(kind);
CREATE INDEX idx_node_code     ON node(code);
CREATE INDEX idx_node_parent   ON node(parent_id);
CREATE INDEX idx_node_data     ON node USING GIN(data);
CREATE INDEX idx_node_embed    ON node USING hnsw(embedding vector_cosine_ops);

COMMENT ON TABLE node IS 'Universal lexicon. kind discriminates. parent_id gives hierarchy. data carries type-specific attributes.';


-- ── EDGE ─────────────────────────────────────────────────
-- Every relationship between nodes. Each row = one BioChain
-- formula = one edge in the knowledge graph.

CREATE TABLE edge (
    id          SERIAL PRIMARY KEY,
    source_id   INT NOT NULL REFERENCES node(id),
    target_id   INT NOT NULL REFERENCES node(id),
    kind        VARCHAR(12) NOT NULL,               -- formula, feedback, modulation, mechanism
    formula     TEXT NOT NULL,                       -- full BioChain notation string
    data        JSONB DEFAULT '{}',                  -- {operator, region_id, temporal, feedback_type,
                                                     --  gate_expression, mod_factor, modulation_type,
                                                     --  dose_response:{pattern,low,optimal,high},
                                                     --  mechanism, tau, confidence}
    embedding   vector(1536)
);

CREATE INDEX idx_edge_source   ON edge(source_id);
CREATE INDEX idx_edge_target   ON edge(target_id);
CREATE INDEX idx_edge_kind     ON edge(kind);
CREATE INDEX idx_edge_data     ON edge USING GIN(data);
CREATE INDEX idx_edge_embed    ON edge USING hnsw(embedding vector_cosine_ops);

COMMENT ON TABLE edge IS 'Knowledge graph edges. One row = one formula. kind: formula, feedback, modulation, mechanism.';


-- ── PATH ─────────────────────────────────────────────────
-- Named sequences of edges. Pathways, circuits, templates,
-- intervention mechanisms. Steps live in data JSONB.

CREATE TABLE path (
    id      SERIAL PRIMARY KEY,
    code    VARCHAR(50) NOT NULL UNIQUE,             -- hpa_axis, mesolimbic_da, ssri_mechanism
    kind    VARCHAR(15) NOT NULL,                    -- pathway, circuit, template, mechanism
    data    JSONB NOT NULL                           -- {label, template_type, source_region, target_region,
                                                     --  compact_formula,
                                                     --  steps: [{node_id, operator, region_id, gate, formula}],
                                                     --  phases: [{order, label, temporal, state_block}],
                                                     --  targets, acute, chronic, key_gate}
);

CREATE INDEX idx_path_code     ON path(code);
CREATE INDEX idx_path_kind     ON path(kind);
CREATE INDEX idx_path_data     ON path USING GIN(data);

COMMENT ON TABLE path IS 'Named sequences. Pathways, circuits, intervention mechanisms. Steps and phases in data JSONB.';


-- ═══════════════════════════════════════════════════════════
-- PERSON DOMAIN
-- Dynamic, per-individual. Events flow in, profiles evolve.
-- ═══════════════════════════════════════════════════════════

-- ── PERSON ───────────────────────────────────────────────

CREATE TABLE person (
    id          UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    owner_id    TEXT NOT NULL,                        -- auth system user ID
    name        VARCHAR(100) NOT NULL,
    data        JSONB DEFAULT '{}',                   -- email, phone, birthdate, demographics
    created_at  TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_person_owner  ON person(owner_id);
CREATE UNIQUE INDEX idx_person_owner_name ON person(owner_id, name);

COMMENT ON TABLE person IS 'Identity. Minimal — rich data lives in events and profiles.';


-- ── EVENT ────────────────────────────────────────────────
-- Append-only evidence. Each row = one formula observed or
-- inferred for a person. The atomic input to everything.

CREATE TABLE event (
    id          SERIAL PRIMARY KEY,
    person_id   UUID NOT NULL REFERENCES person(id) ON DELETE CASCADE,
    node_id     INT NOT NULL REFERENCES node(id),    -- primary signal/entity observed
    kind        VARCHAR(15) NOT NULL,                -- observation, questionnaire, clinical, wearable, inferred, behavioral
    formula     TEXT NOT NULL,                       -- full BioChain notation
    data        JSONB DEFAULT '{}',                  -- {subject_state, operator, target_id, target_state,
                                                     --  region_id, temporal, feedback_type, failure_mode,
                                                     --  confidence, intensity, dose_range, gate_formula,
                                                     --  receptor_state, tags:[], notes, raw_input}
    embedding   vector(1536),
    created_at  TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_event_person   ON event(person_id);
CREATE INDEX idx_event_node     ON event(node_id);
CREATE INDEX idx_event_kind     ON event(kind);
CREATE INDEX idx_event_data     ON event USING GIN(data);
CREATE INDEX idx_event_embed    ON event USING hnsw(embedding vector_cosine_ops);
CREATE INDEX idx_event_time     ON event(person_id, created_at DESC);

COMMENT ON TABLE event IS 'Append-only evidence log. One row = one observed/inferred formula. The atomic input.';


-- ── PROFILE ──────────────────────────────────────────────
-- The person's current neurochemical state. Versioned via
-- prior_id → walk the chain for full history.
-- Recomputed as events accumulate.

CREATE TABLE profile (
    id          SERIAL PRIMARY KEY,
    person_id   UUID NOT NULL REFERENCES person(id) ON DELETE CASCADE,
    prior_id    INT REFERENCES profile(id),          -- previous version → history chain
    state       JSONB NOT NULL,                      -- {signals: {NT:DA: {state, baseline, sensitivity, confidence}},
                                                     --  receptors: {DA.D2: {state, density}},
                                                     --  active_pathways: [{path_id, states, bottlenecks}]}
    data        JSONB DEFAULT '{}',                  -- {summary, tags:[], computed_by, model_version}
    embedding   vector(1536),                        -- profile vector for similarity + drift
    created_at  TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_profile_person ON profile(person_id);
CREATE INDEX idx_profile_prior  ON profile(prior_id);
CREATE INDEX idx_profile_state  ON profile USING GIN(state);
CREATE INDEX idx_profile_embed  ON profile USING hnsw(embedding vector_cosine_ops);
CREATE INDEX idx_profile_latest ON profile(person_id, created_at DESC);

COMMENT ON TABLE profile IS 'Versioned neurochemical profile. prior_id chains = full history. state carries signals + receptors + active pathways.';


-- ═══════════════════════════════════════════════════════════
-- DERIVED DOMAIN
-- Computed by ModulMathematics. ML × LLM × vectors ×
-- algorithms × probabilistic models write here.
-- ═══════════════════════════════════════════════════════════

-- ── TRACE ────────────────────────────────────────────────
-- Everything we've figured out about a person.
-- Hypotheses, predictions, loops, trajectories — unified.
-- kind discriminates. status evolves. evidence links back.

CREATE TABLE trace (
    id          SERIAL PRIMARY KEY,
    person_id   UUID NOT NULL REFERENCES person(id) ON DELETE CASCADE,
    kind        VARCHAR(12) NOT NULL,                -- hypothesis, prediction, loop, trajectory
    status      VARCHAR(12) NOT NULL DEFAULT 'active',
                                                     -- hypothesis: active, confirmed, eliminated, superseded
                                                     -- loop:       intact, degraded, broken, latched, resolved
                                                     -- trajectory: active, resolved
    confidence  FLOAT,                               -- 0.0–1.0 (hypotheses + predictions)
    formula     TEXT,                                -- BioChain notation (loops, pathway traces)
    evidence    INT[] DEFAULT '{}',                  -- → event.id array
    data        JSONB DEFAULT '{}',                  -- kind-specific payload:
                                                     --   hypothesis:  {trigger, state, pathway_trace, gates,
                                                     --                 feedback, dysregulation, predictions,
                                                     --                 distinguishers}
                                                     --   prediction:  {signal_id, predicted_state, tau,
                                                     --                 expected_by, actual_state, outcome}
                                                     --   loop:        {loop_type, polarity, feedback_type,
                                                     --                 failure_mode, involved_ids:[], path_id,
                                                     --                 severity, resolved_at}
                                                     --   trajectory:  {phases:[], path_id, domain}
    embedding   vector(1536),
    created_at  TIMESTAMPTZ DEFAULT NOW(),
    updated_at  TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_trace_person   ON trace(person_id);
CREATE INDEX idx_trace_kind     ON trace(kind);
CREATE INDEX idx_trace_status   ON trace(status);
CREATE INDEX idx_trace_evidence ON trace USING GIN(evidence);
CREATE INDEX idx_trace_data     ON trace USING GIN(data);
CREATE INDEX idx_trace_embed    ON trace USING hnsw(embedding vector_cosine_ops);
CREATE INDEX idx_trace_active   ON trace(person_id, kind) WHERE status IN ('active', 'intact');

COMMENT ON TABLE trace IS 'Unified derived understanding. kind: hypothesis, prediction, loop, trajectory. evidence links to events.';


-- ═══════════════════════════════════════════════════════════
-- TABLE → BIOCHAIN LAYER MAPPING
-- ═══════════════════════════════════════════════════════════
--
--  Layer 0  Lexicon         → node (kind discriminates type)
--  Layer 1  State           → event.data.subject_state + profile.state
--  Layer 2  Operators       → edge.data.operator + event.data.operator
--  Layer 2  Feedback        → edge.data.feedback_type + trace.data.feedback_type
--  Layer 2  Dysregulation   → event.data.failure_mode + trace.data.failure_mode
--  Layer 2  Temporal        → edge.data.temporal + event.data.temporal
--  Layer 3  Gates           → node(kind='gate') + edge.data.gate_expression
--  Layer 4  Pathways        → path(kind='pathway'|'circuit'|'template')
--  Layer 4  Hypotheses      → trace(kind='hypothesis')
--  Layer 4  Dose-Response   → edge.data.dose_response
--  Layer 5  Formula Mode    → event.formula + edge.formula
--  Persist  BioConstants    → profile.state
--  Persist  SignalHistory   → profile chain (prior_id)
--  Persist  Interventions   → node(kind='substance') + path(kind='mechanism') + edge(kind='mechanism')
--  Persist  Predictions     → trace(kind='prediction')
--  Persist  Active Loops    → trace(kind='loop')
--  Persist  Trajectories    → trace(kind='trajectory')
--
-- ═══════════════════════════════════════════════════════════