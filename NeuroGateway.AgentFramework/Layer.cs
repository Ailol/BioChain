using Microsoft.Extensions.AI;
using NeuroGateway.Models;
using NeuroGateway.Repository;
using NeuroGateway.Utils;

namespace NeuroGateway.AgentFramework;

/// <summary>
/// Biochemical layer response generation: 3+1 agents (neurotransmitter, hormone, peptide + synthesizer).
/// Each layer agent sees only its own biochemical profile via {chemicals}.
/// Agents output HERE/SHIFT/SUGGEST format. Synthesizer sees all 3 + gap map.
/// </summary>
public class Layer
{
    private readonly LlmService _llm;
    private readonly Lazy<Task<Dictionary<string, ResponderGroupConfig>>> _neuroChatAgents;

    public Layer(LlmService llm, AgentTemplateRepository templateRepo)
    {
        _llm = llm;
        _neuroChatAgents = new(() => templateRepo.GetNeuroChatAgentsAsync());
    }

    /// <summary>
    /// Get neurochat agent profiles for a specific responder group (e.g., Dating, Friend).
    /// </summary>
    public async Task<Dictionary<string, AgentProfile>?> GetNeuroChatProfilesAsync(ResponderGroup group)
    {
        var chatAgents = await _neuroChatAgents.Value;
        if (!chatAgents.TryGetValue(group.ToString(), out var groupConfig))
            return null;

        return groupConfig.Agents.ToDictionary(a => a.Name, a => new AgentProfile
        {
            Role = a.Role, Style = a.Style, MaxWords = a.MaxWords,
            Conclusion = a.IsSynthesizer, Layer = a.Layer
        });
    }

    /// <summary>
    /// Run 3+1 biochemical layer agents in parallel (NT, hormone, peptide) + synthesizer.
    /// Each agent sees ONLY its own layer profile via {chemicals}.
    /// Per-layer relationship estimates feed into the synthesizer gap map.
    /// </summary>
    public async Task<List<NeuroResponse>> RunLayerResponseAsync(
        Dictionary<string, AgentProfile> profiles,
        string topic,
        string ntProfile, string hormoneProfile, string peptideProfile,
        string? communicationStyle = null,
        string? ntEstimate = null, string? hormoneEstimate = null, string? peptideEstimate = null,
        string? chosenRelationship = null)
    {
        var synthesizer = profiles.FirstOrDefault(p => p.Value.Conclusion);
        var layerAgents = profiles.Where(p => !p.Value.Conclusion).ToList();

        static string LabelFor(string? layer) => layer?.ToLowerInvariant() switch
        {
            "neurotransmitter" => "Neurotransmitters", "hormone" => "Hormones", "peptide" => "Peptides", _ => "Unknown"
        };

        // Per-layer profile routing: each agent sees only its own layer
        string GetOwnLayerProfile(string? layer) => layer?.ToLowerInvariant() switch
        {
            "neurotransmitter" => ntProfile, "hormone" => hormoneProfile, "peptide" => peptideProfile, _ => ntProfile
        };

        string GetEstimateForLayer(string? layer) => (layer?.ToLowerInvariant() switch
        {
            "neurotransmitter" => ntEstimate, "hormone" => hormoneEstimate, "peptide" => peptideEstimate, _ => null
        }) ?? "Unknown";

        var commStyle = communicationStyle ?? "Not yet determined";
        var chosenRel = chosenRelationship ?? "Unknown";

        var tasks = layerAgents.Select(async kv =>
        {
            var label = LabelFor(kv.Value.Layer);
            var estimate = GetEstimateForLayer(kv.Value.Layer);
            try
            {
                // Replace per-layer placeholders
                var style = kv.Value.Style
                    .Replace("{chemicals}", GetOwnLayerProfile(kv.Value.Layer))
                    .Replace("{communication_style}", commStyle)
                    .Replace("{estimated_relationship}", estimate)
                    .Replace("{chosen_relationship}", chosenRel);

                var userMsg = topic;
                var response = await _llm.ChatWithProfileAsync(
                    kv.Value.WithStyle(style),
                    [new(ChatRole.User, userMsg)]);

                var (here, shift, suggest) = ResponseService.ParseAgentResponse(response);
                return new NeuroResponse(label, suggest ?? response.Trim(),
                    Here: here, Shift: shift, EstimatedRelationship: estimate);
            }
            catch { return new NeuroResponse(label, "[Agent failed to respond]"); }
        });

        var layerResults = (await Task.WhenAll(tasks)).ToList();

        if (synthesizer.Key != null)
        {
            // Build full text from each layer result (HERE + SHIFT + SUGGEST)
            var ntResult = layerResults.FirstOrDefault(r => r.Source == "Neurotransmitters");
            var hormoneResult = layerResults.FirstOrDefault(r => r.Source == "Hormones");
            var peptideResult = layerResults.FirstOrDefault(r => r.Source == "Peptides");

            static string FullText(NeuroResponse? r) => r == null ? "[No response]" :
                $"HERE: {r.Here ?? "N/A"}\nSHIFT: {r.Shift ?? "N/A"}\nSUGGEST: {r.Message}";

            var synthStyle = synthesizer.Value.Style
                .Replace("{nt_response}", FullText(ntResult))
                .Replace("{hormone_response}", FullText(hormoneResult))
                .Replace("{peptide_response}", FullText(peptideResult))
                .Replace("{nt_estimate}", ntEstimate ?? "Unknown")
                .Replace("{hormone_estimate}", hormoneEstimate ?? "Unknown")
                .Replace("{peptide_estimate}", peptideEstimate ?? "Unknown")
                .Replace("{communication_style}", commStyle)
                .Replace("{chosen_relationship}", chosenRel);

            try
            {
                var response = await _llm.ChatWithProfileAsync(
                    synthesizer.Value.WithStyle(synthStyle),
                    [new(ChatRole.User, topic)]);

                var (_, synthShift, synthSuggest) = ResponseService.ParseAgentResponse(response);
                layerResults.Add(new NeuroResponse("Synthesizer",
                    synthSuggest ?? response.Trim(), Shift: synthShift));
            }
            catch { layerResults.Add(new NeuroResponse("Synthesizer", "[Synthesizer failed to respond]")); }
        }

        return layerResults;
    }
}
