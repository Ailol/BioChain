-- init.sql
-- Full schema for MultiAgentAiMcp
-- v6 — Dynamic + full BioChain notation coverage.
-- Every layer of the notation has a structural home in the DB.
-- Tables are domain-agnostic but notation-complete.

CREATE EXTENSION IF NOT EXISTS vector;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS pg_trgm;


-- ═══════════════════════════════════════════════════════════
-- CORE IDENTITY
-- ═══════════════════════════════════════════════════════════

CREATE TABLE person (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    owner_id TEXT NOT NULL,
    first_name VARCHAR(50) NOT NULL,
    last_name VARCHAR(50),
    phone VARCHAR(20),
    email VARCHAR(100),
    ssn VARCHAR(20),
    birthdate DATE,
    address VARCHAR(200),
    postcode VARCHAR(10),
    city VARCHAR(100),
    created_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_person_owner ON person(owner_id);
CREATE UNIQUE INDEX idx_person_owner_name ON person(owner_id, lower(first_name));

CREATE TABLE person_share (
    id SERIAL PRIMARY KEY,
    person_id UUID NOT NULL REFERENCES person(id) ON DELETE CASCADE,
    shared_with_email TEXT NOT NULL,
    shared_with_user_id TEXT,
    shared_by_user_id TEXT NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE (person_id, shared_with_email)
);
CREATE INDEX idx_person_share_user ON person_share(shared_with_user_id);
CREATE INDEX idx_person_share_email ON person_share(shared_with_email);

CREATE TABLE user_role (
    id SERIAL PRIMARY KEY,
    user_id TEXT NOT NULL,
    email TEXT,
    role VARCHAR(20) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE (user_id, role)
);
CREATE INDEX idx_user_role_user ON user_role(user_id);

CREATE TABLE personality (
    id SERIAL PRIMARY KEY,
    person_id UUID NOT NULL UNIQUE REFERENCES person(id) ON DELETE CASCADE,
    communication_style TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_personality_person ON personality(person_id);


-- ═══════════════════════════════════════════════════════════
-- INPUT LAYER
-- ═══════════════════════════════════════════════════════════

CREATE TABLE analyzed_data (
    id SERIAL PRIMARY KEY,
    person_id UUID NOT NULL REFERENCES person(id) ON DELETE CASCADE,
    content TEXT NOT NULL,
    source_type VARCHAR(30),
    source_uri VARCHAR,
    metadata JSONB DEFAULT '{}',
    embedding vector(1536),
    created_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_analyzed_data_person ON analyzed_data(person_id);
CREATE INDEX idx_analyzed_data_source ON analyzed_data(source_type);
CREATE INDEX idx_analyzed_data_meta ON analyzed_data USING GIN(metadata);


-- ═══════════════════════════════════════════════════════════
-- DOMAIN REGISTRY
-- ═══════════════════════════════════════════════════════════

CREATE TABLE domain (
    id SERIAL PRIMARY KEY,
    key VARCHAR(50) NOT NULL UNIQUE,
    name VARCHAR(100) NOT NULL,
    description TEXT,
    config JSONB DEFAULT '{}',
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);


-- ═══════════════════════════════════════════════════════════
-- LAYER 0 — LEXICON
-- BioChain has distinct entity types: molecules, enzymes,
-- transporters, receptors, G-proteins, second messengers,
-- brain regions. Each gets proper typing.
-- ═══════════════════════════════════════════════════════════

-- SIGNALS: The molecules / measurable things
-- NT, H, P, NI, NS, eCB — or behavioral/metric signals

CREATE TABLE signal (
    id SERIAL PRIMARY KEY,
    domain_id INT NOT NULL REFERENCES domain(id) ON DELETE CASCADE,
    key VARCHAR(50) NOT NULL UNIQUE,
    label VARCHAR(100) NOT NULL,
    layer VARCHAR(30) NOT NULL,                    -- NT, H, P, NI, NS, eCB, behavior, metric
    code VARCHAR(20) NOT NULL UNIQUE,              -- NT:DA, H:CORT, P:OXT, BEH:SOC
    unit VARCHAR(20),
    config JSONB DEFAULT '{}',                     -- normal_range, inverted_U params, notes
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_signal_domain ON signal(domain_id);
CREATE INDEX idx_signal_layer ON signal(layer);
CREATE INDEX idx_signal_code ON signal(code);

-- RECEPTORS: Input pins of the circuit
-- Distinct from signals. A receptor binds a signal and transduces it.

CREATE TABLE receptor (
    id SERIAL PRIMARY KEY,
    signal_id INT NOT NULL REFERENCES signal(id) ON DELETE CASCADE,  -- which signal binds here
    key VARCHAR(50) NOT NULL UNIQUE,               -- D1, D2, 5HT.1A, GABA.A, mu_OR, CB1
    label VARCHAR(100) NOT NULL,
    subtype VARCHAR(30),                           -- receptor subtype
    g_protein VARCHAR(10),                         -- Gs, Gi, Gq, ion, nuclear, beta-arr
    ion_channel VARCHAR(20),                       -- Cl-, Na+, Ca2+, K+ (for ionotropic)
    location VARCHAR(30),                          -- presynaptic, postsynaptic, somatodendritic, auto
    config JSONB DEFAULT '{}',                     -- coupling details, desensitization rates
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_receptor_signal ON receptor(signal_id);
CREATE INDEX idx_receptor_gprotein ON receptor(g_protein);

-- ENZYMES: Catalysts that synthesize, degrade, or convert signals

CREATE TABLE enzyme (
    id SERIAL PRIMARY KEY,
    key VARCHAR(30) NOT NULL UNIQUE,               -- TH, AADC, MAO-A, MAO-B, COMT, TPH2, IDO, FAAH
    label VARCHAR(100) NOT NULL,
    function VARCHAR(20) NOT NULL,                 -- synthesis, degradation, conversion, shunt
    substrate_signal_id INT REFERENCES signal(id), -- what it acts on
    product_signal_id INT REFERENCES signal(id),   -- what it produces (if conversion)
    is_rate_limiting BOOLEAN DEFAULT FALSE,         -- ⧫ marker
    config JSONB DEFAULT '{}',                     -- cofactors, kinetics, regulation
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_enzyme_substrate ON enzyme(substrate_signal_id);
CREATE INDEX idx_enzyme_product ON enzyme(product_signal_id);
CREATE INDEX idx_enzyme_function ON enzyme(function);

-- TRANSPORTERS: Reuptake / vesicular loading / clearance

CREATE TABLE transporter (
    id SERIAL PRIMARY KEY,
    key VARCHAR(20) NOT NULL UNIQUE,               -- DAT, SERT, NET, GAT, VMAT2, EAAT, ChT
    label VARCHAR(100) NOT NULL,
    signal_id INT NOT NULL REFERENCES signal(id),  -- what it transports
    transport_type VARCHAR(20) NOT NULL,            -- reuptake, vesicular, clearance
    location VARCHAR(30),                          -- presynaptic, astrocyte, vesicular
    config JSONB DEFAULT '{}',
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_transporter_signal ON transporter(signal_id);

-- SECOND MESSENGERS: Intracellular cascade components

CREATE TABLE second_messenger (
    id SERIAL PRIMARY KEY,
    key VARCHAR(20) NOT NULL UNIQUE,               -- cAMP, cGMP, IP3, DAG, Ca2+, PKA, PKC, CREB, MAPK
    label VARCHAR(100) NOT NULL,
    messenger_type VARCHAR(20) NOT NULL,            -- messenger, kinase, transcription_factor, phosphoprotein
    config JSONB DEFAULT '{}',
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- BRAIN REGIONS: Where things happen

CREATE TABLE brain_region (
    id SERIAL PRIMARY KEY,
    key VARCHAR(10) NOT NULL UNIQUE,               -- VTA, NAc, PFC, AMY, HPC, DRN, LC, HYP, PVN
    label VARCHAR(100) NOT NULL,
    region_type VARCHAR(30),                       -- nucleus, cortical_area, brainstem, gland
    parent_region_id INT REFERENCES brain_region(id),  -- hierarchy: PVN → HYP
    config JSONB DEFAULT '{}',
    created_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_br_parent ON brain_region(parent_region_id);


-- ═══════════════════════════════════════════════════════════
-- LAYER 2 — OPERATORS / INTERACTIONS
-- How signals affect each other. Full operator vocabulary.
-- ═══════════════════════════════════════════════════════════

CREATE TABLE signal_interaction (
    id SERIAL PRIMARY KEY,
    source_signal_id INT NOT NULL REFERENCES signal(id) ON DELETE CASCADE,
    target_signal_id INT NOT NULL REFERENCES signal(id) ON DELETE CASCADE,
    operator VARCHAR(20) NOT NULL,                 -- →, ⊣, ⊃, ⊂, ⊩, ⇌, ∥, ⊗, ≫, ≂, ⊘→
                                                   -- ASCII: CAUSES, INHIB, UPREG, DNREG, DISINHIBIT,
                                                   --        BIDIR, CO_ACTIVATE, ANTAG, DOMINATES, MIMICS, BLOCKS
    mod_factor FLOAT,
    mechanism TEXT,
    via_enzyme_id INT REFERENCES enzyme(id),        -- if interaction is enzyme-mediated
    via_receptor_id INT REFERENCES receptor(id),    -- if interaction is receptor-mediated
    via_transporter_id INT REFERENCES transporter(id),
    region_id INT REFERENCES brain_region(id),
    temporal VARCHAR(20),                          -- acute, chronic, tonic, phasic, pulsatile, permissive, circadian
    config JSONB DEFAULT '{}',                     -- gate conditions, dose-response, confidence
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE (source_signal_id, target_signal_id, operator, region_id)
);
CREATE INDEX idx_si_source ON signal_interaction(source_signal_id);
CREATE INDEX idx_si_target ON signal_interaction(target_signal_id);
CREATE INDEX idx_si_operator ON signal_interaction(operator);
CREATE INDEX idx_si_region ON signal_interaction(region_id);
CREATE INDEX idx_si_enzyme ON signal_interaction(via_enzyme_id);
CREATE INDEX idx_si_receptor ON signal_interaction(via_receptor_id);


-- ═══════════════════════════════════════════════════════════
-- LAYER 3 — LOGIC GATES
-- Gates are computational primitives. Store them as queryable
-- entities, not just text inside formulas.
-- ═══════════════════════════════════════════════════════════

CREATE TABLE gate (
    id SERIAL PRIMARY KEY,
    gate_type VARCHAR(20) NOT NULL,                -- AND, OR, NOT, XOR, NAND, NOR, THRESHOLD,
                                                   -- GAIN, BUFFER, LATCH, INTEGRATOR, SPLITTER,
                                                   -- GATED, DEMUX, COMPARATOR, FILTER, MODULATOR
    symbol VARCHAR(5),                             -- ⊼, ⊽, ¬, ⊕, ⊨, ⊳, ▷, ⊡, Σ, ⑂, ⊞, ⊟, ◇, ⫙, ◈
    name VARCHAR(50) NOT NULL,
    description TEXT,                              -- neurobiological analog
    config JSONB DEFAULT '{}',
    created_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_gate_type ON gate(gate_type);

-- Gate instances: a specific gate in a specific context
-- "NMDA requires GLU + depolarization + GLY" = one gate_instance

CREATE TABLE gate_instance (
    id SERIAL PRIMARY KEY,
    gate_id INT NOT NULL REFERENCES gate(id) ON DELETE CASCADE,
    name VARCHAR(100),                             -- 'nmda_coincidence', 'hpa_threshold', 'sleep_wake_xor'
    formula TEXT NOT NULL,                         -- {⊼: GLU.bind, depolarization, GLY → NMDA.activate}
    input_signals INT[],                           -- signal IDs (inputs to the gate)
    output_signal_id INT REFERENCES signal(id),    -- what the gate produces
    modulator_signal_id INT REFERENCES signal(id), -- for GATED/MODULATOR types
    threshold_value VARCHAR(50),                   -- for THRESHOLD: '>20μg/dL', '⊨(high)'
    region_id INT REFERENCES brain_region(id),
    config JSONB DEFAULT '{}',                     -- conditions, temporal constraints
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_gi_gate ON gate_instance(gate_id);
CREATE INDEX idx_gi_output ON gate_instance(output_signal_id);
CREATE INDEX idx_gi_region ON gate_instance(region_id);


-- ═══════════════════════════════════════════════════════════
-- LAYER 4 — SIGNAL LIFECYCLE
-- Every signal has a lifecycle: syn → pkg → trg → rel → bnd →
-- txd → eff → trm → fbk. Store each stage.
-- ═══════════════════════════════════════════════════════════

CREATE TABLE lifecycle_stage (
    id SERIAL PRIMARY KEY,
    signal_id INT NOT NULL REFERENCES signal(id) ON DELETE CASCADE,
    stage VARCHAR(10) NOT NULL,                    -- syn, pkg, trg, rel, bnd, txd, amp, eff, trm, fbk
    stage_order INT NOT NULL,                      -- 1-10 ordering
    formula TEXT NOT NULL,                         -- the notation for this stage
    description TEXT,                              -- human-readable
    rate_limiting_enzyme_id INT REFERENCES enzyme(id),  -- ⧫ marker
    gate_instance_id INT REFERENCES gate_instance(id),  -- trigger gates
    transporter_id INT REFERENCES transporter(id),      -- for pkg/trm stages
    receptor_ids INT[],                            -- for bnd stage: which receptors
    region_id INT REFERENCES brain_region(id),
    config JSONB DEFAULT '{}',                     -- mode (tonic/phasic), metabolites, cofactors
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE (signal_id, stage)
);
CREATE INDEX idx_ls_signal ON lifecycle_stage(signal_id);
CREATE INDEX idx_ls_stage ON lifecycle_stage(stage);


-- ═══════════════════════════════════════════════════════════
-- LAYER 5 — PATHWAYS
-- Named, reusable signal routes. These are the "wiring diagrams."
-- ═══════════════════════════════════════════════════════════

CREATE TABLE pathway (
    id SERIAL PRIMARY KEY,
    domain_id INT REFERENCES domain(id) ON DELETE SET NULL,
    key VARCHAR(50) NOT NULL UNIQUE,               -- 'hpa_axis', 'mesolimbic_da', 'drn_5ht', 'sleep_wake'
    name VARCHAR(100) NOT NULL,
    description TEXT,
    source_region_id INT REFERENCES brain_region(id),
    target_region_id INT REFERENCES brain_region(id),
    primary_signal_id INT REFERENCES signal(id),   -- the main signal this pathway carries
    compact_formula TEXT,                           -- the one-liner compact notation
    template_type VARCHAR(30),                     -- linear_cascade, neg_feedback, disinhibition,
                                                   -- coincidence, opponent, permissive_gating, custom
    config JSONB DEFAULT '{}',                     -- full pathway block notation, wiring details
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_pw_domain ON pathway(domain_id);
CREATE INDEX idx_pw_signal ON pathway(primary_signal_id);
CREATE INDEX idx_pw_template ON pathway(template_type);
CREATE INDEX idx_pw_source_region ON pathway(source_region_id);
CREATE INDEX idx_pw_target_region ON pathway(target_region_id);

-- Pathway steps: ordered nodes in the pathway

CREATE TABLE pathway_step (
    id SERIAL PRIMARY KEY,
    pathway_id INT NOT NULL REFERENCES pathway(id) ON DELETE CASCADE,
    step_order INT NOT NULL,
    signal_id INT REFERENCES signal(id),
    region_id INT REFERENCES brain_region(id),
    receptor_id INT REFERENCES receptor(id),
    enzyme_id INT REFERENCES enzyme(id),
    gate_instance_id INT REFERENCES gate_instance(id),
    connection_type VARCHAR(20) NOT NULL,           -- excitatory, inhibitory, modulatory, gated, blocked
                                                   -- maps to: ─────→ ─────⊣ ─ ─ → ═════→ ··→ ──╫──→ ──┤├──
    formula TEXT,                                   -- step-level notation
    config JSONB DEFAULT '{}',
    UNIQUE (pathway_id, step_order)
);
CREATE INDEX idx_ps_pathway ON pathway_step(pathway_id);
CREATE INDEX idx_ps_signal ON pathway_step(signal_id);
CREATE INDEX idx_ps_region ON pathway_step(region_id);


-- ═══════════════════════════════════════════════════════════
-- LAYER 6 — CIRCUITS
-- Composed multi-pathway architectures.
-- A circuit combines multiple pathways with temporal phases.
-- ═══════════════════════════════════════════════════════════

CREATE TABLE circuit (
    id SERIAL PRIMARY KEY,
    domain_id INT REFERENCES domain(id) ON DELETE SET NULL,
    key VARCHAR(50) NOT NULL UNIQUE,               -- 'stress_adaptation_failure', 'reward_learning', 'sleep_cycle'
    name VARCHAR(100) NOT NULL,
    description TEXT,
    trigger_description TEXT,                      -- what initiates the circuit
    compact_formula TEXT,
    config JSONB DEFAULT '{}',                     -- full circuit block notation
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_circuit_domain ON circuit(domain_id);

-- Which pathways are engaged in each circuit

CREATE TABLE circuit_pathway (
    id SERIAL PRIMARY KEY,
    circuit_id INT NOT NULL REFERENCES circuit(id) ON DELETE CASCADE,
    pathway_id INT NOT NULL REFERENCES pathway(id) ON DELETE CASCADE,
    role VARCHAR(50),                              -- 'primary', 'modulatory', 'feedback', 'opponent'
    config JSONB DEFAULT '{}',
    UNIQUE (circuit_id, pathway_id)
);
CREATE INDEX idx_cp_circuit ON circuit_pathway(circuit_id);
CREATE INDEX idx_cp_pathway ON circuit_pathway(pathway_id);

-- Circuit phases: temporal progression within a circuit

CREATE TABLE circuit_phase (
    id SERIAL PRIMARY KEY,
    circuit_id INT NOT NULL REFERENCES circuit(id) ON DELETE CASCADE,
    phase_order INT NOT NULL,
    phase_label VARCHAR(50) NOT NULL,              -- 'initiation', 'amplification', 'multi_target', 'feedback', 'failure'
    temporal VARCHAR(30),                          -- 'seconds-minutes', 'minutes', 'hours', 'weeks-months'
    state_block TEXT NOT NULL,                     -- full state notation for this phase
    description TEXT,
    config JSONB DEFAULT '{}',
    UNIQUE (circuit_id, phase_order)
);
CREATE INDEX idx_cph_circuit ON circuit_phase(circuit_id);

-- Dose-response patterns (INVERTED-U, LINEAR, SIGMOID, BIPHASIC, U-SHAPED)

CREATE TABLE dose_response (
    id SERIAL PRIMARY KEY,
    signal_id INT NOT NULL REFERENCES signal(id) ON DELETE CASCADE,
    pattern VARCHAR(20) NOT NULL,                  -- INVERTED_U, LINEAR, SIGMOID, BIPHASIC, U_SHAPED
    low_effect TEXT,                               -- effect at low dose
    optimal_effect TEXT,                           -- effect at optimal dose
    high_effect TEXT,                              -- effect at high dose
    excess_effect TEXT,                            -- effect at toxic/excess dose
    region_id INT REFERENCES brain_region(id),
    context TEXT,                                  -- when this pattern applies
    config JSONB DEFAULT '{}',
    created_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_dr_signal ON dose_response(signal_id);
CREATE INDEX idx_dr_pattern ON dose_response(pattern);


-- ═══════════════════════════════════════════════════════════
-- DIMENSION SYSTEM
-- ═══════════════════════════════════════════════════════════

CREATE TABLE dimension (
    id SERIAL PRIMARY KEY,
    domain_id INT REFERENCES domain(id) ON DELETE SET NULL,
    name VARCHAR(50) NOT NULL UNIQUE,
    section VARCHAR(20) NOT NULL,
    category VARCHAR(50) NOT NULL,
    description TEXT NOT NULL,
    work_relevance FLOAT NOT NULL DEFAULT 1.0,
    private_relevance FLOAT NOT NULL DEFAULT 1.0,
    archetype_name VARCHAR(50),
    archetype_essence VARCHAR(100),
    sort_order INT NOT NULL DEFAULT 0,
    config JSONB DEFAULT '{}',
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE dimension_signal_affinity (
    id SERIAL PRIMARY KEY,
    dimension_id INT NOT NULL REFERENCES dimension(id) ON DELETE CASCADE,
    signal_id INT NOT NULL REFERENCES signal(id) ON DELETE CASCADE,
    weight FLOAT NOT NULL,
    config JSONB DEFAULT '{}',
    UNIQUE (dimension_id, signal_id)
);
CREATE INDEX idx_dsa_dimension ON dimension_signal_affinity(dimension_id);
CREATE INDEX idx_dsa_signal ON dimension_signal_affinity(signal_id);


-- ═══════════════════════════════════════════════════════════
-- ANALYSIS SYSTEM
-- Self-describing. The 10 analyses are rows, not code.
-- ═══════════════════════════════════════════════════════════

CREATE TABLE analysis_type (
    id SERIAL PRIMARY KEY,
    domain_id INT REFERENCES domain(id) ON DELETE SET NULL,
    key VARCHAR(50) NOT NULL UNIQUE,
    name VARCHAR(100) NOT NULL,
    description TEXT,
    category VARCHAR(50),
    version INT NOT NULL DEFAULT 1,
    depends_on INT[],                              -- analysis_type IDs that must complete first
    sort_order INT DEFAULT 0,
    config JSONB DEFAULT '{}',                     -- agent_instructions, signals_to_detect, output_spec
    is_active BOOLEAN DEFAULT TRUE,
    is_system BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_at_domain ON analysis_type(domain_id);
CREATE INDEX idx_at_category ON analysis_type(category);

CREATE TABLE analysis_dimension (
    id SERIAL PRIMARY KEY,
    analysis_type_id INT NOT NULL REFERENCES analysis_type(id) ON DELETE CASCADE,
    key VARCHAR(50) NOT NULL,
    name VARCHAR(100) NOT NULL,
    description TEXT,
    target_signals INT[],
    target_regions INT[],                          -- brain_region IDs
    output_type VARCHAR(30) DEFAULT 'state',
    config JSONB DEFAULT '{}',
    sort_order INT DEFAULT 0,
    UNIQUE (analysis_type_id, key)
);
CREATE INDEX idx_adim_analysis ON analysis_dimension(analysis_type_id);

CREATE TABLE analysis_run (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    person_id UUID NOT NULL REFERENCES person(id) ON DELETE CASCADE,
    analysis_type_id INT NOT NULL REFERENCES analysis_type(id),
    status VARCHAR(20) NOT NULL DEFAULT 'pending',
    triggered_by VARCHAR(30),
    parent_run_id UUID REFERENCES analysis_run(id),
    input_data_ids INT[],
    started_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ,
    error TEXT,
    summary JSONB,
    config JSONB DEFAULT '{}',
    created_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_ar_person ON analysis_run(person_id);
CREATE INDEX idx_ar_type ON analysis_run(analysis_type_id);
CREATE INDEX idx_ar_status ON analysis_run(status);
CREATE INDEX idx_ar_parent ON analysis_run(parent_run_id);


-- ═══════════════════════════════════════════════════════════
-- OBSERVATIONS
-- The core evidence log. Append-only.
-- Full BioChain LAYER 8 formula mode coverage.
-- Every field from the canonical grammar has a column.
-- ═══════════════════════════════════════════════════════════

CREATE TABLE observation (
    id SERIAL PRIMARY KEY,
    person_id UUID NOT NULL REFERENCES person(id) ON DELETE CASCADE,
    personality_id INT NOT NULL REFERENCES personality(id) ON DELETE CASCADE,
    analysis_run_id UUID REFERENCES analysis_run(id) ON DELETE SET NULL,
    analyzed_data_id INT REFERENCES analyzed_data(id) ON DELETE SET NULL,

    -- ── LAYER 8 CANONICAL FIELDS ──
    -- SUBJECT[state] OPERATOR TARGET[state] @REGION (temporal) {gate} <stage> #conf ~ctx

    -- SUBJECT
    signal_id INT NOT NULL REFERENCES signal(id),
    subject_receptor_id INT REFERENCES receptor(id),    -- if subject is signal.receptor (e.g., DA.D1)
    subject_state VARCHAR(15),                          -- [↑],[↓],[↑↑],[↓↓],[~],[≈],[⊘],[◭],[◊],[●]
    subject_dose_range VARCHAR(10),                     -- low, mid, high, excess (⊨ markers)

    -- OPERATOR
    operator VARCHAR(20),                               -- full vocabulary: →,⊣,⊃,⊂,⊩,⇌,∥,⊗,≫,≂,⊘→
                                                        -- or ASCII: CAUSES,INHIB,UPREG,DNREG,DISINHIBIT,
                                                        -- BIDIR,CO_ACTIVATE,ANTAG,DOMINATES,MIMICS,BLOCKS

    -- TARGET
    target_signal_id INT REFERENCES signal(id),
    target_receptor_id INT REFERENCES receptor(id),
    target_state VARCHAR(15),

    -- @REGION
    region_id INT REFERENCES brain_region(id),

    -- (temporal)
    temporal VARCHAR(20),                               -- acute,chronic,tonic,phasic,pulsatile,
                                                        -- permissive,delayed,circadian,subacute

    -- {gate}
    gate_instance_id INT REFERENCES gate_instance(id),  -- structured gate reference
    gate_formula TEXT,                                  -- inline gate notation if not a stored instance

    -- <stage>
    lifecycle_stage VARCHAR(10),                         -- syn,sto,rel,bnd,trd,eff,trm,mod

    -- #confidence
    confidence VARCHAR(10),                             -- explicit(●), strong(◐), weak(○), absent(∅)

    -- ~context
    context VARCHAR(20),                                -- academic, casual, diary, clinical, professional, chat

    -- ── ANALYSIS-SPECIFIC ──
    failure_mode VARCHAR(30),                           -- depletion, resistance, sensitization, uncoupling,
                                                        -- spillover, kindling, loop_failure, crosstalk, shunt
    intensity FLOAT,
    pathway_id INT REFERENCES pathway(id),              -- if observation relates to a known pathway
    circuit_id INT REFERENCES circuit(id),              -- if observation relates to a known circuit

    -- ── FREETEXT (AI output, vector-searchable) ──
    signals_text TEXT,                                  -- raw signal evidence from source text
    formula TEXT NOT NULL,                              -- the full notation formula
    state_text TEXT,
    circuits_text TEXT,
    notes TEXT,

    -- ── EXTENSION ──
    metadata JSONB DEFAULT '{}',

    -- ── EMBEDDING ──
    embedding vector(1536),

    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Core indexes
CREATE INDEX idx_obs_person ON observation(person_id);
CREATE INDEX idx_obs_personality ON observation(personality_id);
CREATE INDEX idx_obs_run ON observation(analysis_run_id);
CREATE INDEX idx_obs_analyzed ON observation(analyzed_data_id);

-- Signal/target indexes (LAYER 0)
CREATE INDEX idx_obs_signal ON observation(signal_id);
CREATE INDEX idx_obs_subject_receptor ON observation(subject_receptor_id);
CREATE INDEX idx_obs_target ON observation(target_signal_id);
CREATE INDEX idx_obs_target_receptor ON observation(target_receptor_id);

-- Operator/region/temporal indexes (LAYER 2)
CREATE INDEX idx_obs_operator ON observation(operator);
CREATE INDEX idx_obs_region ON observation(region_id);
CREATE INDEX idx_obs_temporal ON observation(temporal);

-- Gate/lifecycle indexes (LAYER 3-4)
CREATE INDEX idx_obs_gate ON observation(gate_instance_id);
CREATE INDEX idx_obs_lifecycle ON observation(lifecycle_stage);

-- Pathway/circuit indexes (LAYER 5-6)
CREATE INDEX idx_obs_pathway ON observation(pathway_id);
CREATE INDEX idx_obs_circuit ON observation(circuit_id);

-- Failure/confidence/context indexes (LAYER 7-8)
CREATE INDEX idx_obs_failure ON observation(failure_mode);
CREATE INDEX idx_obs_confidence ON observation(confidence);
CREATE INDEX idx_obs_context ON observation(context);
CREATE INDEX idx_obs_dose ON observation(subject_dose_range);

-- JSONB + vector
CREATE INDEX idx_obs_meta ON observation USING GIN(metadata);
CREATE INDEX idx_obs_embedding_hnsw ON observation
    USING hnsw (embedding vector_cosine_ops) WITH (m = 16, ef_construction = 64);

-- Uniqueness
CREATE UNIQUE INDEX idx_obs_unique_per_run
    ON observation(personality_id, analysis_run_id, analyzed_data_id, signal_id)
    WHERE analysis_run_id IS NOT NULL AND analyzed_data_id IS NOT NULL;


-- ═══════════════════════════════════════════════════════════
-- TAGS (universal)
-- ═══════════════════════════════════════════════════════════

CREATE TABLE tag (
    id SERIAL PRIMARY KEY,
    key VARCHAR(50) NOT NULL UNIQUE,
    name VARCHAR(100) NOT NULL,
    tag_type VARCHAR(30) NOT NULL,                 -- phenotype, trait, risk, domain, symptom, strength, custom
    domain_id INT REFERENCES domain(id) ON DELETE SET NULL,
    description TEXT,
    severity_default VARCHAR(10),
    config JSONB DEFAULT '{}',
    created_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_tag_type ON tag(tag_type);
CREATE INDEX idx_tag_domain ON tag(domain_id);

CREATE TABLE entity_tag (
    id SERIAL PRIMARY KEY,
    tag_id INT NOT NULL REFERENCES tag(id) ON DELETE CASCADE,
    entity_type VARCHAR(30) NOT NULL,              -- observation, analysis_run, trajectory, loop, person
    entity_id TEXT NOT NULL,
    severity VARCHAR(10),
    confidence VARCHAR(10),
    notes TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE (tag_id, entity_type, entity_id)
);
CREATE INDEX idx_et_tag ON entity_tag(tag_id);
CREATE INDEX idx_et_entity ON entity_tag(entity_type, entity_id);
CREATE INDEX idx_et_severity ON entity_tag(severity);


-- ═══════════════════════════════════════════════════════════
-- TRAJECTORIES
-- ═══════════════════════════════════════════════════════════

CREATE TABLE trajectory (
    id SERIAL PRIMARY KEY,
    person_id UUID NOT NULL REFERENCES person(id) ON DELETE CASCADE,
    personality_id INT NOT NULL REFERENCES personality(id) ON DELETE CASCADE,
    domain_id INT REFERENCES domain(id) ON DELETE SET NULL,
    circuit_id INT REFERENCES circuit(id),          -- if trajectory follows a known circuit
    name VARCHAR(100) NOT NULL,
    trajectory_type VARCHAR(50),
    status VARCHAR(20) DEFAULT 'active',
    config JSONB DEFAULT '{}',
    started_at TIMESTAMPTZ,
    resolved_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_traj_person ON trajectory(person_id);
CREATE INDEX idx_traj_personality ON trajectory(personality_id);
CREATE INDEX idx_traj_domain ON trajectory(domain_id);
CREATE INDEX idx_traj_circuit ON trajectory(circuit_id);
CREATE INDEX idx_traj_type ON trajectory(trajectory_type);
CREATE INDEX idx_traj_status ON trajectory(status);

CREATE TABLE trajectory_phase (
    id SERIAL PRIMARY KEY,
    trajectory_id INT NOT NULL REFERENCES trajectory(id) ON DELETE CASCADE,
    phase_number INT NOT NULL,
    phase_label VARCHAR(50),
    state_snapshot TEXT NOT NULL,
    summary TEXT,
    observation_ids INT[],
    analysis_run_id UUID REFERENCES analysis_run(id),
    circuit_phase_id INT REFERENCES circuit_phase(id), -- maps to known circuit phase
    metadata JSONB DEFAULT '{}',
    state_embedding vector(1536),
    observed_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE (trajectory_id, phase_number)
);
CREATE INDEX idx_tp_trajectory ON trajectory_phase(trajectory_id);
CREATE INDEX idx_tp_circuit_phase ON trajectory_phase(circuit_phase_id);
CREATE INDEX idx_tp_embedding_hnsw ON trajectory_phase
    USING hnsw (state_embedding vector_cosine_ops) WITH (m = 16, ef_construction = 64);


-- ═══════════════════════════════════════════════════════════
-- ACTIVE LOOPS (LAYER 7 — FAILURE MODES)
-- ═══════════════════════════════════════════════════════════

CREATE TABLE active_loop (
    id SERIAL PRIMARY KEY,
    person_id UUID NOT NULL REFERENCES person(id) ON DELETE CASCADE,
    personality_id INT NOT NULL REFERENCES personality(id) ON DELETE CASCADE,
    domain_id INT REFERENCES domain(id) ON DELETE SET NULL,
    pathway_id INT REFERENCES pathway(id),          -- if loop follows a known pathway
    name VARCHAR(100) NOT NULL,
    loop_type VARCHAR(10) NOT NULL,                -- NFB, PFB
    polarity VARCHAR(20),                          -- virtuous, vicious, stabilizing, destabilizing
    status VARCHAR(20) NOT NULL,                   -- intact, degraded, broken, latched, emerging
    formula TEXT NOT NULL,
    involved_signals INT[] NOT NULL,
    involved_gate_ids INT[],                       -- gate_instance IDs in the loop
    failure_mode VARCHAR(30),
    severity VARCHAR(10),
    analysis_run_id UUID REFERENCES analysis_run(id),
    notes TEXT,
    metadata JSONB DEFAULT '{}',
    embedding vector(1536),
    first_detected_at TIMESTAMPTZ,
    last_confirmed_at TIMESTAMPTZ,
    resolved_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX idx_al_person ON active_loop(person_id);
CREATE INDEX idx_al_personality ON active_loop(personality_id);
CREATE INDEX idx_al_domain ON active_loop(domain_id);
CREATE INDEX idx_al_pathway ON active_loop(pathway_id);
CREATE INDEX idx_al_status ON active_loop(status);
CREATE INDEX idx_al_type ON active_loop(loop_type);
CREATE INDEX idx_al_failure ON active_loop(failure_mode);
CREATE INDEX idx_al_embedding_hnsw ON active_loop
    USING hnsw (embedding vector_cosine_ops) WITH (m = 16, ef_construction = 64);


-- ═══════════════════════════════════════════════════════════
-- PROFILE SNAPSHOTS
-- ═══════════════════════════════════════════════════════════

CREATE TABLE profile_snapshot (
    id SERIAL PRIMARY KEY,
    person_id UUID NOT NULL REFERENCES person(id) ON DELETE CASCADE,
    personality_id INT NOT NULL REFERENCES personality(id) ON DELETE CASCADE,
    signal_id INT NOT NULL REFERENCES signal(id),
    latest_state VARCHAR(15),
    latest_intensity FLOAT,
    latest_failure_mode VARCHAR(30),
    latest_region_id INT REFERENCES brain_region(id),
    latest_temporal VARCHAR(20),
    latest_confidence VARCHAR(10),
    latest_dose_range VARCHAR(10),
    previous_state VARCHAR(15),
    trend VARCHAR(10),                             -- improving, stable, declining, volatile
    observation_count INT DEFAULT 0,
    last_observation_id INT REFERENCES observation(id),
    last_observed_at TIMESTAMPTZ,
    metadata JSONB DEFAULT '{}',
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE (personality_id, signal_id)
);
CREATE INDEX idx_snap_person ON profile_snapshot(person_id);
CREATE INDEX idx_snap_personality ON profile_snapshot(personality_id);
CREATE INDEX idx_snap_signal ON profile_snapshot(signal_id);
CREATE INDEX idx_snap_trend ON profile_snapshot(trend);
CREATE INDEX idx_snap_failure ON profile_snapshot(latest_failure_mode);


-- ═══════════════════════════════════════════════════════════
-- RELATIONSHIP TYPES
-- ═══════════════════════════════════════════════════════════

CREATE TABLE relationship_type (
    id SERIAL PRIMARY KEY,
    name VARCHAR UNIQUE NOT NULL,
    description TEXT,
    config JSONB DEFAULT '{}'
);


-- ═══════════════════════════════════════════════════════════
-- AGENT SYSTEM
-- ═══════════════════════════════════════════════════════════

CREATE TABLE agent_template (
    id SERIAL PRIMARY KEY,
    category VARCHAR(50) NOT NULL,
    group_name VARCHAR(100),
    name VARCHAR(100) NOT NULL,
    layer VARCHAR(50),
    role TEXT NOT NULL,
    responsibilities TEXT[],
    style TEXT NOT NULL,
    max_words INT DEFAULT 200,
    is_synthesizer BOOLEAN DEFAULT FALSE,
    sort_order INT DEFAULT 0,
    config JSONB DEFAULT '{}',
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE (category, group_name, name)
);
CREATE INDEX idx_agent_template_category ON agent_template(category);

CREATE TABLE agent_group (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    person_id UUID REFERENCES person(id) ON DELETE CASCADE,
    name VARCHAR(100) NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE UNIQUE INDEX idx_agent_group_person_name ON agent_group(person_id, name) WHERE person_id IS NOT NULL;
CREATE UNIQUE INDEX idx_agent_group_shared_name ON agent_group(name) WHERE person_id IS NULL;
CREATE INDEX idx_agent_group_person ON agent_group(person_id) WHERE person_id IS NOT NULL;

CREATE TABLE agent (
    id SERIAL PRIMARY KEY,
    group_id UUID REFERENCES agent_group(id) ON DELETE CASCADE,
    person_id UUID REFERENCES person(id) ON DELETE CASCADE,
    name VARCHAR(50) NOT NULL,
    role VARCHAR(100) NOT NULL,
    responsibilities TEXT[] NOT NULL,
    style TEXT NOT NULL,
    max_words INT DEFAULT 200,
    is_synthesizer BOOLEAN DEFAULT FALSE,
    sort_order INT DEFAULT 0,
    config JSONB DEFAULT '{}',
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE (group_id, name)
);
CREATE INDEX idx_agent_group_id ON agent(group_id);
CREATE INDEX idx_agent_person_id ON agent(person_id);


-- ═══════════════════════════════════════════════════════════
-- PIPELINE + LAYER
-- ═══════════════════════════════════════════════════════════

CREATE TABLE pipeline (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    person_id UUID NOT NULL REFERENCES person(id) ON DELETE CASCADE,
    relationship_type_id INT REFERENCES relationship_type(id),
    description TEXT,
    is_active BOOLEAN DEFAULT TRUE,
    config JSONB DEFAULT '{}',
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE (person_id, name)
);
CREATE INDEX idx_pipeline_person ON pipeline(person_id);

CREATE TABLE layer (
    id SERIAL PRIMARY KEY,
    pipeline_id INT NOT NULL REFERENCES pipeline(id) ON DELETE CASCADE,
    name VARCHAR(50) NOT NULL,
    agent_id INT NOT NULL REFERENCES agent(id),
    sort_order INT DEFAULT 0,
    is_synthesizer BOOLEAN DEFAULT FALSE,
    config JSONB DEFAULT '{}',
    created_at TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE (pipeline_id, sort_order)
);
CREATE INDEX idx_layer_pipeline ON layer(pipeline_id);


-- ═══════════════════════════════════════════════════════════
-- EMBEDDING CACHE
-- ═══════════════════════════════════════════════════════════

CREATE TABLE embedding_cache (
    id SERIAL PRIMARY KEY,
    cache_type VARCHAR(50) NOT NULL,
    domain_id INT REFERENCES domain(id) ON DELETE SET NULL,
    lookup_key VARCHAR(200) NOT NULL,
    label TEXT,
    embedding vector(1536) NOT NULL,
    metadata JSONB DEFAULT '{}',
    created_at TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE (cache_type, lookup_key)
);
CREATE INDEX idx_ec_type ON embedding_cache(cache_type);
CREATE INDEX idx_ec_domain ON embedding_cache(domain_id);
CREATE INDEX idx_ec_lookup ON embedding_cache(lookup_key);
CREATE INDEX idx_ec_embedding_hnsw ON embedding_cache
    USING hnsw (embedding vector_cosine_ops) WITH (m = 16, ef_construction = 64);


-- ═══════════════════════════════════════════════════════════
-- QUESTIONNAIRE SYSTEM
-- ═══════════════════════════════════════════════════════════

CREATE TABLE questionnaire_item (
    id SERIAL PRIMARY KEY,
    domain_id INT REFERENCES domain(id) ON DELETE SET NULL,
    sort_order INT NOT NULL,
    scenario TEXT NOT NULL,
    label CHAR(1) NOT NULL,
    option_text TEXT NOT NULL,
    primary_signal VARCHAR(50),
    secondary_signal VARCHAR(50),
    is_inverted BOOLEAN DEFAULT FALSE,
    config JSONB DEFAULT '{}',
    created_at TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE (sort_order, label)
);
CREATE INDEX idx_qi_sort ON questionnaire_item(sort_order);
CREATE INDEX idx_qi_domain ON questionnaire_item(domain_id);

CREATE TABLE questionnaire (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    person_id UUID NOT NULL REFERENCES person(id) ON DELETE CASCADE,
    domain_id INT REFERENCES domain(id) ON DELETE SET NULL,
    token VARCHAR(64) NOT NULL UNIQUE,
    status VARCHAR(20) NOT NULL DEFAULT 'pending',
    config JSONB DEFAULT '{}',
    created_at TIMESTAMPTZ DEFAULT NOW(),
    completed_at TIMESTAMPTZ
);
CREATE INDEX idx_questionnaire_person ON questionnaire(person_id);
CREATE INDEX idx_questionnaire_token ON questionnaire(token);
CREATE INDEX idx_questionnaire_domain ON questionnaire(domain_id);

CREATE TABLE questionnaire_answer (
    id SERIAL PRIMARY KEY,
    questionnaire_id UUID NOT NULL REFERENCES questionnaire(id) ON DELETE CASCADE,
    item_id INT NOT NULL REFERENCES questionnaire_item(id),
    created_at TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE (questionnaire_id, item_id)
);
CREATE INDEX idx_qa_questionnaire ON questionnaire_answer(questionnaire_id);


-- ═══════════════════════════════════════════════════════════
-- PROFILE SNAPSHOT REFRESH FUNCTION
-- ═══════════════════════════════════════════════════════════

CREATE OR REPLACE FUNCTION refresh_profile_snapshot(p_personality_id INT)
RETURNS VOID AS $$
BEGIN
    INSERT INTO profile_snapshot (
        person_id, personality_id, signal_id,
        latest_state, latest_intensity, latest_failure_mode,
        latest_region_id, latest_temporal, latest_confidence,
        latest_dose_range, previous_state, trend,
        observation_count, last_observation_id, last_observed_at, updated_at
    )
    SELECT DISTINCT ON (o.personality_id, o.signal_id)
        o.person_id,
        o.personality_id,
        o.signal_id,
        o.subject_state,
        o.intensity,
        o.failure_mode,
        o.region_id,
        o.temporal,
        o.confidence,
        o.subject_dose_range,
        (SELECT o2.subject_state FROM observation o2
         WHERE o2.personality_id = o.personality_id
           AND o2.signal_id = o.signal_id
           AND o2.created_at < o.created_at
         ORDER BY o2.created_at DESC LIMIT 1),
        CASE
            WHEN (SELECT o3.intensity FROM observation o3
                  WHERE o3.personality_id = o.personality_id
                    AND o3.signal_id = o.signal_id
                    AND o3.created_at < o.created_at
                  ORDER BY o3.created_at DESC LIMIT 1) IS NULL THEN 'new'
            WHEN o.intensity > COALESCE((SELECT o3.intensity FROM observation o3
                  WHERE o3.personality_id = o.personality_id
                    AND o3.signal_id = o.signal_id
                    AND o3.created_at < o.created_at
                  ORDER BY o3.created_at DESC LIMIT 1), 0) + 0.1 THEN 'increasing'
            WHEN o.intensity < COALESCE((SELECT o3.intensity FROM observation o3
                  WHERE o3.personality_id = o.personality_id
                    AND o3.signal_id = o.signal_id
                    AND o3.created_at < o.created_at
                  ORDER BY o3.created_at DESC LIMIT 1), 0) - 0.1 THEN 'declining'
            ELSE 'stable'
        END,
        (SELECT COUNT(*) FROM observation oc
         WHERE oc.personality_id = o.personality_id AND oc.signal_id = o.signal_id),
        o.id,
        o.created_at,
        NOW()
    FROM observation o
    WHERE o.personality_id = p_personality_id
    ORDER BY o.personality_id, o.signal_id, o.created_at DESC
    ON CONFLICT (personality_id, signal_id)
    DO UPDATE SET
        latest_state = EXCLUDED.latest_state,
        latest_intensity = EXCLUDED.latest_intensity,
        latest_failure_mode = EXCLUDED.latest_failure_mode,
        latest_region_id = EXCLUDED.latest_region_id,
        latest_temporal = EXCLUDED.latest_temporal,
        latest_confidence = EXCLUDED.latest_confidence,
        latest_dose_range = EXCLUDED.latest_dose_range,
        previous_state = profile_snapshot.latest_state,
        trend = EXCLUDED.trend,
        observation_count = EXCLUDED.observation_count,
        last_observation_id = EXCLUDED.last_observation_id,
        last_observed_at = EXCLUDED.last_observed_at,
        updated_at = NOW();
END;
$$ LANGUAGE plpgsql;


-- ═══════════════════════════════════════════════════════════
-- BIOCHAIN LAYER → TABLE MAPPING
-- ═══════════════════════════════════════════════════════════
--
-- LAYER 0 LEXICON
--   Molecules          → signal
--   Receptors          → receptor
--   Enzymes            → enzyme
--   Transporters       → transporter
--   Second Messengers  → second_messenger
--   Brain Regions      → brain_region
--
-- LAYER 1 STATE
--   Concentrations     → observation.subject_state / target_state
--   Dose-Range         → observation.subject_dose_range
--
-- LAYER 2 OPERATORS
--   Interaction ops    → observation.operator / signal_interaction.operator
--   Pathway flow ops   → pathway_step.connection_type
--   Temporal markers   → observation.temporal
--   Confidence         → observation.confidence
--   Context            → observation.context
--   Cross-layer [NT]   → signal.layer
--
-- LAYER 3 LOGIC GATES
--   Gate primitives    → gate
--   Gate instances     → gate_instance
--   Gate in formulas   → observation.gate_instance_id / gate_formula
--
-- LAYER 4 LIFECYCLE
--   Phase markers      → lifecycle_stage
--   Stage in formulas  → observation.lifecycle_stage
--
-- LAYER 5 PATHWAYS
--   Named pathways     → pathway
--   Pathway steps      → pathway_step
--   Templates          → pathway.template_type
--   Connection types   → pathway_step.connection_type
--
-- LAYER 6 CIRCUITS
--   Circuit decl       → circuit
--   Circuit pathways   → circuit_pathway
--   Circuit phases     → circuit_phase
--   Dose-response      → dose_response
--   State snapshots    → trajectory_phase.state_snapshot
--
-- LAYER 7 FAILURE MODES
--   Dysregulation      → observation.failure_mode / active_loop.failure_mode
--   Loop status        → active_loop.status
--
-- LAYER 8 FORMULA MODE
--   SUBJECT            → observation.signal_id + subject_receptor_id + subject_state
--   OPERATOR           → observation.operator
--   TARGET             → observation.target_signal_id + target_receptor_id + target_state
--   @REGION            → observation.region_id → brain_region
--   (temporal)         → observation.temporal
--   {gate}             → observation.gate_instance_id / gate_formula
--   <stage>            → observation.lifecycle_stage
--   #confidence        → observation.confidence
--   ~context           → observation.context
--
-- ═══════════════════════════════════════════════════════════