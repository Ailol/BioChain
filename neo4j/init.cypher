// ═══════════════════════════════════════════════════════════
// Neo4j Schema Init — BioChain Graph Sync
// Run once via: cypher-shell -u neo4j -p biochain_graph < init.cypher
// Or mount as /var/lib/neo4j/import/init.cypher and run via APOC
// ═══════════════════════════════════════════════════════════

// Uniqueness constraints — (person_id, code) must be unique per label
CREATE CONSTRAINT IF NOT EXISTS FOR (n:Signal) REQUIRE (n.person_id, n.code) IS UNIQUE;
CREATE CONSTRAINT IF NOT EXISTS FOR (n:Receptor) REQUIRE (n.person_id, n.code) IS UNIQUE;
CREATE CONSTRAINT IF NOT EXISTS FOR (n:Transporter) REQUIRE (n.person_id, n.code) IS UNIQUE;
CREATE CONSTRAINT IF NOT EXISTS FOR (n:Gate) REQUIRE (n.person_id, n.code) IS UNIQUE;
CREATE CONSTRAINT IF NOT EXISTS FOR (n:Limiter) REQUIRE (n.person_id, n.code) IS UNIQUE;
CREATE CONSTRAINT IF NOT EXISTS FOR (n:Interface) REQUIRE (n.person_id, n.code) IS UNIQUE;
CREATE CONSTRAINT IF NOT EXISTS FOR (n:Region) REQUIRE (n.person_id, n.code) IS UNIQUE;

// Person_id indexes — DELETE filters on person_id alone (constraints cover person_id+code)
CREATE INDEX IF NOT EXISTS FOR (n:Signal) ON (n.person_id);
CREATE INDEX IF NOT EXISTS FOR (n:Receptor) ON (n.person_id);
CREATE INDEX IF NOT EXISTS FOR (n:Transporter) ON (n.person_id);
CREATE INDEX IF NOT EXISTS FOR (n:Gate) ON (n.person_id);
CREATE INDEX IF NOT EXISTS FOR (n:Limiter) ON (n.person_id);
CREATE INDEX IF NOT EXISTS FOR (n:Interface) ON (n.person_id);
CREATE INDEX IF NOT EXISTS FOR (n:Region) ON (n.person_id);
