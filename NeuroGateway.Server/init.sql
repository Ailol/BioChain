-- init.sql
-- Full schema for MultiAgentAiMcp personality database
-- Replaces all previous versions — single source of truth

CREATE EXTENSION IF NOT EXISTS vector;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- ─────────────────────────────────────
-- Core
-- ─────────────────────────────────────

CREATE TABLE person (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    first_name VARCHAR(50) NOT NULL,
    last_name VARCHAR(50),
    phone VARCHAR(20),
    email VARCHAR(100),
    ssn VARCHAR(20),                          -- encrypt at application layer
    birthdate DATE,
    address VARCHAR(200),
    postcode VARCHAR(10),
    city VARCHAR(100),
    created_at TIMESTAMP DEFAULT NOW()
);

-- ─────────────────────────────────────
-- Biochemistry
-- neurotransmitter, hormone, peptide are reference tables.
-- Profiles link them to personality (observed behavior).
-- ─────────────────────────────────────

CREATE TABLE neurotransmitter (
    id SERIAL PRIMARY KEY,
    name VARCHAR(50) UNIQUE NOT NULL
);

CREATE TABLE hormone (
    id SERIAL PRIMARY KEY,
    name VARCHAR(50) UNIQUE NOT NULL,
    embedding vector(4096)
);

CREATE TABLE peptide (
    id SERIAL PRIMARY KEY,
    name VARCHAR(50) UNIQUE NOT NULL,
    embedding vector(4096)
);

-- ─────────────────────────────────────
-- Personality
-- Thin 1:1 anchor per person.
-- "Personality = the full biochemical landscape."
-- Profiles ARE the personality — queried fresh at runtime.
-- ─────────────────────────────────────

CREATE TABLE personality (
    id SERIAL PRIMARY KEY,
    person_id UUID NOT NULL UNIQUE REFERENCES person(id) ON DELETE CASCADE,
    communication_style TEXT,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW()
);
CREATE INDEX idx_personality_person ON personality(person_id);

-- ─────────────────────────────────────
-- Analyzed Data
-- Every input ever analyzed lives here with its embedding.
-- The raw material that biochemical agents process.
-- ─────────────────────────────────────

CREATE TABLE analyzed_data (
    id SERIAL PRIMARY KEY,
    person_id UUID NOT NULL REFERENCES person(id) ON DELETE CASCADE,
    content TEXT NOT NULL,
    source_type VARCHAR(30),                   -- document | chat | manual
    source_uri VARCHAR,                        -- URL, file path, chat ID
    embedding vector(4096),
    created_at TIMESTAMP DEFAULT NOW()
);
CREATE INDEX idx_analyzed_data_person ON analyzed_data(person_id);

-- ─────────────────────────────────────
-- Biochemical Profiles (children of personality)
-- "Profiles are how behavior expresses biochemically."
-- Each agent layer writes its own profile rows per analyzed input.
-- Multiple rows per chemical allowed (one per analyzed input that triggered it).
-- ─────────────────────────────────────

CREATE TABLE neurotransmitter_profile (
    id SERIAL PRIMARY KEY,
    personality_id INT NOT NULL REFERENCES personality(id) ON DELETE CASCADE,
    neurotransmitter_id INT NOT NULL REFERENCES neurotransmitter(id),
    analyzed_data_id INT REFERENCES analyzed_data(id) ON DELETE SET NULL,
    reasoning TEXT,
    reasoning_embedding vector(4096),
    cluster_id INT,
    is_cluster_representative BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW(),
    UNIQUE (personality_id, neurotransmitter_id, analyzed_data_id)
);
CREATE INDEX idx_nt_profile_personality ON neurotransmitter_profile(personality_id);
CREATE INDEX idx_nt_profile_cluster ON neurotransmitter_profile(personality_id, cluster_id);
CREATE INDEX idx_nt_profile_analyzed ON neurotransmitter_profile(analyzed_data_id);
-- Note: HNSW indexes require ≤2000 dims; reasoning_embedding is 4096 dims, so sequential scan is used

