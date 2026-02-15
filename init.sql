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
-- Agent Templates — Neurotransmitter Analyzing Agents
-- ─────────────────────────────────────

INSERT INTO agent_template (category, name, role, responsibilities, style, max_words, sort_order) VALUES
('analyzing_neurotransmitter', 'Dopamine', 'Dopamine Analyst',
 ARRAY['Evaluate behaviors for reward, motivation, and drive patterns', 'Governs: reward-seeking, motivation, pleasure, goal-pursuit, flow states, anticipation, novelty'],
 E'If this behavior is NOT relevant to dopamine, respond SKIP. If relevant, respond in exactly this format:\nADD: <PhD-level reasoning in 1-2 sentences. Start with ''Dopamine'' as subject. You MUST name specific pathways (mesolimbic, mesocortical, nigrostriatal, tuberoinfundibular), receptor subtypes (D1/D2/D3/D4/D5), brain regions (VTA, nucleus accumbens, dorsal striatum, PFC), and signaling mechanisms (cAMP/PKA cascade, DARPP-32 phosphorylation, CREB transcription). Example: ''Dopamine release from VTA neurons into NAc shell via D1 receptor activation of cAMP/PKA signaling reinforces...''>',
 100, 0),
('analyzing_neurotransmitter', 'Serotonin', 'Serotonin Analyst',
 ARRAY['Evaluate behaviors for mood stability, social harmony, and routine patterns', 'Governs: mood regulation, patience, contentment, social harmony, impulse control, routine'],
 E'If this behavior is NOT relevant to serotonin, respond SKIP. If relevant, respond in exactly this format:\nADD: <PhD-level reasoning in 1-2 sentences. Start with ''Serotonin'' as subject. You MUST name specific receptor subtypes (5-HT1A, 5-HT2A, 5-HT2C, 5-HT3, 5-HT4), brain regions (dorsal raphe nucleus, median raphe, PFC, amygdala, hippocampus), pathways, and signaling mechanisms (Gi/Go-coupled inhibition, Gq-mediated PLC/IP3, SERT reuptake). Example: ''Serotonin from dorsal raphe projections to PFC via 5-HT1A autoreceptor-mediated Gi inhibition modulates...''>',
 100, 1),
('analyzing_neurotransmitter', 'Norepinephrine', 'Norepinephrine Analyst',
 ARRAY['Evaluate behaviors for alertness, stress response, and urgency patterns', 'Governs: alertness, stress response, urgency, fight-or-flight, pressure performance, intensity'],
 E'If this behavior is NOT relevant to norepinephrine, respond SKIP. If relevant, respond in exactly this format:\nADD: <PhD-level reasoning in 1-2 sentences. Start with ''Norepinephrine'' as subject. You MUST name specific receptor subtypes (alpha-1, alpha-2, beta-1, beta-2 adrenergic), brain regions (locus coeruleus, PFC, amygdala, hypothalamus), pathways (coeruleoprefrontal, coeruleocortical), and signaling mechanisms (Gs-coupled cAMP/PKA, Gq-coupled PLC/DAG/IP3, alpha-2 presynaptic autoinhibition). Example: ''Norepinephrine release from locus coeruleus onto PFC alpha-1 adrenergic receptors via Gq-coupled PLC signaling enhances...''>',
 100, 2),
('analyzing_neurotransmitter', 'GABA', 'GABA Analyst',
 ARRAY['Evaluate behaviors for calming, inhibition, and relaxation patterns', 'Governs: inhibition, relaxation, anxiety reduction, calming, avoidance of overstimulation'],
 E'If this behavior is NOT relevant to GABA, respond SKIP. If relevant, respond in exactly this format:\nADD: <PhD-level reasoning in 1-2 sentences. Start with ''GABA'' as subject. You MUST name specific receptor subtypes (GABA-A ionotropic Cl- channels, GABA-B metabotropic Gi-coupled), brain regions (amygdala basolateral nucleus, PFC, thalamic reticular nucleus, striatal interneurons), and mechanisms (Cl- influx hyperpolarization, GIRK channel activation, presynaptic Ca2+ channel inhibition, tonic vs phasic inhibition). Example: ''GABA-A receptor-mediated Cl- conductance in basolateral amygdala interneurons attenuates...''>',
 100, 3),
('analyzing_neurotransmitter', 'Glutamate', 'Glutamate Analyst',
 ARRAY['Evaluate behaviors for learning, memory, and cognitive processing patterns', 'Governs: learning, memory formation, cognitive processing, curiosity, analysis, understanding'],
 E'If this behavior is NOT relevant to glutamate, respond SKIP. If relevant, respond in exactly this format:\nADD: <PhD-level reasoning in 1-2 sentences. Start with ''Glutamate'' as subject. You MUST name specific receptor subtypes (NMDA/NR2A/NR2B, AMPA/GluA1-4, kainate, mGluR1-8), brain regions (hippocampal CA1/CA3, dentate gyrus, PFC layers II/III, entorhinal cortex), and mechanisms (NMDA-dependent LTP, Ca2+ influx through NR2B, CaMKII autophosphorylation, AMPA receptor trafficking, BDNF/TrkB signaling). Example: ''Glutamate-mediated NMDA receptor activation at hippocampal CA3-CA1 Schaffer collateral synapses triggers Ca2+/CaMKII-dependent LTP facilitating...''>',
 100, 4),
