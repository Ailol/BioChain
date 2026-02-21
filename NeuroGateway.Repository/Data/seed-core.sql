-- seed-core.sql
-- Core seed data: relationship types, default person.
-- Idempotent (ON CONFLICT DO NOTHING).

-- ─────────────────────────────────────
-- Relationship Types
-- ─────────────────────────────────────

INSERT INTO relationship_type (name, description) VALUES
    ('partner',      'Committed romantic partner'),
    ('dating',       'Early or casual romantic connection'),
    ('ex',           'Former romantic partner or spouse'),
    ('family',       'Family and kinship bonds'),
    ('friend',       'Friendship and close social bonds'),
    ('coworker',     'Professional workplace relationship'),
    ('mentor',       'Mentoring or coaching relationship'),
    ('acquaintance', 'Casual or distant social connection'),
    ('coparent',     'Shared parenting after separation'),
    ('therapist',    'Therapeutic or counseling relationship')
ON CONFLICT (name) DO NOTHING;

-- ─────────────────────────────────────
-- Default Person
-- ─────────────────────────────────────

INSERT INTO person (owner_id, first_name) VALUES ('seed-default', 'Ailo')
ON CONFLICT DO NOTHING;

INSERT INTO personality (person_id)
    SELECT p.id FROM person p WHERE p.first_name = 'Ailo'
    AND NOT EXISTS (SELECT 1 FROM personality WHERE person_id = p.id);

INSERT INTO analyzed_data (person_id, content, source_type)
    SELECT p.id, 'Programming: Flow states and problem-solving trigger dopamine reward loops.', 'manual'
    FROM person p WHERE p.first_name = 'Ailo'
    AND NOT EXISTS (
        SELECT 1 FROM analyzed_data ad
        WHERE ad.person_id = p.id AND ad.content LIKE 'Programming:%'
    );

INSERT INTO chemical_observation (personality_id, analyzed_data_id, chemical, reasoning, intensity_factor)
    SELECT per.id, ad.id, 'dopamine',
           'Dopamine at +0.16. Reward anticipation activated through sustained mesolimbic drive during problem-solving flow states. Novelty in debugging triggers phasic VTA firing.',
           0.16
    FROM personality per
    JOIN person p ON p.id = per.person_id
    JOIN analyzed_data ad ON ad.person_id = p.id AND ad.content LIKE 'Programming:%'
    WHERE p.first_name = 'Ailo'
    AND NOT EXISTS (
        SELECT 1 FROM chemical_observation co
        WHERE co.personality_id = per.id AND co.analyzed_data_id = ad.id
    );

-- ─────────────────────────────────────
-- Default dev-user role (admin)
-- ─────────────────────────────────────

INSERT INTO user_role (user_id, email, role) VALUES
    ('dev-user', 'dev@ailo.no', 'admin')
ON CONFLICT (user_id, role) DO NOTHING;
