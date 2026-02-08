-- 002_vectorize_hormones_peptides.sql
-- Replaces hardcoded interaction table with vector-based hormone/peptide scoring.
-- Hormone/peptide descriptions get embedded as vectors; personality scan computes
-- relevance via cosine similarity against person's trait embeddings.

-- Add description and embedding columns to hormone table
ALTER TABLE hormone ADD COLUMN IF NOT EXISTS description TEXT;
ALTER TABLE hormone ADD COLUMN IF NOT EXISTS embedding vector(4096);

-- Add description and embedding columns to peptide table
ALTER TABLE peptide ADD COLUMN IF NOT EXISTS description TEXT;
ALTER TABLE peptide ADD COLUMN IF NOT EXISTS embedding vector(4096);

-- Seed hormone descriptions
UPDATE hormone SET description = 'Drive, dominance, competitiveness, risk-taking behavior, assertiveness, physical confidence, ambition, territorial instincts, desire for status and achievement, impulsive decision-making under challenge, leadership drive, boldness in social situations' WHERE name = 'Testosterone';
UPDATE hormone SET description = 'Emotional sensitivity, social bonding, empathy, verbal fluency, nurturing behavior, mood regulation, relationship orientation, emotional memory formation, aesthetic appreciation, cooperative social strategies, intuitive understanding of others' WHERE name = 'Estrogen';
UPDATE hormone SET description = 'Calming influence, anxiety reduction, nesting behavior, routine-seeking, protective instincts, maternal care patterns, sleep regulation, emotional stability during transitions, patience and tolerance, preference for safety and predictability' WHERE name = 'Progesterone';
UPDATE hormone SET description = 'Stress response, hypervigilance, worry patterns, threat detection, energy mobilization under pressure, rumination, perfectionism driven by anxiety, avoidance behavior, chronic tension and overthinking, heightened awareness of potential problems' WHERE name = 'Cortisol';
UPDATE hormone SET description = 'Fight-or-flight activation, thrill-seeking, acute stress performance, excitement under danger, physical readiness, panic responses, urgency-driven action, peak performance under pressure, rapid decision-making, love of intense experiences' WHERE name = 'Adrenaline';
UPDATE hormone SET description = 'Sleep-wake regulation, circadian rhythm sensitivity, seasonal mood changes, introspective tendencies during evening hours, dream vividness, sensitivity to light and environment, restorative withdrawal patterns, preference for quiet contemplation' WHERE name = 'Melatonin';
UPDATE hormone SET description = 'Metabolic energy regulation, mental processing speed, temperature sensitivity, weight and energy fluctuations, cognitive sharpness, mood stability tied to energy levels, motivation tied to physical vitality, sustained mental focus and alertness' WHERE name = 'Thyroid';

-- Seed peptide descriptions
UPDATE peptide SET description = 'Social bonding, trust formation, attachment behavior, physical touch affinity, generosity, in-group loyalty, empathy in close relationships, reduced social anxiety, pair bonding, parental attachment, warmth in intimate connections' WHERE name = 'Oxytocin';
UPDATE peptide SET description = 'Territorial behavior, mate guarding, social memory, aggression in defense of bonds, pair-bond maintenance, stress-mediated social behavior, vigilance toward social threats, loyalty and protectiveness, jealousy and possessiveness' WHERE name = 'Vasopressin';
UPDATE peptide SET description = 'Pain modulation, euphoria from physical exertion, reward from laughter and social connection, stress-buffering, resilience through physical activity, pleasure from music and creativity, natural high from achievement and exercise' WHERE name = 'Endorphins';
UPDATE peptide SET description = 'Pain suppression, comfort-seeking behavior, emotional numbing under trauma, soothing response to familiar environments, preference for routine over novelty as coping mechanism, withdrawal into safe spaces when overwhelmed' WHERE name = 'Enkephalins';
UPDATE peptide SET description = 'Pain signaling and sensitivity, emotional distress amplification, inflammatory stress responses, sensitivity to physical discomfort, heightened pain awareness, stress-related somatic complaints, emotional pain manifesting physically' WHERE name = 'Substance P';
UPDATE peptide SET description = 'Appetite regulation, stress resilience, anxiety reduction, energy homeostasis, calm under pressure, feeding behavior patterns, emotional eating, ability to stay composed during high-stress situations, mental toughness' WHERE name = 'NPY';
UPDATE peptide SET description = 'Stress axis activation, anxiety initiation, fear responses, HPA axis triggering, depression-related patterns, appetite suppression under stress, sleep disruption from worry, anticipatory anxiety, catastrophic thinking patterns' WHERE name = 'CRH';

-- Drop the hardcoded interaction table (no longer needed)
DROP TABLE IF EXISTS interaction;
