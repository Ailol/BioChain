using NeuroGateway.AgentFramework;
using NeuroGateway.Models;
using NeuroGateway.Repository;
using NeuroGateway.Utils.Parsing;

namespace NeuroGateway.Service;

public class AnalyzeService(
    ChatClient chatClient,
    AgentConfiguration config,
    AgentTemplateRepository templateRepo,
    AnalyzedDataRepository analyzedDataRepo,
    ProfileRepository profileRepo,
    PersonService personService)
{
    // Set after construction by DI wiring — null when embedding not configured
    public EmbeddingService? Embedder { get; set; }
    private static readonly string[] Categories =
        ["analyzing_neurotransmitter", "analyzing_hormone", "analyzing_peptide"];

    public async Task<List<AnalysisDecision>> AnalyzeAsync(
        string person,
        string text,
        string? relationship = null,
        string sourceType = "manual",
        string? sourceUri = null,
        int? maxConcurrency = null,
        bool save = true,
        IReadOnlySet<string>? targetChemicals = null)
    {
        var (personId, personalityId) = await personService.EnsureAsync(person);

        var agents = await LoadAgentsAsync(targetChemicals);
        var userMessage = $"person: {person}\ncurrent_relationship: {relationship ?? "unknown"}\ndata: {text}";

        var results = await Orchestrator.RunAllAsync(chatClient, agents, userMessage, maxConcurrency ?? config.MaxParallelAgents);
        var decisions = AgentAnalyzer.Parse(results);

        if (save)
        {
            var analyzedDataId = await analyzedDataRepo.InsertAsync(personId, text, sourceType, sourceUri);

            if (Embedder is not null)
            {
                // Auto-embed the source text
                var adVector = await Embedder.GenerateVectorAsync(text);
                await analyzedDataRepo.UpdateEmbeddingAsync(analyzedDataId, adVector);

                // Insert observations with embeddings inline
                foreach (var d in decisions)
                {
                    var vector = await Embedder.GenerateVectorAsync(d.Reasoning);
                    await profileRepo.InsertAsync(personalityId, analyzedDataId, d.Chemical, d.Reasoning, 1.0f, vector);
                }
            }
            else
            {
                foreach (var d in decisions)
                    await profileRepo.InsertAsync(personalityId, analyzedDataId, d.Chemical, d.Reasoning, 1.0f, null);
            }
        }

        return decisions;
    }

    private async Task<List<AgentDefinition>> LoadAgentsAsync(IReadOnlySet<string>? targetChemicals = null)
    {
        var agents = new List<AgentDefinition>();
        foreach (var category in Categories)
        {
            var templates = await templateRepo.GetByCategoryAsync(category);
            var filtered = targetChemicals is not null
                ? templates.Where(t => targetChemicals.Contains(t.Name, StringComparer.OrdinalIgnoreCase))
                : templates;
            agents.AddRange(filtered.Select(t => new AgentDefinition(t.Name, t.Role, t.Layer)));
        }
        return agents;
    }
}
