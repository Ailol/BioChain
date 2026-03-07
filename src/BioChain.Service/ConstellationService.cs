using System.Text.Json;
using BioChain.Repository.Repositories;
using BioChain.Utils.Parsing;
using Microsoft.Extensions.AI;

namespace BioChain.Service;

public class ConstellationService(
    IGraphQueryRepository graphQuery,
    IChatClient engine,
    LlmSemaphore llmSemaphore) : IConstellationService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // ── Known biochemical vocabulary ─────────────────────────────────────────
    // Compiled from TauConstants.json + common LLM aliases.
    // Signal nodes whose code is NOT in this set (e.g. ATTENTION, ACTION, BEHAVIOR)
    // are filtered out — they are behavioral abstractions hallucinated by the LLM,
    // not real neurochemical signals.

    private static readonly HashSet<string> KnownBiochemicalCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        // Neurotransmitters
        "DA", "5HT", "NE", "GABA", "GLU", "ACH", "GLYCINE", "HISTAMINE",
        "ADENOSINE", "ASPARTATE", "TAURINE", "ATP",
        "SEROTONIN", "DOPAMINE", "NOREPINEPHRINE", "GLUTAMATE",
        // Hormones
        "CORT", "CORTISOL", "CORTISONE", "ACTH", "CRH", "CRF", "TRH", "TSH",
        "T3", "T4", "GNRH", "ESTRADIOL", "PROGESTERONE", "TESTOSTERONE", "DHEA",
        "ALDOSTERONE", "ADH", "MELATONIN", "MEL", "INSULIN", "INS", "GLUCAGON",
        "LEPTIN", "GHRELIN", "PROLACTIN", "GH", "IGF1",
        "ADRENALINE", "EPINEPHRINE", "NORADRENALINE", "THYROID",
        // Neuropeptides
        "OXT", "OXYTOCIN", "AVP", "VASOPRESSIN", "BDNF", "NGF", "NPY",
        "SUBSTANCE_P", "SUBP", "VIP", "CCK", "OREXIN", "GALANIN",
        "DYNORPHIN", "ENKEPHALIN", "ENDORPHIN", "CGRP", "NEUROTENSIN",
        "SOMATOSTATIN", "ANP", "BNP", "ANGIOTENSIN_II",
        // Endocannabinoids
        "2AG", "AEA", "PEA", "OEA", "ANANDAMIDE", "ANA", "ECB",
        // Cytokines / Neuroimmune
        "IL1B", "IL1", "IL6", "IL10", "TNFA", "TNF", "IFNG", "IFN", "CRP",
        "TGFB", "IL4", "IL17", "HMGB1", "PGE2", "KYNURENINE", "QUINOLINIC_ACID",
        "IDO", "NFKB",
        // Neurosteroids
        "ALLOPREGNANOLONE", "ALLO", "THDOC", "PREGNENOLONE", "DHEAS", "ANDROSTANEDIOL",
    };

    /// <summary>
    /// Checks whether a signal code represents a known biochemical entity.
    /// Handles region-suffixed codes (e.g. DA_VTA → checks DA, CORT_HPA → checks CORT).
    /// </summary>
    private static bool IsKnownSignalCode(string code)
    {
        if (string.IsNullOrEmpty(code)) return false;
        if (KnownBiochemicalCodes.Contains(code)) return true;

        // Check base code before region suffix (DA_VTA → DA, 5HT_DRN → 5HT)
        var idx = code.IndexOf('_');
        return idx > 0 && KnownBiochemicalCodes.Contains(code[..idx]);
    }

    // ── Graph data (fast, DB-only) ──────────────────────────────────────────

    public async Task<ConstellationGraphResponse> GetGraphAsync(Guid subjectId, CancellationToken ct = default)
    {
        // Parallel DB calls
        var graphTask = graphQuery.ExportGraphJsonAsync(subjectId, ct);
        var loopsTask = graphQuery.FindFeedbackLoopsAsync(subjectId, ct);
        var cascadesTask = graphQuery.FindDysregCascadesAsync(subjectId, ct);
        var regionsTask = graphQuery.GetRegionActivityAsync(subjectId, ct);
        var bindsTask = graphQuery.GetBindEntriesAsync(subjectId, ct);

        await Task.WhenAll(graphTask, loopsTask, cascadesTask, regionsTask, bindsTask);

        var graphJson = await graphTask;
        var loops = await loopsTask;
        var cascades = await cascadesTask;
        var regions = await regionsTask;
        var binds = await bindsTask;

        // Build region_id → region_code mapping
        var regionIdToCode = regions.ToDictionary(r => r.RegionId, r => r.Code);

        // Parse export_graph_json output
        var (nodes, edges) = ParseGraphJson(graphJson, regionIdToCode);

        // Integrate BIND entries as virtual nodes (behavioral/functional composites)
        IntegrateBindNodes(binds, nodes, edges);

        // Assign community indices based on region grouping
        var communities = BuildCommunities(nodes, regions);

        // Compute bridges (nodes with cross-community edges)
        var bridges = ComputeBridges(nodes, edges);

        // Compute profile geometry
        var geometry = ComputeGeometry(nodes);

        return new ConstellationGraphResponse(
            nodes, edges, communities, loops, cascades, bridges, geometry);
    }

    // ── Deep analysis (slow, LLM-powered) ───────────────────────────────────

    public async Task<ConstellationAnalysisResponse> AnalyzeAsync(Guid subjectId, CancellationToken ct = default)
    {
        var dsl = await graphQuery.SerializeProfileDslAsync(subjectId, ct);

        // Get graph data for context
        var graphJson = await graphQuery.ExportGraphJsonAsync(subjectId, ct);
        var loops = await graphQuery.FindFeedbackLoopsAsync(subjectId, ct);
        var cascades = await graphQuery.FindDysregCascadesAsync(subjectId, ct);

        var context = BuildAnalysisContext(dsl, graphJson, loops, cascades);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, ConstellationAnalysisPrompt),
            new(ChatRole.User, context),
        };

        var options = new ChatOptions
        {
            MaxOutputTokens = 16384,
            Temperature = 0.4f,
            TopP = 0.9f,
        };

        var response = await llmSemaphore.RunAsync(
            () => engine.GetResponseAsync(messages, options, cancellationToken: ct), ct);

        var raw = StripThinkBlocks(response.Text ?? "{}");

        // Extract JSON from response (may be wrapped in ```json blocks)
        var json = ExtractJson(raw);

        try
        {
            var result = JsonSerializer.Deserialize<ConstellationAnalysisResponse>(json, JsonOpts);
            return result ?? ConstellationAnalysisResponse.Empty;
        }
        catch (JsonException)
        {
            return ConstellationAnalysisResponse.Empty;
        }
    }

    // ── Graph parsing ───────────────────────────────────────────────────────

    private static (List<ConstellationNode>, List<ConstellationEdge>) ParseGraphJson(
        string json, Dictionary<int, string> regionIdToCode)
    {
        var nodes = new List<ConstellationNode>();
        var edges = new List<ConstellationEdge>();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // nid (kind:id) → unique node key for edge resolution
        var nidToKey = new Dictionary<string, string>();

        if (root.TryGetProperty("nodes", out var nodesEl))
        {
            foreach (var n in nodesEl.EnumerateArray())
            {
                var kind = n.GetProp("kind");
                var code = n.GetProp("code");
                if (string.IsNullOrEmpty(code)) continue;

                // Filter out behavioral abstractions the LLM hallucinated as signals
                // (e.g. ATTENTION, ACTION, BEHAVIOR, COGNITION, FRUSTRATION)
                if (kind == "signal" && !IsKnownSignalCode(code)) continue;

                // Properties are nested inside a "properties" object
                var hasProps = n.TryGetProperty("properties", out var props) &&
                               props.ValueKind == JsonValueKind.Object;

                // Map region_id (int) → region_code (string)
                var regionCode = "";
                if (hasProps && props.TryGetProperty("region_id", out var rid) &&
                    rid.ValueKind == JsonValueKind.Number)
                {
                    regionIdToCode.TryGetValue(rid.GetInt32(), out regionCode!);
                    regionCode ??= "";
                }

                // Unique node key: "kind:dbId" (matches Neo4j nid pattern)
                var dbId = n.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.Number
                    ? idEl.GetInt32() : 0;
                var nodeKey = $"{kind}:{dbId}";
                nidToKey[nodeKey] = nodeKey;

                nodes.Add(new ConstellationNode(
                    Id: nodeKey,
                    Kind: kind,
                    Type: hasProps ? props.GetProp("type") : "",
                    Region: regionCode,
                    State: n.GetProp("state", "≈"),
                    Community: -1, // assigned later
                    Confidence: hasProps ? props.GetDecimal("confidence", 1.0m) : 1.0m,
                    TauMin: hasProps ? props.GetNullableLong("tau_min_ms") : null,
                    TauMax: hasProps ? props.GetNullableLong("tau_max_ms") : null,
                    Plasticity: hasProps ? props.GetNullableString("plasticity") : null,
                    Betweenness: 0, // computed below
                    Code: code,
                    Weight: 1
                ));
            }
        }

        if (root.TryGetProperty("edges", out var edgesEl))
        {
            foreach (var e in edgesEl.EnumerateArray())
            {
                var sourceType = e.GetProp("source_type", "signal");
                var targetType = e.GetProp("target_type", "signal");

                // Resolve to "kind:id" keys
                string source = "", target = "";
                if (e.TryGetProperty("source_id", out var si) && si.ValueKind == JsonValueKind.Number)
                    source = $"{sourceType}:{si.GetInt32()}";
                if (e.TryGetProperty("target_id", out var ti) && ti.ValueKind == JsonValueKind.Number)
                    target = $"{targetType}:{ti.GetInt32()}";

                if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target)) continue;
                // Only include edges whose endpoints exist in our node set
                if (!nidToKey.ContainsKey(source) || !nidToKey.ContainsKey(target)) continue;

                edges.Add(new ConstellationEdge(
                    Source: source,
                    Target: target,
                    Operator: e.GetProp("operator", "→"),
                    OperatorClass: e.GetProp("class", "causal"),
                    Gain: e.GetNullableDecimal("gain"),
                    DelayMs: e.GetNullableLong("delay_ms"),
                    DysregType: e.GetNullableString("dysreg_type"),
                    Active: e.GetBool("gate_active", true)
                ));
            }
        }

        // Deduplicate signal nodes: DA×11 → one node with Weight=11
        (nodes, edges) = DeduplicateSignals(nodes, edges);

        // Compute approximate betweenness from degree centrality
        var degree = new Dictionary<string, int>();
        foreach (var e in edges)
        {
            degree[e.Source] = degree.GetValueOrDefault(e.Source) + 1;
            degree[e.Target] = degree.GetValueOrDefault(e.Target) + 1;
        }
        var maxDeg = degree.Values.DefaultIfEmpty(1).Max();
        for (var i = 0; i < nodes.Count; i++)
        {
            var d = degree.GetValueOrDefault(nodes[i].Id);
            nodes[i] = nodes[i] with { Betweenness = maxDeg > 0 ? (decimal)d / maxDeg : 0 };
        }

        return (nodes, edges);
    }

    // ── Signal deduplication ─────────────────────────────────────────────────

    /// <summary>
    /// Merges duplicate signal nodes (same code+region) into a single node
    /// with Weight = occurrence count. Remaps and deduplicates edges accordingly.
    /// Non-signal nodes (receptor, transporter, limiter, gate, module) are untouched.
    /// </summary>
    private static (List<ConstellationNode>, List<ConstellationEdge>) DeduplicateSignals(
        List<ConstellationNode> nodes, List<ConstellationEdge> edges)
    {
        var signals = nodes.Where(n => n.Kind == "signal").ToList();
        var others = nodes.Where(n => n.Kind != "signal").ToList();

        if (signals.Count == 0) return (nodes, edges);

        var idRemap = new Dictionary<string, string>();
        var deduped = new List<ConstellationNode>();

        foreach (var group in signals.GroupBy(n => $"{n.Code}|{n.Region}".ToUpperInvariant()))
        {
            // Keep the node with the strongest state signal; weight = group count
            var best = group.OrderByDescending(n => Math.Abs(StateToValue(n.State))).First();
            deduped.Add(best with { Weight = group.Count() });
            foreach (var n in group)
                idRemap[n.Id] = best.Id;
        }

        var allNodes = deduped.Concat(others).ToList();

        // Remap edge endpoints and deduplicate
        var remappedEdges = new List<ConstellationEdge>();
        var seen = new HashSet<string>();
        foreach (var e in edges)
        {
            var src = idRemap.GetValueOrDefault(e.Source, e.Source);
            var tgt = idRemap.GetValueOrDefault(e.Target, e.Target);
            if (src == tgt) continue; // skip self-loops created by merging
            var key = $"{src}|{tgt}|{e.Operator}";
            if (!seen.Add(key)) continue; // skip duplicate edges
            remappedEdges.Add(e with { Source = src, Target = tgt });
        }

        return (allNodes, remappedEdges);
    }

    // ── BIND integration (behavioral composites as virtual nodes) ──────────

    /// <summary>
    /// Parses BIND entries from the analysis table and adds them as virtual nodes
    /// to the constellation graph. Each BIND node (kind="bind") represents a
    /// behavioral/functional outcome composed of real neurochemical signals.
    /// Edges connect constituent signals to the BIND node.
    /// </summary>
    private static void IntegrateBindNodes(
        List<BindEntryRow> binds, List<ConstellationNode> nodes, List<ConstellationEdge> edges)
    {
        if (binds.Count == 0) return;

        // Index existing signal nodes by code (uppercased) for matching
        var signalIndex = new Dictionary<string, List<ConstellationNode>>(StringComparer.OrdinalIgnoreCase);
        foreach (var n in nodes.Where(n => n.Kind == "signal"))
        {
            if (!signalIndex.TryGetValue(n.Code, out var list))
            {
                list = [];
                signalIndex[n.Code] = list;
            }
            list.Add(n);
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var bind in binds)
        {
            var parsed = BioChainParser.ExtractBind(bind.Formula);
            if (parsed is null) continue;

            var (name, sources) = parsed.Value;

            // Deduplicate BIND entries with the same name
            if (!seen.Add(name)) continue;

            var bindNodeId = $"bind:{bind.AnalysisId}";

            // Determine state from status text (simple heuristic)
            var state = InferBindState(bind.Status);

            nodes.Add(new ConstellationNode(
                Id: bindNodeId,
                Kind: "bind",
                Type: "B",    // Behavioral composite
                Region: "",   // Not region-specific
                State: state,
                Community: -1,
                Confidence: 1.0m,
                TauMin: null,
                TauMax: null,
                Plasticity: null,
                Betweenness: 0,
                Code: name,
                Weight: 1
            ));

            // Create edges from constituent signal nodes to the BIND node
            foreach (var (code, region) in sources)
            {
                if (!signalIndex.TryGetValue(code, out var candidates)) continue;

                // Match by code + region if possible, otherwise just code
                var match = region is not null
                    ? candidates.FirstOrDefault(n =>
                        n.Region.Equals(region, StringComparison.OrdinalIgnoreCase))
                      ?? candidates.First()
                    : candidates.First();

                edges.Add(new ConstellationEdge(
                    Source: match.Id,
                    Target: bindNodeId,
                    Operator: "⊃",        // "contributes to"
                    OperatorClass: "bind",
                    Gain: null,
                    DelayMs: null,
                    DysregType: null,
                    Active: true
                ));
            }
        }
    }

    /// <summary>
    /// Infers a state symbol from BIND status text.
    /// </summary>
    private static string InferBindState(string? status)
    {
        if (string.IsNullOrEmpty(status)) return "≈";
        var lower = status.ToLowerInvariant();
        if (lower.Contains("impair") || lower.Contains("deficit") || lower.Contains("deplet")
            || lower.Contains("compromis") || lower.Contains("suppress") || lower.Contains("blunt"))
            return "↓";
        if (lower.Contains("severe") || lower.Contains("collapse") || lower.Contains("crisis"))
            return "↓↓";
        if (lower.Contains("elevat") || lower.Contains("heighten") || lower.Contains("enhanc")
            || lower.Contains("increas"))
            return "↑";
        if (lower.Contains("hyper") || lower.Contains("excessive") || lower.Contains("runaway"))
            return "↑↑";
        return "≈";
    }

    // ── Community detection ──────────────────────────────────────────────────

    private static List<ConstellationCommunity> BuildCommunities(
        List<ConstellationNode> nodes, List<RegionActivityRow> regions)
    {
        // Group by region, assign community indices
        var regionCodes = regions.Select(r => r.Code).Distinct().OrderBy(c => c).ToList();
        var regionIndex = regionCodes.Select((c, i) => (c, i)).ToDictionary(x => x.c, x => x.i);
        var regionLookup = regions.ToDictionary(r => r.Code);

        for (var i = 0; i < nodes.Count; i++)
        {
            var region = nodes[i].Region;
            if (!string.IsNullOrEmpty(region) && regionIndex.TryGetValue(region, out var ci))
                nodes[i] = nodes[i] with { Community = ci };
            else
                nodes[i] = nodes[i] with { Community = regionCodes.Count }; // ungrouped
        }

        var communities = new List<ConstellationCommunity>();
        foreach (var (code, idx) in regionIndex)
        {
            var ra = regionLookup.GetValueOrDefault(code);
            var memberNodes = nodes.Where(n => n.Community == idx).Select(n => n.Id).ToList();

            var status = ra switch
            {
                { DysregCount: > 2 } or { Elevated: > 0, Depleted: > 2 } => "dysfunctional",
                { DysregCount: > 0 } or { ReceptorsImpaired: > 1 } => "impaired",
                { Elevated: > 0 } or { Depleted: > 0 } => "compensated",
                _ => "functional"
            };

            communities.Add(new ConstellationCommunity(
                Id: idx,
                Name: ra?.FullName ?? code,
                Code: code,
                Status: status,
                SignalCount: ra?.SignalCount ?? (long)memberNodes.Count,
                Elevated: ra?.Elevated ?? 0,
                Depleted: ra?.Depleted ?? 0,
                DysregCount: ra?.DysregCount ?? 0,
                Nodes: memberNodes
            ));
        }

        // Add ungrouped community if any nodes have no region
        var ungrouped = nodes.Where(n => n.Community == regionCodes.Count).Select(n => n.Id).ToList();
        if (ungrouped.Count > 0)
        {
            communities.Add(new ConstellationCommunity(
                Id: regionCodes.Count, Name: "Ungrouped", Code: "UNG",
                Status: "functional", SignalCount: ungrouped.Count,
                Elevated: 0, Depleted: 0, DysregCount: 0, Nodes: ungrouped));
        }

        return communities;
    }

    // ── Bridge detection ────────────────────────────────────────────────────

    private static List<ConstellationBridge> ComputeBridges(
        List<ConstellationNode> nodes, List<ConstellationEdge> edges)
    {
        var nodeCommunity = nodes.ToDictionary(n => n.Id, n => n.Community);
        var crossEdges = new Dictionary<string, HashSet<int>>();
        var crossCounts = new Dictionary<string, int>();

        foreach (var e in edges)
        {
            if (!nodeCommunity.TryGetValue(e.Source, out var sc) ||
                !nodeCommunity.TryGetValue(e.Target, out var tc)) continue;
            if (sc == tc) continue;

            if (!crossEdges.ContainsKey(e.Source)) crossEdges[e.Source] = [];
            if (!crossEdges.ContainsKey(e.Target)) crossEdges[e.Target] = [];
            crossEdges[e.Source].Add(tc);
            crossEdges[e.Target].Add(sc);
            crossCounts[e.Source] = crossCounts.GetValueOrDefault(e.Source) + 1;
            crossCounts[e.Target] = crossCounts.GetValueOrDefault(e.Target) + 1;
        }

        return crossEdges
            .Where(kv => kv.Value.Count >= 2)
            .OrderByDescending(kv => kv.Value.Count)
            .Take(10)
            .Select(kv => new ConstellationBridge(
                Node: kv.Key,
                Between: kv.Value.ToList(),
                CrossEdges: crossCounts.GetValueOrDefault(kv.Key)))
            .ToList();
    }

    // ── Geometry computation ────────────────────────────────────────────────

    private static ConstellationGeometry ComputeGeometry(List<ConstellationNode> nodes)
    {
        if (nodes.Count == 0)
            return new ConstellationGeometry("Empty", 0, 1, 0, 0);

        var stateValues = nodes.Select(n => StateToValue(n.State)).ToList();
        var elevated = stateValues.Count(v => v > 0);
        var depleted = stateValues.Count(v => v < 0);
        var neutral = stateValues.Count(v => v == 0);
        var total = (decimal)nodes.Count;

        var activation = total > 0 ? elevated / total : 0;
        var depletion = total > 0 ? depleted / total : 0;
        var polarization = Math.Abs(activation - depletion);
        var entropy = total > 0 ? neutral / total : 1;
        var sharpness = stateValues.Count(v => Math.Abs(v) >= 0.8m) / total;

        var shape = polarization > 0.5m ? "Polarized" :
                    sharpness > 0.3m ? "Spiked" :
                    entropy > 0.6m ? "Diffuse" : "Mixed";

        return new ConstellationGeometry(shape, sharpness, entropy, polarization, 1 - entropy);
    }

    private static decimal StateToValue(string state) => state switch
    {
        "↑↑" => 1, "↑" => 0.5m, "≈" => 0, "↓" => -0.5m, "↓↓" => -1,
        "active" => 0.2m, "desens" => -0.3m, "intern" => -0.5m,
        "upreg" => 0.4m, "downreg" => -0.4m, "resist" => -0.6m,
        _ => 0
    };

    // ── Analysis context builder ────────────────────────────────────────────

    private static string BuildAnalysisContext(
        string dsl, string graphJson, List<FeedbackLoopRow> loops, List<DysregCascadeRow> cascades)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# NEUROCHEMICAL PROFILE DSL");
        sb.AppendLine(dsl);
        sb.AppendLine();
        sb.AppendLine("# DETECTED FEEDBACK LOOPS");
        foreach (var l in loops)
            sb.AppendLine($"  {(l.IsPositive ? "⟳⁺" : "⟳⁻")} {string.Join(" → ", l.LoopPath)}  [{string.Join(", ", l.Operators)}]");
        sb.AppendLine();
        sb.AppendLine("# DYSREGULATION CASCADES");
        foreach (var c in cascades)
            sb.AppendLine($"  {c.RootCode} ({c.DysregType}) depth={c.CascadeDepth}: {string.Join(" → ", c.AffectedPath)}");
        return sb.ToString();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string StripThinkBlocks(string raw)
    {
        if (!raw.Contains("</think>")) return raw;
        while (raw.Contains("<think>") && raw.Contains("</think>"))
        {
            var start = raw.IndexOf("<think>", StringComparison.Ordinal);
            var end = raw.IndexOf("</think>", StringComparison.Ordinal) + "</think>".Length;
            if (start >= 0 && end > start) raw = (raw[..start] + raw[end..]).TrimStart();
            else break;
        }
        return raw.StartsWith("</think>") ? raw["</think>".Length..].TrimStart() : raw;
    }

    private static string ExtractJson(string raw)
    {
        // Try to extract JSON from ```json blocks
        var jsonStart = raw.IndexOf("```json", StringComparison.Ordinal);
        if (jsonStart >= 0)
        {
            var contentStart = raw.IndexOf('\n', jsonStart) + 1;
            var jsonEnd = raw.IndexOf("```", contentStart, StringComparison.Ordinal);
            if (jsonEnd > contentStart)
                return raw[contentStart..jsonEnd].Trim();
        }

        // Try to find raw JSON object
        var braceStart = raw.IndexOf('{');
        var braceEnd = raw.LastIndexOf('}');
        if (braceStart >= 0 && braceEnd > braceStart)
            return raw[braceStart..(braceEnd + 1)];

        return raw;
    }

    // ── LLM System Prompt ───────────────────────────────────────────────────

    private const string ConstellationAnalysisPrompt = """
        You are a neurochemical systems analyst. Given a subject's biochemical signal graph (as DSL), feedback loops, and dysregulation cascades, produce a comprehensive analysis in JSON format.

        Return ONLY valid JSON matching this exact schema:
        {
          "communities": [
            {
              "id": 0,
              "name": "Community Name",
              "status": "functional|compensated|impaired|dysfunctional|collapsed",
              "summary": "Brief functional summary",
              "whenWorking": "Description of healthy function",
              "whenBroken": "Description of current dysfunction",
              "fix": [
                {
                  "action": "Specific intervention",
                  "target": "Target signals",
                  "why": "Mechanism of action",
                  "priority": "critical|high|medium|low"
                }
              ]
            }
          ],
          "narratives": [
            {
              "id": "n1",
              "formula": "signal.pathway expression",
              "title": "Human-readable title",
              "nodes": ["NODE1", "NODE2"],
              "text": "Detailed explanation",
              "load": 0.0-1.0,
              "controlEffort": 0.0-1.0,
              "fragility": 0.0-1.0
            }
          ],
          "contradictions": [
            {
              "id": "c1",
              "surface": ["Observable statement 1", "Contradicting statement 2"],
              "resolution": "Graph-based explanation of why both are true",
              "nodes": ["NODE1", "NODE2"],
              "tension": 0.0-1.0
            }
          ],
          "compensators": [
            {
              "id": "comp1",
              "what": "What is compensating",
              "masking": "What it hides",
              "cost": "Metabolic/resource cost",
              "fragility": "What breaks it",
              "nodes": ["NODE1", "NODE2"],
              "costScore": 0.0-1.0
            }
          ],
          "motifs": [
            {
              "id": "m1",
              "name": "Motif Name",
              "pattern": "Abstract pattern description",
              "instances": [
                { "path": ["N1","N2","N3"], "label": "Instance label" }
              ],
              "meaning": "What this pattern means"
            }
          ],
          "architecture": [
            {
              "id": "a1",
              "title": "Trajectory title",
              "frame": "Frame label",
              "text": "Detailed explanation",
              "nodes": ["NODE1", "NODE2"],
              "severity": "structural|critical|high|opportunity"
            }
          ],
          "perturbations": {
            "Intervention Name": {
              "targets": [
                { "node": "NODE", "delta": "+1", "delay": "timeframe", "mechanism": "How it works" }
              ],
              "llm": "Graph-grounded analysis of this intervention"
            }
          },
          "humanLabels": {
            "NODE_CODE": "Human-readable name"
          }
        }

        Guidelines:
        - Use actual signal codes from the DSL (e.g., CORT, DA, 5HT, NE, BDNF)
        - Communities should group functionally related signals
        - Status should reflect the aggregate state of signals in each community
        - Narratives should describe behavioral equations emerging from the graph
        - Contradictions should identify conflicting surface observations resolved by the graph
        - Compensators should identify hidden load-bearing mechanisms
        - Motifs should identify recurring subgraph patterns
        - Architecture should describe system trajectory if current state continues
        - Perturbations should suggest 3-5 specific interventions with node-level deltas
        - Human labels should provide plain-language names for all signal codes
        """;
}