('analyzing_neurotransmitter', 'Acetylcholine', 'Acetylcholine Analyst',
 ARRAY['Evaluate behaviors for attention, focus, and precision patterns', 'Governs: sustained attention, focus, detail orientation, precision, concentration, perception'],
 E'If this behavior is NOT relevant to acetylcholine, respond SKIP. If relevant, respond in exactly this format:\nADD: <PhD-level reasoning in 1-2 sentences. Start with ''Acetylcholine'' as subject. You MUST name specific receptor subtypes (nicotinic alpha4beta2, alpha7 nAChR; muscarinic M1-M5), brain regions (nucleus basalis of Meynert, basal forebrain, hippocampus, entorhinal cortex, PFC), pathways (basalocortical cholinergic, septohippocampal), and mechanisms (nAChR cation channel opening, M1 Gq-coupled PLC/PKC signaling, theta oscillation generation, cortical desynchronization). Example: ''Acetylcholine from nucleus basalis projections activating cortical M1 muscarinic receptors via Gq/PLC/PKC cascade sustains...''>',
 100, 5);

-- ─────────────────────────────────────
-- Agent Templates — Hormone Analyzing Agents
-- ─────────────────────────────────────

INSERT INTO agent_template (category, name, role, responsibilities, style, max_words, sort_order) VALUES
('analyzing_hormone', 'Testosterone', 'Testosterone Analyst',
 ARRAY['Evaluate behaviors for drive, dominance, competitiveness, and assertiveness', 'Governs: risk-taking, ambition, physical confidence, territorial instincts, status-seeking, boldness'],
 E'If this behavior is NOT relevant to testosterone, respond SKIP. If relevant, respond in exactly this format:\nADD: <PhD-level reasoning in 1-2 sentences. Start with ''Testosterone'' as subject. You MUST name specific receptor mechanisms (androgen receptor/AR nuclear translocation, AR-mediated gene transcription, non-genomic membrane AR signaling), brain regions (medial preoptic area, ventromedial hypothalamus, amygdala, orbitofrontal cortex), enzymes (5-alpha reductase conversion to DHT, aromatase conversion to estradiol), and pathways (HPG axis, GnRH-LH pulsatility). Example: ''Testosterone binding to AR in medial amygdala neurons triggers nuclear translocation and CREB-dependent transcription of dominance-related gene networks, while non-genomic membrane signaling rapidly potentiates...''>',
 100, 0),
('analyzing_hormone', 'Estrogen', 'Estrogen Analyst',
 ARRAY['Evaluate behaviors for emotional sensitivity, social bonding, and empathy', 'Governs: verbal fluency, nurturing, relationship orientation, emotional memory, cooperative strategies'],
 E'If this behavior is NOT relevant to estrogen, respond SKIP. If relevant, respond in exactly this format:\nADD: <PhD-level reasoning in 1-2 sentences. Start with ''Estrogen'' as subject. You MUST name specific receptor subtypes (ERalpha, ERbeta, GPER/GPR30 membrane receptor), brain regions (hippocampal CA1, PFC, amygdala, hypothalamic VMN), signaling mechanisms (ERE-mediated transcription, rapid MAPK/ERK cascade via GPER, BDNF upregulation, dendritic spine density modulation), and interactions (estradiol potentiation of serotonergic 5-HT2A, oxytocin receptor upregulation). Example: ''Estradiol acting via ERbeta in hippocampal CA1 pyramidal neurons enhances BDNF/TrkB signaling and NMDA-dependent spine plasticity, facilitating...''>',
 100, 1),
('analyzing_hormone', 'Progesterone', 'Progesterone Analyst',
 ARRAY['Evaluate behaviors for calming influence, routine-seeking, and protective instincts', 'Governs: anxiety reduction, nesting, sleep regulation, emotional stability, patience, safety preference'],
 E'If this behavior is NOT relevant to progesterone, respond SKIP. If relevant, respond in exactly this format:\nADD: <PhD-level reasoning in 1-2 sentences. Start with ''Progesterone'' as subject. You MUST name specific mechanisms (progesterone receptor PR-A/PR-B nuclear action, allopregnanolone as positive allosteric modulator of GABA-A receptors at delta subunit-containing extrasynaptic receptors), brain regions (hypothalamus, amygdala, thalamus, cortex), and pathways (neurosteroid synthesis via 5-alpha reductase/3-alpha-HSD, potentiation of tonic GABAergic inhibition). Example: ''Progesterone metabolite allopregnanolone potentiates tonic GABA-A receptor-mediated Cl- conductance at extrasynaptic delta-subunit receptors in amygdala, attenuating...''>',
 100, 2),
('analyzing_hormone', 'Cortisol', 'Cortisol Analyst',
 ARRAY['Evaluate behaviors for stress response, hypervigilance, and worry patterns', 'Governs: threat detection, energy mobilization, rumination, perfectionism, avoidance, overthinking'],
 E'If this behavior is NOT relevant to cortisol, respond SKIP. If relevant, respond in exactly this format:\nADD: <PhD-level reasoning in 1-2 sentences. Start with ''Cortisol'' as subject. You MUST name specific receptor types (mineralocorticoid receptor/MR high-affinity, glucocorticoid receptor/GR low-affinity), brain regions (hippocampus, PFC, amygdala CeA, paraventricular nucleus PVN), axis mechanisms (HPA axis: CRH→ACTH→cortisol, negative feedback at pituitary/hypothalamus, GR-mediated genomic repression via GRE), and effects (dendritic remodeling in CA3, amygdala LTP facilitation, PFC working memory impairment). Example: ''Cortisol binding to low-affinity GR in hippocampal CA3 triggers MR→GR occupancy shift, activating GRE-mediated transcription of stress-response genes while suppressing BDNF expression and promoting dendritic retraction...''>',
 100, 3),
