        -- init.sql
        -- Full schema for MultiAgentAiMcp personality database
        -- v4 — Simplified. Model carries pharmacology. DB stores observations.
        -- Expanded relationship types to match training corpus.

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

        CREATE TABLE personality (
            id SERIAL PRIMARY KEY,
            person_id UUID NOT NULL UNIQUE REFERENCES person(id) ON DELETE CASCADE,
            communication_style TEXT,
            created_at TIMESTAMP DEFAULT NOW(),
            updated_at TIMESTAMP DEFAULT NOW()
        );
        CREATE INDEX idx_personality_person ON personality(person_id);

        -- ─────────────────────────────────────
        -- Input
        -- ─────────────────────────────────────

        CREATE TABLE analyzed_data (
            id SERIAL PRIMARY KEY,
            person_id UUID NOT NULL REFERENCES person(id) ON DELETE CASCADE,
            content TEXT NOT NULL,
            source_type VARCHAR(30),                   -- document | chat | manual
            source_uri VARCHAR,
            embedding vector(2560),
            created_at TIMESTAMP DEFAULT NOW()
        );
        CREATE INDEX idx_analyzed_data_person ON analyzed_data(person_id);

        -- ─────────────────────────────────────
        -- Biochemical Profile
        -- Append-only evidence log.
        -- One row per chemical per analyzed input.
        -- Model knows pharmacology. DB stores observations.
        -- ─────────────────────────────────────

        CREATE TABLE biochemical_profile (
            id SERIAL PRIMARY KEY,
            personality_id INT NOT NULL REFERENCES personality(id) ON DELETE CASCADE,
            analyzed_data_id INT REFERENCES analyzed_data(id) ON DELETE SET NULL,
            chemical VARCHAR(30) NOT NULL,              -- dopamine, oxytocin, estradiol, etc.
            reasoning TEXT NOT NULL,
            embedding vector(2560),
            modulation_factor FLOAT NOT NULL,          -- -1.0 (inhibitory) → +1.0 (excitatory)
            created_at TIMESTAMP DEFAULT NOW(),
            UNIQUE (personality_id, analyzed_data_id, chemical)
        );
        CREATE INDEX idx_bp_personality ON biochemical_profile(personality_id);
        CREATE INDEX idx_bp_analyzed ON biochemical_profile(analyzed_data_id);

        -- ─────────────────────────────────────
        -- Relationship Types
        -- ─────────────────────────────────────

        CREATE TABLE relationship_type (
            id SERIAL PRIMARY KEY,
            name VARCHAR UNIQUE NOT NULL,
            description TEXT
        );

        -- ─────────────────────────────────────
        -- Agent System
        -- ─────────────────────────────────────

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
            created_at TIMESTAMP DEFAULT NOW(),
            updated_at TIMESTAMP DEFAULT NOW(),
            UNIQUE (category, group_name, name)
        );
        CREATE INDEX idx_agent_template_category ON agent_template(category);

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
        -- Pipeline + Layer
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

