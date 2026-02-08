-- Migration: Add custom agent groups tables
-- Run this on your Supabase database to add the new tables

-- Custom agent groups generated from personalities
CREATE TABLE IF NOT EXISTS agent_group (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    person_id UUID REFERENCES person(id) ON DELETE CASCADE,
    name VARCHAR(100) NOT NULL,
    created_at TIMESTAMP DEFAULT NOW(),
    UNIQUE (person_id, name)
);

-- Individual agents within a group
CREATE TABLE IF NOT EXISTS agent (
    id SERIAL PRIMARY KEY,
    group_id UUID REFERENCES agent_group(id) ON DELETE CASCADE,
    name VARCHAR(50) NOT NULL,
    role VARCHAR(100) NOT NULL,
    responsibilities TEXT[] NOT NULL,
    style VARCHAR(500) NOT NULL,
    max_words INT DEFAULT 200,
    is_synthesizer BOOLEAN DEFAULT FALSE,
    sort_order INT DEFAULT 0,
    UNIQUE (group_id, name)
);

-- Indexes
CREATE INDEX IF NOT EXISTS idx_agent_group_person ON agent_group(person_id);
CREATE INDEX IF NOT EXISTS idx_agent_group_id ON agent(group_id);
