using NeuroGateway.AgentFramework;
using NeuroGateway.AgentFramework.Algorithms;
using NeuroGateway.Models;
using NeuroGateway.Repository;

namespace NeuroGateway.Service;

/// <summary>
/// Personality entry management: runs biochemical analysis + writes profiles with clustering.
/// Inserts analyzed_data rows and links biochemical profiles to them.
/// Also provides full personality scan with clustering and heatmap data.
/// </summary>
public class PersonalityService
{
    private readonly LlmService _llm;
    private readonly PersonRepository _personRepo;
    private readonly PersonalityRepository _personalityRepo;
    private readonly ProfileRepository _profileRepo;
    private readonly AnalyzedDataRepository _analyzedDataRepo;
    private readonly VectorService _vectorService;
    private readonly BackgroundEmbeddingQueue _embeddingQueue;
    private AnalyseService _analyseService;

    private const double WriteTimeClusterThreshold = 0.15;

    public PersonalityService(LlmService llm, PersonRepository personRepo, PersonalityRepository personalityRepo,
        ProfileRepository profileRepo, AnalyzedDataRepository analyzedDataRepo, VectorService vectorService,
        BackgroundEmbeddingQueue embeddingQueue)
    {
        _llm = llm;
        _personRepo = personRepo;
        _personalityRepo = personalityRepo;
        _profileRepo = profileRepo;
        _analyzedDataRepo = analyzedDataRepo;
        _vectorService = vectorService;
        _embeddingQueue = embeddingQueue;
        _analyseService = null!;
    }

    /// <summary>
    /// Called by DI setup to break PersonalityService <-> AnalyseService circular dependency.
    /// </summary>
    public void SetAnalyseService(AnalyseService analyseService) => _analyseService = analyseService;

    // ===== Evolving Communication Style =====

    /// <summary>
    /// Generate or update communication style based on new content.
    /// First time: generate from scratch. Subsequent: check if new content reveals something new.
    /// Responds "UNCHANGED" if nothing new — avoids unnecessary DB writes.
    /// </summary>
    public async Task RefreshCommunicationStyleAsync(string person, string newContent)
    {
        var currentStyle = await _personalityRepo.GetCommunicationStyleAsync(person);

        if (currentStyle == null)
        {
            // First time — generate from scratch
            var prompt = $"""
                Describe how {person} communicates in 2-3 sentences.
                Sentence length, formality, humor, emoji use, vocabulary level, energy.
                Content: "{newContent}"
                """;
            var style = await _llm.AskAsync(prompt, _llm.OrchestratorModel);
            if (!string.IsNullOrWhiteSpace(style))
                await _personalityRepo.UpdateCommunicationStyleAsync(person, style.Trim());
            return;
        }

        // Has existing style — check if new content reveals something different
        var updatePrompt = $"""
            Current communication style description: {currentStyle}

            New content from this person: "{newContent}"

            Does this content reveal anything NEW about how they communicate
            that isn't already captured? If yes, write an updated 2-3 sentence
            style description that incorporates the new observation.
            If no significant change, respond UNCHANGED.
            """;
        var updated = await _llm.AskAsync(updatePrompt, _llm.OrchestratorModel);

        if (!string.IsNullOrWhiteSpace(updated) &&
            !updated.Trim().Equals("UNCHANGED", StringComparison.OrdinalIgnoreCase) &&
            !updated.Trim().StartsWith("UNCHANGED", StringComparison.OrdinalIgnoreCase))
            await _personalityRepo.UpdateCommunicationStyleAsync(person, updated.Trim());
    }

    // ===== Full Personality Scan =====

