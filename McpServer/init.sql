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
-- The parent row: one per (person, topic).
-- "Personality is what you observe."
-- source_type + source_uri = what created this row
-- ─────────────────────────────────────

CREATE TABLE personality (
    id SERIAL PRIMARY KEY,
    person_id UUID NOT NULL REFERENCES person(id) ON DELETE CASCADE,
    topic VARCHAR(100) NOT NULL,
    explanation TEXT,
    explanatory_context TEXT,
    embedding vector(4096),
    source_type VARCHAR,                       -- document | chat | manual
    source_uri VARCHAR,                        -- URL, file path, chat ID
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW(),
    UNIQUE (person_id, topic)
);
CREATE INDEX idx_personality_person ON personality(person_id);

-- ─────────────────────────────────────
-- Biochemical Profiles (children of personality)
-- "Profiles are how it expresses biochemically."
-- Each agent layer writes its own profile rows.
-- ─────────────────────────────────────

CREATE TABLE neurotransmitter_profile (
    id SERIAL PRIMARY KEY,
    personality_id INT NOT NULL REFERENCES personality(id) ON DELETE CASCADE,
    neurotransmitter_id INT NOT NULL REFERENCES neurotransmitter(id),
    reasoning TEXT,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW(),
    UNIQUE (personality_id, neurotransmitter_id)
);
CREATE INDEX idx_nt_profile_personality ON neurotransmitter_profile(personality_id);

CREATE TABLE hormone_profile (
    id SERIAL PRIMARY KEY,
    personality_id INT NOT NULL REFERENCES personality(id) ON DELETE CASCADE,
    hormone_id INT NOT NULL REFERENCES hormone(id),
    reasoning TEXT,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW(),
    UNIQUE (personality_id, hormone_id)
);
CREATE INDEX idx_hormone_profile_personality ON hormone_profile(personality_id);

CREATE TABLE peptide_profile (
    id SERIAL PRIMARY KEY,
    personality_id INT NOT NULL REFERENCES personality(id) ON DELETE CASCADE,
    peptide_id INT NOT NULL REFERENCES peptide(id),
    reasoning TEXT,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW(),
    UNIQUE (personality_id, peptide_id)
);
CREATE INDEX idx_peptide_profile_personality ON peptide_profile(personality_id);

-- ─────────────────────────────────────
-- Agent Groups
-- Custom agent ensembles generated from personalities
-- ─────────────────────────────────────

CREATE TABLE agent_group (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    person_id UUID NOT NULL REFERENCES person(id) ON DELETE CASCADE,
    name VARCHAR(100) NOT NULL,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW(),
    UNIQUE (person_id, name)
);
CREATE INDEX idx_agent_group_person ON agent_group(person_id);

CREATE TABLE agent (
    id SERIAL PRIMARY KEY,
    group_id UUID NOT NULL REFERENCES agent_group(id) ON DELETE CASCADE,
    name VARCHAR(50) NOT NULL,
    role VARCHAR(100) NOT NULL,
    responsibilities TEXT[] NOT NULL,
    style VARCHAR(500) NOT NULL,
    max_words INT DEFAULT 200,
    is_synthesizer BOOLEAN DEFAULT FALSE,
    sort_order INT DEFAULT 0,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW(),
    UNIQUE (group_id, name)
);
CREATE INDEX idx_agent_group_id ON agent(group_id);

-- ─────────────────────────────────────
-- Relationship Matching
-- compatibility_vector is derived from personality + biochemical profiles
-- ─────────────────────────────────────

CREATE TABLE relationship_type (
    id SERIAL PRIMARY KEY,
    name VARCHAR UNIQUE NOT NULL,
    description TEXT
);

CREATE TABLE relationship_profile (
    id SERIAL PRIMARY KEY,
    person_id UUID NOT NULL REFERENCES person(id),
    relationship_type_id INT NOT NULL REFERENCES relationship_type(id),
    compatibility_vector vector(4096),
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW(),
    UNIQUE (person_id, relationship_type_id)
);

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

-- Default person with example personality + NT profile
INSERT INTO person (first_name) VALUES ('Ailo');

INSERT INTO personality (person_id, topic, explanation, source_type)
    SELECT p.id, 'Programming', 'Flow states and problem-solving trigger dopamine reward loops.', 'manual'
    FROM person p WHERE p.first_name = 'Ailo';

INSERT INTO neurotransmitter_profile (personality_id, neurotransmitter_id, reasoning)
    SELECT per.id, nt.id, 'Dopamine reinforces flow states through sustained mesolimbic activation during problem-solving cycles.'
    FROM personality per, neurotransmitter nt
    WHERE per.topic = 'Programming' AND nt.name = 'Dopamine'
      AND per.person_id = (SELECT id FROM person WHERE first_name = 'Ailo');
