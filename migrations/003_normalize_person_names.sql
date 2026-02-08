-- 003_normalize_person_names.sql
-- Normalize all person names to lowercase and add case-insensitive unique index.

-- Lowercase all person names
UPDATE person SET name = LOWER(name) WHERE name != LOWER(name);

-- Drop old case-sensitive unique constraint, add case-insensitive index
ALTER TABLE person DROP CONSTRAINT IF EXISTS person_name_key;
CREATE UNIQUE INDEX IF NOT EXISTS idx_person_name_lower ON person (LOWER(name));
