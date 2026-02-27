-- ═══════════════════════════════════════════════════════════
-- biochain_init.sql
-- BioChain v5.0 — Core Schema
--
-- 9 tables. Protocol-centric. States, not floats.
--
-- IDENTITY:      person
-- EVIDENCE:      data
-- COMPONENTS:    signal, receptor, transporter, gate, limiter, interface
-- INSTRUCTION:   protocol
--
-- MODES:
--   OBSERVE    chat → LLM → protocol (with component refs)
--   SIMULATE   load person → inject stimulus → walk protocols
--   EXPLORE    BioSphere / BioInsight / person optimization
-- ═══════════════════════════════════════════════════════════

CREATE EXTENSION IF NOT EXISTS vector;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS pg_trgm;


-- ═══════════════════════════════════════════════════════════
-- PERSON
-- ═══════════════════════════════════════════════════════════

CREATE TABLE person (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    owner_id        TEXT NOT NULL,
    name            VARCHAR(100) NOT NULL,
    data            JSONB DEFAULT '{}',
    embedding       vector(1536),
    created_on_utc  TIMESTAMPTZ DEFAULT NOW(),
    updated_on_utc  TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_person_owner ON person(owner_id);
CREATE UNIQUE INDEX idx_person_owner_name ON person(owner_id, name);
CREATE INDEX idx_person_embed ON person USING hnsw(embedding vector_cosine_ops);

COMMENT ON TABLE person IS 'Identity. AI optimization target.';


-- ═══════════════════════════════════════════════════════════
-- DATA — universal log
-- ═══════════════════════════════════════════════════════════

CREATE TABLE data (
    id              SERIAL PRIMARY KEY,
    person_id       UUID NOT NULL REFERENCES person(id) ON DELETE CASCADE,
    kind            VARCHAR(30) NOT NULL,
    source_text     TEXT,
    formula         TEXT,
    analyzed        BOOLEAN DEFAULT false,
    content         JSONB DEFAULT '{}',
    embedding       vector(1536),
    created_on_utc  TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_data_pid ON data(person_id);
CREATE INDEX idx_data_kind ON data(kind);
CREATE INDEX idx_data_analyzed ON data(person_id, analyzed) WHERE analyzed = false;
CREATE INDEX idx_data_time ON data(person_id, created_on_utc DESC);
CREATE INDEX idx_data_content ON data USING GIN(content);
CREATE INDEX idx_data_embed ON data USING hnsw(embedding vector_cosine_ops);

COMMENT ON TABLE data IS
'Universal log. Append-only.
 kind: chat | observation | inferred | clinical | wearable | behavioral
       | simulation | cascade_step | hypothesis | prediction | loop_check | checkpoint.
 source_text: original human input. formula: BioChain notation.
 analyzed: false = queued for LLM processing.';


-- ═══════════════════════════════════════════════════════════
-- SIGNAL — the register
-- ═══════════════════════════════════════════════════════════

CREATE TABLE signal (
    id              SERIAL PRIMARY KEY,
    person_id       UUID NOT NULL REFERENCES person(id) ON DELETE CASCADE,
    type            VARCHAR(5) NOT NULL,
    code            VARCHAR(20) NOT NULL,
    region          VARCHAR(20),
    state           VARCHAR(5) NOT NULL DEFAULT '≈',
    baseline        VARCHAR(5) NOT NULL DEFAULT '≈',
    tau_min         VARCHAR(10),
    tau_max         VARCHAR(10),
    embedding       vector(1536),
    created_on_utc  TIMESTAMPTZ DEFAULT NOW(),
    updated_on_utc  TIMESTAMPTZ DEFAULT NOW()
);

CREATE UNIQUE INDEX idx_signal_person_region ON signal(person_id, code, region) WHERE region IS NOT NULL;
CREATE UNIQUE INDEX idx_signal_person_systemic ON signal(person_id, code) WHERE region IS NULL;
CREATE INDEX idx_signal_pid ON signal(person_id);
CREATE INDEX idx_signal_type ON signal(type);
CREATE INDEX idx_signal_code ON signal(code);
CREATE INDEX idx_signal_region ON signal(region);
CREATE INDEX idx_signal_state ON signal(person_id, state);
CREATE INDEX idx_signal_embed ON signal USING hnsw(embedding vector_cosine_ops);

COMMENT ON TABLE signal IS
'The register. One row = one molecule in one region for one person.
 type: NT | H | P | NI | NS | eCB (BioChain layer tag).
 state: ↑ ↓ ↑↑ ↓↓ ~ ≈ ⊘ ● (Layer 1 signal states).
        Parser may normalize ↑↑↑/↓↓↓ to ↑↑/↓↓ (model gradient extrapolation).
 baseline: what [≈] means for THIS person (may differ from global ≈).
 DA@VTA and DA@NAc are separate rows with independent states.
 Simulator uses type for τ ordering: NT(fast) → P(mid) → H(slow).';


-- ═══════════════════════════════════════════════════════════
-- RECEPTOR — the modulator
-- ═══════════════════════════════════════════════════════════

CREATE TABLE receptor (
    id              SERIAL PRIMARY KEY,
    person_id       UUID NOT NULL REFERENCES person(id) ON DELETE CASCADE,
    signal_id       INT NOT NULL REFERENCES signal(id) ON DELETE CASCADE,
    code            VARCHAR(20) NOT NULL,
    subtype         VARCHAR(20),
    state           VARCHAR(10) NOT NULL DEFAULT 'active',
    embedding       vector(1536),
    created_on_utc  TIMESTAMPTZ DEFAULT NOW(),
    updated_on_utc  TIMESTAMPTZ DEFAULT NOW()
);

CREATE UNIQUE INDEX idx_receptor_person_code ON receptor(person_id, code);
CREATE INDEX idx_receptor_pid ON receptor(person_id);
CREATE INDEX idx_receptor_signal ON receptor(signal_id);
CREATE INDEX idx_receptor_state ON receptor(person_id, state);
CREATE INDEX idx_receptor_code ON receptor(code);
CREATE INDEX idx_receptor_embed ON receptor USING hnsw(embedding vector_cosine_ops);

COMMENT ON TABLE receptor IS
'Modulator. State IS the full picture:
 active | desens | intern | upreg | downreg | resist | primed.
 subtype: G-protein coupling (Gs, Gi, Gq, ion, Cl⁻, Ca²⁺).
 signal_id: parent signal (DA.D1 → DA).';


-- ═══════════════════════════════════════════════════════════
-- TRANSPORTER — clearance mechanism
-- ═══════════════════════════════════════════════════════════

CREATE TABLE transporter (
    id              SERIAL PRIMARY KEY,
    person_id       UUID NOT NULL REFERENCES person(id) ON DELETE CASCADE,
    signal_id       INT NOT NULL REFERENCES signal(id) ON DELETE CASCADE,
    code            VARCHAR(20) NOT NULL,
    state           VARCHAR(10) NOT NULL DEFAULT 'active',
    clearance       VARCHAR(5) NOT NULL DEFAULT '≈',
    embedding       vector(1536),
    created_on_utc  TIMESTAMPTZ DEFAULT NOW(),
    updated_on_utc  TIMESTAMPTZ DEFAULT NOW()
);

CREATE UNIQUE INDEX idx_transporter_person_code ON transporter(person_id, code);
CREATE INDEX idx_transporter_pid ON transporter(person_id);
CREATE INDEX idx_transporter_signal ON transporter(signal_id);
CREATE INDEX idx_transporter_state ON transporter(state);
CREATE INDEX idx_transporter_embed ON transporter USING hnsw(embedding vector_cosine_ops);

COMMENT ON TABLE transporter IS
'Clearance. DAT reuptakes DA. SERT reuptakes 5HT.
 state: active | blocked | impaired | enhanced.
 clearance: ↑↑ ↑ ≈ ↓ ↓↓ ⊘ — how fast signal is removed.';


-- ═══════════════════════════════════════════════════════════
-- GATE — logic unit
-- ═══════════════════════════════════════════════════════════

CREATE TABLE gate (
    id              SERIAL PRIMARY KEY,
    person_id       UUID NOT NULL REFERENCES person(id) ON DELETE CASCADE,
    code            VARCHAR(30) NOT NULL,
    type            VARCHAR(15) NOT NULL,
    threshold       VARCHAR(5),
    expression      TEXT,
    parent_id       INT REFERENCES gate(id),
    history         TEXT[] DEFAULT '{}',
    latched         BOOLEAN DEFAULT false,
    embedding       vector(1536),
    created_on_utc  TIMESTAMPTZ DEFAULT NOW(),
    updated_on_utc  TIMESTAMPTZ DEFAULT NOW()
);

CREATE UNIQUE INDEX idx_gate_person_code ON gate(person_id, code);
CREATE INDEX idx_gate_pid ON gate(person_id);
CREATE INDEX idx_gate_type ON gate(type);
CREATE INDEX idx_gate_parent ON gate(parent_id);
CREATE INDEX idx_gate_latched ON gate(person_id) WHERE latched = true;
CREATE INDEX idx_gate_embed ON gate USING hnsw(embedding vector_cosine_ops);

COMMENT ON TABLE gate IS
'Logic unit. The ALU.
 type: and | or | not | xor | threshold | gain | latch | integrator | splitter | novelty.
 threshold: state level that triggers firing (e.g. ↑ means fires when signal ≥ ↑).
 expression: complex gate notation for nesting.
 parent_id: tree structure for gate composition.
 history[]: novelty gate (⊛) past activation states.
 latched: latch gate (⊡) bistable lock.';


-- ═══════════════════════════════════════════════════════════
-- LIMITER — enzyme / rate-limiting step
-- ═══════════════════════════════════════════════════════════

CREATE TABLE limiter (
    id              SERIAL PRIMARY KEY,
    person_id       UUID NOT NULL REFERENCES person(id) ON DELETE CASCADE,
    target_id       INT REFERENCES signal(id),
    code            VARCHAR(20) NOT NULL,
    reaction        TEXT,
    rate_limiting   BOOLEAN DEFAULT false,
    activity        VARCHAR(5) NOT NULL DEFAULT '≈',
    embedding       vector(1536),
    created_on_utc  TIMESTAMPTZ DEFAULT NOW(),
    updated_on_utc  TIMESTAMPTZ DEFAULT NOW()
);

CREATE UNIQUE INDEX idx_limiter_person_code ON limiter(person_id, code);
CREATE INDEX idx_limiter_pid ON limiter(person_id);
CREATE INDEX idx_limiter_target ON limiter(target_id);
CREATE INDEX idx_limiter_bottleneck ON limiter(person_id) WHERE rate_limiting = true;
CREATE INDEX idx_limiter_embed ON limiter USING hnsw(embedding vector_cosine_ops);

COMMENT ON TABLE limiter IS
'Enzyme / rate-limiting step.
 code: TH, AADC, MAO-A, MAO-B, COMT, TPH2, IDO.
 reaction: e.g. TYR → L-DOPA, 5HT → 5-HIAA.
 rate_limiting: true = bottleneck (⧫).
 activity: ↑↑ ↑ ≈ ↓ ↓↓ ⊘ — enzyme activity level.';


-- ═══════════════════════════════════════════════════════════
-- INTERFACE — region connections
-- ═══════════════════════════════════════════════════════════

CREATE TABLE interface (
    id              SERIAL PRIMARY KEY,
    person_id       UUID NOT NULL REFERENCES person(id) ON DELETE CASCADE,
    code            VARCHAR(30) NOT NULL,
    source_region   VARCHAR(20) NOT NULL,
    target_region   VARCHAR(20) NOT NULL,
    pathway         VARCHAR(50),
    active          BOOLEAN DEFAULT true,
    embedding       vector(1536),
    created_on_utc  TIMESTAMPTZ DEFAULT NOW(),
    updated_on_utc  TIMESTAMPTZ DEFAULT NOW()
);

CREATE UNIQUE INDEX idx_interface_person_code ON interface(person_id, code);
CREATE INDEX idx_interface_pid ON interface(person_id);
CREATE INDEX idx_interface_source ON interface(source_region);
CREATE INDEX idx_interface_target ON interface(target_region);
CREATE INDEX idx_interface_pathway ON interface(pathway);
CREATE INDEX idx_interface_active ON interface(person_id) WHERE active = true;
CREATE INDEX idx_interface_embed ON interface USING hnsw(embedding vector_cosine_ops);

COMMENT ON TABLE interface IS
'Region connections. The @REGION in formulas.
 VTA → NAc = mesolimbic. PVN → PIT = HPA axis.
 active: false = pathway disconnected/damaged.';


-- ═══════════════════════════════════════════════════════════
-- PROTOCOL — the compiled formula
-- ═══════════════════════════════════════════════════════════

CREATE TABLE protocol (
    id                  SERIAL PRIMARY KEY,
    person_id           UUID REFERENCES person(id) ON DELETE CASCADE,
    tag                 VARCHAR(15),
    formula             TEXT NOT NULL,
    status              VARCHAR(50),
    phase               VARCHAR(50),
    data_id             INT REFERENCES data(id),
    signal_source_id    INT REFERENCES signal(id),
    signal_target_id    INT REFERENCES signal(id),
    receptor_id         INT REFERENCES receptor(id),
    transporter_id      INT REFERENCES transporter(id),
    gate_id             INT REFERENCES gate(id),
    limiter_id          INT REFERENCES limiter(id),
    interface_id        INT REFERENCES interface(id),
    embedding           vector(1536),
    created_on_utc      TIMESTAMPTZ DEFAULT NOW(),
    updated_on_utc      TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_protocol_pid ON protocol(person_id);
CREATE INDEX idx_protocol_tag ON protocol(tag);
CREATE INDEX idx_protocol_phase ON protocol(phase);
CREATE INDEX idx_protocol_source ON protocol(signal_source_id);
CREATE INDEX idx_protocol_target ON protocol(signal_target_id);
CREATE INDEX idx_protocol_receptor ON protocol(receptor_id);
CREATE INDEX idx_protocol_transporter ON protocol(transporter_id);
CREATE INDEX idx_protocol_gate ON protocol(gate_id);
CREATE INDEX idx_protocol_limiter ON protocol(limiter_id);
CREATE INDEX idx_protocol_interface ON protocol(interface_id);
CREATE INDEX idx_protocol_data ON protocol(data_id);
CREATE INDEX idx_protocol_embed ON protocol USING hnsw(embedding vector_cosine_ops);
CREATE INDEX idx_protocol_walk ON protocol(signal_source_id, person_id);

COMMENT ON TABLE protocol IS
'The compiled formula. Lean by design.
 tag: line type from LLM output. Closed set:
      SIGNAL | RECEPTOR | GATE | LIMITER | FEEDBACK | FORMULA | STATE
      | TRANSPORT | INTERFACE | DEF | DYSREG | HYPOTHESIS | PREDICTION | INTERVENTION.
 formula: the full BioChain notation — IS the operator, tau, kind.
 status: the "— status:" suffix value from LLM output (e.g. latched, exceeded, broken).
 phase: current #PHASE header (e.g. ONSET, PROGRESSION, RESISTANCE).
 FKs: point to every component involved — IS the query index.
 data_id: what raw input spawned this.
 person_id NULL = global textbook protocol.
 Operators in formula: → ← ⊃ ⊂ ⊣ ⊩ ⊗ ⊘→ ⇌ ∥ ◈ ⤳ ⧫ ⟲ ⟳⁻ ⟳⁺.
 Gate symbols in formula: ⊨(threshold) ⊡(latch) Σ(integrator) ⊛(novelty) ⊳(gain).
 Dysreg in formula: ⚡.type.<mechanism>.
 Parser extracts operator/tau/kind from formula at runtime.
 Simulator: walk protocols via signal_source_id.';