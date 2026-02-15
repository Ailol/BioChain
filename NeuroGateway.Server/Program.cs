using System.ClientModel;
using Anthropic;
using Microsoft.EntityFrameworkCore;
// ReSharper disable AccessToDisposedClosure
using Microsoft.Extensions.AI;
using NeuroGateway.AgentFramework;
using NeuroGateway.Models;
using NeuroGateway.Repository;
using NeuroGateway.Server.Api;
using NeuroGateway.Service;
using OllamaSharp;
using OpenAI;

if (args.Contains("--stdio"))
    await RunStdioMode(args);
else
    await RunHttpMode(args);

// ── Configuration ────────────────────────────────────────────────────────────

static (AgentConfiguration Llm, string Db) LoadConfig(IConfiguration appConfig)
{
    var environment = Environment.GetEnvironmentVariable("ENVIRONMENT") ?? "Development";
    var dbEnvVar = environment.Equals("Production", StringComparison.OrdinalIgnoreCase)
        ? "PRODUCTION_DB" : "DEVELOPMENT_DB";

    var db = Environment.GetEnvironmentVariable(dbEnvVar)
        ?? Environment.GetEnvironmentVariable("PERSONALITY_DB")
        ?? appConfig.GetConnectionString("Personality")
        ?? throw new InvalidOperationException($"{dbEnvVar} env var is required (ENVIRONMENT={environment})");

    Console.WriteLine($"Environment: {environment} → DB: {db[..Math.Min(60, db.Length)]}...");

    var llm = appConfig.GetSection("Llm").Get<AgentConfiguration>() ?? new AgentConfiguration();
    llm.Validate();

    return (llm, db);
}

// ── Provider factories ──────────────────────────────────────────────────────

static Uri EnsureV1Path(Uri uri) =>
    uri.AbsolutePath.Contains("/v1") ? uri : new Uri(uri, "v1/");

static IChatClient CreateChatClient(LlmProviderConfig cfg) =>
    cfg.ResolvedBackend switch
    {
        "Anthropic" => new AnthropicClient { ApiKey = cfg.ApiKey! }
            .AsIChatClient(cfg.Model),

        "OpenAI" => new OpenAIClient(cfg.ApiKey!)
            .GetChatClient(cfg.Model).AsIChatClient(),

        "Ollama" => new OllamaApiClient(
            new HttpClient { BaseAddress = new Uri(cfg.Endpoint!), Timeout = TimeSpan.FromMinutes(10) },
            cfg.Model),

        _ => new OpenAIClient(
                new ApiKeyCredential(cfg.ApiKey ?? "unused"),
                new OpenAIClientOptions { Endpoint = EnsureV1Path(new Uri(cfg.Endpoint!)) })
            .GetChatClient(cfg.Model).AsIChatClient()
    };

static IEmbeddingGenerator<string, Embedding<float>>? CreateEmbeddingGenerator(LlmProviderConfig? cfg)
{
    if (cfg is null) return null;
    return cfg.ResolvedBackend switch
    {
        "OpenAI" => new OpenAIClient(cfg.ApiKey!)
            .GetEmbeddingClient(cfg.Model).AsIEmbeddingGenerator(),

        "Ollama" => new OllamaApiClient(
            new HttpClient { BaseAddress = new Uri(cfg.Endpoint!), Timeout = TimeSpan.FromMinutes(10) },
            cfg.Model),

        _ => new OpenAIClient(
                new ApiKeyCredential(cfg.ApiKey ?? "unused"),
                new OpenAIClientOptions { Endpoint = EnsureV1Path(new Uri(cfg.Endpoint!)) })
            .GetEmbeddingClient(cfg.Model).AsIEmbeddingGenerator()
    };
}

// ── Registration ─────────────────────────────────────────────────────────────

