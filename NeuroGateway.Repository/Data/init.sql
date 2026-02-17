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
        -- Chemical Master Table
        -- ─────────────────────────────────────

        CREATE TABLE chemical (
            id SERIAL PRIMARY KEY,
            key VARCHAR(30) NOT NULL UNIQUE,
            label VARCHAR(50) NOT NULL,
            layer VARCHAR(20) NOT NULL,
            created_at TIMESTAMP DEFAULT NOW(),
            updated_at TIMESTAMP DEFAULT NOW()
        );
        CREATE INDEX idx_chemical_key ON chemical(key);
        CREATE INDEX idx_chemical_layer ON chemical(layer);

        -- ─────────────────────────────────────
        -- Dimension Master Table
        -- ─────────────────────────────────────

        CREATE TABLE dimension (
            id SERIAL PRIMARY KEY,
            name VARCHAR(50) NOT NULL UNIQUE,
            section VARCHAR(20) NOT NULL,
            category VARCHAR(50) NOT NULL,
            description TEXT NOT NULL,
            work_relevance FLOAT NOT NULL DEFAULT 1.0,
            private_relevance FLOAT NOT NULL DEFAULT 1.0,
            sort_order INT NOT NULL DEFAULT 0,
            created_at TIMESTAMP DEFAULT NOW(),
            updated_at TIMESTAMP DEFAULT NOW()
        );

        -- ─────────────────────────────────────
        -- Dimension ↔ Chemical Affinity
        -- ─────────────────────────────────────

        CREATE TABLE dimension_chemical_affinity (
            id SERIAL PRIMARY KEY,
            dimension_id INT NOT NULL REFERENCES dimension(id) ON DELETE CASCADE,
            chemical_id INT NOT NULL REFERENCES chemical(id) ON DELETE CASCADE,
            weight FLOAT NOT NULL,
            UNIQUE (dimension_id, chemical_id)
        );
        CREATE INDEX idx_dca_dimension ON dimension_chemical_affinity(dimension_id);
        CREATE INDEX idx_dca_chemical ON dimension_chemical_affinity(chemical_id);

        -- ─────────────────────────────────────
        -- Chemical Interactions
        -- ─────────────────────────────────────

        CREATE TABLE chemical_interaction (
            id SERIAL PRIMARY KEY,
            source_chemical_id INT NOT NULL REFERENCES chemical(id) ON DELETE CASCADE,
            target_chemical_id INT NOT NULL REFERENCES chemical(id) ON DELETE CASCADE,
            mod_factor FLOAT NOT NULL,
            mechanism TEXT,
            notes TEXT,
            created_at TIMESTAMP DEFAULT NOW(),
            updated_at TIMESTAMP DEFAULT NOW(),
            UNIQUE (source_chemical_id, target_chemical_id)
        );
        CREATE INDEX idx_ci_source ON chemical_interaction(source_chemical_id);
        CREATE INDEX idx_ci_target ON chemical_interaction(target_chemical_id);

        -- ─────────────────────────────────────
        -- Chemical Observations (per-person evidence log)
        -- Append-only. One row per chemical per analyzed input.
        -- ─────────────────────────────────────

        CREATE TABLE chemical_observation (
            id SERIAL PRIMARY KEY,
            personality_id INT NOT NULL REFERENCES personality(id) ON DELETE CASCADE,
            analyzed_data_id INT REFERENCES analyzed_data(id) ON DELETE SET NULL,
            chemical VARCHAR(30) NOT NULL,
            reasoning TEXT NOT NULL,
            embedding vector(2560),
            intensity_factor FLOAT NOT NULL,
            created_at TIMESTAMP DEFAULT NOW(),
            UNIQUE (personality_id, analyzed_data_id, chemical)
        );
        CREATE INDEX idx_co_personality ON chemical_observation(personality_id);
        CREATE INDEX idx_co_analyzed ON chemical_observation(analyzed_data_id);

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

        -- ─────────────────────────────────────
        -- Shadow Embedding Cache
        -- Pre-computed embeddings of shadow profile level descriptions.
        -- Eliminates cold-start embedding latency (~4 min → <1s).
        -- ─────────────────────────────────────

        CREATE TABLE shadow_embedding (
            id SERIAL PRIMARY KEY,
            dimension VARCHAR(50) NOT NULL,
            mode VARCHAR(20) NOT NULL,
            chemical VARCHAR(30) NOT NULL,
            level INT NOT NULL CHECK (level BETWEEN 1 AND 5),
            embedding vector(2560) NOT NULL,
            created_at TIMESTAMP DEFAULT NOW(),
            UNIQUE (dimension, mode, chemical, level)
        );

