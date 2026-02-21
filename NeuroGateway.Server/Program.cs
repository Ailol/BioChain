using System.ClientModel;
using Anthropic;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
// ReSharper disable AccessToDisposedClosure
using Microsoft.Extensions.AI;
using NeuroGateway.AgentFramework;
using NeuroGateway.Models;
using NeuroGateway.Repository;
using NeuroGateway.Repository.Roles;
using NeuroGateway.Server;
using NeuroGateway.Server.Api;
using NeuroGateway.Server.Auth;
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

static void RegisterAll(IServiceCollection services, AgentConfiguration llm, string db, bool isHttp)
{
    services.AddSingleton(llm);

    // EF + Npgsql
    services.AddPooledDbContextFactory<PersonalityDbContext>(options =>
        options.UseNpgsql(db, npgsql => npgsql.UseVector())
               .UseSnakeCaseNamingConvention());

    // User context — HTTP reads JWT claims; stdio uses env var
    if (isHttp)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<IUserContext, HttpUserContext>();
    }
    else
    {
        var userId = Environment.GetEnvironmentVariable("MCP_USER_ID") ?? "stdio-user";
        var email = Environment.GetEnvironmentVariable("MCP_USER_EMAIL");
        services.AddSingleton<IUserContext>(new FixedUserContext(userId, email));
    }

    // Repositories
    services.AddSingleton<PersonRepository>();
    services.AddSingleton<PersonalityRepository>();
    services.AddSingleton<AnalyzedDataRepository>();
    services.AddSingleton<AgentGroupRepository>();
    services.AddSingleton<AgentTemplateRepository>();
    services.AddSingleton<RelationshipRepository>();
    services.AddSingleton<ProfileRepository>();
    services.AddSingleton<ChemicalRepository>();
    services.AddSingleton<DimensionRepository>();
    services.AddSingleton<ChemicalInteractionRepository>();
    services.AddSingleton<ShadowEmbeddingRepository>();
    services.AddSingleton<QuestionnaireRepository>();
    services.AddSingleton<PersonShareRepository>();
    services.AddSingleton<UserRoleRepository>();

    // RBAC
    services.AddSingleton<IRoleService, RoleService>();

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

    // ChatClient for orchestrator (document chunking — Qwen3-32B-AWQ, 16K context)
    var orchestratorClient = new ChatClient(CreateChatClient(llm.Orchestrator!));

    // Embedding generator — required
    var embedGen = CreateEmbeddingGenerator(llm.Embedding)
        ?? throw new InvalidOperationException("Llm:Embedding must be configured");
    services.AddSingleton<DimensionDefinitionsService>();
    services.AddSingleton(embedGen);
    services.AddSingleton<EmbeddingService>();
    services.AddSingleton<ShadowAnchorService>();
    services.AddSingleton<DimensionService>();
    services.AddSingleton<CalibrationService>();

    // Services
    services.AddSingleton<PersonService>();
    services.AddSingleton<AnalyzeService>();
    services.AddSingleton<ProfileService>();
    services.AddSingleton<MbtiService>();
    services.AddSingleton<BigFiveService>();
    services.AddSingleton(sp => new ProfileAnalysisService(
        sp.GetRequiredService<ProfileRepository>(),
        sp.GetRequiredService<DimensionDefinitionsService>(),
        sp.GetRequiredService<ShadowAnchorService>(),
        sp.GetRequiredService<AnalyzeService>(),
        reasoningChatClient,
        sp.GetRequiredService<EmbeddingService>(),
        sp.GetRequiredService<MbtiService>(),
        sp.GetRequiredService<BigFiveService>()));
    services.AddSingleton<AnalysisQueueService>();
    services.AddHostedService<AnalysisBackgroundWorker>();
    services.AddSingleton<QuestionnaireService>();
    services.AddSingleton(sp => new NeuroService(
        orchestratorClient,
        reasoningChatClient,
        layerChatClient,
        sp.GetRequiredService<AgentTemplateRepository>(),
        sp.GetRequiredService<AnalyzeService>(),
        sp.GetRequiredService<DimensionService>(),
        sp.GetRequiredService<DimensionDefinitionsService>()));

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
    RegisterAll(builder.Services, llm, db, isHttp: true);

    builder.Services.AddCors(options =>
        options.AddDefaultPolicy(policy =>
            policy.SetIsOriginAllowed(origin =>
                    new Uri(origin).Host == "localhost")
                  .AllowAnyHeader()
                  .AllowAnyMethod()));

    // Keycloak JWT bearer auth
    builder.Services.AddAuthentication()
        .AddKeycloakJwtBearer("keycloak", "neurogateway", options =>
        {
            options.Audience = "neurogateway-api";
            if (builder.Environment.IsDevelopment())
                options.RequireHttpsMetadata = false;
        });
    builder.Services.AddSingleton<IRoleProvider, KeycloakRoleProvider>();
    builder.Services.AddTransient<IClaimsTransformation, DbRolesClaimsTransformation>();
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("Admin", policy => policy.RequireRole("admin"));
        options.AddPolicy("Work", policy => policy.RequireRole("admin", "work", "both"));
        options.AddPolicy("Private", policy => policy.RequireRole("admin", "private", "both"));
        options.AddPolicy("Worker", policy => policy.RequireRole("admin", "worker"));
        options.AddPolicy("HasRole", policy => policy.RequireRole("admin", "work", "private", "both", "worker"));
    });

    builder.Services
        .AddMcpServer()
        .WithHttpTransport(options => { options.Stateless = true; })
        .WithToolsFromAssembly();

    var app = builder.Build();
    if (app.Environment.IsDevelopment())
        app.UseDeveloperExceptionPage();
    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapDefaultEndpoints();

    // REST API — in Development allow anonymous (Aspire rewrites Keycloak issuer URL)
    if (app.Environment.IsDevelopment())
    {
        app.MapAuthApi().AllowAnonymous();
        var personGroup = app.MapPersonApi().AllowAnonymous();
        app.MapAnalyzeApi().AllowAnonymous();
        app.MapRelationshipApi().AllowAnonymous();
        app.MapChemicalApi().AllowAnonymous();
        app.MapChemicalInteractionApi().AllowAnonymous();
        app.MapDimensionMasterApi().AllowAnonymous();
        app.MapEmbeddingApi().AllowAnonymous();
        personGroup.MapDimensionApi();
        app.MapInsightsApi().AllowAnonymous();
        app.MapMbtiApi().AllowAnonymous();
        app.MapBigFiveApi().AllowAnonymous();
        app.MapQuestionnaireApi().AllowAnonymous();
    }
    else
    {
        app.MapAuthApi().RequireAuthorization();
        var personGroup = app.MapPersonApi().RequireAuthorization("Work");
        app.MapAnalyzeApi().RequireAuthorization("HasRole");
        app.MapRelationshipApi().RequireAuthorization("HasRole");
        app.MapChemicalApi().RequireAuthorization("HasRole");
        app.MapChemicalInteractionApi().RequireAuthorization("HasRole");
        app.MapDimensionMasterApi().RequireAuthorization("Admin");
        app.MapEmbeddingApi().RequireAuthorization("Admin");
        personGroup.MapDimensionApi();
        app.MapInsightsApi().RequireAuthorization("HasRole");
        app.MapMbtiApi().RequireAuthorization("HasRole");
        app.MapBigFiveApi().RequireAuthorization("HasRole");
        // Questionnaire has mixed auth (create requires auth, rest is public)
        app.MapQuestionnaireApi();
    }

    // MCP protocol
    app.MapMcp("/mcp");

    // Diagnostic endpoint
    app.MapGet("/ping", () => "pong");

    // Verify DB connectivity and wire auto-embedding after startup
    var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
    lifetime.ApplicationStarted.Register(() =>
    {
        _ = Task.Run(async () =>
        {
            var factory = app.Services.GetRequiredService<IDbContextFactory<PersonalityDbContext>>();
            for (var attempt = 1; attempt <= 30; attempt++)
            {
                try
                {
                    await using var testDb = await factory.CreateDbContextAsync();
                    await testDb.Persons.CountAsync();

                    // Run schema migrations before services start using the DB
                    await app.Services.GetRequiredService<ShadowEmbeddingRepository>()
                        .MigrateLevelConstraintAsync();

                    // Wire auto-embedding now that DB is confirmed ready
                    app.Services.GetRequiredService<AnalyzeService>().Embedder =
                        app.Services.GetRequiredService<EmbeddingService>();
                    return;
                }
                catch (Exception ex) when (attempt < 30)
                {
                    Console.WriteLine($"DB not ready (attempt {attempt}/30): {ex.Message}");
                    await Task.Delay(2000);
                }
            }
        });
    });

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
            RegisterAll(services, llm, db, isHttp: false);

            services
                .AddMcpServer()
                .WithStdioServerTransport()
                .WithToolsFromAssembly();
        })
        .Build();

    await host.RunAsync();
}
