using System.Text.RegularExpressions;

namespace Agents;

/// <summary>
/// Static service for extracting structured responses from LLM agent output.
/// Parses markdown-formatted group chat results to find the synthesized answer.
/// </summary>
public static class ResponseService
{
    /// <summary>
    /// Extract text after a CONCLUSION: marker, trimmed to the next section separator.
    /// </summary>
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

    /// <summary>
    /// Extract text after the last SUGGEST: marker.
    /// </summary>
    public static string? ExtractSuggestion(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var idx = text.LastIndexOf("SUGGEST:", StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return null;

        var result = text[(idx + 8)..].Trim();

        // Strip any trailing CONCLUSION: block the LLM may have appended
        var conclusionIdx = result.IndexOf("CONCLUSION:", StringComparison.OrdinalIgnoreCase);
        if (conclusionIdx > 0)
            result = result[..conclusionIdx].Trim();

        return result;
    }

    /// <summary>
    /// Extract all SUGGEST: markers from text, returning up to maxCount suggestions.
    /// </summary>
    public static List<string> ExtractAllSuggestions(string? text, int maxCount = 3)
    {
        if (string.IsNullOrWhiteSpace(text) || maxCount <= 0)
            return [];

        var suggestions = new List<string>();
        var idx = 0;
        while (idx < text.Length)
        {
            var pos = text.IndexOf("SUGGEST:", idx, StringComparison.OrdinalIgnoreCase);
            if (pos < 0) break;

            var start = pos + 8;
            var end = text.IndexOf('\n', start);
            var value = (end > start ? text[start..end] : text[start..]).Trim();

            if (!string.IsNullOrWhiteSpace(value))
                suggestions.Add(value);

            idx = end > start ? end : text.Length;
        }

        return suggestions.Take(maxCount).ToList();
    }

    /// <summary>
    /// Extract the synthesized/crafted response from group chat output.
    /// Tries multiple strategies: CONCLUSION:, SUGGEST:, [Response: "..."], quoted text, Synthesizer section.
    /// </summary>
    public static string? ExtractCraftedResponse(string? groupChatResult)
    {
        if (string.IsNullOrWhiteSpace(groupChatResult))
            return null;

        // Priority 1: CONCLUSION: marker
        var conclusion = ExtractConclusion(groupChatResult);
        if (conclusion != null)
            return conclusion;

        // Priority 2: SUGGEST: marker
        var suggestion = ExtractSuggestion(groupChatResult);
        if (suggestion != null)
            return suggestion;

        // Priority 3: [Response: "..."] pattern
        var match = Regex.Match(groupChatResult,
            @"\[Response:\s*""([^""]+)""\]", RegexOptions.RightToLeft);
        if (match.Success && match.Groups[1].Value.Length > 10)
            return match.Groups[1].Value.Trim();

        // Priority 4: Last quoted text in final section
        var lastSep = groupChatResult.LastIndexOf("\n---\n", StringComparison.Ordinal);
        if (lastSep >= 0)
        {
            var lastSection = groupChatResult[(lastSep + 5)..];
            var quotes = Regex.Matches(lastSection, @"""([^""]{15,})""");
            if (quotes.Count > 0)
            {
                var responses = quotes.Cast<Match>()
                    .Select(m => m.Groups[1].Value)
                    .Where(s => !s.Contains("perspective") && !s.Contains("neurotransmitter"))
                    .TakeLast(2).ToList();
                if (responses.Count > 0)
                    return string.Join(" ", responses).Trim();
            }
        }

        // Priority 5: Synthesizer section content
        var synthIdx = groupChatResult.LastIndexOf("Synthesizer]", StringComparison.OrdinalIgnoreCase);
        if (synthIdx >= 0)
        {
            var after = groupChatResult[(synthIdx + 12)..];
            var start = after.IndexOf('\n') + 1;
            var end = after.IndexOf("\n---", StringComparison.Ordinal);
            if (start > 0)
            {
                var content = end > start ? after[start..end].Trim() : after[start..].Trim();
                var innerQuote = Regex.Match(content, @"""([^""]{10,})""");
                if (innerQuote.Success)
                    return innerQuote.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(content) && content.Length > 20)
                    return content;
            }
        }

        // Fallback: last meaningful section
        var sections = groupChatResult.Split("\n---\n", StringSplitOptions.RemoveEmptyEntries);
        if (sections.Length > 0 && sections[^1].Trim().Length > 50)
            return sections[^1].Trim();

        return null;
    }
}
