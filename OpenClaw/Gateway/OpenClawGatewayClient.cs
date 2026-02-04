using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace OpenClaw.Gateway;

/// <summary>
/// Client for connecting to the OpenClaw Gateway WebSocket control plane.
/// Handles bidirectional communication for skill invocations and responses.
/// </summary>
public class OpenClawGatewayClient : IAsyncDisposable
{
    private readonly ILogger<OpenClawGatewayClient> _logger;
    private ClientWebSocket? _webSocket;
    private readonly Uri _gatewayUri;
    private CancellationTokenSource _cts;
    private readonly Dictionary<string, TaskCompletionSource<GatewayResponse>> _pendingRequests;
    private Task? _receiveTask;

    public event EventHandler<SkillInvocationEventArgs>? OnSkillInvocation;
    public event EventHandler<GatewayEventArgs>? OnGatewayEvent;
    public event EventHandler<Exception>? OnError;

    public bool IsConnected => _webSocket?.State == WebSocketState.Open;

    public OpenClawGatewayClient(ILogger<OpenClawGatewayClient> logger, string gatewayUrl = "ws://127.0.0.1:18789")
    {
        _logger = logger;
        _gatewayUri = new Uri(gatewayUrl);
        _cts = new CancellationTokenSource();
        _pendingRequests = new Dictionary<string, TaskCompletionSource<GatewayResponse>>();
    }

    /// <summary>
    /// Connect to the OpenClaw Gateway.
    /// </summary>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Dispose old WebSocket if exists and create a new one
            _webSocket?.Dispose();
            _webSocket = new ClientWebSocket();
            
            // Reset cancellation token source
            if (_cts.IsCancellationRequested)
            {
                _cts.Dispose();
                _cts = new CancellationTokenSource();
            }

            _logger.LogInformation("Connecting to OpenClaw Gateway at {Uri}", _gatewayUri);
            await _webSocket.ConnectAsync(_gatewayUri, cancellationToken);
            _logger.LogInformation("Connected to OpenClaw Gateway");

            // Start receiving messages
            _receiveTask = ReceiveLoopAsync(_cts.Token);

