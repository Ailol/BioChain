namespace NeuroGateway.Utils;

public static class ResponseService
{
    public static string? ExtractConclusion(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var idx = text.IndexOf("CONCLUSION:", StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return null;

        var after = text[(idx + 11)..].Trim();
        var end = after.IndexOf("\n---", StringComparison.Ordinal);
        return end > 0 ? after[..end].Trim() : after.Trim();
    }

    public static string? ExtractSuggestion(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var idx = text.LastIndexOf("SUGGEST:", StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return null;

        var result = text[(idx + 8)..].Trim();

        var conclusionIdx = result.IndexOf("CONCLUSION:", StringComparison.OrdinalIgnoreCase);
        if (conclusionIdx > 0)
            result = result[..conclusionIdx].Trim();

        return result;
    }

    /// <summary>
    /// Parse HERE/SHIFT/SUGGEST markers from agent response text.
    /// Falls back to ExtractSuggestion if no markers found.
    /// </summary>
    public static (string? Here, string? Shift, string? Suggest) ParseAgentResponse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return (null, null, null);

        string[] markers = ["HERE:", "SHIFT:", "SUGGEST:", "CONCLUSION:"];

        var here = ExtractSection(text, "HERE:", markers);
        var shift = ExtractSection(text, "SHIFT:", markers);
        var suggest = ExtractSection(text, "SUGGEST:", markers);

        // If no markers found, treat entire text as suggestion via fallback
        if (here == null && shift == null && suggest == null)
            suggest = ExtractSuggestion(text) ?? text.Trim();

        return (here, shift, suggest);
    }

    /// <summary>
    /// Extract text between a marker and the next known marker (or end of text).
    /// </summary>
    private static string? ExtractSection(string text, string marker, string[] allMarkers)
    {
        var idx = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;

        var start = idx + marker.Length;
        var after = text[start..];

        // Find the earliest next marker
        var endIdx = after.Length;
        foreach (var m in allMarkers)
        {
            if (string.Equals(m, marker, StringComparison.OrdinalIgnoreCase))
                continue;
            var mIdx = after.IndexOf(m, StringComparison.OrdinalIgnoreCase);
            if (mIdx >= 0 && mIdx < endIdx)
                endIdx = mIdx;
        }

        var result = after[..endIdx].Trim();
        return string.IsNullOrEmpty(result) ? null : result;
    }
}
