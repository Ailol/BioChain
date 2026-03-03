using System.ClientModel;
using Anthropic;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
// ReSharper disable AccessToDisposedClosure
using Microsoft.Extensions.AI;
using BioChain.AgentFramework;
using BioChain.Models;
using BioChain.Repository;
using BioChain.Repository.Data;
using BioChain.Repository.Linking;
using BioChain.Repository.Listeners;
using BioChain.Repository.Repositories;
using BioChain.Repository.Roles;
using BioChain.Server;
using BioChain.Server.Api;
using BioChain.Server.Auth;
using BioChain.Service;
using Neo4j.Driver;
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

static void RegisterAll(IServiceCollection services, AgentConfiguration llm, string db, bool isHttp, IConfiguration appConfig)
{
    services.AddSingleton(llm);

    // EF + Npgsql — scoped DbContext
    services.AddDbContext<BioChainDbContext>(options =>
        options.UseNpgsql(db, npgsql => npgsql.UseVector())
               .UseSnakeCaseNamingConvention());

    // User context — HTTP reads JWT claims; stdio uses env var
    if (isHttp)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext, HttpUserContext>();
    }
    else
    {
        var userId = Environment.GetEnvironmentVariable("MCP_USER_ID") ?? "stdio-user";
        var email = Environment.GetEnvironmentVariable("MCP_USER_EMAIL");
        services.AddSingleton<IUserContext>(new FixedUserContext(userId, email));
    }

    // Repositories — scoped (match DbContext lifetime)
    services.AddScoped<ISubjectRepository, SubjectRepository>();
    services.AddScoped<IStimuliRepository, StimuliRepository>();
    services.AddScoped<ISignalRepository, SignalRepository>();
    services.AddScoped<IReceptorRepository, ReceptorRepository>();
    services.AddScoped<ITransporterRepository, TransporterRepository>();
    services.AddScoped<IGateRepository, GateRepository>();
    services.AddScoped<ILimiterRepository, LimiterRepository>();
    services.AddScoped<IInterfaceRepository, InterfaceRepository>();
    services.AddScoped<IProtocolRepository, ProtocolRepository>();
    services.AddScoped<IRegionRepository, RegionRepository>();
    services.AddScoped<IEdgeRepository, EdgeRepository>();
    services.AddScoped<IModuleRepository, ModuleRepository>();
    services.AddScoped<IConstraintDefRepository, ConstraintDefRepository>();
    services.AddScoped<IToolRepository, ToolRepository>();
    services.AddScoped<IQuestionnaireRepository, QuestionnaireRepository>();
    services.AddScoped<IPersonShareRepository, PersonShareRepository>();
    services.AddScoped<IUserRoleRepository, UserRoleRepository>();

    // Repository-layer services (extracted from Service layer)
    services.AddScoped<IComponentLinker, ComponentLinker>();
    services.AddScoped<IGraphQueryRepository, GraphQueryRepository>();
    services.AddSingleton<IGraphChangeListener, PostgresGraphChangeListener>();

    // RBAC
    services.AddScoped<IRoleService, RoleService>();

    // BioChain engine — IChatClient for analysis (singleton, thread-safe)
    if (llm.AgentAnalyzing is not null)
    {
        var engineClient = CreateChatClient(llm.AgentAnalyzing);
        services.AddSingleton(engineClient);
    }

    // Chat model — nanbeige for conversational BioChain responses (keyed singleton)
    // Wrapped with FunctionInvokingChatClient so nanbeige can call DB tools autonomously
    if (llm.Chat is not null)
    {
        var chatClient = new ChatClientBuilder(CreateChatClient(llm.Chat))
            .UseFunctionInvocation()
            .Build();
        services.AddKeyedSingleton<IChatClient>("chat", chatClient);
    }

    // Embedding generator — keep for future use
    var embedGen = CreateEmbeddingGenerator(llm.Embedding);
    if (embedGen is not null)
        services.AddSingleton(embedGen);

    // Services — scoped
    services.AddScoped<AnalyzeService>();
    services.AddScoped<BioChainChatService>();

    // Neo4j graph sync (optional — only if Neo4j:Uri is configured)
    var neo4jUri = appConfig["Neo4j:Uri"];
    if (!string.IsNullOrEmpty(neo4jUri))
    {
        var neo4jPassword = appConfig["Neo4j:Password"] ?? "biochain_graph";
        services.AddSingleton<IDriver>(GraphDatabase.Driver(
            neo4jUri, AuthTokens.Basic("neo4j", neo4jPassword)));
        services.AddSingleton<IGraphStore, Neo4jGraphStore>();
        services.AddHostedService<GraphSyncService>();
    }

    // Agent ecosystem (optional — only if an analysis LLM is configured)
    if (llm.AgentAnalyzing is not null)
    {
        services.AddSingleton<EvolutionEngine>();
        services.AddHostedService<AgentEcosystemService>();
    }

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
    Console.WriteLine($"  Chat:            {Fmt(config.Chat)}");
    Console.WriteLine($"  Embedding:       {Fmt(config.Embedding)}");
    Console.WriteLine($"  Max Parallel:    {config.MaxParallelAgents}");
}

// ── HTTP mode ────────────────────────────────────────────────────────────────

static async Task RunHttpMode(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);
    builder.AddServiceDefaults();

    var (llm, db) = LoadConfig(builder.Configuration);
    RegisterAll(builder.Services, llm, db, isHttp: true, builder.Configuration);

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
        app.MapAnalyzeApi().AllowAnonymous();
        app.MapChatApi().AllowAnonymous();
        app.MapQuestionnaireApi().AllowAnonymous();
    }
    else
    {
        app.MapAuthApi().RequireAuthorization();
        app.MapAnalyzeApi().RequireAuthorization("HasRole");
        app.MapChatApi().RequireAuthorization("HasRole");
        app.MapQuestionnaireApi();
    }

    // MCP protocol
    app.MapMcp("/mcp");

    // Diagnostic endpoint
    app.MapGet("/ping", () => "pong");

    // Verify DB connectivity after startup
    var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
    lifetime.ApplicationStarted.Register(() =>
    {
        _ = Task.Run(async () =>
        {
            using var scope = app.Services.CreateScope();
            var dbCtx = scope.ServiceProvider.GetRequiredService<BioChainDbContext>();
            for (var attempt = 1; attempt <= 30; attempt++)
            {
                try
                {
                    await dbCtx.Subjects.CountAsync();
                    Console.WriteLine("DB connectivity verified.");
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
            RegisterAll(services, llm, db, isHttp: false, ctx.Configuration);

            services
                .AddMcpServer()
                .WithStdioServerTransport()
                .WithToolsFromAssembly();
        })
        .Build();

    await host.RunAsync();
}
