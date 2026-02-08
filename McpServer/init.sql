-- init.sql
CREATE EXTENSION IF NOT EXISTS vector;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS pg_trgm;

CREATE TABLE hormone (
    id SERIAL PRIMARY KEY,
    name VARCHAR(50) UNIQUE NOT NULL,
    description TEXT,
    embedding vector(4096)
);

CREATE TABLE peptide (
    id SERIAL PRIMARY KEY,
    name VARCHAR(50) UNIQUE NOT NULL,
    description TEXT,
    embedding vector(4096)
);

CREATE TABLE neurotransmitter (id SERIAL PRIMARY KEY, name VARCHAR(50) UNIQUE NOT NULL);

CREATE TABLE person (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(100) NOT NULL
);
CREATE UNIQUE INDEX idx_person_name_lower ON person (LOWER(name));

CREATE TABLE personality (
    id SERIAL PRIMARY KEY,
    person_id UUID REFERENCES person(id) ON DELETE CASCADE,
    neurotransmitter_id INT REFERENCES neurotransmitter(id),
    topic VARCHAR(100) NOT NULL,
    explanation VARCHAR(450),
    embedding vector(4096),
    UNIQUE (person_id, neurotransmitter_id, topic)
);

CREATE INDEX idx_personality_person ON personality(person_id);
-- Note: HNSW index removed - Supabase limits HNSW to 2000 dimensions, but qwen3-embedding uses 4096
-- For production with high query volume, consider using IVFFlat index instead:
-- CREATE INDEX idx_personality_embedding ON personality USING ivfflat (embedding vector_cosine_ops) WITH (lists = 100);

-- Seed neurotransmitters
INSERT INTO neurotransmitter (name) VALUES ('Dopamine'),('Serotonin'),('Norepinephrine'),('GABA'),('Glutamate'),('Acetylcholine');

-- Seed hormones with descriptions for vector embedding
INSERT INTO hormone (name, description) VALUES
('Testosterone', 'Drive, dominance, competitiveness, risk-taking behavior, assertiveness, physical confidence, ambition, territorial instincts, desire for status and achievement, impulsive decision-making under challenge, leadership drive, boldness in social situations'),
('Estrogen', 'Emotional sensitivity, social bonding, empathy, verbal fluency, nurturing behavior, mood regulation, relationship orientation, emotional memory formation, aesthetic appreciation, cooperative social strategies, intuitive understanding of others'),
('Progesterone', 'Calming influence, anxiety reduction, nesting behavior, routine-seeking, protective instincts, maternal care patterns, sleep regulation, emotional stability during transitions, patience and tolerance, preference for safety and predictability'),
('Cortisol', 'Stress response, hypervigilance, worry patterns, threat detection, energy mobilization under pressure, rumination, perfectionism driven by anxiety, avoidance behavior, chronic tension and overthinking, heightened awareness of potential problems'),
('Adrenaline', 'Fight-or-flight activation, thrill-seeking, acute stress performance, excitement under danger, physical readiness, panic responses, urgency-driven action, peak performance under pressure, rapid decision-making, love of intense experiences'),
('Melatonin', 'Sleep-wake regulation, circadian rhythm sensitivity, seasonal mood changes, introspective tendencies during evening hours, dream vividness, sensitivity to light and environment, restorative withdrawal patterns, preference for quiet contemplation'),
('Thyroid', 'Metabolic energy regulation, mental processing speed, temperature sensitivity, weight and energy fluctuations, cognitive sharpness, mood stability tied to energy levels, motivation tied to physical vitality, sustained mental focus and alertness');

-- Seed peptides with descriptions for vector embedding
INSERT INTO peptide (name, description) VALUES
('Oxytocin', 'Social bonding, trust formation, attachment behavior, physical touch affinity, generosity, in-group loyalty, empathy in close relationships, reduced social anxiety, pair bonding, parental attachment, warmth in intimate connections'),
('Vasopressin', 'Territorial behavior, mate guarding, social memory, aggression in defense of bonds, pair-bond maintenance, stress-mediated social behavior, vigilance toward social threats, loyalty and protectiveness, jealousy and possessiveness'),
('Endorphins', 'Pain modulation, euphoria from physical exertion, reward from laughter and social connection, stress-buffering, resilience through physical activity, pleasure from music and creativity, natural high from achievement and exercise'),
('Enkephalins', 'Pain suppression, comfort-seeking behavior, emotional numbing under trauma, soothing response to familiar environments, preference for routine over novelty as coping mechanism, withdrawal into safe spaces when overwhelmed'),
('Substance P', 'Pain signaling and sensitivity, emotional distress amplification, inflammatory stress responses, sensitivity to physical discomfort, heightened pain awareness, stress-related somatic complaints, emotional pain manifesting physically'),
('NPY', 'Appetite regulation, stress resilience, anxiety reduction, energy homeostasis, calm under pressure, feeding behavior patterns, emotional eating, ability to stay composed during high-stress situations, mental toughness'),
('CRH', 'Stress axis activation, anxiety initiation, fear responses, HPA axis triggering, depression-related patterns, appetite suppression under stress, sleep disruption from worry, anticipatory anxiety, catastrophic thinking patterns');

-- Seed default person
INSERT INTO person (name) VALUES ('ailo');
INSERT INTO personality (person_id, neurotransmitter_id, topic, explanation)
SELECT id, 1, 'Programming', 'Flow states and problem-solving trigger dopamine reward loops.' FROM person WHERE name = 'ailo';

-- Custom agent groups generated from personalities
CREATE TABLE agent_group (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    person_id UUID REFERENCES person(id) ON DELETE CASCADE,
    name VARCHAR(100) NOT NULL,
    created_at TIMESTAMP DEFAULT NOW(),
    UNIQUE (person_id, name)
);

-- Individual agents within a group
CREATE TABLE agent (
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

CREATE INDEX idx_agent_group_person ON agent_group(person_id);
CREATE INDEX idx_agent_group_id ON agent(group_id);
