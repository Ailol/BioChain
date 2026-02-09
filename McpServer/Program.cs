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

// Create configuration: all LLM settings from appsettings.json Llm section, secrets from env vars
static AgentConfiguration CreateConfiguration(IConfiguration appConfig)
{
    var llm = appConfig.GetSection("Llm");
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
        PersonalityDb = Environment.GetEnvironmentVariable("PERSONALITY_DB")
            ?? appConfig.GetConnectionString("Personality")
            ?? throw new InvalidOperationException("PERSONALITY_DB env var or ConnectionStrings:Personality in appsettings.json is required"),
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

// HTTP mode for OpenWebUI, OpenClaw, and other HTTP-capable clients
static async Task RunHttpMode(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);

    // Add Aspire service defaults (OpenTelemetry, health checks, service discovery)
    builder.AddServiceDefaults();

    // Register configuration and agent services
    var config = CreateConfiguration(builder.Configuration);
    builder.Services.AddSingleton(config);
    var connStr = config.PersonalityDb;
    builder.Services.AddPooledDbContextFactory<PersonalityDbContext>(options =>
        options.UseNpgsql(connStr, npgsql => npgsql.UseVector())
               .UseSnakeCaseNamingConvention());
    builder.Services.AddSingleton<PersonRepository>();
    builder.Services.AddSingleton<PersonalityRepository>();
    builder.Services.AddSingleton<EmbeddingRepository>();
    builder.Services.AddSingleton<AgentGroupRepository>();
    builder.Services.AddSingleton<RelationshipRepository>();
    RegisterLlmServices(builder.Services, config);
    builder.Services.AddSingleton<EmbeddingService>();
    builder.Services.AddSingleton<VectorService>();
    builder.Services.AddSingleton<AgentService>();
    builder.Services.AddSingleton<GroupAgentService>();
    builder.Services.AddSingleton<BackgroundEmbeddingQueue>();
    builder.Services.AddHostedService<McpAgentServer.EmbeddingQueueProcessor>();
    builder.Services.AddSingleton<Agents.PersonalityService>();
    builder.Services.AddSingleton<NeuroService>();

    // Configure MCP server with HTTP transport
    builder.Services
        .AddMcpServer()
        .WithHttpTransport(options =>
        {
            options.Stateless = true;  // If this option exists
        })
        .WithToolsFromAssembly();

    var app = builder.Build();

    // Map Aspire default endpoints (health, alive)
    app.MapDefaultEndpoints();

    // Map MCP endpoint for HTTP clients at /mcp path
    app.MapMcp("/mcp");

    // Get the actual URLs the server is listening on
    var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://localhost:18080";
    Console.WriteLine($"MCP Server running in HTTP mode");
    Console.WriteLine($"Listening on: {urls}");
    Console.WriteLine($"MCP endpoint: /mcp");

    await app.RunAsync();
}

// Stdio mode for Claude Desktop (when run directly without Aspire)
static async Task RunStdioMode(string[] args)
{
    using var host = Host.CreateDefaultBuilder(args)
        .ConfigureLogging(logging =>
        {
            // Disable all console logging - MCP uses stdio for JSON-RPC
            logging.ClearProviders();
            logging.AddConsole(options =>
            {
                // Redirect any logs to stderr so they don't interfere with MCP protocol on stdout
                options.LogToStandardErrorThreshold = LogLevel.Trace;
            });
        })
        .ConfigureServices((hostContext, services) =>
        {
            // Register configuration and agent services
            var config = CreateConfiguration(hostContext.Configuration);
            services.AddSingleton(config);
            var connStr = config.PersonalityDb;
            services.AddPooledDbContextFactory<PersonalityDbContext>(options =>
                options.UseNpgsql(connStr, npgsql => npgsql.UseVector())
                       .UseSnakeCaseNamingConvention());
            services.AddSingleton<PersonRepository>();
            services.AddSingleton<PersonalityRepository>();
            services.AddSingleton<EmbeddingRepository>();
            services.AddSingleton<AgentGroupRepository>();
            services.AddSingleton<RelationshipRepository>();
            RegisterLlmServices(services, config);
            services.AddSingleton<EmbeddingService>();
            services.AddSingleton<VectorService>();
            services.AddSingleton<AgentService>();
            services.AddSingleton<GroupAgentService>();
            services.AddSingleton<BackgroundEmbeddingQueue>();
            services.AddHostedService<McpAgentServer.EmbeddingQueueProcessor>();
            services.AddSingleton<Agents.PersonalityService>();
            services.AddSingleton<NeuroService>();

            // Configure MCP server with stdio transport
            services
                .AddMcpServer()
                .WithStdioServerTransport()
                .WithToolsFromAssembly();
        })
        .Build();

    await host.RunAsync();
}
