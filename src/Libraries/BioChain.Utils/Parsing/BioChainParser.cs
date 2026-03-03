namespace BioChain.Utils.Parsing;

using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Parses BioChain Engine v5.0 tagged output into structured protocol lines
/// and extracts component references for DB linking.
/// </summary>
public static partial class BioChainParser
{
    public record ParsedLine(string Tag, string Formula, string? Status, string? Phase);

    private static readonly HashSet<string> Tags = new(StringComparer.OrdinalIgnoreCase)
    {
        "SIGNAL", "RECEPTOR", "GATE", "LIMITER", "FEEDBACK", "FORMULA", "STATE",
        "TRANSPORT", "INTERFACE", "DEF", "DYSREG", "HYPOTHESIS", "PREDICTION", "INTERVENTION",
        "CONSTRAINT", "EQUILIBRIUM", "BOUNDARY", "CONSERVE", "TOOL", "LLM_GATE",
        "EMIT", "MESSAGE", "MODULE", "IMPORT", "BIND", "FAIL"
    };

    // Tags that use { ... } multi-line block syntax
    private static readonly HashSet<string> BlockTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "TOOL", "LLM_GATE", "MODULE"
    };

    public static List<ParsedLine> Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];

        var result = new List<ParsedLine>();
        string? phase = null;

        // Block accumulation state for TOOL/LLM_GATE/MODULE { ... } blocks
        string? blockTag = null;
        string? blockContent = null;
        string? blockPhase = null;
        int braceDepth = 0;

        foreach (var rawLine in raw.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;

            // Inside a block accumulation — collect lines until braces balance
            if (blockTag is not null)
            {
                braceDepth += line.Count(c => c == '{') - line.Count(c => c == '}');
                blockContent += "\n" + line;

                if (braceDepth <= 0)
                {
                    // Block complete — emit as single parsed line
                    result.Add(new ParsedLine(blockTag, blockContent!.Trim(), null, blockPhase));
                    blockTag = null;
                    blockContent = null;
                }
                continue;
            }

            // #PHASE: name (temporal)
            if (line.StartsWith("#PHASE:", StringComparison.OrdinalIgnoreCase))
            {
                phase = line["#PHASE:".Length..].Trim();
                continue;
            }

            // TAG: content — status: value
            var colon = line.IndexOf(':');
            if (colon is <= 0 or > 15) continue;

            var tag = line[..colon].Trim().ToUpperInvariant();
            if (!Tags.Contains(tag)) continue;

            var rest = line[(colon + 1)..].Trim();

            // Check for block tags with opening brace — start accumulation
            if (BlockTags.Contains(tag) && rest.Contains('{'))
            {
                braceDepth = rest.Count(c => c == '{') - rest.Count(c => c == '}');
                if (braceDepth > 0)
                {
                    // Block spans multiple lines — start accumulating
                    blockTag = tag;
                    blockContent = rest;
                    blockPhase = phase;
                    continue;
                }
                // Single-line block (braces balanced) — fall through to normal processing
            }

            // Extract "— status:" (em-dash) or "- status:" (hyphen) suffix
            string? status = null;
            var emIdx = rest.IndexOf("\u2014 status:", StringComparison.Ordinal);
            if (emIdx >= 0)
            {
                status = rest[(emIdx + "\u2014 status:".Length)..].Trim();
                rest = rest[..emIdx].TrimEnd();
            }
            else
            {
                var hyIdx = rest.IndexOf("- status:", StringComparison.Ordinal);
                if (hyIdx >= 0)
                {
                    status = rest[(hyIdx + "- status:".Length)..].Trim();
                    rest = rest[..hyIdx].TrimEnd();
                }
            }

            result.Add(new ParsedLine(tag, rest, status, phase));
        }

        // Flush any incomplete block (LLM may not have closed braces)
        if (blockTag is not null && blockContent is not null)
            result.Add(new ParsedLine(blockTag, blockContent.Trim(), null, blockPhase));

        return result;
    }

    // ── Signal: DA[↑↑] @VTA→NAc (phasic) (τ=ms) ──
    // Also handles optional type prefix: NT:DA[↑↑] @VTA

    public static (string Type, string Code, string? State, string? Region)? ExtractSignal(string formula)
    {
        var m = SignalPattern().Match(formula);
        if (!m.Success) return null;

        var code = m.Groups["code"].Value;

        // Explicit type prefix (NT:, H:, P:, etc.) or infer from code
        var type = m.Groups["type"].Success && m.Groups["type"].Value.Length > 0
            ? m.Groups["type"].Value
            : InferSignalType(code);

        string? state = null;
        if (m.Groups["state"].Success)
        {
            var raw = m.Groups["state"].Value;
            // Handle transitions ≈→↑↑ — take target state
            var arrow = raw.IndexOf('\u2192'); // →
            state = arrow >= 0 ? Normalize(raw[(arrow + 1)..]) : Normalize(raw);
        }

        // Region may be a route like VTA→NAc; take first part as primary region
        string? region = null;
        if (m.Groups["region"].Success)
        {
            var rawRegion = m.Groups["region"].Value;
            var routeArrow = rawRegion.IndexOf('\u2192'); // →
            region = routeArrow >= 0 ? rawRegion[..routeArrow] : rawRegion;
        }

        return (type, code, state, region);
    }

    // ── Receptor: DA.D2(Gi)[.desens] @NAc  or  DA.D2[.desens] @VTA (Gi) ──

    public static (string SignalCode, string Code, string? State, string? Subtype)? ExtractReceptor(string formula)
    {
        var m = ReceptorPattern().Match(formula);
        if (!m.Success) return null;

        // Subtype can appear before or after the state bracket
        var subtype = m.Groups["subtype1"].Success ? m.Groups["subtype1"].Value
            : m.Groups["subtype2"].Success ? m.Groups["subtype2"].Value
            : null;

        return (m.Groups["signal"].Value, m.Groups["code"].Value,
            m.Groups["state"].Success ? m.Groups["state"].Value.TrimStart('.') : null,
            subtype);
    }

    // ── Gate: {⊨(DA > threshold) → PFC.executive[↓]} ──

    public static (string Expression, string Type)? ExtractGate(string formula)
    {
        // Primary format: {sym(cond) → effect} or {sym: expr}
        var m = GatePattern().Match(formula);
        if (m.Success)
        {
            string expr;
            if (m.Groups["cond"].Success)
            {
                var cond = m.Groups["cond"].Value.Trim();
                var effect = m.Groups["effect"].Success ? m.Groups["effect"].Value.Trim() : "";
                expr = string.IsNullOrEmpty(effect) ? cond : $"{cond}\u2192{effect}";
            }
            else
            {
                expr = m.Groups["cond2"].Value.Trim();
            }
            return (expr.Length > 60 ? expr[..60] : expr, MapGateType(m.Groups["sym"].Value));
        }

        // Bare format: SYMBOL[condition] or SYMBOL(condition) without braces
        // Handles LLM output like: ⊨[TIME.ELAPSED <= 10s]
        var mb = BareGatePattern().Match(formula);
        if (mb.Success)
        {
            var expr = mb.Groups["cond"].Value.Trim();
            return (expr.Length > 60 ? expr[..60] : expr, MapGateType(mb.Groups["sym"].Value));
        }

        return null;
    }

    // ── Limiter: TH⧫[≈] → DA.synthesis @VTA ──

    public static (string Code, string? Activity, bool RateLimiting, string? Reaction)? ExtractLimiter(string formula)
    {
        var m = LimiterPattern().Match(formula);
        if (!m.Success) return null;
        var rawCode = m.Groups["code"].Value;
        return (rawCode.Replace("\u29EB", ""), // remove ⧫
            m.Groups["act"].Success ? Normalize(m.Groups["act"].Value) : null,
            rawCode.Contains('\u29EB'),
            m.Groups["reaction"].Success ? m.Groups["reaction"].Value.Trim() : null);
    }

    // ── Transporter: DAT[≈] @NAc ──

    public static (string Code, string? State, string? Clearance)? ExtractTransporter(string formula)
    {
        var m = TransporterPattern().Match(formula);
        if (!m.Success) return null;
        var state = m.Groups["state"].Success ? m.Groups["state"].Value : null;
        // Clearance derived from state — map text states to BioChain symbols
        return (m.Groups["code"].Value, state, state is not null ? NormalizeClearance(state) : null);
    }

    // ── Interface: VTA → NAc (mesolimbic) ──

    public static (string Source, string Target, string? Pathway)? ExtractInterface(string formula)
    {
        var m = InterfacePattern().Match(formula);
        if (!m.Success) return null;
        return (m.Groups["src"].Value, m.Groups["tgt"].Value,
            m.Groups["path"].Success ? m.Groups["path"].Value : null);
    }

    // ── Formula signal refs: DA@VTA → ... → GLU@PFC  or  DA[↑↑] @VTA ──

    public static ((string Code, string? Region)? Source, (string Code, string? Region)? Target)
        ExtractFormulaSignalRefs(string formula)
    {
        var matches = SignalRefPattern().Matches(formula);
        if (matches.Count == 0) return (null, null);

        static (string, string?) Extract(Match m) =>
            (m.Groups["code"].Value, m.Groups["region"].Success ? m.Groups["region"].Value : null);

        var src = Extract(matches[0]);
        var tgt = matches.Count > 1 ? Extract(matches[^1]) : src;
        return (src, tgt);
    }

    // ── Formula gate condition extraction ──

    /// <summary>
    /// Strips gate condition suffix {⊨(...)} from FORMULA/FEEDBACK lines.
    /// Returns the gate info + cleaned formula without the gate suffix.
    /// </summary>
    public static ((string Expression, string Type)? Gate, string CleanFormula)
        ExtractFormulaGateCondition(string formula)
    {
        var braceStart = formula.LastIndexOf('{');
        var braceEnd = formula.LastIndexOf('}');

        if (braceStart < 0 || braceEnd <= braceStart)
            return (null, formula);

        var gatePart = formula[braceStart..(braceEnd + 1)];
        var cleanPart = formula[..braceStart].TrimEnd();
        var extracted = ExtractGate(gatePart);

        return (extracted, cleanPart);
    }

    /// <summary>
    /// Converts a gate condition expression to structured JSON for gate.expression.
    /// Handles forms: "H:CORT[↑]", "DA@NAc >= ↑↑", "DA@NAc > baseline", "CORT[↑↑]@ADR".
    /// Unrecognized → {"raw":"..."} (evaluate_gate returns true).
    /// </summary>
    public static string? ParseGateExpression(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression)) return null;

        // Strip effect part if present (condition→effect — we only need condition)
        var arrowIdx = expression.IndexOf('\u2192'); // →
        var condPart = arrowIdx >= 0 ? expression[..arrowIdx].Trim() : expression.Trim();

        // Form 1: TYPE:CODE[state] @REGION?  — "H:CORT[↑]", "NT:DA[↑↑] @NAc"
        var m1 = GateExprStatePattern().Match(condPart);
        if (m1.Success)
        {
            var code = m1.Groups["code"].Value;
            var state = Normalize(m1.Groups["state"].Value);
            var region = m1.Groups["region"].Success && m1.Groups["region"].Value.Length > 0
                ? m1.Groups["region"].Value : null;
            return BuildExprJson(code, region, ">=", state);
        }

        // Form 2: CODE(@REGION)? op state  — "DA@NAc >= ↑↑", "CORT > baseline"
        var m2 = GateExprComparePattern().Match(condPart);
        if (m2.Success)
        {
            var code = m2.Groups["code"].Value;
            var region = m2.Groups["region"].Success && m2.Groups["region"].Value.Length > 0
                ? m2.Groups["region"].Value : null;
            var op = NormalizeOp(m2.Groups["op"].Value);
            var state = NormalizeThresholdWord(m2.Groups["state"].Value);
            return BuildExprJson(code, region, op, state);
        }

        // Fallback: store raw — evaluate_gate returns true for unrecognized
        return JsonSerializer.Serialize(new { raw = condPart });
    }

    private static string BuildExprJson(string signal, string? region, string op, string state)
        => JsonSerializer.Serialize(new { signal, region, op, state });

    private static string NormalizeOp(string raw) => raw.Trim() switch
    {
        ">=" or "\u2265" => ">=", // ≥
        ">" => ">",
        "<=" or "\u2264" => "<=", // ≤
        "<" => "<",
        "=" or "==" => "=",
        "!=" or "\u2260" => "!=", // ≠
        _ => ">="
    };

    private static string NormalizeThresholdWord(string raw) => raw.Trim().ToLowerInvariant() switch
    {
        "baseline" or "normal" or "homeostatic" => "\u2248", // ≈
        "elevated" or "high" => "\u2191",                     // ↑
        "very high" or "very elevated" => "\u2191\u2191",     // ↑↑
        "depleted" or "low" => "\u2193",                      // ↓
        "very low" or "very depleted" => "\u2193\u2193",      // ↓↓
        _ => Normalize(raw.Trim())
    };

    // ── Transporter → Signal code mapping ──

    public static string? MapTransporterToSignal(string code) => code.ToUpperInvariant() switch
    {
        "DAT" => "DA", "SERT" => "5HT", "NET" => "NE", "GAT" => "GABA",
        "VMAT2" => "DA", "EAAT" => "GLU", "CHT" => "ACh", _ => null
    };

    // ── Signal code → type inference ──

    public static string InferSignalType(string code) => code.ToUpperInvariant() switch
    {
        // Neurotransmitters
        "DA" or "5HT" or "NE" or "GABA" or "GLU" or "ACH" or "GLYCINE"
            or "HISTAMINE" or "ATP" or "ADENOSINE" => "NT",
        // Hormones
        "CORTISOL" or "CRH" or "ACTH" or "TESTOSTERONE" or "ESTRADIOL"
            or "PROGESTERONE" or "DHEA" or "MELATONIN" or "INSULIN"
            or "LEPTIN" or "GHRELIN" or "THYROID" or "T3" or "T4"
            or "ADRENALINE" or "EPINEPHRINE" or "NORADRENALINE" => "H",
        // Neuropeptides
        "OXT" or "AVP" or "DYNORPHIN" or "ENDORPHIN" or "ENKEPHALIN"
            or "SUBSTANCE_P" or "NPY" or "CRF" or "BDNF" or "NGF"
            or "OREXIN" or "VIP" or "CCK" or "CGRP" => "P",
        // Endocannabinoids
        "AEA" or "2AG" or "ANA" or "ANANDAMIDE" or "PEA" or "OEA"
            or "ECB" => "eCB",
        // Neuroimmune
        "IL6" or "IL1" or "IL10" or "TNF" or "TNFA" or "NFKB"
            or "IFN" or "CRP" => "NI",
        // Neurosteroidal
        "ALLOPREGNANOLONE" or "PREGNENOLONE" or "DHEAS" => "NS",
        _ => "NT" // default to neurotransmitter
    };

    // ── Helpers ──

    private static string Normalize(string s) => s switch
    {
        "\u2191\u2191\u2191" => "\u2191\u2191", // ↑↑↑ → ↑↑
        "\u2193\u2193\u2193" => "\u2193\u2193", // ↓↓↓ → ↓↓
        _ => s
    };

    /// <summary>
    /// Maps transporter state text or symbols to a BioChain clearance symbol (≤5 chars).
    /// BNF: clearance: ↑↑ | ↑ | ≈ | ↓ | ↓↓ | ⊘
    /// </summary>
    private static string NormalizeClearance(string s) => s.Trim().ToLowerInvariant() switch
    {
        "active" or "normal" or "\u2248" => "\u2248",           // ≈
        "enhanced" or "increased" or "upregulated" => "\u2191",  // ↑
        "impaired" or "reduced" or "decreased" or "downregulated" => "\u2193", // ↓
        "blocked" or "absent" or "inactive" or "\u2298" => "\u2298", // ⊘
        _ when s.Contains('\u2191') => Normalize(s),  // pass through ↑ symbols
        _ when s.Contains('\u2193') => Normalize(s),  // pass through ↓ symbols
        _ => "\u2248" // default to ≈ (normal) for unrecognized
    };

    private static string MapGateType(string sym) => sym switch
    {
        "\u22A8" => "threshold",  // ⊨
        "\u22A1" => "latch",      // ⊡
        "\u03A3" => "integrator", // Σ
        "\u229B" => "novelty",    // ⊛
        "\u22B3" => "gain",       // ⊳
        "\u22BC" => "and",        // ⊼
        "\u22BD" => "or",         // ⊽
        "\u00AC" => "not",        // ¬
        "\u2295" => "xor",        // ⊕
        "\u2442" => "splitter",   // ⑂
        _ => "threshold"
    };

    // ── Regex ──

    // Signal: optional "TYPE:" prefix, then CODE[state] @REGION (mode) (τ=...)
    // Matches: DA[↑↑] @VTA→NAc (phasic) (τ=ms)
    //          NT:DA[↑↑] @VTA
    //          5HT[↓] @DRN→PFC (tonic)
    //          DA[↑↑] @NAc
    [GeneratedRegex(@"(?:(?<type>NT|H|P|NI|NS|eCB):)?(?<code>[\w]+)\[(?<state>[^\]]+)\]\s*(?:@(?<region>[^\s(]+))?")]
    private static partial Regex SignalPattern();

    // Receptor: signal.code(subtype)[state] @region  OR  signal.code[state] @region (subtype)
    // Matches: DA.D2(Gi)[.desens] @NAc
    //          DA.D2[.desens] @VTA (Gi)
    [GeneratedRegex(@"(?<signal>\w+)\.(?<code>\w+)(?:\((?<subtype1>[^)]+)\))?(?:\[\.?(?<state>[^\]]+)\])?\s*(?:@\w+)?\s*(?:\((?<subtype2>[^)]+)\))?")]
    private static partial Regex ReceptorPattern();

    // Gate: {SYMBOL(condition) → effect} optionally @REGION
    // Matches: {⊨(DA > threshold) → PFC.executive[↓]}
    //          {⊛(novelty) → DA.phasic[↑↑]} @NAc
    //          {⊨: DA@NAc ≥ ↑}  (legacy format also supported via alternation)
    [GeneratedRegex(@"\{(?<sym>[\u22A8\u22A1\u03A3\u229B\u22B3\u22BC\u22BD\u00AC\u2295\u2442])(?:\((?<cond>[^)]+)\)\s*\u2192\s*(?<effect>[^}]+)|:\s*(?<cond2>[^}]+))\}")]
    private static partial Regex GatePattern();

    // Limiter: CODE⧫?[activity] → reaction @REGION
    // Matches: TH⧫[≈] → DA.synthesis @VTA
    //          GAD[↓] → GABA.synthesis @PFC
    //          FAAH[↑] → eCB.ANA[↓] @PFC
    [GeneratedRegex(@"(?<code>[\w\u29EB]+)\[(?<act>[^\]]+)\]\s*\u2192\s*(?<reaction>.+?)(?:\s+@\w+)?\s*$")]
    private static partial Regex LimiterPattern();

    // Transporter: CODE[state] @REGION
    // Matches: DAT[≈] @NAc
    //          SERT[↓] @DRN
    //          VMAT2[≈] @VTA
    [GeneratedRegex(@"(?<code>\w+)\[(?<state>[^\]]+)\]\s*(?:@(?<region>\w+))?")]
    private static partial Regex TransporterPattern();

    // Interface: REGION → REGION (pathway)
    // Matches: VTA → NAc (mesolimbic)
    [GeneratedRegex(@"^(?<src>[\w]+)\s*\u2192\s*(?<tgt>[\w]+)(?:\s*\((?<path>[^)]+)\))?")]
    private static partial Regex InterfacePattern();

    // Signal refs in formulas: CODE@REGION or CODE[state] @REGION
    // Matches: DA@VTA, GLU@PFC, DA[↑↑] @NAc
    [GeneratedRegex(@"(?<code>\w+)(?:@|\[.+?\]\s*@)(?<region>\w+)")]
    private static partial Regex SignalRefPattern();

    // Bare gate: SYMBOL[condition] or SYMBOL(condition) without surrounding braces
    // Matches: ⊨[TIME.ELAPSED <= 10s], ⊡(LATCHED), ⊛[novelty > 0.5]
    [GeneratedRegex(@"^(?<sym>[\u22A8\u22A1\u03A3\u229B\u22B3\u22BC\u22BD\u00AC\u2295\u2442])[\[\(](?<cond>[^\]\)]+)[\]\)]")]
    private static partial Regex BareGatePattern();

    // Gate expression: TYPE:CODE[state] @REGION?  — "H:CORT[↑]", "NT:DA[↑↑] @NAc"
    [GeneratedRegex(@"(?:(?:\w+):)?(?<code>[\w]+)\[(?<state>[^\]]+)\]\s*(?:@(?<region>\w+))?$")]
    private static partial Regex GateExprStatePattern();

    // Gate expression: CODE(@REGION)? op state  — "DA@NAc >= ↑↑", "CORT > baseline"
    [GeneratedRegex(@"(?<code>\w+)(?:@(?<region>\w+))?\s*(?<op>>=|<=|>|<|!=|=|\u2265|\u2264|\u2260)\s*(?<state>.+)$")]
    private static partial Regex GateExprComparePattern();

    // ── Signal Code Extraction ────────────────────────────────────────────────

    private static readonly HashSet<string> PatternSkipWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "FEEDBACK", "LOOP", "CASCADE", "MONITOR", "DIAGNOSER",
        "FEEDBACK_LOOP", "depth", "status"
    };

    /// <summary>
    /// Extracts plausible signal codes from a pattern description string.
    /// Filters out known keywords and requires mixed case + reasonable length.
    /// </summary>
    public static string[] ExtractSignalCodesFromPattern(string pattern)
    {
        var codes = new List<string>();
        var parts = pattern.Split([':', '\u2192', '-', '>', '(', ')', ' '],
            StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.Length >= 2 && trimmed.Length <= 30 &&
                trimmed.All(c => char.IsLetterOrDigit(c) || c == '.' || c == '_') &&
                trimmed.Any(char.IsUpper) &&
                !PatternSkipWords.Contains(trimmed))
            {
                codes.Add(trimmed);
            }
        }

        return codes.Distinct().Take(5).ToArray();
    }
}
