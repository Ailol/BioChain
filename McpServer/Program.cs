using Agents;
using OpenClaw;

// Check for stdio mode (backward compatibility with Claude Desktop direct connection)
if (args.Contains("--stdio"))
{
    await RunStdioMode(args);
}
else
{
    await RunHttpMode(args);
}

// HTTP mode for OpenWebUI, OpenClaw, and other HTTP-capable clients
static async Task RunHttpMode(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);

    // Add Aspire service defaults (OpenTelemetry, health checks, service discovery)
    builder.AddServiceDefaults();

    // Register agent services
    builder.Services.AddSingleton<MultiAgentService>();
    builder.Services.AddSingleton<PersonalityService>();

    // Configure MCP server with HTTP transport
    builder.Services
        .AddMcpServer()
        .WithHttpTransport(options =>
        {
            options.Stateless = true;  // If this option exists
        })
        .WithToolsFromAssembly();

    // Add OpenClaw Gateway integration (optional - connects to OpenClaw for multi-channel access)
    var enableOpenClaw = builder.Configuration.GetValue<bool>("OpenClaw:Enabled", false);
    if (enableOpenClaw)
    {
        builder.Services.AddOpenClaw(options =>
        {
            options.GatewayUrl = builder.Configuration["OpenClaw:GatewayUrl"] ?? "ws://127.0.0.1:18789";
            options.McpEndpoint = builder.Configuration["OpenClaw:McpEndpoint"] ?? "http://localhost:13370/mcp";
            options.ReconnectDelaySeconds = builder.Configuration.GetValue("OpenClaw:ReconnectDelaySeconds", 5);
            options.EnableVoice = builder.Configuration.GetValue("OpenClaw:EnableVoice", true);
        });
        Console.WriteLine("OpenClaw Gateway integration enabled");
    }

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
        .ConfigureServices(services =>
        {
            // Register agent services
            services.AddSingleton<MultiAgentService>();
            services.AddSingleton<PersonalityService>();

            // Configure MCP server with stdio transport
            services
                .AddMcpServer()
                .WithStdioServerTransport()
                .WithToolsFromAssembly();
        })
        .Build();

    await host.RunAsync();
}