CREATE TABLE hormone_profile (
    id SERIAL PRIMARY KEY,
    personality_id INT NOT NULL REFERENCES personality(id) ON DELETE CASCADE,
    hormone_id INT NOT NULL REFERENCES hormone(id),
    analyzed_data_id INT REFERENCES analyzed_data(id) ON DELETE SET NULL,
    reasoning TEXT,
    reasoning_embedding vector(4096),
    cluster_id INT,
    is_cluster_representative BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW(),
    UNIQUE (personality_id, hormone_id, analyzed_data_id)
);
CREATE INDEX idx_hormone_profile_personality ON hormone_profile(personality_id);
CREATE INDEX idx_hormone_profile_cluster ON hormone_profile(personality_id, cluster_id);
CREATE INDEX idx_hormone_profile_analyzed ON hormone_profile(analyzed_data_id);
-- Note: HNSW indexes require ≤2000 dims; reasoning_embedding is 4096 dims, so sequential scan is used

CREATE TABLE peptide_profile (
    id SERIAL PRIMARY KEY,
    personality_id INT NOT NULL REFERENCES personality(id) ON DELETE CASCADE,
    peptide_id INT NOT NULL REFERENCES peptide(id),
    analyzed_data_id INT REFERENCES analyzed_data(id) ON DELETE SET NULL,
    reasoning TEXT,
    reasoning_embedding vector(4096),
    cluster_id INT,
    is_cluster_representative BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW(),
    UNIQUE (personality_id, peptide_id, analyzed_data_id)
);
CREATE INDEX idx_peptide_profile_personality ON peptide_profile(personality_id);
CREATE INDEX idx_peptide_profile_cluster ON peptide_profile(personality_id, cluster_id);
CREATE INDEX idx_peptide_profile_analyzed ON peptide_profile(analyzed_data_id);
-- Note: HNSW indexes require ≤2000 dims; reasoning_embedding is 4096 dims, so sequential scan is used

-- ─────────────────────────────────────
-- get_full_biochemical_profile
-- UNIONs all NT + hormone + peptide reasoning for a person.
-- When embedding provided, scores each row by reasoning_embedding similarity.
-- Orphaned profiles (NULL analyzed_data_id) still get scored via reasoning_embedding.
-- ─────────────────────────────────────

CREATE OR REPLACE FUNCTION get_full_biochemical_profile(
    p_person_id UUID,
    p_embedding vector(4096) DEFAULT NULL
)
RETURNS TABLE (
    layer TEXT,
    chemical_name VARCHAR(50),
    reasoning TEXT,
    analyzed_data_id INT,
    similarity DOUBLE PRECISION
) AS $$
BEGIN
    RETURN QUERY
    SELECT * FROM (
        -- Neurotransmitter layer
        SELECT
            'neurotransmitter'::TEXT AS layer,
            nt.name AS chemical_name,
            np.reasoning,
            np.analyzed_data_id,
            CASE
                WHEN p_embedding IS NOT NULL AND np.reasoning_embedding IS NOT NULL
                THEN 1.0 - (np.reasoning_embedding <=> p_embedding)
                ELSE 0.5
            END AS similarity
        FROM neurotransmitter_profile np
        JOIN personality per ON per.id = np.personality_id
        JOIN neurotransmitter nt ON nt.id = np.neurotransmitter_id
        WHERE per.person_id = p_person_id

        UNION ALL

        -- Hormone layer
        SELECT
            'hormone'::TEXT AS layer,
            h.name AS chemical_name,
            hp.reasoning,
            hp.analyzed_data_id,
            CASE
                WHEN p_embedding IS NOT NULL AND hp.reasoning_embedding IS NOT NULL
                THEN 1.0 - (hp.reasoning_embedding <=> p_embedding)
                ELSE 0.5
            END AS similarity
        FROM hormone_profile hp
        JOIN personality per ON per.id = hp.personality_id
        JOIN hormone h ON h.id = hp.hormone_id
        WHERE per.person_id = p_person_id

        UNION ALL

        -- Peptide layer
        SELECT
            'peptide'::TEXT AS layer,
            p.name AS chemical_name,
            pp.reasoning,
            pp.analyzed_data_id,
            CASE
                WHEN p_embedding IS NOT NULL AND pp.reasoning_embedding IS NOT NULL
                THEN 1.0 - (pp.reasoning_embedding <=> p_embedding)
                ELSE 0.5
            END AS similarity
        FROM peptide_profile pp
        JOIN personality per ON per.id = pp.personality_id
        JOIN peptide p ON p.id = pp.peptide_id
        WHERE per.person_id = p_person_id
    ) combined
    ORDER BY similarity DESC;
END;
$$ LANGUAGE plpgsql STABLE;

