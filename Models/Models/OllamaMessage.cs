using System.Text.Json.Serialization;

namespace Models;

// Ollama API DTOs
public class OllamaMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
}
