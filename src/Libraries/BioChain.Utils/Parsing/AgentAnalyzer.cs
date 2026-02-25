namespace BioChain.Utils.Parsing;

using BioChain.Models;

public static class AgentAnalyzer
{
    // Known NCN section headers in order
    private static readonly string[] SectionHeaders =
        ["CHEMICAL:", "ACTION:", "SIGNALS:", "FORMULAS:", "STATE:", "CIRCUITS:"];

    /// <summary>
    /// Map from NCN code → (signal key, signal id).
    /// Must be populated at startup from the signal table.
    /// </summary>
    public static Dictionary<string, (string Key, int Id)> NcnLookup { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public static List<AnalysisDecision> Parse(List<AgentResult> results) =>
        results
            .Where(r => r.Success)
            .Select(r => ParseOne(r.AgentName, r.RawResponse))
            .Where(d => d is not null)
            .ToList()!;

    private static AnalysisDecision? ParseOne(string agentSignal, string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var text = raw.Trim();

        // ── NCN format: CHEMICAL: / ACTION: / SIGNALS: / FORMULAS: / STATE: / CIRCUITS: ──
        var action = ExtractSection(text, "ACTION:");
        if (action is not null)
        {
            if (!action.Equals("ADD", StringComparison.OrdinalIgnoreCase))
                return null;

            var signalCode = ExtractSection(text, "CHEMICAL:");
            var signals = ExtractSection(text, "SIGNALS:");
            var formulas = ExtractSection(text, "FORMULAS:");
            var state = ExtractSection(text, "STATE:");
            var circuits = ExtractSection(text, "CIRCUITS:");

            var signalKey = agentSignal; // fallback to agent name
            var signalId = 0;

            if (signalCode is not null && NcnLookup.TryGetValue(signalCode.Trim(), out var lookup))
            {
                signalKey = lookup.Key;
                signalId = lookup.Id;
            }
            else if (NcnLookup.Count > 0)
            {
                var match = NcnLookup.Values.FirstOrDefault(v =>
                    v.Key.Equals(agentSignal, StringComparison.OrdinalIgnoreCase));
                if (match.Key is not null)
                {
                    signalKey = match.Key;
                    signalId = match.Id;
                }
            }

            var intensity = ComputeIntensity(signals, formulas);
            return new AnalysisDecision(
                signalKey, signalId, signals, formulas ?? "",
                state, circuits,
                SubjectState: null, Operator: null, TargetSignalId: null,
                TargetState: null, RegionId: null, Temporal: null,
                Confidence: null, FailureMode: null, Intensity: intensity);
        }

        // ── Legacy tagged format: <t>, <r>, <action>, <a> ──
        var legacyAction = ExtractTag(text, "action");
        if (legacyAction is not null)
        {
            if (!legacyAction.Equals("ADD", StringComparison.OrdinalIgnoreCase))
                return null;

            var notation = ExtractTag(text, "t");
            var reasoning = ExtractTag(text, "r");

            var signalId = 0;
            if (NcnLookup.Count > 0)
            {
                var match = NcnLookup.Values.FirstOrDefault(v =>
                    v.Key.Equals(agentSignal, StringComparison.OrdinalIgnoreCase));
                if (match.Key is not null) signalId = match.Id;
            }

            var intensity = ComputeIntensity(notation, reasoning);
            return new AnalysisDecision(
                agentSignal, signalId, notation, reasoning ?? "",
                null, null,
                SubjectState: null, Operator: null, TargetSignalId: null,
                TargetState: null, RegionId: null, Temporal: null,
                Confidence: null, FailureMode: null, Intensity: intensity);
        }

        // ── Legacy YAML format: reasoning: ...\naction: ADD|SKIP ──
        if (text.Contains("action:", StringComparison.OrdinalIgnoreCase))
        {
            var reasoning = ExtractReasoning(text);
            var yamlAction = ExtractField(text, "action:");

            if (yamlAction?.Equals("ADD", StringComparison.OrdinalIgnoreCase) == true)
            {
                var signalId = 0;
                if (NcnLookup.Count > 0)
                {
                    var match = NcnLookup.Values.FirstOrDefault(v =>
                        v.Key.Equals(agentSignal, StringComparison.OrdinalIgnoreCase));
                    if (match.Key is not null) signalId = match.Id;
                }

                var intensity = ComputeIntensity(null, reasoning);
                return new AnalysisDecision(
                    agentSignal, signalId, null, reasoning ?? "",
                    null, null,
                    SubjectState: null, Operator: null, TargetSignalId: null,
                    TargetState: null, RegionId: null, Temporal: null,
                    Confidence: null, FailureMode: null, Intensity: intensity);
            }
            return null;
        }

        return null;
    }

    /// <summary>
    /// Extract the content of an NCN section (e.g., "SIGNALS:", "FORMULAS:").
    /// Returns the text between this section header and the next section header (or end of text).
    /// </summary>
    private static string? ExtractSection(string text, string header)
    {
        var start = text.IndexOf(header, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return null;

        start += header.Length;

        // Find the next section header
        var end = text.Length;
        foreach (var nextHeader in SectionHeaders)
        {
            if (nextHeader.Equals(header, StringComparison.OrdinalIgnoreCase)) continue;
            var nextPos = text.IndexOf(nextHeader, start, StringComparison.OrdinalIgnoreCase);
            if (nextPos > start && nextPos < end)
                end = nextPos;
        }

        var content = text[start..end].Trim();
        return content.Length > 0 ? content : null;
    }

    /// <summary>
    /// Compute intensity factor from signal confidence markers.
    /// #● = 1.0 (high), #◐ = 0.6 (medium), #○ = 0.3 (low)
    /// Returns the average confidence, defaulting to 1.0 if none found.
    /// </summary>
    public static float ComputeIntensity(string? signals, string? formulas)
    {
        var confidences = new List<float>();
        var combined = (signals ?? "") + "\n" + (formulas ?? "");

        foreach (var line in combined.Split('\n'))
        {
            if (line.Contains("#●")) confidences.Add(1.0f);
            else if (line.Contains("#◐")) confidences.Add(0.6f);
            else if (line.Contains("#○")) confidences.Add(0.3f);
        }

        return confidences.Count > 0
            ? confidences.Average()
            : 1.0f;
    }

    // ── Legacy helpers (kept for backward compatibility) ──

    private static string? ExtractTag(string text, string tag)
    {
        var openTag = $"<{tag}>";
        var closeTag = $"</{tag}>";
        var start = text.IndexOf(openTag, StringComparison.OrdinalIgnoreCase);
        if (start >= 0)
        {
            start += openTag.Length;
            var end = text.IndexOf(closeTag, start, StringComparison.OrdinalIgnoreCase);
            if (end > start)
                return text[start..end].Trim();
        }

        start = text.IndexOf(openTag, StringComparison.OrdinalIgnoreCase);
        if (start >= 0)
        {
            start += openTag.Length;
            var remaining = text[start..];
            if (remaining.StartsWith(" - "))
                remaining = remaining[3..];

            var nextTag = remaining.IndexOf('<');
            var content = nextTag > 0 ? remaining[..nextTag] : remaining;
            var trimmed = content.Trim();
            return trimmed.Length > 0 ? trimmed : null;
        }

        return null;
    }

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