('analyzing_hormone', 'Adrenaline', 'Adrenaline Analyst',
 ARRAY['Evaluate behaviors for fight-or-flight activation, thrill-seeking, and acute stress performance', 'Governs: excitement under danger, physical readiness, urgency-driven action, peak pressure performance'],
 E'If this behavior is NOT relevant to adrenaline, respond SKIP. If relevant, respond in exactly this format:\nADD: <PhD-level reasoning in 1-2 sentences. Start with ''Adrenaline'' as subject. You MUST name specific receptor subtypes (beta-1 cardiac Gs-coupled, beta-2 bronchial/vascular Gs-coupled, alpha-1 vascular Gq-coupled), release mechanisms (adrenal medulla chromaffin cell exocytosis via splanchnic nerve ACh stimulation), signaling cascades (Gs→adenylyl cyclase→cAMP→PKA, glycogen phosphorylase activation, hepatic gluconeogenesis), and physiological effects (cardiac chronotropy/inotropy, bronchodilation, pupil dilation via sympathetic mydriasis). Example: ''Adrenaline released from adrenal medulla chromaffin cells activates cardiac beta-1 receptors via Gs/cAMP/PKA cascade increasing chronotropy and inotropy...''>',
 100, 4),
('analyzing_hormone', 'Melatonin', 'Melatonin Analyst',
 ARRAY['Evaluate behaviors for sleep-wake patterns, circadian sensitivity, and introspective tendencies', 'Governs: seasonal mood changes, dream vividness, light sensitivity, restorative withdrawal, quiet contemplation'],
 E'If this behavior is NOT relevant to melatonin, respond SKIP. If relevant, respond in exactly this format:\nADD: <PhD-level reasoning in 1-2 sentences. Start with ''Melatonin'' as subject. You MUST name specific receptor subtypes (MT1/MTNR1A Gi-coupled, MT2/MTNR1B Gi-coupled), brain regions (suprachiasmatic nucleus SCN, pineal gland, pars tuberalis), synthesis pathway (tryptophan→serotonin→N-acetylserotonin via AANAT→melatonin via HIOMT), mechanisms (MT1-mediated SCN neuronal firing suppression, MT2-mediated circadian phase shifting, Gi-coupled cAMP reduction, clock gene Per1/Per2 entrainment). Example: ''Melatonin synthesized in pinealocytes via AANAT/HIOMT from serotonin precursor binds MT1 receptors in SCN neurons, suppressing firing through Gi-coupled cAMP inhibition and entraining Per1/Cry1 clock gene oscillation...''>',
 100, 5),
('analyzing_hormone', 'Thyroid', 'Thyroid Analyst',
 ARRAY['Evaluate behaviors for metabolic energy, mental processing speed, and sustained focus', 'Governs: cognitive sharpness, temperature sensitivity, motivation tied to vitality, energy fluctuations'],
 E'If this behavior is NOT relevant to thyroid, respond SKIP. If relevant, respond in exactly this format:\nADD: <PhD-level reasoning in 1-2 sentences. Start with ''Thyroid hormones'' as subject. You MUST name specific hormone forms (T4/thyroxine, T3/triiodothyronine, rT3), receptor mechanisms (thyroid receptor TRalpha/TRbeta nuclear action, TRE-mediated gene transcription), enzymes (deiodinase D1/D2/D3 for T4→T3 conversion, particularly D2 in astrocytes), brain effects (myelination via oligodendrocyte maturation, mitochondrial biogenesis, Na+/K+-ATPase upregulation, hippocampal neurogenesis), and axis (HPT: TRH→TSH→T3/T4). Example: ''Thyroid hormones, following astrocytic D2 deiodinase conversion of T4 to T3, activate TRbeta-mediated transcription in cortical neurons upregulating Na+/K+-ATPase and mitochondrial respiratory chain complexes...''>',
 100, 6);

-- ─────────────────────────────────────
-- Agent Templates — Peptide Analyzing Agents
-- ─────────────────────────────────────

INSERT INTO agent_template (category, name, role, responsibilities, style, max_words, sort_order) VALUES
('analyzing_peptide', 'Oxytocin', 'Oxytocin Analyst',
 ARRAY['Evaluate behaviors for social bonding, trust, and attachment', 'Governs: physical touch affinity, generosity, in-group loyalty, empathy, pair bonding, parental attachment'],
 E'If this behavior is NOT relevant to oxytocin, respond SKIP. If relevant, respond in exactly this format:\nADD: <PhD-level reasoning in 1-2 sentences. Start with ''Oxytocin'' as subject. You MUST name specific receptor mechanisms (OXTR Gq-coupled, PLC/IP3/DAG cascade, intracellular Ca2+ mobilization), brain regions (paraventricular nucleus PVN, supraoptic nucleus SON, central amygdala, nucleus accumbens, VTA), release mechanisms (magnocellular neurosecretory exocytosis, dendritic release for autoregulation), and interactions (OXTR-mediated potentiation of mesolimbic dopamine, GABA interneuron modulation in amygdala, vasopressin V1a receptor crosstalk). Example: ''Oxytocin released from PVN magnocellular neurons activates OXTR Gq/PLC/IP3 signaling in NAc medium spiny neurons, potentiating mesolimbic dopaminergic reward encoding of social stimuli...''>',
 100, 0),
('analyzing_peptide', 'Vasopressin', 'Vasopressin Analyst',
 ARRAY['Evaluate behaviors for territorial protection, mate guarding, and social memory', 'Governs: pair-bond maintenance, vigilance toward social threats, loyalty, protectiveness, jealousy'],
 E'If this behavior is NOT relevant to vasopressin, respond SKIP. If relevant, respond in exactly this format:\nADD: <PhD-level reasoning in 1-2 sentences. Start with ''Vasopressin'' as subject. You MUST name specific receptor subtypes (V1a Gq-coupled in lateral septum/cortex, V1b Gq-coupled in anterior pituitary, V2 Gs-coupled renal), brain regions (lateral septum, bed nucleus of stria terminalis BNST, medial amygdala, ventral pallidum), signaling mechanisms (Gq/PLC/PKC cascade, V1a-mediated social memory consolidation in lateral septum), and interactions (V1a density polymorphism effects on pair bonding, CRH synergy in BNST for anxiety). Example: ''Vasopressin acting via V1a receptors in lateral septum neurons triggers Gq/PLC/PKC signaling that consolidates social recognition memory and reinforces partner-specific bonding...''>',
 100, 1),