static void RegisterAll(IServiceCollection services, AgentConfiguration llm, string db)
{
    services.AddSingleton(llm);

    // EF + Npgsql
    services.AddPooledDbContextFactory<PersonalityDbContext>(options =>
        options.UseNpgsql(db, npgsql => npgsql.UseVector())
               .UseSnakeCaseNamingConvention());

    // Repositories
    services.AddSingleton<PersonRepository>();
    services.AddSingleton<PersonalityRepository>();
    services.AddSingleton<AnalyzedDataRepository>();
    services.AddSingleton<AgentGroupRepository>();
    services.AddSingleton<AgentTemplateRepository>();
    services.AddSingleton<RelationshipRepository>();
    services.AddSingleton<ProfileRepository>();

    // ChatClient for analyzing agents (AgentAnalyzing — neuro LoRA, SKIP/ADD)
    var analyzingChatClient = new ChatClient(CreateChatClient(llm.AgentAnalyzing!));
    services.AddSingleton(analyzingChatClient);

    // ChatClient for reasoning synthesis (AgentReasoning — falls back to analyzing if not configured)
    var reasoningChatClient = llm.AgentReasoning is not null
        ? new ChatClient(CreateChatClient(llm.AgentReasoning))
        : analyzingChatClient;

    // ChatClient for layer agents (AgentLayer — falls back to reasoning if not configured)
    var layerChatClient = llm.AgentLayer is not null
        ? new ChatClient(CreateChatClient(llm.AgentLayer))
        : reasoningChatClient;

    // Embedding generator (optional — null if not configured)
    var embedGen = CreateEmbeddingGenerator(llm.Embedding);
    if (embedGen is not null)
    {
        services.AddSingleton(embedGen);
        services.AddSingleton<EmbeddingService>();
        services.AddSingleton<DimensionService>();
    }

    // Services
    services.AddSingleton<PersonService>();
    services.AddSingleton<AnalyzeService>();
    services.AddSingleton<ProfileService>();
    services.AddSingleton(sp => new NeuroService(
        reasoningChatClient,
        layerChatClient,
        sp.GetRequiredService<AgentTemplateRepository>(),
        sp.GetRequiredService<AnalyzeService>()));

    LogLlmConfig(llm);
}

static void LogLlmConfig(AgentConfiguration config)
{
    static string Fmt(LlmProviderConfig? c) =>
        c is null ? "(not configured)" : $"{c.ResolvedBackend,-16} @ {c.Endpoint ?? "(default)",-50} → {c.Model}";

    Console.WriteLine("[LLM Config]");
    Console.WriteLine($"  Orchestrator:    {Fmt(config.Orchestrator)}");
    Console.WriteLine($"  AgentAnalyzing:  {Fmt(config.AgentAnalyzing)}");
    Console.WriteLine($"  AgentReasoning:  {Fmt(config.AgentReasoning)}");
    Console.WriteLine($"  AgentLayer:      {Fmt(config.AgentLayer)}");
    Console.WriteLine($"  Embedding:       {Fmt(config.Embedding)}");
    Console.WriteLine($"  Max Parallel:    {config.MaxParallelAgents}");
}

// ── HTTP mode ────────────────────────────────────────────────────────────────

static async Task RunHttpMode(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);
    builder.AddServiceDefaults();

    var (llm, db) = LoadConfig(builder.Configuration);
    RegisterAll(builder.Services, llm, db);

    builder.Services.AddCors(options =>
        options.AddDefaultPolicy(policy =>
            policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
                  .AllowAnyHeader()
                  .AllowAnyMethod()));

    builder.Services
        .AddMcpServer()
        .WithHttpTransport(options => { options.Stateless = true; })
        .WithToolsFromAssembly();

    var app = builder.Build();
    app.UseCors();
    app.MapDefaultEndpoints();

    // REST API
    var personGroup = app.MapPersonApi();
    app.MapAnalyzeApi();
    app.MapRelationshipApi();
    if (app.Services.GetService<EmbeddingService>() is not null)
        app.MapEmbeddingApi();
    if (app.Services.GetService<DimensionService>() is not null)
        personGroup.MapDimensionApi();

    // MCP protocol
    app.MapMcp("/mcp");

    // Verify DB connectivity at startup
    var dbFactory = app.Services.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<PersonalityDbContext>>();
    await using var testDb = await dbFactory.CreateDbContextAsync();
    var personCount = await testDb.Persons.CountAsync();
    Console.WriteLine($"DB connected — {personCount} person(s) found");

    var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://localhost:18080";
    Console.WriteLine($"MCP Server running in HTTP mode on {urls} at /mcp");

    await app.RunAsync();
}

// ── stdio mode ───────────────────────────────────────────────────────────────

static async Task RunStdioMode(string[] args)
{
    using var host = Host.CreateDefaultBuilder(args)
        .ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole(options => { options.LogToStandardErrorThreshold = LogLevel.Trace; });
        })
        .ConfigureServices((ctx, services) =>
        {
            var (llm, db) = LoadConfig(ctx.Configuration);
            RegisterAll(services, llm, db);

            services
                .AddMcpServer()
                .WithStdioServerTransport()
                .WithToolsFromAssembly();
        })
        .Build();

    await host.RunAsync();
}
