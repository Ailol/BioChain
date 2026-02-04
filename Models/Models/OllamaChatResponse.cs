using System.Text.Json.Serialization;

namespace Models;

public class OllamaChatResponse
{
    [JsonPropertyName("message")]
    public OllamaMessage? Message { get; set; }
}