    /// <summary>
    /// Full personality scan with entries, hormone/peptide scores, clustering, and heatmap.
    /// </summary>
    public async Task<FullPersonalityScan?> GetFullPersonalityScanAsync(string person)
    {
        var personalityResult = await _personalityRepo.GetPersonalityAsync(person);
        if (personalityResult.Profile == null) return null;

        var entries = personalityResult.Profile.Entries;
        var matchedName = personalityResult.Profile.Person;
        var communicationStyle = personalityResult.Profile.CommunicationStyle;
        var entriesWithEmbeddings = await _analyzedDataRepo.GetWithEmbeddingsAndMetadataAsync(person);

        if (entriesWithEmbeddings.Count == 0)
            return new FullPersonalityScan(matchedName, entries, [], [], [], CommunicationStyle: communicationStyle);

        var ntTask = _profileRepo.GetNeurotransmitterScoresAsync(person);
        var hormoneTask = _profileRepo.GetHormoneScoresAsync(person);
        var peptideTask = _profileRepo.GetPeptideScoresAsync(person);
        await Task.WhenAll(ntTask, hormoneTask, peptideTask);

        var clusters = ClusteringAlgorithms.GreedyAgglomerative(entriesWithEmbeddings);
        var neighbors = ClusteringAlgorithms.FindNeighbors(entriesWithEmbeddings);
        var centroids = ClusteringAlgorithms.ComputeCentroids(entriesWithEmbeddings);
        var heatmap = await _vectorService.ComputeHeatmapAsync(entriesWithEmbeddings, "hormone");

        var scan = new FullPersonalityScan(matchedName, entries, ntTask.Result, hormoneTask.Result, peptideTask.Result,
            clusters, neighbors, centroids, heatmap, communicationStyle);

        var analysis = await SynthesizeScanAsync(scan);
        return scan with { Analysis = analysis };
    }

    private async Task<string> SynthesizeScanAsync(FullPersonalityScan scan)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Person: {scan.Person}");

        if (scan.CommunicationStyle != null)
            sb.AppendLine($"Communication Style: {scan.CommunicationStyle}");

        sb.AppendLine($"\nEntries ({scan.Entries.Count}):");
        foreach (var e in scan.Entries.DistinctBy(e => e.Content))
            sb.AppendLine($"  - {e.Content}: {e.AllChemicals()}");

        if (scan.Neurotransmitters.Count > 0)
            sb.AppendLine($"\nNeurotransmitters: {string.Join(", ", scan.Neurotransmitters.Select(n => $"{n.Name} ({n.TraitCount} traits)"))}");
        if (scan.Hormones.Count > 0)
            sb.AppendLine($"\nHormones: {string.Join(", ", scan.Hormones.Select(h => $"{h.Name} ({h.TraitCount} traits)"))}");
        if (scan.Peptides.Count > 0)
            sb.AppendLine($"\nPeptides: {string.Join(", ", scan.Peptides.Select(p => $"{p.Name} ({p.TraitCount} traits)"))}");

        if (scan.TraitClusters?.Count > 0)
        {
            sb.AppendLine($"\nClusters ({scan.TraitClusters.Count}):");
            foreach (var c in scan.TraitClusters)
                sb.AppendLine($"  [{string.Join(", ", c.Neurotransmitters)}] {c.Label}: {string.Join(", ", c.Entries.Distinct())}");
        }

        if (scan.NtCentroids?.Count > 0)
        {
            sb.AppendLine("\nNT Centroids:");
            foreach (var nc in scan.NtCentroids)
                sb.AppendLine($"  {nc.Neurotransmitter}: {nc.TraitCount} traits, cohesion {nc.CohesionScore:F2}");
        }

        if (scan.HormoneHeatmap?.Count > 0)
        {
            sb.AppendLine("\nHormone Heatmap:");
            foreach (var h in scan.HormoneHeatmap.Take(5))
                sb.AppendLine($"  {h.Name}: strength {h.OverallStrength:F2}, top contributors: {string.Join(", ", h.TopContributors.Take(3).Select(c => $"{c.Entry} ({c.Similarity:P0})"))}");
        }

        if (scan.TraitRelationships?.Count > 0)
        {
            var unique = scan.TraitRelationships.DistinctBy(r => r.Entry).ToList();
            sb.AppendLine($"\nEntry Relationships ({unique.Count}):");
            foreach (var r in unique)
            {
                var nearest = r.Neighbors.Where(n => n.Entry != r.Entry).Take(2);
                sb.AppendLine($"  {r.Entry} -> {string.Join(", ", nearest.Select(n => $"{n.Entry} ({n.Similarity:P0})"))}");
            }
        }

        var prompt = $"""
            You are a biochemical psychologist analyzing {scan.Person}'s personality scan.
            The reader already sees the raw numbers — do NOT restate which chemicals are most frequent or dominant.

            {sb}

            Focus exclusively on what is NOT obvious from the raw data:
            - Cross-layer interactions: how do specific NTs + hormones + peptides amplify or suppress each other?
            - Hidden contradictions or tensions in the biochemical profile
            - What clustering patterns and entry relationships reveal about behavioral dynamics
            - Non-obvious behavioral predictions that emerge from the chemical combinations
            - Heatmap insights: which hormone-entry connections are surprising or underrepresented?
            Reference specific chemicals and data points. Under 300 words.
            """;