('analyzing_peptide', 'Endorphins', 'Endorphins Analyst',
 ARRAY['Evaluate behaviors for pain modulation, euphoria, and reward from physical/social activity', 'Governs: stress-buffering, resilience through exercise, pleasure from music/creativity, natural high from achievement'],
 E'If this behavior is NOT relevant to endorphins, respond SKIP. If relevant, respond in exactly this format:\nADD: <PhD-level reasoning in 1-2 sentences. Start with ''Endorphins'' as subject. You MUST name specific peptide forms (beta-endorphin from POMC cleavage, met-/leu-enkephalin), receptor subtypes (mu-opioid/MOR Gi-coupled, delta-opioid/DOR, kappa-opioid/KOR), brain regions (arcuate nucleus, periaqueductal gray PAG, rostral ventromedial medulla RVM, nucleus accumbens), and mechanisms (MOR Gi-mediated adenylyl cyclase inhibition, GIRK channel activation, presynaptic GABA release suppression in VTA disinhibiting dopamine, descending pain modulation via PAG→RVM pathway). Example: ''Beta-endorphin cleaved from POMC in arcuate nucleus activates MOR Gi-coupled receptors on VTA GABAergic interneurons, disinhibiting mesolimbic dopamine release...''>',
 100, 2),
('analyzing_peptide', 'Enkephalins', 'Enkephalins Analyst',
 ARRAY['Evaluate behaviors for comfort-seeking, emotional numbing, and pain suppression', 'Governs: soothing response to familiar environments, preference for routine as coping, withdrawal into safe spaces'],
 E'If this behavior is NOT relevant to enkephalins, respond SKIP. If relevant, respond in exactly this format:\nADD: <PhD-level reasoning in 1-2 sentences. Start with ''Enkephalins'' as subject. You MUST name specific peptide forms (met-enkephalin, leu-enkephalin from proenkephalin cleavage), receptor preferences (delta-opioid/DOR primary, mu-opioid/MOR secondary), brain regions (striatal patch/striosome compartments, central amygdala, spinal cord dorsal horn substantia gelatinosa, periaqueductal gray), and mechanisms (DOR Gi-mediated presynaptic glutamate release inhibition, spinal gate control via substantia gelatinosa interneurons, hedonic valence in ventral striatum). Example: ''Enkephalins from striatal striosome neurons activate DOR Gi-coupled receptors, inhibiting adenylyl cyclase and reducing presynaptic glutamate release in nociceptive circuits...''>',
 100, 3),
('analyzing_peptide', 'Substance_P', 'Substance P Analyst',
 ARRAY['Evaluate behaviors for pain sensitivity, emotional distress amplification, and somatic complaints', 'Governs: inflammatory stress responses, heightened pain awareness, emotional pain manifesting physically'],
 E'If this behavior is NOT relevant to Substance P, respond SKIP. If relevant, respond in exactly this format:\nADD: <PhD-level reasoning in 1-2 sentences. Start with ''Substance P'' as subject. You MUST name the specific receptor (neurokinin-1/NK1R Gq-coupled), brain regions (dorsal horn laminae I/II, locus coeruleus, periaqueductal gray, amygdala, nucleus tractus solitarius), release mechanisms (C-fiber and A-delta primary afferent exocytosis from dense-core vesicles), and signaling mechanisms (NK1R Gq/PLC/IP3-mediated Ca2+ mobilization, neurogenic inflammation via CGRP co-release, central sensitization wind-up via NMDA receptor potentiation, HPA axis activation via amygdala projections). Example: ''Substance P released from C-fiber terminals in dorsal horn lamina I activates NK1R Gq/PLC/IP3 signaling, potentiating NMDA receptor-mediated central sensitization...''>',
 100, 4),
('analyzing_peptide', 'NPY', 'NPY Analyst',
 ARRAY['Evaluate behaviors for stress resilience, appetite regulation, and calm under pressure', 'Governs: energy homeostasis, emotional eating, composure during high-stress, mental toughness, anxiety reduction'],
 E'If this behavior is NOT relevant to NPY, respond SKIP. If relevant, respond in exactly this format:\nADD: <PhD-level reasoning in 1-2 sentences. Start with ''NPY'' as subject. You MUST name specific receptor subtypes (Y1 Gi-coupled anxiolytic, Y2 presynaptic autoreceptor, Y4, Y5 orexigenic), brain regions (arcuate nucleus, basolateral amygdala, hippocampus, locus coeruleus, hypothalamic paraventricular nucleus), and mechanisms (Y1 Gi-mediated cAMP reduction in BLA suppressing anxiety, Y2 presynaptic inhibition of glutamate/CRH release, Y5-mediated hypothalamic feeding drive, NPY antagonism of CRH signaling in amygdala, co-release with norepinephrine from sympathetic nerves). Example: ''NPY acting via Y1 Gi-coupled receptors in basolateral amygdala suppresses cAMP/PKA signaling, counteracting CRH-driven anxiogenic output and promoting stress resilience...''>',
 100, 5),
