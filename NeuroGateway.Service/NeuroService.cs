using System.Text;
using NeuroGateway.AgentFramework;
using NeuroGateway.Models;
using NeuroGateway.Repository;

namespace NeuroGateway.Service;

public class NeuroService(
    ChatClient reasoningClient,
    ChatClient layerClient,
    AgentTemplateRepository templateRepo,
    AnalyzeService analyzeService)
{
    // 7 neurotransmitters, 10 hormones, 10 peptides = 27 total
    private static readonly Dictionary<string, string> ChemicalLayers = new(StringComparer.OrdinalIgnoreCase)
    {
        // neurotransmitter (7)
        ["dopamine"] = "neurotransmitter", ["serotonin"] = "neurotransmitter",
        ["norepinephrine"] = "neurotransmitter", ["gaba"] = "neurotransmitter",
        ["acetylcholine"] = "neurotransmitter", ["endocannabinoid"] = "neurotransmitter",
        ["glutamate"] = "neurotransmitter",
        // hormone (10)
        ["cortisol"] = "hormone", ["testosterone"] = "hormone", ["estradiol"] = "hormone",
        ["progesterone"] = "hormone", ["thyroid"] = "hormone", ["adrenaline"] = "hormone",
        ["melatonin"] = "hormone", ["dhea"] = "hormone", ["prolactin"] = "hormone",
        ["oxytocin_h"] = "hormone",
        // peptide (10)
        ["oxytocin"] = "peptide", ["vasopressin"] = "peptide", ["endorphins"] = "peptide",
        ["enkephalins"] = "peptide", ["dynorphin"] = "peptide", ["substance_p"] = "peptide",
        ["crh"] = "peptide", ["npy"] = "peptide", ["bdnf"] = "peptide", ["orexin"] = "peptide",
    };

    private static readonly HashSet<string> AllChemicals = new(ChemicalLayers.Keys, StringComparer.OrdinalIgnoreCase);

    public async Task<NeuroRespondResult> NeuroRespondAsync(
        string person,
        string text,
        string? relationship = null,
        string? projectedRelationship = null)
    {
        relationship ??= "unknown";

        // ── Step 1: Analyze (neuro LoRA via AnalyzeService) ──────────────────
        var decisions = await analyzeService.AnalyzeAsync(person, text, relationship);

        // Group ADD decisions by layer
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

        // Compute skipped chemicals
        var addedChemicals = new HashSet<string>(decisions.Select(d => d.Chemical), StringComparer.OrdinalIgnoreCase);
        var skipped = AllChemicals.Where(c => !addedChemicals.Contains(c)).Order().ToList();

        // ── Step 2: ReasoningSynthesizer (AgentReasoning) ────────────────────
        var synthesis = await RunReasoningSynthesizerAsync(person, relationship, layerDecisions, skipped);

        // ── Step 3: Layer agents (AgentLayer, parallel) ──────────────────────
        var layerResponses = await RunLayerAgentsAsync(
            person, text, relationship, projectedRelationship ?? relationship,
            layerDecisions, synthesis);

        // ── Step 4: Synthesizer (AgentLayer) ──────────────────────────────────
        var suggestedResponse = await RunSynthesizerAsync(
            person, relationship, layerResponses);

        return new NeuroRespondResult(decisions, synthesis, layerResponses, suggestedResponse);
    }

    private async Task<string> RunReasoningSynthesizerAsync(
        string person,
        string relationship,
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
        {
            // Fallback: try "relationship" as a default group
            templates = await templateRepo.GetByGroupAsync("relationship");
        }

        if (templates.Count == 0)
            return new Dictionary<string, string>
            {
                ["error"] = $"No neurochat templates found for relationship '{relationship}'"
            };

        // Separate layer agents from synthesizer
        var layerAgents = templates.Where(t => !t.IsSynthesizer).ToList();

        // Build agent definitions with per-layer user messages
        var agentDefs = new List<AgentDefinition>();
        foreach (var agent in layerAgents)
        {
            var layer = agent.Layer ?? "default";
            var chemicalProfile = BuildChemicalProfile(layer, layerDecisions);

            // The system prompt is the template's Role field (contains full prompt)
            agentDefs.Add(new AgentDefinition(agent.Name, agent.Role, layer));
        }

        // Build common user message parts
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

    private static string BuildChemicalProfile(
        string layer,
        Dictionary<string, List<AnalysisDecision>> layerDecisions)
    {
        if (!layerDecisions.TryGetValue(layer, out var decs) || decs.Count == 0)
            return "(none active)";

        return string.Join("\n", decs.Select(d => $"- {d.Chemical}: {d.Reasoning}"));
    }
}
