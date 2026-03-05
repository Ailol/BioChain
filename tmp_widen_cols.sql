-- Widen tight VARCHAR columns to prevent LLM-generated values from truncating.
-- Must drop dependent views first, then recreate.

BEGIN;

-- Drop materialized view first (depends on v_system)
DROP MATERIALIZED VIEW IF EXISTS v_node CASCADE;

-- Drop v_system (depends on all current views)
DROP VIEW IF EXISTS v_system CASCADE;

-- Drop all current views
DROP VIEW IF EXISTS v_signal_current CASCADE;
DROP VIEW IF EXISTS v_receptor_current CASCADE;
DROP VIEW IF EXISTS v_transporter_current CASCADE;
DROP VIEW IF EXISTS v_gate_current CASCADE;
DROP VIEW IF EXISTS v_limiter_current CASCADE;
DROP VIEW IF EXISTS v_interface_current CASCADE;
DROP VIEW IF EXISTS v_region_current CASCADE;
DROP VIEW IF EXISTS v_region_traffic CASCADE;

-- Now widen the columns
ALTER TABLE signal ALTER COLUMN type TYPE VARCHAR(30);
ALTER TABLE signal ALTER COLUMN state TYPE VARCHAR(20);

ALTER TABLE receptor ALTER COLUMN state TYPE VARCHAR(20);

ALTER TABLE transporter ALTER COLUMN state TYPE VARCHAR(20);
ALTER TABLE transporter ALTER COLUMN clearance TYPE VARCHAR(10);

ALTER TABLE limiter ALTER COLUMN activity TYPE VARCHAR(20);

ALTER TABLE gate ALTER COLUMN threshold TYPE VARCHAR(10);

ALTER TABLE edge ALTER COLUMN transfer_fn TYPE VARCHAR(20);

ALTER TABLE region ALTER COLUMN stress_load TYPE VARCHAR(10);

COMMIT;