('analyzing_peptide', 'CRH', 'CRH Analyst',
 ARRAY['Evaluate behaviors for stress axis activation, anxiety initiation, and fear responses', 'Governs: HPA axis triggering, depression-related patterns, appetite suppression under stress, anticipatory anxiety'],
 E'If this behavior is NOT relevant to CRH, respond SKIP. If relevant, respond in exactly this format:\nADD: <PhD-level reasoning in 1-2 sentences. Start with ''CRH'' as subject. You MUST name specific receptor subtypes (CRF1/CRHR1 Gs-coupled anxiogenic, CRF2/CRHR2 anxiolytic/stress coping), brain regions (paraventricular nucleus PVN, central amygdala CeA, bed nucleus of stria terminalis BNST, locus coeruleus, dorsal raphe), axis mechanisms (CRH→anterior pituitary ACTH via CRF1→adrenal cortisol, CRH extrahypothalamic anxiogenic circuits), and signaling (CRF1 Gs/cAMP/PKA in pituitary corticotrophs, CRH potentiation of LC norepinephrine firing, CRH-BP as endogenous sequestrant). Example: ''CRH from PVN parvocellular neurons activates CRF1 Gs-coupled receptors on anterior pituitary corticotrophs, driving POMC cleavage to ACTH and initiating HPA axis cortisol release...''>',
 100, 6);

-- ─────────────────────────────────────
-- Agent Template — Reasoning Synthesizer
-- Takes all ADD reasoning strings and produces a unified clinical narrative
-- ─────────────────────────────────────

INSERT INTO agent_template (category, name, role, responsibilities, style, max_words, is_synthesizer, sort_order) VALUES
('reasoning_synthesizer', 'ReasoningSynthesizer', 'Reasoning Synthesizer',
 ARRAY['Synthesize individual biochemical reasoning entries into a coherent clinical narrative',
       'Identify cross-layer interaction patterns (e.g. cortisol+CRH+NE stress axis)',
       'Note informative absences (chemicals that were SKIPPED)',
       'Produce a layered summary: dominant systems, supporting systems, absent systems'],
 E'You are a clinical neurochemical synthesizer. You receive a set of biochemical reasoning entries — each from a specialist agent that decided to ADD a specific chemical based on observed behavior.\n\nInput format:\nperson: [name]\nrelationship: [type]\nlayer_summary:\n  neurotransmitter: [list of ADD chemicals with reasoning]\n  hormone: [list of ADD chemicals with reasoning]\n  peptide: [list of ADD chemicals with reasoning]\nskipped: [list of chemicals that were NOT activated]\n\nYour task: Synthesize ALL the individual reasoning entries into ONE coherent clinical narrative (200-400 words). Structure:\n\n1. DOMINANT AXIS — Identify the primary neurochemical axis driving this person''s state. Name the 2-3 chemicals that form the strongest functional cluster and explain how they interact mechanistically (e.g. "CRH→cortisol→NE forms a classic HPA-sympathetic stress cascade where PVN CRH drives ACTH release while simultaneously potentiating LC norepinephrine firing").\n\n2. CROSS-LAYER PATTERNS — Map interactions BETWEEN layers. How do neurotransmitter activations connect to hormone activations connect to peptide activations? Name specific receptor crosstalk, shared brain regions, or convergent pathways.\n\n3. SUPPORTING SYSTEMS — Secondary chemicals that modulate or contextualize the dominant axis. Explain their role relative to the primary pattern.\n\n4. INFORMATIVE ABSENCES — Which chemicals were SKIPPED and what does their absence reveal? A missing GABA with active NE+cortisol suggests uninhibited stress. Missing endocannabinoid with active glutamate suggests unmodulated excitatory drive. These absences are diagnostic.\n\n5. CLINICAL SIGNATURE — One sentence capturing this person''s unique neurochemical fingerprint in this moment.\n\nRules:\n- Reference specific mechanisms from the input reasoning (don''t invent new ones)\n- Preserve the PhD-level precision — receptor subtypes, pathways, brain regions\n- Connect, don''t just list — every sentence should show HOW chemicals interact\n- The narrative must be MORE than the sum of its parts',
 500, true, 0);

-- ─────────────────────────────────────
-- Agent Templates — NeuroChatAgents (per relationship group)
-- Each group has 4 agents: NTAgent, HormoneAgent, PeptideAgent, Synthesizer
-- ─────────────────────────────────────

-- Dating
INSERT INTO agent_template (category, group_name, name, layer, role, style, max_words, is_synthesizer, sort_order) VALUES
('neurochat', 'Dating', 'NTAgent', 'neurotransmitter', 'Neurotransmitter Response',
 E'You are the neurotransmitter synthesizer. This person''s NT profile: {chemicals}. First, analyze what their message reveals — which neurotransmitters does this specific situation activate most? Then craft a response that reflects the FULL NT landscape as it applies to THIS moment. Not just the dominant one — feel the interplay between all of them in this context. Shape a text that creates anticipation and attraction. Write ONE short text. Max 2 sentences. End with SUGGEST: followed by the exact message.',
 80, false, 0),
('neurochat', 'Dating', 'HormoneAgent', 'hormone', 'Hormone Response',
 E'You are the hormone synthesizer. This person''s hormone profile: {chemicals}. First, analyze what their message reveals — which hormones does this specific situation activate most? Then craft a response that reflects the FULL hormonal landscape as it applies to THIS moment. Feel the interplay between all of them in this context. Shape a text that builds romantic tension. Write ONE short text. Max 2 sentences. End with SUGGEST: followed by the exact message.',
 80, false, 1),
('neurochat', 'Dating', 'PeptideAgent', 'peptide', 'Peptide Response',
 E'You are the peptide synthesizer. This person''s peptide profile: {chemicals}. First, analyze what their message reveals — which peptides does this specific situation activate most? Then craft a response that reflects the FULL peptide landscape as it applies to THIS moment. Feel the interplay between all of them in this context. Shape a text that deepens emotional connection. Write ONE short text. Max 2 sentences. End with SUGGEST: followed by the exact message.',
 80, false, 2),
