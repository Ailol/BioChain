using System.Text;
using NeuroGateway.AgentFramework;
using NeuroGateway.AnalysisFramework;
using NeuroGateway.Models;
using NeuroGateway.Repository;

namespace NeuroGateway.Service;

public class NeuroService(
    ChatClient reasoningClient,
    ChatClient layerClient,
    AgentTemplateRepository templateRepo,
    AnalyzeService analyzeService)
{
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
        var decisions = await analyzeService.AnalyzeAsync(person, text, relationship, sourceType: sourceType, save: save);

        var layerDecisions = new Dictionary<string, List<AnalysisDecision>>
        {
            ["neurotransmitter"] = [],
            ["hormone"] = [],
            ["peptide"] = []
        };

        foreach (var d in decisions)
        {
            if (ChemicalLayers.TryGetValue(d.Chemical, out var layer))
                layerDecisions[layer].Add(d);
        }

        var addedChemicals = new HashSet<string>(decisions.Select(d => d.Chemical), StringComparer.OrdinalIgnoreCase);
        var skipped = AllChemicals.Where(c => !addedChemicals.Contains(c)).Order().ToList();

        return (decisions, layerDecisions, skipped);
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
            return await reasoningClient.SendAsync(template.Role, sb.ToString());
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
            return await layerClient.SendAsync(synthesizer.Role, sb.ToString());
        }
        catch (Exception ex)
        {
            return $"[Synthesizer error: {ex.Message}]";
        }
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
