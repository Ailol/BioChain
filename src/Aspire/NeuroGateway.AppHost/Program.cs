// Load ../.env into process environment (same file docker-compose uses)
var envFile = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", ".env");
if (File.Exists(envFile))
{
    foreach (var line in File.ReadAllLines(envFile))
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;
        var idx = trimmed.IndexOf('=');
        if (idx <= 0) continue;
        var key = trimmed[..idx].Trim();
        var val = trimmed[(idx + 1)..].Trim();
        if (Environment.GetEnvironmentVariable(key) is null)
            Environment.SetEnvironmentVariable(key, val);
    }
}

var builder = DistributedApplication.CreateBuilder(args);

// Keycloak identity server (persistent — survives Aspire restarts, ~30s faster)
var keycloak = builder.AddKeycloak("keycloak")
    .WithEndpoint("http", e => e.Port = 8080)
    .WithRealmImport("./Realms")
    .WithLifetime(ContainerLifetime.Persistent);

// PostgreSQL with pgvector for personality storage (persistent)
var pgPassword = builder.AddParameter("pg-password", secret: true);
var postgres = builder.AddPostgres("postgres", port: 5434, password: pgPassword)
    .WithEnvironment("POSTGRES_DB", "personality")
    .WithDataVolume("pgdata-v6")
    .WithBindMount("../../Libraries/NeuroGateway.Repository/Data/init.sql", "/docker-entrypoint-initdb.d/01-init.sql")
    .WithBindMount("../../Libraries/NeuroGateway.Repository/Data/seed-core.sql", "/docker-entrypoint-initdb.d/02-seed-core.sql")
    .WithBindMount("../../Libraries/NeuroGateway.Repository/Data/seed-agents.sql", "/docker-entrypoint-initdb.d/03-seed-agents.sql")
    .WithBindMount("../../Libraries/NeuroGateway.Repository/Data/seed-chemicals.sql", "/docker-entrypoint-initdb.d/04-seed-chemicals.sql")
    .WithBindMount("../../Libraries/NeuroGateway.Repository/Data/seed-questionnaire.sql", "/docker-entrypoint-initdb.d/05-seed-questionnaire.sql")
    .WithImageRegistry("docker.io")
    .WithImage("pgvector/pgvector")
    .WithImageTag("pg16")
    .WithLifetime(ContainerLifetime.Persistent);

var personalityDb = postgres.AddDatabase("personality");

// MCP Agent Server — wait for DB and Keycloak to be healthy before starting
var mcpServer = builder.AddProject<Projects.NeuroGateway_Server>("mcp-server")
    .WithEndpoint("http", e => e.Port = 13370)
    .WithReference(personalityDb)
    .WithReference(keycloak)
    .WithEnvironment("Llm__Orchestrator__ApiKey", Environment.GetEnvironmentVariable("Llm__Orchestrator__ApiKey") ?? "")
    .WithEnvironment("Llm__AgentAnalyzing__Endpoint", "http://host.docker.internal:7000")
    .WithEnvironment("Llm__AgentAnalyzing__Model", "med-4b-finetuned")
    .WithEnvironment("Llm__AgentLayer__Endpoint", "http://host.docker.internal:7000")
    .WithEnvironment("Llm__AgentLayer__Model", "qwen3-4b-4bit+layer")
    .WithEnvironment("Llm__Embedding__ApiKey", Environment.GetEnvironmentVariable("Llm__Embedding__ApiKey") ?? "")
    .WithEnvironment("Llm__Embedding__Model", "text-embedding-3-small");

// React frontend (Aspire-managed Vite dev server)
builder.AddViteApp("neuroreact", "../../NeuroGateway.App", "dev")
    .WithEndpoint("http", e => e.Port = 5173)
    .WithReference(mcpServer)
    .WithReference(keycloak)
    .WaitFor(mcpServer);

builder.Build().Run();