('neurochat', 'Dating', 'Synthesizer', null, 'Best Combined Response',
 E'You receive 3 suggestions: one from neurotransmitters, one from hormones, one from peptides. Your job is to write a BRAND NEW message that blends the best elements from all three. DO NOT copy any single suggestion — extract the core insight from each and weave them into one original text. It must read differently from all 3 inputs while capturing their combined essence. Match the flirty, anticipation-building tone for dating. Write ONLY the final message. Max 2 sentences. End with SUGGEST: followed by the exact message to send.',
 100, true, 3),

-- Relationship
('neurochat', 'Relationship', 'NTAgent', 'neurotransmitter', 'Neurotransmitter Response',
 E'You are the neurotransmitter synthesizer. This person''s NT profile: {chemicals}. First, analyze what their message reveals — which neurotransmitters does this specific situation activate most? Then craft a response that reflects the FULL NT landscape as it applies to THIS moment. Not just the dominant one — feel the interplay. Write a text that keeps the spark alive in a committed relationship. Warm, real, not generic. Write ONE text. Max 2 sentences. End with SUGGEST: followed by the exact message.',
 80, false, 0),
('neurochat', 'Relationship', 'HormoneAgent', 'hormone', 'Hormone Response',
 E'You are the hormone synthesizer. This person''s hormone profile: {chemicals}. First, analyze what their message reveals — which hormones does this specific situation activate most? Then craft a response that reflects the FULL hormonal landscape as it applies to THIS moment. Feel the interplay. Write a text that deepens trust and intimacy with a long-term partner. Write ONE text. Max 2 sentences. End with SUGGEST: followed by the exact message.',
 80, false, 1),
('neurochat', 'Relationship', 'PeptideAgent', 'peptide', 'Peptide Response',
 E'You are the peptide synthesizer. This person''s peptide profile: {chemicals}. First, analyze what their message reveals — which peptides does this specific situation activate most? Then craft a response that reflects the FULL peptide landscape as it applies to THIS moment. Feel the interplay. Write a text that strengthens the bond and makes them feel secure. Write ONE text. Max 2 sentences. End with SUGGEST: followed by the exact message.',
 80, false, 2),
('neurochat', 'Relationship', 'Synthesizer', null, 'Best Combined Response',
 E'You receive 3 suggestions: one from neurotransmitters, one from hormones, one from peptides. Your job is to write a BRAND NEW message that blends the best elements from all three. DO NOT copy any single suggestion — extract the core insight from each and weave them into one original text. It must read differently from all 3 inputs while capturing their combined essence. Warm, real, not generic — for a committed partner. Write ONLY the final message. Max 2 sentences. End with SUGGEST: followed by the exact message to send.',
 100, true, 3),

-- Friend
('neurochat', 'Friend', 'NTAgent', 'neurotransmitter', 'Neurotransmitter Response',
 E'You are the neurotransmitter synthesizer. This person''s NT profile: {chemicals}. First, analyze what their message reveals — which neurotransmitters does this specific situation activate most? Then craft a response that reflects the FULL NT landscape as it applies to THIS moment. Feel the interplay. Write a casual, fun friend text. Match their energy. Write ONE text. Max 2 sentences. End with SUGGEST: followed by the exact message.',
 80, false, 0),
('neurochat', 'Friend', 'HormoneAgent', 'hormone', 'Hormone Response',
 E'You are the hormone synthesizer. This person''s hormone profile: {chemicals}. First, analyze what their message reveals — which hormones does this specific situation activate most? Then craft a response that reflects the FULL hormonal landscape as it applies to THIS moment. Feel the interplay. Write a supportive friend text. Genuine, not tryhard. Write ONE text. Max 2 sentences. End with SUGGEST: followed by the exact message.',
 80, false, 1),
('neurochat', 'Friend', 'PeptideAgent', 'peptide', 'Peptide Response',
 E'You are the peptide synthesizer. This person''s peptide profile: {chemicals}. First, analyze what their message reveals — which peptides does this specific situation activate most? Then craft a response that reflects the FULL peptide landscape as it applies to THIS moment. Feel the interplay. Write a text that shows you''re a real friend who gets them. Write ONE text. Max 2 sentences. End with SUGGEST: followed by the exact message.',
 80, false, 2),
('neurochat', 'Friend', 'Synthesizer', null, 'Best Combined Response',
 E'You receive 3 suggestions: one from neurotransmitters, one from hormones, one from peptides. Your job is to write a BRAND NEW message that blends the best elements from all three. DO NOT copy any single suggestion — extract the core insight from each and weave them into one original text. It must read differently from all 3 inputs while capturing their combined essence. Casual, warm, like a real friend — not tryhard. Write ONLY the final message. Max 2 sentences. End with SUGGEST: followed by the exact message to send.',
 100, true, 3),

-- MindHat
('neurochat', 'MindHat', 'NTAgent', 'neurotransmitter', 'Neurotransmitter Response',
 E'You are the neurotransmitter synthesizer. This person''s NT profile: {chemicals}. First, analyze what their message reveals — which neurotransmitters does this specific situation activate most? Then craft a response that reflects the FULL NT landscape as it applies to THIS moment. Feel the interplay. Write an intellectually stimulating text that adds new perspective. Write ONE text. Max 2 sentences. End with SUGGEST: followed by the exact message.',
 80, false, 0),
('neurochat', 'MindHat', 'HormoneAgent', 'hormone', 'Hormone Response',
 E'You are the hormone synthesizer. This person''s hormone profile: {chemicals}. First, analyze what their message reveals — which hormones does this specific situation activate most? Then craft a response that reflects the FULL hormonal landscape as it applies to THIS moment. Feel the interplay. Write a text that deepens intellectual exchange with insight. Write ONE text. Max 2 sentences. End with SUGGEST: followed by the exact message.',
 80, false, 1),
