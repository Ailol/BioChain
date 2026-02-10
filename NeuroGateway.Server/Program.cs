using System.ClientModel;
using Anthropic;
using NeuroGateway.AgentFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using NeuroGateway.Models;
using OllamaSharp;
using OpenAI;
using NeuroGateway.Repository;
using NeuroGateway.Service;

// Check for stdio mode (backward compatibility with Claude Desktop direct connection)
if (args.Contains("--stdio"))
{
    await RunStdioMode(args);
}
else
{
    await RunHttpMode(args);
}

static AgentConfiguration CreateConfiguration(IConfiguration appConfig)
{
    var llm = appConfig.GetSection("Llm");
    var environment = Environment.GetEnvironmentVariable("ENVIRONMENT") ?? "Development";
    var dbEnvVar = environment.Equals("Production", StringComparison.OrdinalIgnoreCase)
        ? "PRODUCTION_DB" : "DEVELOPMENT_DB";

    var personalityDb = Environment.GetEnvironmentVariable(dbEnvVar)
        ?? Environment.GetEnvironmentVariable("PERSONALITY_DB") // backward compat
        ?? appConfig.GetConnectionString("Personality")
        ?? throw new InvalidOperationException($"{dbEnvVar} env var is required (ENVIRONMENT={environment})");

    Console.WriteLine($"Environment: {environment} → DB from {dbEnvVar}");

    var config = llm.Get<AgentConfiguration>() ?? new AgentConfiguration { PersonalityDb = personalityDb };
    config.PersonalityDb = personalityDb;
    config.Validate();

    return config;
}

// ── Provider factories ──────────────────────────────────────────────────────

static Uri EnsureV1Path(Uri uri) =>
    uri.AbsolutePath.Contains("/v1") ? uri : new Uri(uri, "v1/");

static IChatClient CreateChatClient(LlmProviderConfig cfg)
{
    return cfg.ResolvedBackend switch
    {
        "Anthropic" => new AnthropicClient { ApiKey = cfg.ApiKey! }
            .AsIChatClient(cfg.Model),

        "OpenAI" => new OpenAIClient(cfg.ApiKey!)
            .GetChatClient(cfg.Model).AsIChatClient(),

        "Ollama" => new OllamaApiClient(
            new HttpClient { BaseAddress = new Uri(cfg.Endpoint!), Timeout = TimeSpan.FromMinutes(10) },
            cfg.Model),

        _ => new OpenAIClient( // OpenAiCompatible — vLLM, RunPod, etc.
                new ApiKeyCredential(cfg.ApiKey ?? "unused"),
                new OpenAIClientOptions { Endpoint = EnsureV1Path(new Uri(cfg.Endpoint!)) })
            .GetChatClient(cfg.Model).AsIChatClient()
    };
}

static IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(LlmProviderConfig cfg)
{
    return cfg.ResolvedBackend switch
    {
        "OpenAI" => new OpenAIClient(cfg.ApiKey!)
            .GetEmbeddingClient(cfg.Model).AsIEmbeddingGenerator(),

        "Ollama" => new OllamaApiClient(
            new HttpClient { BaseAddress = new Uri(cfg.Endpoint!), Timeout = TimeSpan.FromMinutes(10) },
            cfg.Model),

        _ => new OpenAIClient( // OpenAiCompatible — vLLM, RunPod, etc.
                new ApiKeyCredential(cfg.ApiKey ?? "unused"),
                new OpenAIClientOptions { Endpoint = EnsureV1Path(new Uri(cfg.Endpoint!)) })
            .GetEmbeddingClient(cfg.Model).AsIEmbeddingGenerator()
    };
}

static bool SameProvider(LlmProviderConfig a, LlmProviderConfig b) =>
    a.ResolvedBackend == b.ResolvedBackend
    && string.Equals(a.Endpoint, b.Endpoint, StringComparison.OrdinalIgnoreCase);

// ── Registration ─────────────────────────────────────────────────────────────

static void RegisterLlmServices(IServiceCollection services, AgentConfiguration config)
{
    var agentClient = CreateChatClient(config.AgentFramework!);
    var orchestratorClient = SameProvider(config.Orchestrator!, config.AgentFramework!)
        ? agentClient
        : CreateChatClient(config.Orchestrator!);
    var embedGen = CreateEmbeddingGenerator(config.Embedding!);

    services.AddSingleton(new LlmService(agentClient, orchestratorClient, embedGen, config));
}

static void LogLlmConfig(AgentConfiguration config)
{
    static string Fmt(LlmProviderConfig c) =>
        $"{c.ResolvedBackend,-16} @ {c.Endpoint ?? "(default)",-50} → {c.Model}";

    var shared = SameProvider(config.Orchestrator!, config.AgentFramework!)
        ? " (shared client)" : "";

    Console.WriteLine("[LLM Config]");
    Console.WriteLine($"  Orchestrator:    {Fmt(config.Orchestrator!)}{shared}");
    Console.WriteLine($"  AgentFramework:  {Fmt(config.AgentFramework!)}");
    Console.WriteLine($"  Embedding:       {Fmt(config.Embedding!)}");
    Console.WriteLine($"  Max Parallel:    {config.MaxParallelAgents}");
}

