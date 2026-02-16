using System.Text;
using NeuroGateway.AgentFramework;
using NeuroGateway.AnalysisFramework;
using NeuroGateway.Models;
using NeuroGateway.Repository;

namespace NeuroGateway.Service;

public class NeuroService(
    ChatClient orchestratorClient,
    ChatClient reasoningClient,
    ChatClient layerClient,
    AgentTemplateRepository templateRepo,
    AnalyzeService analyzeService,
    DimensionService dimensionService)
{
    private const int ChunkThreshold = 500; // chars — short messages skip chunking

    private static readonly IReadOnlyDictionary<string, string> ChemicalLayers = DimensionDefinitions.ChemicalToLayer;
    private static readonly HashSet<string> AllChemicals = new(ChemicalLayers.Keys, StringComparer.OrdinalIgnoreCase);

    // ── Chat: full 4-step pipeline with suggested response ───────────────────

    public async Task<ChatRespondResult> ChatRespondAsync(
        string person,
        string text,
        string? relationship = null,
        string? projectedRelationship = null,
        bool save = true)
    {
        relationship ??= "unknown";

        var (decisions, layerDecisions, skipped) = await AnalyzeAndGroupAsync(person, text, relationship, "chat", save);
        var synthesis = await RunReasoningSynthesizerAsync(person, relationship, "chat", layerDecisions, skipped);

        var layerResponses = await RunLayerAgentsAsync(
            person, text, relationship, projectedRelationship ?? relationship,
            layerDecisions, synthesis);

        var suggestedResponse = await RunSynthesizerAsync(person, relationship, layerResponses);

        return new ChatRespondResult(decisions, synthesis, layerResponses, suggestedResponse);
    }

    // ── Work: 27 agents + reasoning synthesis (no layer agents) ──────────────

    public async Task<AnalysisResult> WorkAnalyzeAsync(
        string person,
        string text,
        string? relationship = null,
        bool save = true)
    {
        relationship ??= "unknown";

        var (decisions, layerDecisions, skipped) = await AnalyzeAndGroupAsync(person, text, relationship, "work", save);
        var synthesis = await RunReasoningSynthesizerAsync(person, relationship, "work", layerDecisions, skipped);

        return new AnalysisResult(decisions, synthesis);
    }

    // ── Journal: 27 agents + reasoning synthesis (no relationship) ───────────

    public async Task<AnalysisResult> JournalAnalyzeAsync(
        string person,
        string text,
        bool save = true)
    {
        var (decisions, layerDecisions, skipped) = await AnalyzeAndGroupAsync(person, text, "self", "journal", save);
        var synthesis = await RunReasoningSynthesizerAsync(person, "self", "journal", layerDecisions, skipped);

        return new AnalysisResult(decisions, synthesis);
    }

    // ── Shared: analyze + group by layer ─────────────────────────────────────

    private async Task<(List<AnalysisDecision> Decisions, Dictionary<string, List<AnalysisDecision>> LayerDecisions, List<string> Skipped)>
        AnalyzeAndGroupAsync(string person, string text, string relationship, string sourceType, bool save)
    {
        // Chunk long documents via orchestrator, short messages pass through directly
        var chunks = text.Length > ChunkThreshold
            ? await ChunkDocumentAsync(text)
            : [text];

        var allDecisions = new List<AnalysisDecision>();
        foreach (var chunk in chunks)
        {
            var decisions = await analyzeService.AnalyzeAsync(
                person, chunk, relationship, sourceType: sourceType, save: save);
            allDecisions.AddRange(decisions);
        }

        // Deduplicate: keep the decision with longest reasoning per chemical
        var merged = allDecisions
            .GroupBy(d => d.Chemical, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(d => d.Reasoning.Length).First())
            .ToList();

        var layerDecisions = new Dictionary<string, List<AnalysisDecision>>
        {
            ["neurotransmitter"] = [],
            ["hormone"] = [],
            ["peptide"] = []
        };

        foreach (var d in merged)
        {
            if (ChemicalLayers.TryGetValue(d.Chemical, out var layer))
                layerDecisions[layer].Add(d);
        }

        var addedChemicals = new HashSet<string>(merged.Select(d => d.Chemical), StringComparer.OrdinalIgnoreCase);
        var skipped = AllChemicals.Where(c => !addedChemicals.Contains(c)).Order().ToList();

        return (merged, layerDecisions, skipped);
    }

    // ── Document chunking via orchestrator LLM ─────────────────────────────

    private async Task<List<string>> ChunkDocumentAsync(string document)
    {
        const string systemPrompt = """
            You are a document chunking assistant.
            Split the following document into logical sections.
            Each section should be a self-contained piece of content (e.g., a single job role, education block, project description, or skills section).

            CRITICAL: Each chunk must be SHORT — maximum 800 characters (~200 words / ~400 tokens).
            If a section is longer than 800 characters, split it into smaller sub-sections.
            The downstream model has a 2048 token context limit, so smaller chunks are better.

            Return ONLY the sections, separated by ---CHUNK--- markers.
            Do not add commentary or labels. Preserve the original text exactly.
            """;

        try
        {
            var response = await orchestratorClient.SendAsync(systemPrompt, document);
            var chunks = response
                .Split("---CHUNK---", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(c => c.Length > 20)
                .ToList();

            return chunks.Count > 0 ? chunks : [document];
        }
        catch
        {
            return [document]; // fallback: analyze full document as single chunk
        }
    }

    // ── Step 2: ReasoningSynthesizer ─────────────────────────────────────────

    private async Task<string> RunReasoningSynthesizerAsync(
        string person,
        string relationship,
        string sourceType,
        Dictionary<string, List<AnalysisDecision>> layerDecisions,
        List<string> skipped)
    {
        var templates = await templateRepo.GetByCategoryAsync("reasoning_synthesizer");
        if (templates.Count == 0)
            return "[ReasoningSynthesizer template not found in DB]";

        var template = templates[0];

        var sb = new StringBuilder();
        sb.AppendLine($"person: {person}");
        sb.AppendLine($"relationship: {relationship}");
        sb.AppendLine($"source_type: {sourceType}");
        sb.AppendLine("layer_summary:");

        foreach (var (layer, layerDecisionList) in layerDecisions)
        {
            sb.AppendLine($"  {layer}:");
            if (layerDecisionList.Count == 0)
            {
                sb.AppendLine("    (none active)");
            }
            else
            {
                foreach (var d in layerDecisionList)
                    sb.AppendLine($"    - {d.Chemical}: {d.Reasoning}");
            }
        }

        sb.AppendLine($"skipped: {string.Join(", ", skipped)}");

        try
        {
            return await orchestratorClient.SendAsync(template.Role, sb.ToString());
        }
        catch (Exception ex)
        {
            return $"[ReasoningSynthesizer error: {ex.Message}]";
        }
    }

    // ── Step 3: Layer agents (chat mode only) ────────────────────────────────

    private async Task<Dictionary<string, string>> RunLayerAgentsAsync(
        string person,
        string text,
        string relationship,
        string projectedRelationship,
        Dictionary<string, List<AnalysisDecision>> layerDecisions,
        string synthesis)
    {
        var templates = await templateRepo.GetByGroupAsync(relationship);
        if (templates.Count == 0)
            templates = await templateRepo.GetByGroupAsync("relationship");

        if (templates.Count == 0)
            return new Dictionary<string, string>
            {
                ["error"] = $"No neurochat templates found for relationship '{relationship}'"
            };

        var layerAgents = templates.Where(t => !t.IsSynthesizer).ToList();

        var agentDefs = new List<AgentDefinition>();
        foreach (var agent in layerAgents)
        {
            var layer = agent.Layer ?? "default";
            agentDefs.Add(new AgentDefinition(agent.Name, agent.Role, layer));
        }

        var userMessage = BuildLayerUserMessage(
            person, text, relationship, projectedRelationship,
            layerDecisions, synthesis);

        try
        {
            var results = await Orchestrator.RunAllAsync(layerClient, agentDefs, userMessage, 3);
            return results
                .Where(r => r.Success)
                .ToDictionary(r => r.Layer ?? r.AgentName, r => r.RawResponse);
        }
        catch (Exception ex)
        {
            return new Dictionary<string, string>
            {
                ["error"] = $"Layer agents error: {ex.Message}"
            };
        }
    }

    // ── Step 4: Synthesizer (chat mode only) ─────────────────────────────────

    private async Task<string> RunSynthesizerAsync(
        string person,
        string relationship,
        Dictionary<string, string> layerResponses)
    {
        var templates = await templateRepo.GetByGroupAsync(relationship);
        if (templates.Count == 0)
            templates = await templateRepo.GetByGroupAsync("relationship");

        var synthesizer = templates.FirstOrDefault(t => t.IsSynthesizer);
        if (synthesizer is null)
            return "[Synthesizer template not found]";

        var sb = new StringBuilder();
        sb.AppendLine($"person: {person}");
        sb.AppendLine($"relationship: {relationship}");
        sb.AppendLine("layer_suggestions:");

        foreach (var (layer, response) in layerResponses)
        {
            if (layer == "error") continue;
            sb.AppendLine($"  {layer}:");
            sb.AppendLine($"    {response}");
        }

        try
        {
            return await orchestratorClient.SendAsync(synthesizer.Role, sb.ToString());
        }
        catch (Exception ex)
        {
            return $"[Synthesizer error: {ex.Message}]";
        }
    }

    // ── Orchestrator chat: direct multi-turn conversation ─────────────────

    public async Task<string> OrchestratorChatAsync(
        string person,
        List<Microsoft.Extensions.AI.ChatMessage> messages,
        CancellationToken ct = default)
    {
        var dims = await dimensionService.ScoreAsync(person);
        var systemPrompt = BuildOrchestratorSystemPrompt(person, dims);

        var fullMessages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(Microsoft.Extensions.AI.ChatRole.System, systemPrompt)
        };
        fullMessages.AddRange(messages);

        return await orchestratorClient.SendAsync(fullMessages, ct);
    }

    private static string BuildOrchestratorSystemPrompt(string person, List<DimensionScore> dims)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"You are a biochemical personality analyst. You have deep knowledge of {person}'s psychological profile based on 27-agent neurochemical analysis.");
        sb.AppendLine($"When the user asks about \"{person}\" or uses pronouns like \"their\"/\"them\", they are referring to this person.");
        sb.AppendLine();
        sb.AppendLine($"## {person}'s Dimension Profile");
        sb.AppendLine();

        var grouped = dims
            .Where(d => d.EvidenceCount > 0)
            .GroupBy(d => d.Section)
            .OrderBy(g => g.Key);

        foreach (var section in grouped)
        {
            sb.AppendLine($"### {section.Key}");
            foreach (var d in section.OrderByDescending(d => d.Score))
            {
                sb.Append($"- **{d.Name}**: score={d.Score}/100, confidence={d.Confidence:F2}, consistency={d.Consistency:F2}, evidence={d.EvidenceCount}");
                if (d.Trajectory is { } t)
                    sb.Append($", trend={t.Direction} ({t.Slope:+0.00;-0.00}/day, R²={t.R2:F2})");
                if (d.Circuit is { } c)
                    sb.Append($", circuit={c.Pattern} ({c.CoherenceScore:F2})");
                sb.AppendLine();

                // Include top 3 chemical evidence entries (reasoning is the most valuable signal)
                foreach (var ev in d.Evidence.OrderByDescending(e => e.Recency).Take(3))
                    sb.AppendLine($"  - [{ev.Layer}/{ev.Chemical}] L{ev.Level:F1}: {ev.Reasoning}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("Answer questions about this person's profile with specificity. Reference the biochemical evidence, dimension scores, trajectories, and circuit patterns. Be concise but insightful.");
        return sb.ToString();
    }

    private static string BuildLayerUserMessage(
        string person,
        string text,
        string relationship,
        string projectedRelationship,
        Dictionary<string, List<AnalysisDecision>> layerDecisions,
        string synthesis)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"name: {person}");
        sb.AppendLine($"current_relationship: {relationship}");
        sb.AppendLine($"projected_relationship: {projectedRelationship}");
        sb.AppendLine($"message: {text}");
        sb.AppendLine("chemical_profile:");

        foreach (var (layer, decs) in layerDecisions)
        {
            if (decs.Count == 0) continue;
            sb.AppendLine($"  {layer}:");
            foreach (var d in decs)
                sb.AppendLine($"    - {d.Chemical}: {d.Reasoning}");
        }

        sb.AppendLine($"analysis: {synthesis}");
        return sb.ToString();
    }
}