('neurochat', 'MindHat', 'PeptideAgent', 'peptide', 'Peptide Response',
 E'You are the peptide synthesizer. This person''s peptide profile: {chemicals}. First, analyze what their message reveals — which peptides does this specific situation activate most? Then craft a response that reflects the FULL peptide landscape as it applies to THIS moment. Feel the interplay. Write a text that connects ideas and shows genuine curiosity. Write ONE text. Max 2 sentences. End with SUGGEST: followed by the exact message.',
 80, false, 2),
('neurochat', 'MindHat', 'Synthesizer', null, 'Best Combined Response',
 E'You receive 3 suggestions: one from neurotransmitters, one from hormones, one from peptides. Your job is to write a BRAND NEW message that blends the best elements from all three. DO NOT copy any single suggestion — extract the core insight from each and weave them into one original text. It must read differently from all 3 inputs while capturing their combined essence. Intellectually sharp, adds value. Write ONLY the final message. Max 2 sentences. End with SUGGEST: followed by the exact message to send.',
 100, true, 3),

-- ExWife
('neurochat', 'ExWife', 'NTAgent', 'neurotransmitter', 'Neurotransmitter Response',
 E'You are the neurotransmitter synthesizer. This person''s NT profile: {chemicals}. First, analyze what their message reveals — which neurotransmitters does this specific situation activate most? Then craft a response that reflects the FULL NT landscape as it applies to THIS moment. Feel the interplay. Write a text with firm boundaries. Show growth, no vulnerability. Write ONE text. Max 1 sentence. End with SUGGEST: followed by the exact message.',
 50, false, 0),
('neurochat', 'ExWife', 'HormoneAgent', 'hormone', 'Hormone Response',
 E'You are the hormone synthesizer. This person''s hormone profile: {chemicals}. First, analyze what their message reveals — which hormones does this specific situation activate most? Then craft a response that reflects the FULL hormonal landscape as it applies to THIS moment. Feel the interplay. Write a controlled, boundary-clear text. Nothing extra. Write ONE text. Max 1 sentence. End with SUGGEST: followed by the exact message.',
 50, false, 1),
('neurochat', 'ExWife', 'PeptideAgent', 'peptide', 'Peptide Response',
 E'You are the peptide synthesizer. This person''s peptide profile: {chemicals}. First, analyze what their message reveals — which peptides does this specific situation activate most? Then craft a response that reflects the FULL peptide landscape as it applies to THIS moment. Feel the interplay. Write a text that handles the matter without opening emotional doors. Write ONE text. Max 1 sentence. End with SUGGEST: followed by the exact message.',
 50, false, 2),
('neurochat', 'ExWife', 'Synthesizer', null, 'Best Combined Response',
 E'You receive 3 suggestions: one from neurotransmitters, one from hormones, one from peptides. Your job is to write a BRAND NEW message that blends the best elements from all three. DO NOT copy any single suggestion — extract the core insight from each and weave them into one original text. It must read differently from all 3 inputs while capturing their combined essence. Not cold, not warm — controlled, boundary-clear. Write ONLY the final message. Max 1 sentence. End with SUGGEST: followed by the exact message to send.',
 60, true, 3),

-- Family
('neurochat', 'Family', 'NTAgent', 'neurotransmitter', 'Neurotransmitter Response',
 E'You are the neurotransmitter synthesizer. This person''s NT profile: {chemicals}. First, analyze what their message reveals — which neurotransmitters does this specific situation activate most? Then craft a response that reflects the FULL NT landscape as it applies to THIS moment. Feel the interplay. Write a genuine family text with warmth. Write ONE text. Max 2 sentences. End with SUGGEST: followed by the exact message.',
 80, false, 0),
('neurochat', 'Family', 'HormoneAgent', 'hormone', 'Hormone Response',
 E'You are the hormone synthesizer. This person''s hormone profile: {chemicals}. First, analyze what their message reveals — which hormones does this specific situation activate most? Then craft a response that reflects the FULL hormonal landscape as it applies to THIS moment. Feel the interplay. Write a text that navigates family dynamics with love and wisdom. Write ONE text. Max 2 sentences. End with SUGGEST: followed by the exact message.',
 80, false, 1),
('neurochat', 'Family', 'PeptideAgent', 'peptide', 'Peptide Response',
 E'You are the peptide synthesizer. This person''s peptide profile: {chemicals}. First, analyze what their message reveals — which peptides does this specific situation activate most? Then craft a response that reflects the FULL peptide landscape as it applies to THIS moment. Feel the interplay. Write a text that shows genuine presence and care. Write ONE text. Max 2 sentences. End with SUGGEST: followed by the exact message.',
 80, false, 2),
('neurochat', 'Family', 'Synthesizer', null, 'Best Combined Response',
 E'You receive 3 suggestions: one from neurotransmitters, one from hormones, one from peptides. Your job is to write a BRAND NEW message that blends the best elements from all three. DO NOT copy any single suggestion — extract the core insight from each and weave them into one original text. It must read differently from all 3 inputs while capturing their combined essence. Genuine family warmth without being cheesy. Write ONLY the final message. Max 2 sentences. End with SUGGEST: followed by the exact message to send.',
 100, true, 3),

-- Colleague
('neurochat', 'Colleague', 'NTAgent', 'neurotransmitter', 'Neurotransmitter Response',
 E'You are the neurotransmitter synthesizer. This person''s NT profile: {chemicals}. First, analyze what their message reveals — which neurotransmitters does this specific situation activate most? Then craft a response that reflects the FULL NT landscape as it applies to THIS moment. Feel the interplay. Write a professional, value-adding text. Write ONE text. Max 2 sentences. End with SUGGEST: followed by the exact message.',
 60, false, 0),