static void RegisterServices(IServiceCollection services, AgentConfiguration config)
{
    services.AddSingleton(config);
    services.AddPooledDbContextFactory<PersonalityDbContext>(options =>
        options.UseNpgsql(config.PersonalityDb, npgsql => npgsql.UseVector())
               .UseSnakeCaseNamingConvention());
    // Repositories
    services.AddSingleton<PersonRepository>();
    services.AddSingleton<PersonalityRepository>();
    services.AddSingleton<EmbeddingRepository>();
    services.AddSingleton<AnalyzedDataRepository>();
    services.AddSingleton<AgentGroupRepository>();
    services.AddSingleton<AgentTemplateRepository>();
    services.AddSingleton<RelationshipRepository>();
    services.AddSingleton<ProfileRepository>();
    // LLM + AgentFramework
    RegisterLlmServices(services, config);
    services.AddSingleton<NeuroGateway.AgentFramework.Analyze>();
    services.AddSingleton<NeuroGateway.AgentFramework.Layer>();
    services.AddSingleton<NeuroGateway.AgentFramework.Pipeline>();
    services.AddSingleton<NeuroGateway.AgentFramework.Agents>();
    services.AddSingleton<NeuroGateway.AgentFramework.Chat>();
    services.AddSingleton<NeuroGateway.AgentFramework.BackgroundEmbeddingQueue>();
    services.AddSingleton<NeuroGateway.AgentFramework.BackgroundEnrichmentQueue>();
    services.AddHostedService<NeuroGateway.AgentFramework.EmbeddingQueueProcessor>();
    services.AddHostedService<NeuroGateway.Service.EnrichmentQueueProcessor>();
    // Services
    services.AddSingleton<NeuroGateway.Service.EmbeddingService>();
    services.AddSingleton<NeuroGateway.Service.VectorService>();
    // AnalyseService ↔ PersonalityService circular dependency: wire via SetXxx
    services.AddSingleton<NeuroGateway.Service.AnalyseService>();
    services.AddSingleton<NeuroGateway.Service.PersonalityService>(sp =>
    {
        var svc = new NeuroGateway.Service.PersonalityService(
            sp.GetRequiredService<LlmService>(),
            sp.GetRequiredService<PersonRepository>(),
            sp.GetRequiredService<PersonalityRepository>(),
            sp.GetRequiredService<ProfileRepository>(),
            sp.GetRequiredService<AnalyzedDataRepository>(),
            sp.GetRequiredService<NeuroGateway.Service.VectorService>(),
            sp.GetRequiredService<NeuroGateway.AgentFramework.BackgroundEmbeddingQueue>());
        var analyseService = sp.GetRequiredService<NeuroGateway.Service.AnalyseService>();
        svc.SetAnalyseService(analyseService);
        analyseService.SetPersonalityService(svc);
        return svc;
    });
    services.AddSingleton<NeuroGateway.Service.AgentService>();
    services.AddSingleton<NeuroGateway.AgentFramework.ContextEmbeddingCache>();
    services.AddSingleton<NeuroGateway.Service.ProfileScoringService>();
    services.AddSingleton<NeuroGateway.Service.NeuroService>();
    services.AddSingleton<NeuroGateway.Service.AgentTemplateSeedService>();
}

static async Task RunHttpMode(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);
    builder.AddServiceDefaults();

    var config = CreateConfiguration(builder.Configuration);
    LogLlmConfig(config);
    RegisterServices(builder.Services, config);

    builder.Services
        .AddMcpServer()
        .WithHttpTransport(options => { options.Stateless = true; })
        .WithToolsFromAssembly();

    var app = builder.Build();
    app.MapDefaultEndpoints();
    app.MapMcp("/mcp");

    // Seed agent templates from YAML on startup
    var seedService = app.Services.GetRequiredService<NeuroGateway.Service.AgentTemplateSeedService>();
    await seedService.SeedAsync();

    // Load relationship embeddings for context-aware scoring
    var contextCache = app.Services.GetRequiredService<NeuroGateway.AgentFramework.ContextEmbeddingCache>();
    var llmService = app.Services.GetRequiredService<LlmService>();
    var relPath = Path.Combine(AppContext.BaseDirectory, "AgentTemplates", "RelationshipEmbeddings");
    await contextCache.LoadDimensionAsync("relationship", relPath, llmService);

    var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://localhost:18080";
    Console.WriteLine($"MCP Server running in HTTP mode on {urls} at /mcp");

    await app.RunAsync();
}

static async Task RunStdioMode(string[] args)
{
    using var host = Host.CreateDefaultBuilder(args)
        .ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole(options => { options.LogToStandardErrorThreshold = LogLevel.Trace; });
        })
        .ConfigureServices((hostContext, services) =>
        {
            var config = CreateConfiguration(hostContext.Configuration);
            LogLlmConfig(config);
            RegisterServices(services, config);

            services
                .AddMcpServer()
                .WithStdioServerTransport()
                .WithToolsFromAssembly();
        })
        .Build();

    // Seed agent templates from YAML on startup
    var seedService = host.Services.GetRequiredService<NeuroGateway.Service.AgentTemplateSeedService>();
    await seedService.SeedAsync();

    // Load relationship embeddings for context-aware scoring
    var contextCache = host.Services.GetRequiredService<NeuroGateway.AgentFramework.ContextEmbeddingCache>();
    var llmService = host.Services.GetRequiredService<LlmService>();
    var relPath = Path.Combine(AppContext.BaseDirectory, "AgentTemplates", "RelationshipEmbeddings");
    await contextCache.LoadDimensionAsync("relationship", relPath, llmService);

    await host.RunAsync();
}