            // Register as a skill provider
            await RegisterSkillProviderAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to OpenClaw Gateway");
            throw;
        }
    }

    /// <summary>
    /// Register this server as a skill provider with OpenClaw.
    /// </summary>
    private async Task RegisterSkillProviderAsync(CancellationToken cancellationToken)
    {
        var registration = new GatewayMessage
        {
            Type = "skill_register",
            Payload = new
            {
                name = "multiagent-mcp",
                description = "Multi-perspective AI discussions via Hats, Neuro, and Group agents",
                version = "1.0.0",
                tools = new[]
                {
                    new { name = "hats_chat", description = "Run Hats group chat (medicine + systems engineering)" },
                    new { name = "neuro_chat", description = "Run Neuro group chat (neurotransmitter analysis)" },
                    new { name = "group_chat", description = "Run standard 5-agent group discussion" },
                    new { name = "get_personality", description = "Get personality profile with neurotransmitter traits" },
                    new { name = "update_personality", description = "Submit behavior for personality analysis" },
                    new { name = "full_personality_scan", description = "Get comprehensive personality scan with hormones/peptides" },
                    new { name = "create_personality", description = "Create a new person in the personality system" },
                    new { name = "scan_chat_update_personality", description = "Analyze chat for behavior patterns" }
                },
                endpoint = "http://localhost:13370/mcp"
            }
        };

        await SendMessageAsync(registration, cancellationToken);
        _logger.LogInformation("Registered MultiAgentMcp as OpenClaw skill provider");
    }

    /// <summary>
    /// Send a message to the Gateway.
    /// </summary>
    public async Task SendMessageAsync(GatewayMessage message, CancellationToken cancellationToken = default)
    {
        if (_webSocket == null || _webSocket.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("WebSocket is not connected");
        }
        
        var json = JsonSerializer.Serialize(message, JsonOptions.Default);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
        _logger.LogDebug("Sent message: {Type}", message.Type);
    }

    /// <summary>
    /// Send a skill response back to OpenClaw.
    /// </summary>
    public async Task SendSkillResponseAsync(string requestId, object result, CancellationToken cancellationToken = default)
    {
        var response = new GatewayMessage
        {
            Type = "skill_response",
            Id = requestId,
            Payload = result
        };
        await SendMessageAsync(response, cancellationToken);
    }

    /// <summary>
    /// Send an error response back to OpenClaw.
    /// </summary>
    public async Task SendErrorResponseAsync(string requestId, string error, CancellationToken cancellationToken = default)
    {
        var response = new GatewayMessage
        {
            Type = "skill_error",
            Id = requestId,
            Payload = new { error }
        };
        await SendMessageAsync(response, cancellationToken);
    }

    /// <summary>
    /// Background loop for receiving messages from the Gateway.
    /// </summary>
    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        var messageBuffer = new List<byte>();

        try
        {
            while (!cancellationToken.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
            {
                var result = await _webSocket.ReceiveAsync(buffer, cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogInformation("Gateway closed connection");
                    break;
                }

                messageBuffer.AddRange(buffer.Take(result.Count));

                if (result.EndOfMessage)
                {
                    var json = Encoding.UTF8.GetString(messageBuffer.ToArray());
                    messageBuffer.Clear();
                    await HandleMessageAsync(json);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Receive loop cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in receive loop");
            OnError?.Invoke(this, ex);
        }
    }

    /// <summary>
    /// Handle an incoming message from the Gateway.
    /// </summary>
    private async Task HandleMessageAsync(string json)
    {
        try
        {
            var message = JsonSerializer.Deserialize<GatewayMessage>(json, JsonOptions.Default);
            if (message == null) return;

            _logger.LogDebug("Received message: {Type}", message.Type);

            switch (message.Type)
            {
                case "skill_invoke":
                    await HandleSkillInvocationAsync(message);
                    break;

                case "ping":
                    await SendMessageAsync(new GatewayMessage { Type = "pong", Id = message.Id }, default);
                    break;

                case "response":
                    HandleResponse(message);
                    break;

                default:
                    OnGatewayEvent?.Invoke(this, new GatewayEventArgs(message.Type, message.Payload));
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling message");
        }
    }

    /// <summary>
    /// Handle a skill invocation request from OpenClaw.
    /// </summary>
    private async Task HandleSkillInvocationAsync(GatewayMessage message)
    {
        var invocation = JsonSerializer.Deserialize<SkillInvocation>(
            JsonSerializer.Serialize(message.Payload), JsonOptions.Default);

        if (invocation == null)
        {
            await SendErrorResponseAsync(message.Id ?? "", "Invalid invocation payload", default);
            return;
        }

        var args = new SkillInvocationEventArgs(message.Id ?? Guid.NewGuid().ToString(), invocation);
        OnSkillInvocation?.Invoke(this, args);
    }

    /// <summary>
    /// Handle a response to a pending request.
    /// </summary>
    private void HandleResponse(GatewayMessage message)
    {
        if (message.Id != null && _pendingRequests.TryGetValue(message.Id, out var tcs))
        {
            _pendingRequests.Remove(message.Id);
            tcs.SetResult(new GatewayResponse(message.Payload));
        }
    }

    /// <summary>
    /// Disconnect from the Gateway.
    /// </summary>
    public async Task DisconnectAsync()
    {
        _cts.Cancel();

        if (_webSocket?.State == WebSocketState.Open)
        {
            try
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnecting", CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error closing WebSocket");
            }
        }

        if (_receiveTask != null)
        {
            try
            {
                await _receiveTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling
            }
        }

        _logger.LogInformation("Disconnected from OpenClaw Gateway");
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        _webSocket?.Dispose();
        _cts.Dispose();
    }
}

#region Models

public class GatewayMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("payload")]
    public object? Payload { get; set; }
}

public class SkillInvocation
{
    [JsonPropertyName("tool")]
    public string Tool { get; set; } = "";

    [JsonPropertyName("arguments")]
    public Dictionary<string, object>? Arguments { get; set; }

    [JsonPropertyName("channel")]
    public ChannelInfo? Channel { get; set; }

    [JsonPropertyName("user")]
    public UserInfo? User { get; set; }
}

public class ChannelInfo
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = ""; // whatsapp, telegram, slack, discord, voice, webchat

    [JsonPropertyName("id")]
    public string? Id { get; set; }
}

public class UserInfo
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public class GatewayResponse
{
    public object? Data { get; }
    public GatewayResponse(object? data) => Data = data;
}

public class SkillInvocationEventArgs : EventArgs
{
    public string RequestId { get; }
    public SkillInvocation Invocation { get; }

    public SkillInvocationEventArgs(string requestId, SkillInvocation invocation)
    {
        RequestId = requestId;
        Invocation = invocation;
    }
}

public class GatewayEventArgs : EventArgs
{
    public string EventType { get; }
    public object? Payload { get; }

    public GatewayEventArgs(string eventType, object? payload)
    {
        EventType = eventType;
        Payload = payload;
    }
}

internal static class JsonOptions
{
    public static JsonSerializerOptions Default { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

#endregion