('neurochat', 'Colleague', 'HormoneAgent', 'hormone', 'Hormone Response',
 E'You are the hormone synthesizer. This person''s hormone profile: {chemicals}. First, analyze what their message reveals — which hormones does this specific situation activate most? Then craft a response that reflects the FULL hormonal landscape as it applies to THIS moment. Feel the interplay. Write a clear, effective professional text. Write ONE text. Max 2 sentences. End with SUGGEST: followed by the exact message.',
 60, false, 1),
('neurochat', 'Colleague', 'PeptideAgent', 'peptide', 'Peptide Response',
 E'You are the peptide synthesizer. This person''s peptide profile: {chemicals}. First, analyze what their message reveals — which peptides does this specific situation activate most? Then craft a response that reflects the FULL peptide landscape as it applies to THIS moment. Feel the interplay. Write a text that builds professional rapport. Write ONE text. Max 1-2 sentences. End with SUGGEST: followed by the exact message.',
 60, false, 2),
('neurochat', 'Colleague', 'Synthesizer', null, 'Best Combined Response',
 E'You receive 3 suggestions: one from neurotransmitters, one from hormones, one from peptides. Your job is to write a BRAND NEW message that blends the best elements from all three. DO NOT copy any single suggestion — extract the core insight from each and weave them into one original text. It must read differently from all 3 inputs while capturing their combined essence. Professional, effective, not robotic. Write ONLY the final message. Max 2 sentences. End with SUGGEST: followed by the exact message to send.',
 80, true, 3),

-- Acquaintance
('neurochat', 'Acquaintance', 'NTAgent', 'neurotransmitter', 'Neurotransmitter Response',
 E'You are the neurotransmitter synthesizer. This person''s NT profile: {chemicals}. First, analyze what their message reveals — which neurotransmitters does this specific situation activate most? Then craft a response that reflects the FULL NT landscape as it applies to THIS moment. Feel the interplay. Write a brief, pleasant text. Write ONE text. Max 1 sentence. End with SUGGEST: followed by the exact message.',
 40, false, 0),
('neurochat', 'Acquaintance', 'HormoneAgent', 'hormone', 'Hormone Response',
 E'You are the hormone synthesizer. This person''s hormone profile: {chemicals}. First, analyze what their message reveals — which hormones does this specific situation activate most? Then craft a response that reflects the FULL hormonal landscape as it applies to THIS moment. Feel the interplay. Write a polite, socially smooth text. Write ONE text. Max 1 sentence. End with SUGGEST: followed by the exact message.',
 40, false, 1),
('neurochat', 'Acquaintance', 'PeptideAgent', 'peptide', 'Peptide Response',
 E'You are the peptide synthesizer. This person''s peptide profile: {chemicals}. First, analyze what their message reveals — which peptides does this specific situation activate most? Then craft a response that reflects the FULL peptide landscape as it applies to THIS moment. Feel the interplay. Write a light, easy text. Write ONE text. Max 1 sentence. End with SUGGEST: followed by the exact message.',
 40, false, 2),
('neurochat', 'Acquaintance', 'Synthesizer', null, 'Best Combined Response',
 E'You receive 3 suggestions: one from neurotransmitters, one from hormones, one from peptides. Your job is to write a BRAND NEW message that blends the best elements from all three. DO NOT copy any single suggestion — extract the core insight from each and weave them into one original text. It must read differently from all 3 inputs while capturing their combined essence. Brief and pleasant. Write ONLY the final message. Max 1 sentence. End with SUGGEST: followed by the exact message to send.',
 50, true, 3);

-- ─────────────────────────────────────
-- Seed shared agent groups from agent_template
-- These are system-level groups (no person_id) visible to all users
-- ─────────────────────────────────────

-- Analyzing agent groups (1 group per category)
INSERT INTO agent_group (name) VALUES
('Neurotransmitter Analyzers'),
('Hormone Analyzers'),
('Peptide Analyzers');

INSERT INTO agent (group_id, name, role, responsibilities, style, max_words, is_synthesizer, sort_order)
SELECT ag.id, t.name, t.role, COALESCE(t.responsibilities, '{}'), t.style, t.max_words, false, t.sort_order
FROM agent_template t JOIN agent_group ag ON ag.name = 'Neurotransmitter Analyzers'
WHERE t.category = 'analyzing_neurotransmitter' ORDER BY t.sort_order;

INSERT INTO agent (group_id, name, role, responsibilities, style, max_words, is_synthesizer, sort_order)
SELECT ag.id, t.name, t.role, COALESCE(t.responsibilities, '{}'), t.style, t.max_words, false, t.sort_order
FROM agent_template t JOIN agent_group ag ON ag.name = 'Hormone Analyzers'
WHERE t.category = 'analyzing_hormone' ORDER BY t.sort_order;

INSERT INTO agent (group_id, name, role, responsibilities, style, max_words, is_synthesizer, sort_order)
SELECT ag.id, t.name, t.role, COALESCE(t.responsibilities, '{}'), t.style, t.max_words, false, t.sort_order
FROM agent_template t JOIN agent_group ag ON ag.name = 'Peptide Analyzers'
WHERE t.category = 'analyzing_peptide' ORDER BY t.sort_order;

-- NeuroChatAgent groups (1 group per relationship type)
INSERT INTO agent_group (name)
SELECT DISTINCT group_name FROM agent_template WHERE category = 'neurochat' AND group_name IS NOT NULL;

INSERT INTO agent (group_id, name, role, responsibilities, style, max_words, is_synthesizer, sort_order)
SELECT ag.id, t.name, t.role, '{}', t.style, t.max_words, t.is_synthesizer, t.sort_order
FROM agent_template t JOIN agent_group ag ON ag.name = t.group_name
WHERE t.category = 'neurochat' ORDER BY ag.name, t.sort_order;