-- ─────────────────────────────────────
-- Agent Templates
-- System-level agent configs (analyzing agents + neurochat responders).
-- No person FK — these are global templates.
-- category: analyzing_neurotransmitter, analyzing_hormone, analyzing_peptide, neurochat
-- group_name: for neurochat = relationship type (Dating, Friend, etc.)
-- ─────────────────────────────────────

CREATE TABLE agent_template (
    id SERIAL PRIMARY KEY,
    category VARCHAR(50) NOT NULL,
    group_name VARCHAR(100),
    name VARCHAR(100) NOT NULL,
    layer VARCHAR(50),
    role VARCHAR(200) NOT NULL,
    responsibilities TEXT[],
    style TEXT NOT NULL,
    max_words INT DEFAULT 200,
    is_synthesizer BOOLEAN DEFAULT FALSE,
    sort_order INT DEFAULT 0,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW(),
    UNIQUE (category, group_name, name)
);
CREATE INDEX idx_agent_template_category ON agent_template(category);

-- ─────────────────────────────────────
-- Agent Groups
-- Custom agent ensembles generated from personalities
-- person_id is optional: NULL = system/shared group
-- ─────────────────────────────────────

CREATE TABLE agent_group (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    person_id UUID REFERENCES person(id) ON DELETE CASCADE,
    name VARCHAR(100) NOT NULL,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW()
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
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW(),
    UNIQUE (group_id, name)
);
CREATE INDEX idx_agent_group_id ON agent(group_id);
CREATE INDEX idx_agent_person_id ON agent(person_id);

-- ─────────────────────────────────────
-- Relationship Types (reference only)
-- No relationship_profile table — profiles queried dynamically at runtime
-- ─────────────────────────────────────

CREATE TABLE relationship_type (
    id SERIAL PRIMARY KEY,
    name VARCHAR UNIQUE NOT NULL,
    description TEXT
);

-- ─────────────────────────────────────
-- Pipeline + Layer
-- Pipeline owns relationship context, layers wire agents to positions
-- ─────────────────────────────────────

CREATE TABLE pipeline (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    person_id UUID NOT NULL REFERENCES person(id) ON DELETE CASCADE,
    relationship_type_id INT REFERENCES relationship_type(id),
    description TEXT,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW(),
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
    created_at TIMESTAMP DEFAULT NOW(),
    UNIQUE (pipeline_id, sort_order)
);
CREATE INDEX idx_layer_pipeline ON layer(pipeline_id);

-- ─────────────────────────────────────
-- render_template — substitute {variable} placeholders in agent prompts
-- ─────────────────────────────────────

CREATE OR REPLACE FUNCTION render_template(template TEXT, vars JSONB)
RETURNS TEXT AS $$
DECLARE
    k TEXT;
    result TEXT := template;
BEGIN
    FOR k IN SELECT jsonb_object_keys(vars) LOOP
        result := replace(result, '{' || k || '}', vars->>k);
    END LOOP;
    RETURN result;
END;
$$ LANGUAGE plpgsql IMMUTABLE;

-- ─────────────────────────────────────
-- Seed data
-- ─────────────────────────────────────

INSERT INTO neurotransmitter (name) VALUES
    ('Dopamine'),('Serotonin'),('Norepinephrine'),('GABA'),('Glutamate'),('Acetylcholine');

INSERT INTO hormone (name) VALUES
    ('Testosterone'),('Estrogen'),('Progesterone'),('Cortisol'),('Adrenaline'),('Melatonin'),('Thyroid');

INSERT INTO peptide (name) VALUES
    ('Oxytocin'),('Vasopressin'),('Endorphins'),('Enkephalins'),('Substance P'),('NPY'),('CRH');

INSERT INTO relationship_type (name, description) VALUES
    ('dating',       'Romantic or dating relationship context'),
    ('friend',       'Friendship and close social bonds'),
    ('coworker',     'Professional workplace relationship'),
    ('mentor',       'Mentoring or coaching relationship'),
    ('family',       'Family and kinship bonds'),
    ('collaborator', 'Creative or project collaboration');

-- Default person with personality anchor + example analyzed data + NT profile
INSERT INTO person (first_name) VALUES ('Ailo');

INSERT INTO personality (person_id)
    SELECT p.id FROM person p WHERE p.first_name = 'Ailo';

INSERT INTO analyzed_data (person_id, content, source_type)
    SELECT p.id, 'Programming: Flow states and problem-solving trigger dopamine reward loops.', 'manual'
    FROM person p WHERE p.first_name = 'Ailo';

