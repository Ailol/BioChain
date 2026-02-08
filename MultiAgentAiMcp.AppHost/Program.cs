var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL with pgvector for personality storage
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("pgdata")
    .WithBindMount("../init.sql", "/docker-entrypoint-initdb.d/init.sql")
    .WithImageRegistry("docker.io")
    .WithImage("pgvector/pgvector")
    .WithImageTag("pg16");

var personalityDb = postgres.AddDatabase("personality");

// Use local Ollama running on host (not containerized) - has all models already
// MCP Agent Server - the main service
// Bind to all interfaces on port 13370 directly (no proxy) for Tailscale access
var mcpServer = builder.AddProject<Projects.McpServer>("mcp-server", launchProfileName: null)
    .WithReference(personalityDb)
    .WithEnvironment("Llm__ChatEndpoint", "http://localhost:11434")
    .WithEnvironment("ASPNETCORE_URLS", "http://0.0.0.0:13370");



builder.Build().Run();
