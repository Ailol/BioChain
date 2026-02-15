namespace NeuroGateway.Utils.Parsing;

using NeuroGateway.Models;

public static class AgentAnalyzer
{
    public static List<AnalysisDecision> Parse(List<AgentResult> results) =>
        results
            .Where(r => r.Success)
            .Select(r => ParseOne(r.AgentName, r.RawResponse))
            .Where(d => d is not null)
            .ToList()!;

    private static AnalysisDecision? ParseOne(string chemical, string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var text = raw.Trim();

        // YAML format: reasoning: ...\naction: ADD|SKIP
        if (text.Contains("action:", StringComparison.OrdinalIgnoreCase))
        {
            var reasoning = ExtractReasoning(text);
            var action = ExtractField(text, "action:");

            if (action?.Equals("ADD", StringComparison.OrdinalIgnoreCase) == true)
                return new AnalysisDecision(chemical, reasoning ?? "");
            return null;
        }

        // Legacy: ADD: reasoning...
        if (text.StartsWith("ADD:", StringComparison.OrdinalIgnoreCase))
            return new AnalysisDecision(chemical, text[4..].Trim());

        return null;
    }

    /// <summary>
    /// Extract multi-line reasoning between "reasoning:" and "action:".
    /// Handles YAML block scalars (>, |) and inline values.
    /// </summary>
    private static string? ExtractReasoning(string text)
    {
        var lines = text.Split('\n');
        var collecting = false;
        var parts = new List<string>();

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.StartsWith("reasoning:", StringComparison.OrdinalIgnoreCase))
            {
                var inline = line["reasoning:".Length..].Trim();
                // Skip YAML block scalar indicators (>, |, >-, |-)
                if (inline.Length > 0 && inline is not ">" and not "|" and not ">-" and not "|-")
                    parts.Add(inline);
                collecting = true;
                continue;
            }
            if (line.StartsWith("action:", StringComparison.OrdinalIgnoreCase))
                break;
            if (collecting && line.Length > 0)
                parts.Add(line);
        }

        var result = string.Join(" ", parts).Trim();
        return result.Length > 0 ? result : null;
    }

    private static string? ExtractField(string text, string field)
    {
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.StartsWith(field, StringComparison.OrdinalIgnoreCase))
                return line[field.Length..].Trim();
        }
        return null;
    }
}
