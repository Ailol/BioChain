using BioChain.AgentFramework;
using BioChain.Models;
using BioChain.Repository;
using BioChain.Repository.Entities;
using BioChain.Utils.Parsing;

namespace BioChain.Service;

public class AnalyzeService(
    ChatClient chatClient,
    AgentConfiguration config,
    AgentTemplateRepository templateRepo,
    AnalyzedDataRepository analyzedDataRepo,
    ObservationRepository observationRepo,
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
        IReadOnlySet<string>? targetSignals = null)
    {
        var (personId, personalityId) = await personService.EnsureAsync(person);

        var agents = await LoadAgentsAsync(targetSignals);
        var allNames = agents.Select(a => a.Name).ToList();

        // Build per-agent user messages with Cross-signal line
        string BuildMessage(AgentDefinition agent)
        {
            var crossSignals = new List<string> { agent.Name };
            crossSignals.AddRange(allNames.Where(n => n != agent.Name));
            var crossLine = $"Cross-signal: {string.Join(", ", crossSignals)}";
            return $"{crossLine}\nperson: {person}\ncurrent_relationship: {relationship ?? "unknown"}\ninput_type: {sourceType}\ndata: {text}";
        }

        var results = await Orchestrator.RunAllAsync(chatClient, agents, BuildMessage, maxConcurrency ?? config.MaxParallelAgents);
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
                    // Embed formulas+circuits (the semantically meaningful NCN content)
                    var embeddingText = d.Formula;
                    if (!string.IsNullOrEmpty(d.Circuits))
                        embeddingText += "\n" + d.Circuits;

                    var vector = await Embedder.GenerateVectorAsync(embeddingText);

                    var entity = new ObservationEntity
                    {
                        PersonId = personId,
                        PersonalityId = personalityId,
                        AnalyzedDataId = analyzedDataId,
                        SignalId = d.SignalId,
                        SignalsText = d.Signals,
                        Formula = d.Formula,
                        StateText = d.State,
                        CircuitsText = d.Circuits,
                        Intensity = d.Intensity,
                        SubjectState = d.SubjectState,
                        Operator = d.Operator,
                        TargetSignalId = d.TargetSignalId,
                        TargetState = d.TargetState,
                        RegionId = d.RegionId,
                        Temporal = d.Temporal,
                        Confidence = d.Confidence,
                        FailureMode = d.FailureMode,
                    };

                    await observationRepo.InsertWithEmbeddingAsync(entity, vector);
                }
            }
            else
            {
                foreach (var d in decisions)
                {
                    var entity = new ObservationEntity
                    {
                        PersonId = personId,
                        PersonalityId = personalityId,
                        AnalyzedDataId = analyzedDataId,
                        SignalId = d.SignalId,
                        SignalsText = d.Signals,
                        Formula = d.Formula,
                        StateText = d.State,
                        CircuitsText = d.Circuits,
                        Intensity = d.Intensity,
                        SubjectState = d.SubjectState,
                        Operator = d.Operator,
                        TargetSignalId = d.TargetSignalId,
                        TargetState = d.TargetState,
                        RegionId = d.RegionId,
                        Temporal = d.Temporal,
                        Confidence = d.Confidence,
                        FailureMode = d.FailureMode,
                    };

                    await observationRepo.InsertAsync(entity);
                }
            }
        }

        return decisions;
    }

    private async Task<List<AgentDefinition>> LoadAgentsAsync(IReadOnlySet<string>? targetSignals = null)
    {
        var agents = new List<AgentDefinition>();
        foreach (var category in Categories)
        {
            var templates = await templateRepo.GetByCategoryAsync(category);
            var filtered = targetSignals is not null
                ? templates.Where(t => targetSignals.Contains(t.Name, StringComparer.OrdinalIgnoreCase))
                : templates;
            agents.AddRange(filtered.Select(t => new AgentDefinition(t.Name, t.Role, t.Layer)));
        }
        return agents;
    }
}
