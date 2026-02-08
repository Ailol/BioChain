using System.Text.Json.Serialization;

namespace Models;

/// <summary>
/// Simple DTO for deserializing chat messages from MCP tool JSON input.
/// Convert to ChatMessage (Microsoft.Extensions.AI) for internal use.
/// </summary>
public class ChatMessageInput
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
}
