using System.Text;
using System.Text.Json.Serialization;

namespace Models;

public class AgentProfile
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    [JsonPropertyName("responsibilities")]
    public List<string> Responsibilities { get; set; } = new();

    [JsonPropertyName("style")]
    public string Style { get; set; } = "";

    [JsonPropertyName("conclusion")]
    public bool Conclusion { get; set; }

    [JsonPropertyName("maxWords")]
    public int MaxWords { get; set; }

    [JsonPropertyName("layer")]
    public string? Layer { get; set; }

    public AgentProfile WithStyle(string style) => new()
    {
        Role = Role, Responsibilities = Responsibilities, Style = style,
        Conclusion = Conclusion, MaxWords = MaxWords, Layer = Layer
    };

    public string ToSystemPrompt()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"You are a {Role}. Your role is to:");
        foreach (var r in Responsibilities)
            sb.AppendLine($"- {r}");
        sb.AppendLine(Style);
        // Only add default CONCLUSION instruction if Style doesn't already contain CONCLUSION guidance
        if (Conclusion && !Style.Contains("CONCLUSION:", StringComparison.OrdinalIgnoreCase))
            sb.AppendLine("End with \"CONCLUSION:\" followed by 3-5 key takeaways.");
        if (MaxWords > 0)
            sb.AppendLine($"Keep your response focused and under {MaxWords} words.");
        return sb.ToString();
    }
}
