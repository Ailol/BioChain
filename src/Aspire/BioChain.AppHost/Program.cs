var builder = DistributedApplication.CreateBuilder(args);

// BioChain API — wraps LLM + SpacetimeDB typed client
var api = builder.AddProject<Projects.BioChain_Api>("biochain-api")
    .WithEndpoint("http", e => e.Port = 5100);

builder.Build().Run();
