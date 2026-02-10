using Microsoft.Extensions.AI;
using NeuroGateway.Models;
using NeuroGateway.Repository;

namespace NeuroGateway.AgentFramework;

/// <summary>
/// Biochemical 3-layer SKIP/ADD agent analysis.
/// Loads agent templates from DB, runs LLM calls in batched parallel, parses ADD/SKIP responses.
/// </summary>
public class Analyze
{
    private readonly LlmService _llm;
    private readonly int _maxParallelAgents;

    private readonly Lazy<Task<Dictionary<string, AgentProfile>>> _neuroAgents;
    private readonly Lazy<Task<Dictionary<string, AgentProfile>>> _hormoneAgents;
    private readonly Lazy<Task<Dictionary<string, AgentProfile>>> _peptideAgents;

    public Analyze(LlmService llm, AgentTemplateRepository templateRepo, AgentConfiguration config)
    {
        _llm = llm;
        _maxParallelAgents = config.MaxParallelAgents;

        _neuroAgents = new(() => templateRepo.GetAnalyzingAgentsAsync("analyzing_neurotransmitter"));
        _hormoneAgents = new(() => templateRepo.GetAnalyzingAgentsAsync("analyzing_hormone"));
        _peptideAgents = new(() => templateRepo.GetAnalyzingAgentsAsync("analyzing_peptide"));
    }

    /// <summary>
    /// Run biochemical analysis. allLayers=true runs all 3 in parallel, false runs NT only.
    /// </summary>
    public async Task<(List<BiochemicalDecision> Nt, List<BiochemicalDecision> Hormone, List<BiochemicalDecision> Peptide)> RunAnalysisAsync(
        string person, string topic, string context, bool allLayers = true)
    {
        var neuroAgents = await _neuroAgents.Value;
        var ntTask = RunLayerAsync(person, topic, context, neuroAgents);

        if (!allLayers)
            return (await ntTask, [], []);

        var hormoneAgents = await _hormoneAgents.Value;
        var peptideAgents = await _peptideAgents.Value;
        var hormoneTask = RunLayerAsync(person, topic, context, hormoneAgents);
        var peptideTask = RunLayerAsync(person, topic, context, peptideAgents);
        await Task.WhenAll(ntTask, hormoneTask, peptideTask);
        return (ntTask.Result, hormoneTask.Result, peptideTask.Result);
    }

    private async Task<List<BiochemicalDecision>> RunLayerAsync(
        string person, string topic, string context, Dictionary<string, AgentProfile> agentProfiles)
    {
        var results = new List<BiochemicalDecision>();
        var userMessage = $"Person: {person}\nTopic: {topic}\nContext: {context}";

        foreach (var batch in agentProfiles.Chunk(_maxParallelAgents))
        {
            var tasks = batch.Select(async kv =>
            {
                try
                {
                    var response = await _llm.ChatWithProfileAsync(kv.Value, [new(ChatRole.User, userMessage)]);
                    var cleaned = response.TrimStart('*', ' ', '#');
                    if (cleaned.StartsWith("ADD:", StringComparison.OrdinalIgnoreCase))
                        return new BiochemicalDecision(kv.Key, cleaned[4..].Trim().TrimEnd('*'));
                }
                catch { /* Skip agent on error */ }
                return null;
            });

            var batchResults = await Task.WhenAll(tasks);
            results.AddRange(batchResults.Where(d => d != null)!);
        }

        return results;
    }
}
