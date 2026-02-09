namespace Agents;

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
}
