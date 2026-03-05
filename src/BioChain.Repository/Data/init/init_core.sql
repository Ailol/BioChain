-- ═══════════════════════════════════════════════════════════
-- core_init.sql
-- Application layer. Auth + data collection.
-- Depends on: biochain_init.sql (entity table)
-- 5 tables.
-- ═══════════════════════════════════════════════════════════


-- ═══════════════════════════════════════════════════════════
-- AUTH
-- ═══════════════════════════════════════════════════════════

CREATE TABLE IF NOT EXISTS person_share (
    id                  SERIAL PRIMARY KEY,
    person_id           UUID NOT NULL REFERENCES entity(id) ON DELETE CASCADE,
    shared_with_email   TEXT NOT NULL,
    shared_with_user_id TEXT,
    shared_by_user_id   TEXT NOT NULL,
    created_at          TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE (person_id, shared_with_email)
);

CREATE INDEX IF NOT EXISTS idx_share_user  ON person_share(shared_with_user_id);
CREATE INDEX IF NOT EXISTS idx_share_email ON person_share(shared_with_email);


CREATE TABLE IF NOT EXISTS user_role (
    id          SERIAL PRIMARY KEY,
    user_id     TEXT NOT NULL,
    email       TEXT,
    role        VARCHAR(20) NOT NULL,
    is_active   BOOLEAN NOT NULL DEFAULT TRUE,
    created_at  TIMESTAMPTZ DEFAULT NOW(),
    updated_at  TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE (user_id, role)
);

CREATE INDEX IF NOT EXISTS idx_role_user ON user_role(user_id);


-- ═══════════════════════════════════════════════════════════
-- QUESTIONNAIRE
-- Items are static. Sessions link to entity.
-- Answers flow into stimuli table as kind='questionnaire'.
-- ═══════════════════════════════════════════════════════════

CREATE TABLE IF NOT EXISTS questionnaire_item (
    id              SERIAL PRIMARY KEY,
    sort_order      INT NOT NULL,
    scenario        TEXT NOT NULL,
    label           CHAR(1) NOT NULL,
    option_text     TEXT NOT NULL,
    primary_signal  VARCHAR(30) NOT NULL DEFAULT '',
    secondary_signal VARCHAR(30),
    is_inverted     BOOLEAN NOT NULL DEFAULT FALSE,
    data            JSONB DEFAULT '{}',
    created_at      TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE (sort_order, label)
);

CREATE INDEX IF NOT EXISTS idx_qi_sort ON questionnaire_item(sort_order);


CREATE TABLE IF NOT EXISTS questionnaire (
    id          UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    person_id   UUID NOT NULL REFERENCES entity(id) ON DELETE CASCADE,
    token       VARCHAR(64) NOT NULL UNIQUE,
    status      VARCHAR(20) NOT NULL DEFAULT 'pending',
    data        JSONB DEFAULT '{}',                  -- {domain, config}
    created_at  TIMESTAMPTZ DEFAULT NOW(),
    completed_at TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_quest_person ON questionnaire(person_id);
CREATE INDEX IF NOT EXISTS idx_quest_token  ON questionnaire(token);


CREATE TABLE IF NOT EXISTS questionnaire_answer (
    id                  SERIAL PRIMARY KEY,
    questionnaire_id    UUID NOT NULL REFERENCES questionnaire(id) ON DELETE CASCADE,
    item_id             INT NOT NULL REFERENCES questionnaire_item(id),
    created_at          TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE (questionnaire_id, item_id)
);

CREATE INDEX IF NOT EXISTS idx_qa_quest ON questionnaire_answer(questionnaire_id);
