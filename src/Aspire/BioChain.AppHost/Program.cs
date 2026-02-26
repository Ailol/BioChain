var builder = DistributedApplication.CreateBuilder(args);

// Keycloak identity server (persistent — survives Aspire restarts, ~30s faster)
var keycloak = builder.AddKeycloak("keycloak")
    .WithEndpoint("http", e => e.Port = 8080)
    .WithRealmImport("./Realms")
    .WithLifetime(ContainerLifetime.Persistent);

// PostgreSQL with pgvector for personality storage (persistent)
var pgPassword = builder.AddParameter("postgres-password", secret: true);
var postgres = builder.AddPostgres("postgres", port: 5434, password: pgPassword)
    .WithEnvironment("POSTGRES_DB", "personality")
    .WithDataVolume("pgdata-v6")
    .WithBindMount("../../Libraries/BioChain.Repository/Data/init.sql", "/docker-entrypoint-initdb.d/01-init.sql")
    .WithBindMount("../../Libraries/BioChain.Repository/Data/seed-core.sql", "/docker-entrypoint-initdb.d/02-seed-core.sql")
    .WithBindMount("../../Libraries/BioChain.Repository/Data/seed-agents.sql", "/docker-entrypoint-initdb.d/03-seed-agents.sql")
    .WithBindMount("../../Libraries/BioChain.Repository/Data/seed-chemicals.sql", "/docker-entrypoint-initdb.d/04-seed-chemicals.sql")
    .WithBindMount("../../Libraries/BioChain.Repository/Data/seed-questionnaire.sql", "/docker-entrypoint-initdb.d/05-seed-questionnaire.sql")
    .WithImageRegistry("docker.io")
    .WithImage("pgvector/pgvector")
    .WithImageTag("pg16")
    .WithLifetime(ContainerLifetime.Persistent);

var personalityDb = postgres.AddDatabase("personality");

// MCP Agent Server — LLM config comes from Server's own appsettings + user-secrets
var mcpServer = builder.AddProject<Projects.BioChain_Server>("mcp-server")
    .WithEndpoint("http", e => e.Port = 13370)
    .WithReference(personalityDb)
    .WithReference(keycloak);

// React frontend (Aspire-managed Vite dev server)
builder.AddViteApp("biochain-app", "../../BioChain.App", "dev")
    .WithEndpoint("http", e => e.Port = 5173)
    .WithReference(mcpServer)
    .WithReference(keycloak)
    .WaitFor(mcpServer);

builder.Build().Run();
