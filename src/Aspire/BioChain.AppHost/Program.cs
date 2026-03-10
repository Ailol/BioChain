var builder = DistributedApplication.CreateBuilder(args);


// PostgreSQL with pgvector for biochain storage (persistent)
var pgPassword = builder.AddParameter("postgres-password", secret: true);
var postgres = builder.AddPostgres("postgres", port: 5434, password: pgPassword)
    .WithEnvironment("POSTGRES_DB", "biochain")
    .WithDataVolume("pgdata-v6")
    .WithBindMount("../../BioChain.Repository/Data/init/biochain_init.sql", "/docker-entrypoint-initdb.d/01-biochain-init.sql")
    .WithBindMount("../../BioChain.Repository/Data/init/views.sql", "/docker-entrypoint-initdb.d/02-views.sql")
    .WithBindMount("../../BioChain.Repository/Data/init/biochain_graph.sql", "/docker-entrypoint-initdb.d/03-graph.sql")
    .WithBindMount("../../BioChain.Repository/Data/init/biochain_functions.sql", "/docker-entrypoint-initdb.d/04-functions.sql")
    .WithBindMount("../../BioChain.Repository/Data/init/init_core.sql", "/docker-entrypoint-initdb.d/05-init-core.sql")
    .WithBindMount("../../BioChain.Repository/Data/seed/seed-questionnaire.sql", "/docker-entrypoint-initdb.d/06-seed-questionnaire.sql")
    .WithImageRegistry("docker.io")
    .WithImage("pgvector/pgvector")
    .WithImageTag("pg16")
    .WithLifetime(ContainerLifetime.Persistent);

var biochainDb = postgres.AddDatabase("biochain");

// MCP Agent Server — LLM config comes from Server's own appsettings + user-secrets
var mcpServer = builder.AddProject<Projects.BioChain_Server>("mcp-server")
    .WithEndpoint("http", e => e.Port = 13370)
    .WithReference(biochainDb);

// React frontend (Aspire-managed Vite dev server)
builder.AddViteApp("biochain-app", "../../BioChain.App", "dev")
    .WithEndpoint("http", e => e.Port = 5173)
    .WithReference(mcpServer)
    .WaitFor(mcpServer);

builder.Build().Run();
