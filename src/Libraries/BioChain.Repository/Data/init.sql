-- ═══════════════════════════════════════════════════════════
-- init.sql — Combined schema entry point
-- Loaded by docker-compose on first run.
-- ═══════════════════════════════════════════════════════════

\i /docker-entrypoint-initdb.d/biochain_init.sql
\i /docker-entrypoint-initdb.d/init_core.sql
\i /docker-entrypoint-initdb.d/views.sql
