using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenClaw.Gateway;

namespace OpenClaw.Skills;

/// <summary>
/// Hosted service that bridges OpenClaw skill invocations to MultiAgentMcp services.
/// Runs as a background service, maintaining the WebSocket connection to OpenClaw Gateway.
/// </summary>
public class MultiAgentSkillService : BackgroundService
{
    private readonly ILogger<MultiAgentSkillService> _logger;
    private readonly OpenClawGatewayClient _gateway;
    private readonly IMultiAgentSkillHandler _skillHandler;
    private readonly OpenClawOptions _options;

    public MultiAgentSkillService(
        ILogger<MultiAgentSkillService> logger,
        OpenClawGatewayClient gateway,
        IMultiAgentSkillHandler skillHandler,
        OpenClawOptions options)
    {
        _logger = logger;
        _gateway = gateway;
        _skillHandler = skillHandler;
        _options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MultiAgent Skill Service starting...");

        // Wire up event handlers
        _gateway.OnSkillInvocation += async (sender, args) =>
        {
            await HandleSkillInvocationAsync(args, stoppingToken);
        };

        _gateway.OnError += (sender, ex) =>
        {
            _logger.LogError(ex, "Gateway error occurred");
        };

        // Connect with retry logic
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _gateway.ConnectAsync(stoppingToken);

                // Keep alive while connected
                while (_gateway.IsConnected && !stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Gateway connection failed, retrying in {Delay}s...", _options.ReconnectDelaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(_options.ReconnectDelaySeconds), stoppingToken);
            }
        }
    }

    private async Task HandleSkillInvocationAsync(SkillInvocationEventArgs args, CancellationToken cancellationToken)
    {
        var invocation = args.Invocation;
        _logger.LogInformation("Skill invocation: {Tool} from {Channel}", invocation.Tool, invocation.Channel?.Type ?? "unknown");

        try
        {
            var result = await _skillHandler.HandleAsync(invocation, cancellationToken);
            await _gateway.SendSkillResponseAsync(args.RequestId, result, cancellationToken);
            _logger.LogInformation("Skill {Tool} completed successfully", invocation.Tool);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Skill {Tool} failed", invocation.Tool);
            await _gateway.SendErrorResponseAsync(args.RequestId, ex.Message, cancellationToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MultiAgent Skill Service stopping...");
        await _gateway.DisconnectAsync();
        await base.StopAsync(cancellationToken);
    }
}

/// <summary>
/// Interface for handling skill invocations from OpenClaw.
/// </summary>
public interface IMultiAgentSkillHandler
{
    Task<object> HandleAsync(SkillInvocation invocation, CancellationToken cancellationToken);
}

/// <summary>
/// Default implementation that routes skill invocations to the MCP server via HTTP.
/// </summary>
public class HttpMultiAgentSkillHandler : IMultiAgentSkillHandler
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpMultiAgentSkillHandler> _logger;
    private readonly string _mcpEndpoint;

    public HttpMultiAgentSkillHandler(
        HttpClient httpClient,
        ILogger<HttpMultiAgentSkillHandler> logger,
        OpenClawOptions options)
    {
        _httpClient = httpClient;
        _logger = logger;
        _mcpEndpoint = options.McpEndpoint;
    }

    public async Task<object> HandleAsync(SkillInvocation invocation, CancellationToken cancellationToken)
    {
        // Build MCP tool call request
        var mcpRequest = new
        {
            jsonrpc = "2.0",
            method = "tools/call",
            id = Guid.NewGuid().ToString(),
            @params = new
            {
                name = invocation.Tool,
                arguments = invocation.Arguments ?? new Dictionary<string, object>()
            }
        };

        var json = JsonSerializer.Serialize(mcpRequest);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        _logger.LogDebug("Calling MCP endpoint: {Endpoint} with tool: {Tool}", _mcpEndpoint, invocation.Tool);

        var response = await _httpClient.PostAsync(_mcpEndpoint, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var mcpResponse = JsonSerializer.Deserialize<JsonElement>(responseJson);

        // Extract result from MCP response
        if (mcpResponse.TryGetProperty("result", out var result))
        {
            return result;
        }

        if (mcpResponse.TryGetProperty("error", out var error))
        {
            throw new InvalidOperationException($"MCP error: {error}");
        }

        return mcpResponse;
    }
}

/// <summary>
/// Configuration options for OpenClaw integration.
/// </summary>
public class OpenClawOptions
{
    /// <summary>
    /// OpenClaw Gateway WebSocket URL.
    /// </summary>
    public string GatewayUrl { get; set; } = "ws://127.0.0.1:18789";

    /// <summary>
    /// MCP server HTTP endpoint.
    /// </summary>
    public string McpEndpoint { get; set; } = "http://localhost:13370/mcp";

    /// <summary>
    /// Delay in seconds before reconnecting after a connection failure.
    /// </summary>
    public int ReconnectDelaySeconds { get; set; } = 5;

    /// <summary>
    /// Enable voice interactions.
    /// </summary>
    public bool EnableVoice { get; set; } = true;

    /// <summary>
    /// Channels to enable for this skill.
    /// </summary>
    public List<string> EnabledChannels { get; set; } = new()
    {
        "whatsapp", "telegram", "slack", "discord", "webchat", "voice"
    };
}