INSERT INTO neurotransmitter_profile (personality_id, neurotransmitter_id, analyzed_data_id, reasoning, is_cluster_representative, cluster_id)
    SELECT per.id, nt.id, ad.id,
           'Dopamine reinforces flow states through sustained mesolimbic activation during problem-solving cycles.',
           true, 1
    FROM personality per
    JOIN person p ON p.id = per.person_id
    JOIN neurotransmitter nt ON nt.name = 'Dopamine'
    JOIN analyzed_data ad ON ad.person_id = p.id AND ad.content LIKE 'Programming:%'
    WHERE p.first_name = 'Ailo';

-- ─────────────────────────────────────
-- get_scored_layer_profile
-- Dual-vector scoring: scores reasoning rows against BOTH message and relationship
-- embeddings separately, combines with weighting. Per-chemical coverage guarantee.
-- Temporal freshness boost (70% floor, 30-day half-life).
-- ─────────────────────────────────────

CREATE OR REPLACE FUNCTION get_scored_layer_profile(
    p_person_name TEXT,                      -- person first_name (case-insensitive match)
    p_layer TEXT,                             -- 'neurotransmitter' | 'hormone' | 'peptide'
    p_message_embedding vector(4096),        -- message context vector
    p_relationship_embedding vector(4096),   -- relationship context vector
    p_message_weight FLOAT DEFAULT 0.6,      -- α: how much message matters vs relationship
    p_top_per_chemical INT DEFAULT 1         -- coverage: top N rows per chemical
)
RETURNS TABLE (
    chemical_name VARCHAR(50),
    reasoning TEXT,
    analyzed_data_id INT,
    message_sim DOUBLE PRECISION,
    relationship_sim DOUBLE PRECISION,
    composite_score DOUBLE PRECISION,
    freshness_score DOUBLE PRECISION
) AS $$
BEGIN
    IF p_layer = 'neurotransmitter' THEN
        RETURN QUERY
        WITH scored AS (
            SELECT
                nt.name AS chem_name,
                np.reasoning AS reas,
                np.analyzed_data_id AS ad_id,
                np.created_at AS created,
                CASE WHEN np.reasoning_embedding IS NOT NULL AND p_message_embedding IS NOT NULL
                     THEN 1.0 - (np.reasoning_embedding <=> p_message_embedding) ELSE 0.5 END AS m_sim,
                CASE WHEN np.reasoning_embedding IS NOT NULL AND p_relationship_embedding IS NOT NULL
                     THEN 1.0 - (np.reasoning_embedding <=> p_relationship_embedding) ELSE 0.5 END AS r_sim,
                ROW_NUMBER() OVER (
                    PARTITION BY nt.name
                    ORDER BY (
                        p_message_weight * COALESCE(1.0 - (np.reasoning_embedding <=> p_message_embedding), 0.5)
                        + (1.0 - p_message_weight) * COALESCE(1.0 - (np.reasoning_embedding <=> p_relationship_embedding), 0.5)
                    ) DESC
                ) AS rn
            FROM neurotransmitter_profile np
            JOIN personality per ON per.id = np.personality_id
            JOIN person pr ON pr.id = per.person_id
            JOIN neurotransmitter nt ON nt.id = np.neurotransmitter_id
            WHERE LOWER(pr.first_name) = LOWER(p_person_name)
        )
        SELECT
            chem_name, reas, ad_id, m_sim, r_sim,
            (p_message_weight * m_sim + (1.0 - p_message_weight) * r_sim),
            (p_message_weight * m_sim + (1.0 - p_message_weight) * r_sim)
                * (0.7 + 0.3 * EXP(-EXTRACT(EPOCH FROM (NOW() - created)) / (86400.0 * 30)))
        FROM scored WHERE rn <= p_top_per_chemical
        ORDER BY (p_message_weight * m_sim + (1.0 - p_message_weight) * r_sim)
            * (0.7 + 0.3 * EXP(-EXTRACT(EPOCH FROM (NOW() - created)) / (86400.0 * 30))) DESC;

    ELSIF p_layer = 'hormone' THEN
        RETURN QUERY
        WITH scored AS (
            SELECT
                h.name AS chem_name,
                hp.reasoning AS reas,
                hp.analyzed_data_id AS ad_id,
                hp.created_at AS created,
                CASE WHEN hp.reasoning_embedding IS NOT NULL AND p_message_embedding IS NOT NULL
                     THEN 1.0 - (hp.reasoning_embedding <=> p_message_embedding) ELSE 0.5 END AS m_sim,
                CASE WHEN hp.reasoning_embedding IS NOT NULL AND p_relationship_embedding IS NOT NULL
                     THEN 1.0 - (hp.reasoning_embedding <=> p_relationship_embedding) ELSE 0.5 END AS r_sim,
                ROW_NUMBER() OVER (
                    PARTITION BY h.name
                    ORDER BY (
                        p_message_weight * COALESCE(1.0 - (hp.reasoning_embedding <=> p_message_embedding), 0.5)
                        + (1.0 - p_message_weight) * COALESCE(1.0 - (hp.reasoning_embedding <=> p_relationship_embedding), 0.5)
                    ) DESC
                ) AS rn
            FROM hormone_profile hp
            JOIN personality per ON per.id = hp.personality_id
            JOIN person pr ON pr.id = per.person_id
            JOIN hormone h ON h.id = hp.hormone_id
            WHERE LOWER(pr.first_name) = LOWER(p_person_name)
        )
        SELECT
            chem_name, reas, ad_id, m_sim, r_sim,
            (p_message_weight * m_sim + (1.0 - p_message_weight) * r_sim),
            (p_message_weight * m_sim + (1.0 - p_message_weight) * r_sim)
                * (0.7 + 0.3 * EXP(-EXTRACT(EPOCH FROM (NOW() - created)) / (86400.0 * 30)))
        FROM scored WHERE rn <= p_top_per_chemical
        ORDER BY (p_message_weight * m_sim + (1.0 - p_message_weight) * r_sim)
            * (0.7 + 0.3 * EXP(-EXTRACT(EPOCH FROM (NOW() - created)) / (86400.0 * 30))) DESC;

    ELSIF p_layer = 'peptide' THEN
        RETURN QUERY
        WITH scored AS (
            SELECT
                p.name AS chem_name,
                pp.reasoning AS reas,
                pp.analyzed_data_id AS ad_id,
                pp.created_at AS created,
                CASE WHEN pp.reasoning_embedding IS NOT NULL AND p_message_embedding IS NOT NULL
                     THEN 1.0 - (pp.reasoning_embedding <=> p_message_embedding) ELSE 0.5 END AS m_sim,
                CASE WHEN pp.reasoning_embedding IS NOT NULL AND p_relationship_embedding IS NOT NULL
                     THEN 1.0 - (pp.reasoning_embedding <=> p_relationship_embedding) ELSE 0.5 END AS r_sim,
                ROW_NUMBER() OVER (
                    PARTITION BY p.name
                    ORDER BY (
                        p_message_weight * COALESCE(1.0 - (pp.reasoning_embedding <=> p_message_embedding), 0.5)
                        + (1.0 - p_message_weight) * COALESCE(1.0 - (pp.reasoning_embedding <=> p_relationship_embedding), 0.5)
                    ) DESC
                ) AS rn
            FROM peptide_profile pp
            JOIN personality per ON per.id = pp.personality_id
            JOIN person pr ON pr.id = per.person_id
            JOIN peptide p ON p.id = pp.peptide_id
            WHERE LOWER(pr.first_name) = LOWER(p_person_name)
        )
        SELECT
            chem_name, reas, ad_id, m_sim, r_sim,
            (p_message_weight * m_sim + (1.0 - p_message_weight) * r_sim),
            (p_message_weight * m_sim + (1.0 - p_message_weight) * r_sim)
                * (0.7 + 0.3 * EXP(-EXTRACT(EPOCH FROM (NOW() - created)) / (86400.0 * 30)))
        FROM scored WHERE rn <= p_top_per_chemical
        ORDER BY (p_message_weight * m_sim + (1.0 - p_message_weight) * r_sim)
            * (0.7 + 0.3 * EXP(-EXTRACT(EPOCH FROM (NOW() - created)) / (86400.0 * 30))) DESC;
    END IF;
END;
$$ LANGUAGE plpgsql STABLE;

-- ─────────────────────────────────────
-- Agent templates seeded from YAML by AgentTemplateSeedService on startup.
-- See: NeuroGateway.AgentFramework/AgentTemplates/GroupAgents/*.yaml  (analyzing agents)
--      NeuroGateway.AgentFramework/AgentTemplates/LayerAgents/agents.yaml  (neurochat agents)
-- ─────────────────────────────────────

-- (All agent_template INSERTs removed — seeded from YAML on startup)
-- (All agent_group + agent seeding INSERTs removed — templates loaded by AgentTemplateSeedService)
