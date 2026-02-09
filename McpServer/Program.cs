using System.ClientModel;
using Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Models;
using OllamaSharp;
using OpenAI;
using Repository;

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

    return new AgentConfiguration
    {
        Backend = llm["Backend"] ?? "Ollama",
        ChatEndpoint = llm["ChatEndpoint"]
            ?? throw new InvalidOperationException("Llm:ChatEndpoint is required (set via Llm__ChatEndpoint env var or appsettings.json)"),
        EmbeddingEndpoint = llm["EmbeddingEndpoint"],
        ThinkingModel = llm["ThinkingModel"]
            ?? throw new InvalidOperationException("Llm:ThinkingModel is required (set via Llm__ThinkingModel env var or appsettings.json)"),
        InstructModel = llm["InstructModel"]
            ?? throw new InvalidOperationException("Llm:InstructModel is required (set via Llm__InstructModel env var or appsettings.json)"),
        EmbeddingModel = llm["EmbeddingModel"]
            ?? throw new InvalidOperationException("Llm:EmbeddingModel is required (set via Llm__EmbeddingModel env var or appsettings.json)"),
        PersonalityDb = personalityDb,
        MaxParallelAgents = llm.GetValue("MaxParallelAgents", 3)
    };
}

// Register IChatClient + IEmbeddingGenerator based on Backend config
static void RegisterLlmServices(IServiceCollection services, AgentConfiguration config)
{
    var chatUri = new Uri(config.ChatEndpoint);
    var embedUri = new Uri(config.EmbeddingEndpoint ?? config.ChatEndpoint);

    if (config.Backend.Equals("Vllm", StringComparison.OrdinalIgnoreCase))
    {
        // vLLM: OpenAI-compatible endpoints (must include /v1/ path)
        static Uri EnsureV1Path(Uri uri) =>
            uri.AbsolutePath.Contains("/v1") ? uri : new Uri(uri, "v1/");

        var chatOai = new OpenAIClient(new ApiKeyCredential("unused"),
            new OpenAIClientOptions { Endpoint = EnsureV1Path(chatUri) });
        services.AddSingleton<IChatClient>(chatOai.GetChatClient(config.InstructModel).AsIChatClient());

        var embedOai = new OpenAIClient(new ApiKeyCredential("unused"),
            new OpenAIClientOptions { Endpoint = EnsureV1Path(embedUri) });
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(
            embedOai.GetEmbeddingClient(config.EmbeddingModel).AsIEmbeddingGenerator());
    }
    else
    {
        // Ollama: native OllamaSharp client (implements IChatClient + IEmbeddingGenerator)
        var chatHttp = new HttpClient { BaseAddress = chatUri, Timeout = TimeSpan.FromMinutes(10) };
        services.AddSingleton<IChatClient>(new OllamaApiClient(chatHttp, config.InstructModel));

        var embedHttp = new HttpClient { BaseAddress = embedUri, Timeout = TimeSpan.FromMinutes(10) };
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(
            new OllamaApiClient(embedHttp, config.EmbeddingModel));
    }

    services.AddSingleton<LlmService>();
}

static void RegisterServices(IServiceCollection services, AgentConfiguration config)
{
    services.AddSingleton(config);
    services.AddPooledDbContextFactory<PersonalityDbContext>(options =>
        options.UseNpgsql(config.PersonalityDb, npgsql => npgsql.UseVector())
               .UseSnakeCaseNamingConvention());
    services.AddSingleton<PersonRepository>();
    services.AddSingleton<PersonalityRepository>();
    services.AddSingleton<EmbeddingRepository>();
    services.AddSingleton<AgentGroupRepository>();
    services.AddSingleton<AgentTemplateRepository>();
    services.AddSingleton<RelationshipRepository>();
    services.AddSingleton<PipelineRepository>();
    RegisterLlmServices(services, config);
    services.AddSingleton<EmbeddingService>();
    services.AddSingleton<VectorService>();
    services.AddSingleton<AgentService>();
    services.AddSingleton<BackgroundEmbeddingQueue>();
    services.AddHostedService<McpAgentServer.EmbeddingQueueProcessor>();
    services.AddSingleton<Agents.PersonalityService>();
    services.AddSingleton<AnalysisService>(sp =>
    {
        var svc = new AnalysisService(
            sp.GetRequiredService<LlmService>(),
            sp.GetRequiredService<PersonRepository>(),
            sp.GetRequiredService<EmbeddingRepository>(),
            sp.GetRequiredService<EmbeddingService>(),
            sp.GetRequiredService<Agents.PersonalityService>(),
            sp.GetRequiredService<PersonalityRepository>(),
            sp.GetRequiredService<RelationshipRepository>(),
            sp.GetRequiredService<AgentTemplateRepository>(),
            sp.GetRequiredService<AgentService>(),
            sp.GetRequiredService<AgentConfiguration>());
        // Wire circular dependency: PersonalityService → AnalysisService
        sp.GetRequiredService<Agents.PersonalityService>().SetAnalysisService(svc);
        return svc;
    });
}

static async Task RunHttpMode(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);
    builder.AddServiceDefaults();

    var config = CreateConfiguration(builder.Configuration);
    RegisterServices(builder.Services, config);

    builder.Services
        .AddMcpServer()
        .WithHttpTransport(options => { options.Stateless = true; })
        .WithToolsFromAssembly();

    var app = builder.Build();
    app.MapDefaultEndpoints();
    app.MapMcp("/mcp");

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
            RegisterServices(services, config);

            services
                .AddMcpServer()
                .WithStdioServerTransport()
                .WithToolsFromAssembly();
        })
        .Build();

    await host.RunAsync();
}
