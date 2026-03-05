-- ═══════════════════════════════════════════════════════════
-- signals_init.sql — Universal Schema
-- Signals Kernel v1.5 Database Layer
--
-- Domain-agnostic. Vocabulary provides codes, types, regions.
-- ALL tables append-only. Current state = latest row by created_on_utc.
-- ALL tables entity-scoped (entity_id NULL = global template).
-- ═══════════════════════════════════════════════════════════

CREATE EXTENSION IF NOT EXISTS vector;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS pg_trgm;


-- ═══════════════════════════════════════
-- ENTITY — the scope target
-- ═══════════════════════════════════════
CREATE TABLE entity (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    owner_id        TEXT NOT NULL,
    namespace       VARCHAR(30) NOT NULL,
    name            VARCHAR(100) NOT NULL,
    kind            VARCHAR(30) NOT NULL DEFAULT 'person',
    data            JSONB DEFAULT '{}',
    embedding       vector(1536),
    created_on_utc  TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_entity_owner ON entity(owner_id);
CREATE UNIQUE INDEX idx_entity_owner_name ON entity(owner_id, namespace, name);
CREATE INDEX idx_entity_ns ON entity(namespace);
CREATE INDEX idx_entity_kind ON entity(kind);
CREATE INDEX idx_entity_embed ON entity USING hnsw(embedding vector_cosine_ops);
COMMENT ON TABLE entity IS
'Scope target. What the graph describes.
 kind: person | market | organism | system | organization | population | device.
 namespace: from vocabulary (bio, mkt, soc, etc). owner_id: tenant/user.';


-- ═══════════════════════════════════════
-- STIMULI — parser inbox, system trigger
-- ═══════════════════════════════════════
CREATE TABLE stimuli (
    id              SERIAL PRIMARY KEY,
    entity_id       UUID NOT NULL REFERENCES entity(id) ON DELETE CASCADE,
    kind            VARCHAR(30) NOT NULL,
    source_text     TEXT,
    formula         TEXT,
    analyzed        BOOLEAN DEFAULT false,
    content         JSONB DEFAULT '{}',
    embedding       vector(1536),
    created_on_utc  TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_stimuli_eid ON stimuli(entity_id);
CREATE INDEX idx_stimuli_kind ON stimuli(kind);
CREATE INDEX idx_stimuli_queue ON stimuli(entity_id, analyzed) WHERE analyzed = false;
CREATE INDEX idx_stimuli_time ON stimuli(entity_id, created_on_utc DESC);
CREATE INDEX idx_stimuli_content ON stimuli USING GIN(content);
CREATE INDEX idx_stimuli_embed ON stimuli USING hnsw(embedding vector_cosine_ops);
COMMENT ON TABLE stimuli IS
'Parser inbox. Everything the system reacts to. Append-only.
 kind: chat | observation | clinical | wearable | behavioral | formula | intervention.
 analyzed=false: queued for parsing. analyzed=true: consumed, graph updated.
 System outputs (events, snapshots, tool results) go to protocol + edge tables,
 not here. Stimuli flow in. Signals flow through. Protocol records what happened.';


-- ═══════════════════════════════════════
-- MODULE — scoped subgraph
-- ═══════════════════════════════════════
CREATE TABLE module (
    id              SERIAL PRIMARY KEY,
    entity_id       UUID REFERENCES entity(id) ON DELETE CASCADE,
    code            VARCHAR(50) NOT NULL,
    namespace       VARCHAR(30),
    parent_id       INT REFERENCES module(id),
    agent_type      VARCHAR(30),
    properties      JSONB DEFAULT '{}',
    created_on_utc  TIMESTAMPTZ DEFAULT NOW()
);
CREATE UNIQUE INDEX idx_module_code ON module(entity_id, code);
CREATE INDEX idx_module_parent ON module(parent_id);
CREATE INDEX idx_module_agent ON module(agent_type);
CREATE INDEX idx_module_ns ON module(namespace);
COMMENT ON TABLE module IS
'Scoped subgraph. Hierarchical via parent_id.
 agent_type: NULL (plain module) | reactive | stateful | threshold
             | goal | strategic | social | reasoning.
 namespace: overrides entity namespace for cross-domain modules.
 entity_id NULL = global template module.';


-- ═══════════════════════════════════════
-- REGION — scope/location within entity
-- ═══════════════════════════════════════
CREATE TABLE region (
    id              SERIAL PRIMARY KEY,
    entity_id       UUID REFERENCES entity(id) ON DELETE CASCADE,
    code            VARCHAR(30) NOT NULL,
    full_name       VARCHAR(100),
    system          VARCHAR(30),
    parent_id       INT REFERENCES region(id),
    module_id       INT REFERENCES module(id),
    activity_state  VARCHAR(15) DEFAULT 'unknown',
    dominant_signal VARCHAR(20),
    stress_load     VARCHAR(5) DEFAULT '≈',
    properties      JSONB DEFAULT '{}',
    cause           TEXT,
    created_on_utc  TIMESTAMPTZ DEFAULT NOW()
);
CREATE UNIQUE INDEX idx_region_global_code ON region(code) WHERE entity_id IS NULL;
CREATE INDEX idx_region_latest ON region(entity_id, code, created_on_utc DESC) WHERE entity_id IS NOT NULL;
CREATE INDEX idx_region_entity ON region(entity_id);
CREATE INDEX idx_region_system ON region(system);
CREATE INDEX idx_region_parent ON region(parent_id);
CREATE INDEX idx_region_module ON region(module_id);
CREATE INDEX idx_region_activity ON region(entity_id, activity_state);
COMMENT ON TABLE region IS
'Location/scope within entity. Append-only.
 activity_state: homeostatic | elevated | depleted | mixed | dysregulated | unknown.
 stress_load: ↑↑ | ↑ | ≈ | ↓ | ↓↓.
 Vocabulary defines valid codes (brain regions, exchanges, sectors, etc).
 entity_id NULL = global template. module_id scopes to subgraph.';


-- ═══════════════════════════════════════
-- SIGNAL — the register
-- ═══════════════════════════════════════
CREATE TABLE signal (
    id              SERIAL PRIMARY KEY,
    entity_id       UUID NOT NULL REFERENCES entity(id) ON DELETE CASCADE,
    type            VARCHAR(10) NOT NULL,
    code            VARCHAR(30) NOT NULL,
    region_id       INT REFERENCES region(id),
    module_id       INT REFERENCES module(id),
    state           VARCHAR(10) NOT NULL DEFAULT '≈',
    value           NUMERIC,
    unit            VARCHAR(20),
    baseline        NUMERIC,
    deviation_pct   NUMERIC,
    range_low       NUMERIC,
    range_high      NUMERIC,
    confidence      NUMERIC DEFAULT 1.0,
    distribution    VARCHAR(30),
    tau_min_ms      BIGINT,
    tau_max_ms      BIGINT,
    trend           VARCHAR(15),
    cause           TEXT,
    protocol_id     INT,
    created_on_utc  TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_signal_latest ON signal(entity_id, code, region_id, created_on_utc DESC);
CREATE INDEX idx_signal_eid ON signal(entity_id);
CREATE INDEX idx_signal_type ON signal(type);
CREATE INDEX idx_signal_code ON signal(code);
CREATE INDEX idx_signal_region ON signal(region_id);
CREATE INDEX idx_signal_module ON signal(module_id);
CREATE INDEX idx_signal_state ON signal(entity_id, state);
CREATE INDEX idx_signal_conf ON signal(entity_id, confidence);
COMMENT ON TABLE signal IS
'The register. Append-only. Current = latest row for (entity_id, code, region_id).
 type: from vocabulary (NT, H, P for bio; PRICE, VOL for market; etc).
 state: ↑↑ | ↑ | ≈ | ↓ | ↓↓ | ~ | ⊘ | ●.
 value/unit/baseline/deviation_pct: L2 numeric payload.
 distribution: N(μ,σ) | U(lo,hi) | B(p) for stochastic mode.
 confidence: 0.0-1.0. trend: rising | falling | stable | oscillating | recovering.';


-- ═══════════════════════════════════════
-- RECEPTOR — input component
-- ═══════════════════════════════════════
CREATE TABLE receptor (
    id              SERIAL PRIMARY KEY,
    entity_id       UUID NOT NULL REFERENCES entity(id) ON DELETE CASCADE,
    signal_id       INT NOT NULL REFERENCES signal(id) ON DELETE CASCADE,
    code            VARCHAR(30) NOT NULL,
    subtype         VARCHAR(20),
    module_id       INT REFERENCES module(id),
    state           VARCHAR(10) NOT NULL DEFAULT 'active',
    cause           TEXT,
    protocol_id     INT,
    created_on_utc  TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_receptor_latest ON receptor(entity_id, code, created_on_utc DESC);
CREATE INDEX idx_receptor_eid ON receptor(entity_id);
CREATE INDEX idx_receptor_signal ON receptor(signal_id);
CREATE INDEX idx_receptor_module ON receptor(module_id);
CREATE INDEX idx_receptor_state ON receptor(entity_id, state);
COMMENT ON TABLE receptor IS
'Input component. Append-only.
 state: active | desens | intern | upreg | downreg | resist | primed.
 subtype: vocabulary-defined (Gs, Gi for bio; limit_order, market_order for market; etc).';


-- ═══════════════════════════════════════
-- TRANSPORTER — clearance component
-- ═══════════════════════════════════════
CREATE TABLE transporter (
    id              SERIAL PRIMARY KEY,
    entity_id       UUID NOT NULL REFERENCES entity(id) ON DELETE CASCADE,
    signal_id       INT NOT NULL REFERENCES signal(id) ON DELETE CASCADE,
    code            VARCHAR(30) NOT NULL,
    module_id       INT REFERENCES module(id),
    state           VARCHAR(10) NOT NULL DEFAULT 'active',
    clearance       VARCHAR(5) NOT NULL DEFAULT '≈',
    cause           TEXT,
    protocol_id     INT,
    created_on_utc  TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_transporter_latest ON transporter(entity_id, code, created_on_utc DESC);
CREATE INDEX idx_transporter_eid ON transporter(entity_id);
CREATE INDEX idx_transporter_signal ON transporter(signal_id);
CREATE INDEX idx_transporter_module ON transporter(module_id);
COMMENT ON TABLE transporter IS
'Clearance component. Append-only.
 state: active | blocked | impaired | enhanced. clearance: ↑↑ | ↑ | ≈ | ↓ | ↓↓ | ⊘.';


-- ═══════════════════════════════════════
-- GATE — control point (includes LLM_GATE)
-- ═══════════════════════════════════════
CREATE TABLE gate (
    id              SERIAL PRIMARY KEY,
    entity_id       UUID NOT NULL REFERENCES entity(id) ON DELETE CASCADE,
    code            VARCHAR(100) NOT NULL,
    type            VARCHAR(15) NOT NULL,
    module_id       INT REFERENCES module(id),
    threshold       VARCHAR(5),
    expression      TEXT,
    probability     NUMERIC,
    parent_id       INT REFERENCES gate(id),
    latched         BOOLEAN DEFAULT false,
    history         TEXT[] DEFAULT '{}',
    -- LLM_GATE fields (NULL for standard gates)
    prompt          TEXT,
    model           VARCHAR(50),
    parse_map       JSONB,
    fallback_expr   TEXT,
    timeout_ms      INT,
    cache_ms        INT,
    --
    cause           TEXT,
    protocol_id     INT,
    created_on_utc  TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_gate_latest ON gate(entity_id, code, created_on_utc DESC);
CREATE INDEX idx_gate_eid ON gate(entity_id);
CREATE INDEX idx_gate_type ON gate(type);
CREATE INDEX idx_gate_module ON gate(module_id);
CREATE INDEX idx_gate_parent ON gate(parent_id);
CREATE INDEX idx_gate_latched ON gate(entity_id) WHERE latched = true;
CREATE INDEX idx_gate_llm ON gate(entity_id, type) WHERE type = 'llm';
COMMENT ON TABLE gate IS
'Control point. Append-only.
 type: threshold | latch | integrator | novelty | gain | and | or | not
       | xor | splitter | llm.
 probability: p:N for probabilistic gates (0.0-1.0).
 type=llm activates: prompt, model, parse_map, fallback_expr, timeout_ms, cache_ms.
 fallback_expr: standard gate expression used when LLM unavailable.
 parse_map: JSON mapping {signal_ref: json_path} for LLM response parsing.';


-- ═══════════════════════════════════════
-- LIMITER — rate-limiting step
-- ═══════════════════════════════════════
CREATE TABLE limiter (
    id              SERIAL PRIMARY KEY,
    entity_id       UUID NOT NULL REFERENCES entity(id) ON DELETE CASCADE,
    target_id       INT REFERENCES signal(id),
    code            VARCHAR(30) NOT NULL,
    module_id       INT REFERENCES module(id),
    reaction        TEXT,
    rate_limiting   BOOLEAN DEFAULT false,
    activity        VARCHAR(10) NOT NULL DEFAULT '≈',
    cause           TEXT,
    protocol_id     INT,
    created_on_utc  TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_limiter_latest ON limiter(entity_id, code, created_on_utc DESC);
CREATE INDEX idx_limiter_eid ON limiter(entity_id);
CREATE INDEX idx_limiter_target ON limiter(target_id);
CREATE INDEX idx_limiter_module ON limiter(module_id);
CREATE INDEX idx_limiter_bottleneck ON limiter(entity_id) WHERE rate_limiting = true;
COMMENT ON TABLE limiter IS
'Rate-limiting step. Append-only.
 rate_limiting: true = bottleneck (⧫). activity: ↑↑ | ↑ | ≈ | ↓ | ↓↓ | ⊘.
 Vocabulary defines codes (enzymes for bio, regulators for market, etc).';


-- ═══════════════════════════════════════
-- INTERFACE — cross-region bridge
-- ═══════════════════════════════════════
CREATE TABLE interface (
    id              SERIAL PRIMARY KEY,
    entity_id       UUID NOT NULL REFERENCES entity(id) ON DELETE CASCADE,
    code            VARCHAR(30) NOT NULL,
    source_region_id INT NOT NULL REFERENCES region(id),
    target_region_id INT NOT NULL REFERENCES region(id),
    module_id       INT REFERENCES module(id),
    pathway         VARCHAR(50),
    pathway_id      INT,  -- FK added after pathway table created
    active          BOOLEAN DEFAULT true,
    cause           TEXT,
    protocol_id     INT,
    created_on_utc  TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_interface_latest ON interface(entity_id, code, created_on_utc DESC);
CREATE INDEX idx_interface_eid ON interface(entity_id);
CREATE INDEX idx_interface_source ON interface(source_region_id);
CREATE INDEX idx_interface_target ON interface(target_region_id);
CREATE INDEX idx_interface_module ON interface(module_id);
CREATE INDEX idx_interface_active ON interface(entity_id) WHERE active = true;
CREATE INDEX idx_iface_pathway ON interface(pathway_id) WHERE pathway_id IS NOT NULL;
COMMENT ON TABLE interface IS
'Cross-region bridge. Append-only. active: false = disconnected.
 pathway_id: FK to pathway (typed reference, replaces VARCHAR pathway during transition).
 Also serves as MODULE INTERFACE marker when module_id set.';


-- ═══════════════════════════════════════
-- LOOP — feedback cycle header
-- ═══════════════════════════════════════
CREATE TABLE loop (
    id               SERIAL PRIMARY KEY,
    entity_id        UUID    NOT NULL REFERENCES entity(id) ON DELETE CASCADE,
    module_id        INT     NOT NULL REFERENCES module(id),
    polarity         VARCHAR(5)  NOT NULL,
    subtype          VARCHAR(20),
    gain_product     NUMERIC,
    time_constant_ms BIGINT,
    active           BOOLEAN DEFAULT true,
    protocol_id      INT,  -- FK added after protocol table
    created_on_utc   TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_loop_entity   ON loop(entity_id);
CREATE INDEX idx_loop_module   ON loop(entity_id, module_id);
CREATE INDEX idx_loop_polarity ON loop(entity_id, polarity);
CREATE INDEX idx_loop_active   ON loop(entity_id) WHERE active = true;
CREATE INDEX idx_loop_runaway  ON loop(entity_id)
    WHERE gain_product > 1 AND active = true;
COMMENT ON TABLE loop IS
'Feedback cycle header. Identity and name live in module (agent_type=''feedback_loop'').
 Membership: edge.loop_id FK points here.
 polarity: negative/stabilizing | positive/amplifying.
 subtype: desens | adapt | decay | latch | amplify.
 gain_product: pre-computed product of all member edge.gain values.
   < 1 = damped/homeostatic  > 1 = runaway/pathological  = -1 = oscillator.
 Append-only. Set active=false when any member edge breaks the cycle.';


-- ═══════════════════════════════════════
-- PLASTICITY — induction context record
-- ═══════════════════════════════════════
CREATE TABLE plasticity (
    id               SERIAL PRIMARY KEY,
    entity_id        UUID    NOT NULL REFERENCES entity(id) ON DELETE CASCADE,
    edge_id          INT,    -- FK added after edge table
    receptor_id      INT     REFERENCES receptor(id),
    plasticity_type  VARCHAR(20) NOT NULL,
    timescale        VARCHAR(20),
    induction_id     INT     REFERENCES signal(id),
    consolidation    BOOLEAN DEFAULT false,
    reversible       BOOLEAN DEFAULT true,
    protocol_id      INT,   -- FK added after protocol table
    created_on_utc   TIMESTAMPTZ DEFAULT NOW(),

    CONSTRAINT plasticity_target_check
        CHECK (edge_id IS NOT NULL OR receptor_id IS NOT NULL)
);
CREATE INDEX idx_plast_entity    ON plasticity(entity_id);
CREATE INDEX idx_plast_edge      ON plasticity(edge_id);
CREATE INDEX idx_plast_receptor  ON plasticity(receptor_id);
CREATE INDEX idx_plast_type      ON plasticity(entity_id, plasticity_type);
CREATE INDEX idx_plast_solid     ON plasticity(entity_id) WHERE consolidation = true;
CREATE INDEX idx_plast_time      ON plasticity(entity_id, created_on_utc DESC);
CREATE INDEX idx_plast_induction ON plasticity(induction_id);
COMMENT ON TABLE plasticity IS
'Induction context for a gain or receptor state change. Append-only.
 Points to edge or receptor that changed + the signal that triggered it.
 plasticity_type: LTP | LTD | homeostatic | receptor_scaling | metaplasticity | structural.
 timescale: rapid_ms | early_ltp | late_ltp | chronic | developmental.
 consolidation: false = labile. true = permanent.
 CONSTRAINT: at least one of edge_id or receptor_id must be set.';


-- ═══════════════════════════════════════
-- PATHWAY — named multi-hop route header
-- ═══════════════════════════════════════
CREATE TABLE pathway (
    id               SERIAL PRIMARY KEY,
    entity_id        UUID    REFERENCES entity(id) ON DELETE CASCADE,
    module_id        INT     NOT NULL REFERENCES module(id),
    source_region_id INT     REFERENCES region(id),
    target_region_id INT     REFERENCES region(id),
    expression       TEXT,
    active           BOOLEAN DEFAULT true,
    protocol_id      INT,   -- FK added after protocol table
    created_on_utc   TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_pathway_entity  ON pathway(entity_id);
CREATE INDEX idx_pathway_module  ON pathway(entity_id, module_id);
CREATE INDEX idx_pathway_source  ON pathway(source_region_id);
CREATE INDEX idx_pathway_target  ON pathway(target_region_id);
CREATE INDEX idx_pathway_active  ON pathway(entity_id) WHERE active = true;
COMMENT ON TABLE pathway IS
'Named multi-hop route header. Identity and class live in module.
 Membership: interface.pathway_id (region bridges) + edge.pathway_id (signal hops).
 expression: raw DEF formula string for round-trip fidelity.
 pathway class stored in module.properties->>''class'':
   projection | cascade | circuit | axis | shunt | loop.
 entity_id NULL = global vocabulary pathway (textbook anatomy).
 Append-only — set active=false when pathway is disrupted, never delete.';


-- ═══════════════════════════════════════
-- CONSTRAINT — simultaneous conditions
-- ═══════════════════════════════════════
CREATE TABLE constraint_def (
    id              SERIAL PRIMARY KEY,
    entity_id       UUID REFERENCES entity(id) ON DELETE CASCADE,
    type            VARCHAR(15) NOT NULL,
    expression      TEXT NOT NULL,
    epsilon         NUMERIC,
    confidence      NUMERIC DEFAULT 1.0,
    module_id       INT REFERENCES module(id),
    active          BOOLEAN DEFAULT true,
    protocol_id     INT,
    created_on_utc  TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_constraint_eid ON constraint_def(entity_id);
CREATE INDEX idx_constraint_type ON constraint_def(type);
CREATE INDEX idx_constraint_module ON constraint_def(module_id);
CREATE INDEX idx_constraint_active ON constraint_def(entity_id) WHERE active = true;
COMMENT ON TABLE constraint_def IS
'Simultaneous conditions. Solved post-cascade.
 type: constraint | equilibrium | boundary | conserve.
 expression: full constraint text (e.g. "sum(X.R, Y.R) == CONST").
 epsilon: convergence tolerance for equilibrium type.
 confidence: soft constraint weight (1.0 = hard). entity_id NULL = global.';


-- ═══════════════════════════════════════
-- TOOL — external action bridge
-- ═══════════════════════════════════════
CREATE TABLE tool (
    id              SERIAL PRIMARY KEY,
    entity_id       UUID REFERENCES entity(id) ON DELETE CASCADE,
    code            VARCHAR(50) NOT NULL,
    invoke          TEXT NOT NULL,
    input_refs      TEXT[] NOT NULL,
    output_refs     TEXT[] NOT NULL,
    gate_expr       TEXT,
    timeout_ms      INT DEFAULT 10000,
    retry_count     INT DEFAULT 0,
    fallback        JSONB,
    module_id       INT REFERENCES module(id),
    protocol_id     INT,
    created_on_utc  TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_tool_eid ON tool(entity_id);
CREATE INDEX idx_tool_code ON tool(code);
CREATE INDEX idx_tool_module ON tool(module_id);
COMMENT ON TABLE tool IS
'External action bridge. Kernel signals in, external results back as signals.
 invoke: endpoint URL, function path, or registered tool name.
 input_refs/output_refs: signal references marshaled/unmarshaled.
 gate_expr: when to invoke. fallback: signal assignments on failure.
 entity_id NULL = global tool definition.';


-- ═══════════════════════════════════════
-- PROTOCOL — immutable audit log
-- ═══════════════════════════════════════
CREATE TABLE protocol (
    id              SERIAL PRIMARY KEY,
    entity_id       UUID REFERENCES entity(id) ON DELETE CASCADE,
    stimuli_id      INT REFERENCES stimuli(id),
    module_id       INT REFERENCES module(id),
    tag             VARCHAR(30),
    formula         TEXT NOT NULL,
    status          TEXT,
    phase           TEXT,
    seq             INT,
    -- edge fields (inline to avoid join for simple formulas)
    edge_gain       NUMERIC,
    edge_noise      NUMERIC,
    edge_fn         VARCHAR(10),
    edge_delay_ms   BIGINT,
    edge_clamp_lo   NUMERIC,
    edge_clamp_hi   NUMERIC,
    -- bind/fail fields
    bind_expr       TEXT,
    fail_condition  TEXT,
    fail_consequence TEXT,
    fail_held_ms    BIGINT,
    --
    embedding       vector(1536),
    created_on_utc  TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_protocol_eid ON protocol(entity_id);
CREATE INDEX idx_protocol_tag ON protocol(tag);
CREATE INDEX idx_protocol_phase ON protocol(phase);
CREATE INDEX idx_protocol_stimuli ON protocol(stimuli_id);
CREATE INDEX idx_protocol_module ON protocol(module_id);
CREATE INDEX idx_protocol_seq ON protocol(entity_id, phase, seq);
CREATE INDEX idx_protocol_embed ON protocol USING hnsw(embedding vector_cosine_ops);
COMMENT ON TABLE protocol IS
'Immutable audit log. Every tag line = one protocol row.
 tag: SIGNAL | RECEPTOR | GATE | LIMITER | FEEDBACK | FORMULA | STATE
      | TRANSPORT | INTERFACE | DEF | DYSREG | HYPOTHESIS | PREDICTION
      | INTERVENTION | EDGE | CONSTRAINT | EQUILIBRIUM | BOUNDARY
      | CONSERVE | TOOL | LLM_GATE | EMIT | MESSAGE | MODULE | IMPORT
      | LOOP | PLASTICITY | PATHWAY.
 entity_id NULL = global textbook. module_id scopes to subgraph.
 LOOP: references loop.id in properties. PLASTICITY: references plasticity.id.
 PATHWAY / DEF: references pathway.id.';

-- FK constraints added after protocol exists
ALTER TABLE signal ADD CONSTRAINT fk_signal_protocol FOREIGN KEY (protocol_id) REFERENCES protocol(id);
ALTER TABLE receptor ADD CONSTRAINT fk_receptor_protocol FOREIGN KEY (protocol_id) REFERENCES protocol(id);
ALTER TABLE transporter ADD CONSTRAINT fk_transporter_protocol FOREIGN KEY (protocol_id) REFERENCES protocol(id);
ALTER TABLE gate ADD CONSTRAINT fk_gate_protocol FOREIGN KEY (protocol_id) REFERENCES protocol(id);
ALTER TABLE limiter ADD CONSTRAINT fk_limiter_protocol FOREIGN KEY (protocol_id) REFERENCES protocol(id);
ALTER TABLE interface ADD CONSTRAINT fk_interface_protocol FOREIGN KEY (protocol_id) REFERENCES protocol(id);
ALTER TABLE loop ADD CONSTRAINT fk_loop_protocol FOREIGN KEY (protocol_id) REFERENCES protocol(id);
ALTER TABLE plasticity ADD CONSTRAINT fk_plast_protocol FOREIGN KEY (protocol_id) REFERENCES protocol(id);
ALTER TABLE pathway ADD CONSTRAINT fk_pathway_protocol FOREIGN KEY (protocol_id) REFERENCES protocol(id);

-- FK constraints added after edge table exists
ALTER TABLE interface ADD CONSTRAINT fk_interface_pathway FOREIGN KEY (pathway_id) REFERENCES pathway(id);
ALTER TABLE plasticity ADD CONSTRAINT fk_plast_edge FOREIGN KEY (edge_id) REFERENCES edge(id);


-- ═══════════════════════════════════════
-- EDGE — graph layer
-- ═══════════════════════════════════════
CREATE TABLE edge (
    id              SERIAL PRIMARY KEY,
    entity_id       UUID REFERENCES entity(id) ON DELETE CASCADE,
    source_type     VARCHAR(15) NOT NULL,
    source_id       INT NOT NULL,
    target_type     VARCHAR(15) NOT NULL,
    target_id       INT NOT NULL,
    operator        VARCHAR(20) NOT NULL,
    operator_class  VARCHAR(15) NOT NULL,
    gain            NUMERIC,
    noise_sigma     NUMERIC,
    transfer_fn     VARCHAR(10),
    delay_ms        BIGINT,
    clamp_lo        NUMERIC,
    clamp_hi        NUMERIC,
    properties      JSONB DEFAULT '{}',
    gate_id         INT REFERENCES gate(id) ON DELETE SET NULL,
    loop_id         INT REFERENCES loop(id),
    pathway_id      INT REFERENCES pathway(id),
    dysreg_type     VARCHAR(20),
    module_id       INT REFERENCES module(id),
    tool_id         INT REFERENCES tool(id),
    protocol_id     INT REFERENCES protocol(id),
    active          BOOLEAN DEFAULT true,
    created_on_utc  TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_edge_entity ON edge(entity_id);
CREATE INDEX idx_edge_source ON edge(source_type, source_id);
CREATE INDEX idx_edge_target ON edge(target_type, target_id);
CREATE INDEX idx_edge_operator ON edge(operator);
CREATE INDEX idx_edge_class ON edge(operator_class);
CREATE INDEX idx_edge_module ON edge(module_id);
CREATE INDEX idx_edge_active ON edge(entity_id) WHERE active = true;
CREATE INDEX idx_edge_protocol ON edge(protocol_id);
CREATE INDEX idx_edge_walk ON edge(entity_id, source_type, source_id, active) WHERE active = true;
CREATE INDEX idx_edge_walk_rev ON edge(entity_id, target_type, target_id, active) WHERE active = true;
CREATE INDEX idx_edge_global ON edge(source_type, source_id, active) WHERE entity_id IS NULL AND active = true;
CREATE INDEX idx_edge_gate ON edge(gate_id) WHERE gate_id IS NOT NULL;
CREATE INDEX idx_edge_tool ON edge(tool_id) WHERE tool_id IS NOT NULL;
CREATE INDEX idx_edge_loop ON edge(loop_id) WHERE loop_id IS NOT NULL;
CREATE INDEX idx_edge_pathway ON edge(pathway_id) WHERE pathway_id IS NOT NULL;
CREATE INDEX idx_edge_dysreg ON edge(entity_id, dysreg_type) WHERE dysreg_type IS NOT NULL;
COMMENT ON TABLE edge IS
'Graph layer. Every relationship is an explicit row. Append-only.
 source_type/target_type: signal | receptor | transporter | limiter | gate | tool.
 operator: kernel symbol (→ ⊣ ⊩ ⊗ ⊘→ ⇌ ∥ ◈ ⟳⁻ ⟳⁺ ⊨ ⚡.* etc).
 operator_class: causal | feedback | gate | flow | dysreg.
 gain/noise_sigma/transfer_fn/delay_ms/clamp: edge modifier fields.
 loop_id: FK to loop. Set on feedback edges. Enables gain_product computation.
 pathway_id: FK to pathway. Groups edges into named traversal sequences.
 dysreg_type: excitotoxicity | depletion | resistance | accumulation |
   spillover | sustained | oscillation | uncoupling | saturation | shunt.
 tool_id: set when edge passes through a TOOL invocation.
 entity_id NULL = global textbook edge.';


-- ═══════════════════════════════════════
-- SUMMARY
-- ═══════════════════════════════════════
-- 17 tables, 0 domain-specific values:
--
--   entity         scope target (person, market, system...)
--   stimuli        parser inbox (what the system reacts to)
--   module         scoped subgraph + agent type
--   region         location within entity
--   signal         the register (with L0-L2 numeric + distribution)
--   receptor       input component
--   transporter    clearance component
--   gate           control point (standard + LLM gates)
--   limiter        rate-limiting step
--   interface      cross-region bridge + module boundary
--   loop           feedback cycle header (gain_product, polarity, subtype)
--   plasticity     induction context (LTP/LTD/homeostatic/structural)
--   pathway        named multi-hop route (DEF circuits, projections)
--   constraint_def simultaneous conditions
--   tool           external action bridge
--   protocol       immutable audit log
--   edge           graph layer (+ loop_id, pathway_id, dysreg_type)
--
-- Flow: stimuli in → parser → signals/edges/gates → eval engine → stable
-- Vocabulary provides: codes, types, regions, thresholds, enums.
-- Schema provides: structure, relationships, append-only history.
-- Parser infers: FKs, ownership, operator_class from context.