// ── Response DTOs ────────────────────────────────────────────────────────

public record ConstellationNode(
    string Id, string Kind, string Type, string Region, string State,
    int Community, decimal Confidence,
    long? TauMin, long? TauMax, string? Plasticity, decimal Betweenness,
    string Code = "", int Weight = 1);

public record ConstellationEdge(
    string Source, string Target, string Operator, string OperatorClass,
    decimal? Gain, long? DelayMs, string? DysregType, bool Active);

public record ConstellationCommunity(
    int Id, string Name, string Code, string Status,
    long SignalCount, long Elevated, long Depleted, long DysregCount,
    List<string> Nodes);

public record ConstellationBridge(string Node, List<int> Between, int CrossEdges);

public record ConstellationGeometry(
    string Shape, decimal Sharpness, decimal Entropy, decimal Polarization, decimal Fragmentation);

public record ConstellationGraphResponse(
    List<ConstellationNode> Nodes,
    List<ConstellationEdge> Edges,
    List<ConstellationCommunity> Communities,
    List<FeedbackLoopRow> FeedbackLoops,
    List<DysregCascadeRow> DysregCascades,
    List<ConstellationBridge> Bridges,
    ConstellationGeometry Geometry);

// ── Analysis response (deserialized from LLM JSON) ──────────────────────

