using NeuroGateway.AgentFramework;
using NeuroGateway.Models;
using NeuroGateway.Repository;
using NeuroGateway.Utils;

namespace NeuroGateway.Service;

/// <summary>
/// Neurorespond RESPOND path: match person → embed → per-layer estimation + blended scoring → 3+1 layer agents → return.
/// Uses ProfileScoringService for shared estimation + blending pipeline.
/// Enrichment (20 analyzing agents) fires in background via BackgroundEnrichmentQueue.
/// </summary>
public class NeuroService
{
    private readonly LlmService _llm;
    private readonly Layer _layer;
    private readonly PersonRepository _personRepo;
    private readonly PersonalityRepository _personalityRepo;
    private readonly ProfileScoringService _scoringService;
    private readonly RelationshipRepository _relationshipRepo;
    private readonly BackgroundEnrichmentQueue _enrichmentQueue;

    private readonly SuggestionConfig _prompts;

    public NeuroService(LlmService llm, Layer layer,
        PersonRepository personRepo, PersonalityRepository personalityRepo,
        ProfileScoringService scoringService,
        RelationshipRepository relationshipRepo, BackgroundEnrichmentQueue enrichmentQueue)
    {
        _llm = llm;
        _layer = layer;
        _personRepo = personRepo;
        _personalityRepo = personalityRepo;
        _scoringService = scoringService;
        _relationshipRepo = relationshipRepo;
        _enrichmentQueue = enrichmentQueue;

        _prompts = ConfigLoader.LoadJson<PromptConfig>("Prompts.json").Suggestions ?? new SuggestionConfig();
    }

    /// <summary>
    /// RESPOND: fast path — embed message, per-layer estimation + blended scoring, run 3+1 layer agents.
    /// ENRICH: fires in background after response returns.
    /// </summary>
    public async Task<NeuroNarrativeResult> NeuroRespondAsync(string person, string theirMessage, string? relationship)
    {
        // 1. Match person by name
        var (matchedPerson, _) = await _personRepo.MatchPersonAsync(person, null);

        // 2. Embed the message once
        var inputEmbedding = await _llm.EmbedAsync(theirMessage);

        // 3. Resolve relationship + responder group
        var resolvedRelationship = await _relationshipRepo.EnsureRelationshipTypeAsync(relationship ?? "dating");
        var group = ParseService.ParseResponderGroup(relationship);

        // 4. Per-layer estimation + blended scoring (SHARED via ProfileScoringService)
        var (estimates, profiles) = await _scoringService.EstimateAndScoreAsync(
            matchedPerson, "relationship", inputEmbedding);

        // 5. Get communication style (voice/style only)
        var communicationStyle = await _personalityRepo.GetCommunicationStyleAsync(matchedPerson);

        // 6. Build topic (slim — profiles go via {chemicals} in agent system prompts)
        var topic = _prompts.TopicTemplate
            .Replace("{person}", matchedPerson)
            .Replace("{text}", theirMessage)
            .Replace("{group}", resolvedRelationship);

        // 7. Run 3+1 layer agents with per-layer estimates
        var chatProfiles = await _layer.GetNeuroChatProfilesAsync(group);
        var responses = chatProfiles != null
            ? await _layer.RunLayerResponseAsync(chatProfiles, topic,
                profiles.NtProfile, profiles.HormoneProfile, profiles.PeptideProfile,
                communicationStyle,
                estimates.NtEstimate, estimates.HormoneEstimate, estimates.PeptideEstimate,
                resolvedRelationship)
            : [new NeuroResponse("Fallback", "No agent configuration found for this relationship group.")];

        // 8. Fire-and-forget enrichment
        _enrichmentQueue.Enqueue(new EnrichmentWorkItem(matchedPerson, theirMessage));

        // 9. Return
        return new NeuroNarrativeResult(matchedPerson, theirMessage, resolvedRelationship, responses);
    }
}