        return await _llm.AskAsync(prompt, _llm.OrchestratorModel);
    }

    // ===== Analyze (formerly AddPersonalityEntryAsync) =====

    private const double DuplicateThreshold = 0.85;

    /// <summary>
    /// Analyze new content for a person: embed, dedup, insert analyzed_data, run biochemical analysis,
    /// write profiles, and optionally generate communication style.
    /// </summary>
    public async Task<NeuroGroupResult> AnalyzeAsync(string person, string content,
        string? sourceType = "manual", string? sourceUri = null, bool embeddings = true)
    {
        await _personRepo.EnsurePersonExistsAsync(person);
        var personalityId = await _personalityRepo.EnsurePersonalityExistsAsync(person);

        if (personalityId == 0)
            return new NeuroGroupResult(person, content, [], "Failed to ensure personality row.");

        // Embed the content
        var contentEmbedding = await _llm.EmbedAsync(content);
        string? embeddingVector = contentEmbedding != null ? VectorAlgorithms.ToPostgresVector(contentEmbedding) : null;

        // Check for duplicate analyzed_data by embedding similarity
        if (embeddingVector != null)
        {
            var nearest = await _analyzedDataRepo.FindNearestAsync(person, embeddingVector);
            if (nearest != null && nearest.Value.Similarity >= DuplicateThreshold)
            {
                return new NeuroGroupResult(person, content, [],
                    $"Duplicate detected (similarity {nearest.Value.Similarity:P0} with existing entry). Skipped.");
            }
        }

        // Insert analyzed_data row
        var analyzedDataId = await _analyzedDataRepo.InsertAsync(person, content, sourceType, sourceUri, embeddingVector);
        if (analyzedDataId == 0)
            return new NeuroGroupResult(person, content, [], "Failed to insert analyzed_data row.");

        // Run biochemical analysis
        var (nt, hormone, peptide) = await _analyseService.RunAnalysisAsync(person, content, content);

        if (nt.Count + hormone.Count + peptide.Count == 0)
            return new NeuroGroupResult(person, content, [], "No biochemical agents found this relevant.");

        // Write profiles with analyzedDataId
        await WriteProfilesAsync(personalityId, person, nt, "neurotransmitter_profile", analyzedDataId,
            (id, chem, reason, adId, emb, cid, rep) => _personalityRepo.InsertNeurotransmitterProfileAsync(id, chem, reason, adId, emb, cid, rep));
        await WriteProfilesAsync(personalityId, person, hormone, "hormone_profile", analyzedDataId,
            (id, chem, reason, adId, emb, cid, rep) => _personalityRepo.InsertHormoneProfileAsync(id, chem, reason, adId, emb, cid, rep));
        await WriteProfilesAsync(personalityId, person, peptide, "peptide_profile", analyzedDataId,
            (id, chem, reason, adId, emb, cid, rep) => _personalityRepo.InsertPeptideProfileAsync(id, chem, reason, adId, emb, cid, rep));

        // Build result entry
        var added = new List<AnalyzedEntry>
        {
            new(content, sourceType,
                nt.Select(d => d.Chemical).ToList(),
                hormone.Select(d => d.Chemical).ToList(),
                peptide.Select(d => d.Chemical).ToList(),
                analyzedDataId)
        };

        // Queue background embedding if inline embedding failed
        if (embeddings && embeddingVector == null)
            _embeddingQueue.Enqueue(new EmbeddingWorkItem(person, analyzedDataId, content));

        // Evolving communication style — generate or update
        if (sourceType is "chat" or "manual" or "neurorespond")
        {
            try { await RefreshCommunicationStyleAsync(person, content); }
            catch (Exception ex) { Console.Error.WriteLine($"Communication style refresh failed: {ex.Message}"); }
        }

        var msg = $"{nt.Count} NT + {hormone.Count} hormone + {peptide.Count} peptide agents responded." +
                  (embeddingVector != null ? " Embedding stored." : embeddings ? " Embedding generating in background." : " Embeddings skipped.");
        return new NeuroGroupResult(person, content, added, msg);
    }

    // ===== Enrich (background enrichment after neurorespond) =====

    /// <summary>
    /// Background enrichment: runs all 20 analyzing agents on a neurorespond message,
    /// writes new profile rows with analyzed_data_id. Called by EnrichmentQueueProcessor.
    /// </summary>
    public async Task EnrichFromMessageAsync(string person, string message)
    {
        await _personRepo.EnsurePersonExistsAsync(person);
        var personalityId = await _personalityRepo.EnsurePersonalityExistsAsync(person);
        if (personalityId == 0) return;

        // Embed + dedup
        var contentEmbedding = await _llm.EmbedAsync(message);
        var embeddingVector = contentEmbedding != null ? VectorAlgorithms.ToPostgresVector(contentEmbedding) : null;

        if (embeddingVector != null)
        {
            var nearest = await _analyzedDataRepo.FindNearestAsync(person, embeddingVector);
            if (nearest != null && nearest.Value.Similarity >= DuplicateThreshold)
            {
                Console.Error.WriteLine($"Enrichment skipped (duplicate {nearest.Value.Similarity:P0}): {person}");
                return;
            }
        }

        // Insert analyzed_data
        var analyzedDataId = await _analyzedDataRepo.InsertAsync(person, message, "neurorespond", null, embeddingVector);
        if (analyzedDataId == 0) return;

        // Run ALL 3 biochemical layers (20 agents)
        var (nt, hormone, peptide) = await _analyseService.RunAnalysisAsync(person, message, message, allLayers: true);

        if (nt.Count + hormone.Count + peptide.Count == 0) return;

        // Write profiles
        await WriteProfilesAsync(personalityId, person, nt, "neurotransmitter_profile", analyzedDataId,
            (id, chem, reason, adId, emb, cid, rep) => _personalityRepo.InsertNeurotransmitterProfileAsync(id, chem, reason, adId, emb, cid, rep));
        await WriteProfilesAsync(personalityId, person, hormone, "hormone_profile", analyzedDataId,
            (id, chem, reason, adId, emb, cid, rep) => _personalityRepo.InsertHormoneProfileAsync(id, chem, reason, adId, emb, cid, rep));
        await WriteProfilesAsync(personalityId, person, peptide, "peptide_profile", analyzedDataId,
            (id, chem, reason, adId, emb, cid, rep) => _personalityRepo.InsertPeptideProfileAsync(id, chem, reason, adId, emb, cid, rep));

        // Queue embedding backfill if inline failed
        if (embeddingVector == null)
            _embeddingQueue.Enqueue(new EmbeddingWorkItem(person, analyzedDataId, message));

        // Evolving communication style — generate or update
        try { await RefreshCommunicationStyleAsync(person, message); }
        catch (Exception ex) { Console.Error.WriteLine($"Communication style refresh failed: {ex.Message}"); }

        Console.Error.WriteLine($"Enrichment wrote {nt.Count} NT + {hormone.Count} hormone + {peptide.Count} peptide profiles for {person}");
    }

    /// <summary>
    /// Write profiles with clustering: generate embedding -> find nearest cluster -> join or create.
    /// Delegate signature includes analyzedDataId for linking profiles to analyzed_data.
    /// </summary>
    private async Task WriteProfilesAsync(
        int personalityId, string person, List<BiochemicalDecision> decisions, string profileTable, int analyzedDataId,
        Func<int, string, string, int?, string?, int?, bool, Task> write)
    {
        foreach (var d in decisions)
        {
            try
            {
                var embedding = await _llm.EmbedAsync(d.Reasoning);
                if (embedding != null)
                {
                    var embeddingVector = VectorAlgorithms.ToPostgresVector(embedding);
                    var nearest = await _profileRepo.FindNearestClusterAsync(profileTable, person, embeddingVector);

                    int clusterId;
                    bool isRepresentative;

                    if (nearest != null && nearest.Value.Distance < WriteTimeClusterThreshold)
                    {
                        clusterId = nearest.Value.ClusterId;
                        isRepresentative = false;
                    }
                    else
                    {
                        clusterId = await _profileRepo.GetNextClusterIdAsync(profileTable, person);
                        isRepresentative = true;
                    }

                    await write(personalityId, d.Chemical, d.Reasoning, analyzedDataId, embeddingVector, clusterId, isRepresentative);
                }
                else
                {
                    await write(personalityId, d.Chemical, d.Reasoning, analyzedDataId, null, null, false);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Profile write failed for {d.Chemical}: {ex.Message}");
            }
        }
    }
}
