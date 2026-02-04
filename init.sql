-- init.sql
CREATE EXTENSION IF NOT EXISTS vector;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS pg_trgm;

CREATE TABLE hormone (id SERIAL PRIMARY KEY, name VARCHAR(50) UNIQUE NOT NULL);
CREATE TABLE peptide (id SERIAL PRIMARY KEY, name VARCHAR(50) UNIQUE NOT NULL);
CREATE TABLE neurotransmitter (id SERIAL PRIMARY KEY, name VARCHAR(50) UNIQUE NOT NULL);

CREATE TABLE interaction (
    neurotransmitter_id INT REFERENCES neurotransmitter(id),
    target_type VARCHAR(7) CHECK (target_type IN ('hormone','peptide')),
    target_id INT,
    strength REAL CHECK (strength BETWEEN 0 AND 1),
    PRIMARY KEY (neurotransmitter_id, target_type, target_id)
);

CREATE TABLE person (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(), 
    name VARCHAR(100) UNIQUE NOT NULL
);

CREATE TABLE personality (
    id SERIAL PRIMARY KEY,
    person_id UUID REFERENCES person(id) ON DELETE CASCADE,
    neurotransmitter_id INT REFERENCES neurotransmitter(id),
    topic VARCHAR(100) NOT NULL,
    explanation VARCHAR(450),
    embedding vector(768),
    UNIQUE (person_id, neurotransmitter_id, topic)
);

CREATE INDEX idx_personality_person ON personality(person_id);
CREATE INDEX idx_personality_embedding ON personality USING hnsw (embedding vector_cosine_ops);

-- Seed
INSERT INTO hormone (name) VALUES ('Testosterone'),('Estrogen'),('Progesterone'),('Cortisol'),('Adrenaline'),('Melatonin'),('Thyroid');
INSERT INTO peptide (name) VALUES ('Oxytocin'),('Vasopressin'),('Endorphins'),('Enkephalins'),('Substance P'),('NPY'),('CRH');
INSERT INTO neurotransmitter (name) VALUES ('Dopamine'),('Serotonin'),('Norepinephrine'),('GABA'),('Glutamate'),('Acetylcholine');

INSERT INTO interaction (neurotransmitter_id, target_type, target_id, strength) VALUES
(1,'hormone',1,0.85),(1,'hormone',2,0.60),(1,'hormone',4,0.70),(1,'hormone',5,0.80),(1,'peptide',1,0.65),(1,'peptide',3,0.85),
(2,'hormone',2,0.80),(2,'hormone',3,0.65),(2,'hormone',4,0.70),(2,'hormone',6,0.90),(2,'peptide',1,0.75),
(3,'hormone',4,0.90),(3,'hormone',5,0.95),(3,'peptide',7,0.90),
(4,'hormone',3,0.85),(4,'hormone',4,0.55),(4,'peptide',3,0.70),(4,'peptide',4,0.65),(4,'peptide',6,0.80),
(5,'hormone',4,0.80),(5,'hormone',7,0.70),(5,'peptide',5,0.85),(5,'peptide',7,0.75),
(6,'hormone',2,0.65),(6,'hormone',4,0.55),(6,'hormone',7,0.75),(6,'peptide',2,0.80);

INSERT INTO person (name) VALUES ('Ailo');
INSERT INTO personality (person_id, neurotransmitter_id, topic, explanation)
SELECT id, 1, 'Programming', 'Flow states and problem-solving trigger dopamine reward loops.' FROM person WHERE name = 'Ailo';