public record ConstellationAnalysisResponse(
    List<AnalysisCommunity>? Communities,
    List<AnalysisNarrative>? Narratives,
    List<AnalysisContradiction>? Contradictions,
    List<AnalysisCompensator>? Compensators,
    List<AnalysisMotif>? Motifs,
    List<AnalysisArchitecture>? Architecture,
    Dictionary<string, AnalysisPerturbation>? Perturbations,
    Dictionary<string, string>? HumanLabels)
{
    public static readonly ConstellationAnalysisResponse Empty = new(null, null, null, null, null, null, null, null);
}

public record AnalysisCommunity(
    int Id, string Name, string Status, string Summary,
    string WhenWorking, string WhenBroken, List<AnalysisFix>? Fix);

public record AnalysisFix(string Action, string Target, string Why, string Priority);

public record AnalysisNarrative(
    string Id, string Formula, string Title, List<string> Nodes,
    string Text, decimal Load, decimal ControlEffort, decimal Fragility);

public record AnalysisContradiction(
    string Id, List<string> Surface, string Resolution, List<string> Nodes, decimal Tension);

public record AnalysisCompensator(
    string Id, string What, string Masking, string Cost,
    string Fragility, List<string> Nodes, decimal CostScore);

public record AnalysisMotif(
    string Id, string Name, string Pattern, List<MotifInstance> Instances, string Meaning);

