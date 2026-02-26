using System.ClientModel;
using Anthropic;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
// ReSharper disable AccessToDisposedClosure
using Microsoft.Extensions.AI;
using BioChain.AgentFramework;
using BioChain.Models;
using BioChain.Repository;
using BioChain.Repository.Roles;
using BioChain.Server;
using BioChain.Server.Api;
using BioChain.Server.Auth;
using BioChain.Service;
using OllamaSharp;
using OpenAI;

if (args.Contains("--stdio"))
    await RunStdioMode(args);
else
    await RunHttpMode(args);

// ── Configuration ────────────────────────────────────────────────────────────

static (AgentConfiguration Llm, string Db) LoadConfig(IConfiguration appConfig)
{
    var db = appConfig.GetConnectionString("personality")
        ?? throw new InvalidOperationException("ConnectionStrings:personality is required");

    Console.WriteLine($"DB: {db[..Math.Min(60, db.Length)]}...");

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
    services.AddSingleton<ObservationRepository>();
    services.AddSingleton<SignalRepository>();
    services.AddSingleton<DimensionRepository>();
    services.AddSingleton<SignalInteractionRepository>();
    services.AddSingleton<EmbeddingCacheRepository>();
    services.AddSingleton<QuestionnaireRepository>();
    services.AddSingleton<PersonShareRepository>();
    services.AddSingleton<UserRoleRepository>();
    services.AddSingleton<ActiveLoopRepository>();
    services.AddSingleton<TrajectoryRepository>();

    // RBAC
    services.AddSingleton<IRoleService, RoleService>();

    // ChatClient for analyzing agents (AgentAnalyzing — neuro LoRA, SKIP/ADD)
    // Optimal sampling params for med-opus 3B LoRA on vLLM:
    //   temperature=0.3 prevents gibberish, repetition_penalty=1.1 suppresses loops,
    //   stop=["</a>"] catches natural end, prefill "<t>" skips base model <think> blocks.
    var analyzingOptions = new ChatOptions
    {
        Temperature = 0.3f,
        StopSequences = ["</a>"],
        AdditionalProperties = new AdditionalPropertiesDictionary
        {
            ["repetition_penalty"] = 1.1
        }
    };
    var analyzingChatClient = new ChatClient(
        CreateChatClient(llm.AgentAnalyzing!), analyzingOptions, assistantPrefill: "<t>");
    services.AddSingleton(analyzingChatClient);

    // ChatClient for layer agents — same backend but NO prefill/stop (different output format)
    var layerChatClient = llm.AgentLayer is not null
        ? new ChatClient(CreateChatClient(llm.AgentLayer))
        : new ChatClient(CreateChatClient(llm.AgentAnalyzing!));

    // ChatClient for orchestrator — nanbeige4.1-3b optimal sampling from HF discussion:
    //   top_k=0 shortens thought chain, min_p=0.01, temp=0.6, top_p=0.95
    var orchestratorOptions = new ChatOptions
    {
        Temperature = 0.6f,
        TopP = 0.95f,
        TopK = 0,
        AdditionalProperties = new AdditionalPropertiesDictionary
        {
            ["min_p"] = 0.01
        }
    };
    var orchestratorClient = llm.Orchestrator is not null
        ? new ChatClient(CreateChatClient(llm.Orchestrator), orchestratorOptions)
        : new ChatClient(CreateChatClient(llm.AgentAnalyzing!));

    // Embedding generator — use no-op if not configured
    var embedGen = CreateEmbeddingGenerator(llm.Embedding)
        ?? (IEmbeddingGenerator<string, Embedding<float>>)new NoOpEmbeddingGenerator();
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
    services.AddSingleton<MlService>();
    services.AddSingleton(sp => new ProfileAnalysisService(
        sp.GetRequiredService<ObservationRepository>(),
        sp.GetRequiredService<DimensionDefinitionsService>(),
        sp.GetRequiredService<ShadowAnchorService>(),
        sp.GetRequiredService<AnalyzeService>(),
        analyzingChatClient,
        sp.GetRequiredService<EmbeddingService>()));
    services.AddSingleton<AnalysisQueueService>();
    services.AddHostedService<AnalysisBackgroundWorker>();
    services.AddSingleton<QuestionnaireService>();
    services.AddSingleton(sp => new NeuroService(
        orchestratorClient,
        layerChatClient,
        sp.GetRequiredService<AgentTemplateRepository>(),
        sp.GetRequiredService<AnalyzeService>(),
        sp.GetRequiredService<DimensionService>(),
        sp.GetRequiredService<DimensionDefinitionsService>()));
    services.AddSingleton<BioSphereService>();
    services.AddSingleton(sp => new PersonalSphereService(
        sp.GetRequiredService<PersonService>(),
        sp.GetRequiredService<NeuroService>(),
        sp.GetRequiredService<ObservationRepository>(),
        sp.GetRequiredService<DimensionService>(),
        orchestratorClient));

    LogLlmConfig(llm);
}

static void LogLlmConfig(AgentConfiguration config)
{
    static string Fmt(LlmProviderConfig? c) =>
        c is null ? "(not configured)" : $"{c.ResolvedBackend,-16} @ {c.Endpoint ?? "(default)",-50} → {c.Model}";

    Console.WriteLine("[LLM Config]");
    Console.WriteLine($"  Orchestrator:    {Fmt(config.Orchestrator)}");
    Console.WriteLine($"  AgentAnalyzing:  {Fmt(config.AgentAnalyzing)}");
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
        .AddKeycloakJwtBearer("keycloak", "biochain", options =>
        {
            options.Audience = "biochain-api";
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
        app.MapSignalApi().AllowAnonymous();
        app.MapSignalInteractionApi().AllowAnonymous();
        app.MapDimensionMasterApi().AllowAnonymous();
        app.MapEmbeddingApi().AllowAnonymous();
        personGroup.MapDimensionApi();
        app.MapInsightsApi().AllowAnonymous();
        app.MapBioSphereApi().AllowAnonymous();
        app.MapPersonalSphereApi().AllowAnonymous();
        app.MapQuestionnaireApi().AllowAnonymous();
    }
    else
    {
        app.MapAuthApi().RequireAuthorization();
        var personGroup = app.MapPersonApi().RequireAuthorization("Work");
        app.MapAnalyzeApi().RequireAuthorization("HasRole");
        app.MapRelationshipApi().RequireAuthorization("HasRole");
        app.MapSignalApi().RequireAuthorization("HasRole");
        app.MapSignalInteractionApi().RequireAuthorization("HasRole");
        app.MapDimensionMasterApi().RequireAuthorization("Admin");
        app.MapEmbeddingApi().RequireAuthorization("Admin");
        personGroup.MapDimensionApi();
        app.MapInsightsApi().RequireAuthorization("HasRole");
        app.MapBioSphereApi().RequireAuthorization("Private");
        app.MapPersonalSphereApi().RequireAuthorization("Private");
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

                    // Wire auto-embedding now that DB is confirmed ready
                    var embedSvc = app.Services.GetService<EmbeddingService>();
                    if (embedSvc is not null)
                        app.Services.GetRequiredService<AnalyzeService>().Embedder = embedSvc;
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

/// <summary>No-op embedding generator for testing without an embedding provider.</summary>
sealed class NoOpEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    public EmbeddingGeneratorMetadata Metadata { get; } = new("no-op");
    public void Dispose() { }
    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values, EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var result = new GeneratedEmbeddings<Embedding<float>>(
            values.Select(_ => new Embedding<float>(new float[1536])).ToList());
        return Task.FromResult(result);
    }
    public object? GetService(Type serviceType, object? serviceKey = null) => null;
}