public record MotifInstance(List<string> Path, string Label);

public record AnalysisArchitecture(
    string Id, string Title, string Frame, string Text, List<string> Nodes, string Severity);

public record AnalysisPerturbation(List<PerturbationTarget> Targets, string Llm);

public record PerturbationTarget(string Node, string Delta, string Delay, string Mechanism);

// ── JsonElement helpers ─────────────────────────────────────────────────

internal static class JsonElementExt
{
    public static string GetProp(this JsonElement el, string name, string fallback = "")
    {
        if (el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String)
            return v.GetString() ?? fallback;
        return fallback;
    }

    public static decimal GetDecimal(this JsonElement el, string name, decimal fallback)
    {
        if (el.TryGetProperty(name, out var v))
        {
            if (v.ValueKind == JsonValueKind.Number) return v.GetDecimal();
            if (v.ValueKind == JsonValueKind.String && decimal.TryParse(v.GetString(), out var d)) return d;
        }
        return fallback;
    }

    public static decimal? GetNullableDecimal(this JsonElement el, string name)
    {
        if (el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number)
            return v.GetDecimal();
        return null;
    }

    public static long? GetNullableLong(this JsonElement el, string name)
    {
        if (el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number)
            return v.GetInt64();
        return null;
    }

    public static string? GetNullableString(this JsonElement el, string name)
    {
        if (el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String)
            return v.GetString();
        return null;
    }

    public static bool GetBool(this JsonElement el, string name, bool fallback)
    {
        if (el.TryGetProperty(name, out var v))
        {
            if (v.ValueKind is JsonValueKind.True) return true;
            if (v.ValueKind is JsonValueKind.False) return false;
        }
        return fallback;
    }
}
