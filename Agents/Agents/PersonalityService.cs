using Models;
using Repository;

namespace Agents;

/// <summary>
/// Core personality operations: add entries, scan profiles, build profile strings.
/// Write-time clustering: every profile write generates an embedding, finds nearest cluster,
/// and either joins (distance &lt; 0.15) or creates a new cluster as representative.
/// </summary>
public class PersonalityService
{
    private readonly PersonRepository _personRepo;
    private readonly PersonalityRepository _personalityRepo;
    private readonly EmbeddingRepository _embeddingRepo;
    private readonly EmbeddingService _embeddingService;
    private readonly VectorService _vectorService;
    private readonly BackgroundEmbeddingQueue _embeddingQueue;
    private AnalysisService _analysisService;

    private const double ClusterThreshold = 0.15;

    public PersonalityService(VectorService vectorService,
        PersonRepository personRepo, PersonalityRepository personalityRepo, EmbeddingRepository embeddingRepo,
        EmbeddingService embeddingService, BackgroundEmbeddingQueue embeddingQueue)
    {
        _personRepo = personRepo;
        _personalityRepo = personalityRepo;
        _embeddingRepo = embeddingRepo;
        _embeddingService = embeddingService;
        _vectorService = vectorService;
        _embeddingQueue = embeddingQueue;
        // AnalysisService set via SetAnalysisService to break circular dependency
        _analysisService = null!;
    }

    /// <summary>
    /// Called by DI setup to break PersonalityService ↔ AnalysisService circular dependency.
    /// </summary>
    public void SetAnalysisService(AnalysisService analysisService) => _analysisService = analysisService;

    public async Task<FullPersonalityScan?> GetFullPersonalityScanAsync(string person)
    {
        var personalityResult = await _personalityRepo.GetPersonalityAsync(person);
        if (personalityResult.Profile == null) return null;

        var traits = personalityResult.Profile.Traits;
        var matchedName = personalityResult.Profile.Person;
        var traitsWithEmbeddings = await _embeddingRepo.GetTraitEmbeddingsWithMetadataAsync(person);

        if (traitsWithEmbeddings.Count == 0)
            return new FullPersonalityScan(matchedName, traits, [], []);

        var hormoneTask = _personalityRepo.GetHormoneScoresAsync(person);
        var peptideTask = _personalityRepo.GetPeptideScoresAsync(person);
        await Task.WhenAll(hormoneTask, peptideTask);

        var clusters = _vectorService.ClusterTraits(traitsWithEmbeddings);
        var neighbors = _vectorService.FindTraitNeighbors(traitsWithEmbeddings);
        var centroids = _vectorService.ComputeNtCentroids(traitsWithEmbeddings);
        var heatmap = await _vectorService.ComputeHeatmapAsync(traitsWithEmbeddings, "hormone");

        return new FullPersonalityScan(matchedName, traits, hormoneTask.Result, peptideTask.Result,
            clusters, neighbors, centroids, heatmap);
    }

    /// <summary>
    /// Add a new personality entry with biochemical analysis (INSERT, not upsert).
    /// Runs all 3 agent layers, inserts personality row + profile rows with clustering.
    /// </summary>
    public async Task<NeuroGroupResult> AddPersonalityEntryAsync(string person, string topic, string context, bool embeddings = true)
    {
        await _personRepo.EnsurePersonExistsAsync(person);

        var (nt, hormone, peptide) = await _analysisService.RunAnalysisAsync(person, topic, context);

        if (nt.Count + hormone.Count + peptide.Count == 0)
            return new NeuroGroupResult(person, topic, [], "No biochemical agents found this relevant.");

        var bestExplanation = (nt.FirstOrDefault() ?? hormone.FirstOrDefault() ?? peptide.First()).Reasoning;
        var personalityId = await _personalityRepo.AddPersonalityTraitAsync(person, topic, bestExplanation);

        if (personalityId == 0)
            return new NeuroGroupResult(person, topic, [], "Failed to insert personality row.");

        await WriteProfilesWithClusteringAsync(personalityId, person, nt, "neurotransmitter_profile",
            (id, chem, reason, emb, cid, rep) => _personalityRepo.UpsertNeurotransmitterProfileAsync(id, chem, reason, emb, cid, rep));
        await WriteProfilesWithClusteringAsync(personalityId, person, hormone, "hormone_profile",
            (id, chem, reason, emb, cid, rep) => _personalityRepo.UpsertHormoneProfileAsync(id, chem, reason, emb, cid, rep));
        await WriteProfilesWithClusteringAsync(personalityId, person, peptide, "peptide_profile",
            (id, chem, reason, emb, cid, rep) => _personalityRepo.UpsertPeptideProfileAsync(id, chem, reason, emb, cid, rep));

        var added = nt.Select(d => new Trait(topic, d.Reasoning, d.Chemical))
            .Concat(hormone.Select(d => new Trait(topic, d.Reasoning, d.Chemical)))
            .Concat(peptide.Select(d => new Trait(topic, d.Reasoning, d.Chemical)))
            .ToList();

        if (embeddings)
            _embeddingQueue.Enqueue(new EmbeddingWorkItem(person, topic, bestExplanation));

        var msg = $"{nt.Count} NT + {hormone.Count} hormone + {peptide.Count} peptide agents responded." +
                  (embeddings ? " Embedding generating in background." : " Embeddings skipped.");
        return new NeuroGroupResult(person, topic, added, msg);
    }

    /// <summary>
    /// Write profiles with clustering: generate embedding → find nearest cluster → join or create.
    /// </summary>
    private async Task WriteProfilesWithClusteringAsync(
        int personalityId, string person, List<BiochemicalDecision> decisions, string profileTable,
        Func<int, string, string, string?, int?, bool, Task> write)
    {
        foreach (var d in decisions)
        {
            try
            {
                var embedding = await _embeddingService.GenerateEmbeddingAsync(d.Reasoning);
                if (embedding != null)
                {
                    var embeddingVector = EmbeddingService.ToPostgresVector(embedding);
                    var nearest = await _personalityRepo.FindNearestClusterAsync(profileTable, person, embeddingVector);

                    int clusterId;
                    bool isRepresentative;

                    if (nearest != null && nearest.Value.Distance < ClusterThreshold)
                    {
                        // Close enough — join existing cluster, not representative
                        clusterId = nearest.Value.ClusterId;
                        isRepresentative = false;
                    }
                    else
                    {
                        // New cluster — this entry is the representative
                        clusterId = await _personalityRepo.GetNextClusterIdAsync(profileTable, person);
                        isRepresentative = true;
                    }

                    await write(personalityId, d.Chemical, d.Reasoning, embeddingVector, clusterId, isRepresentative);
                }
                else
                {
                    // Fallback: write without clustering if embedding fails
                    await write(personalityId, d.Chemical, d.Reasoning, null, null, false);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Profile write failed for {d.Chemical}: {ex.Message}");
            }
        }
    }

    // ===== Static helpers =====

    public static Dictionary<string, AgentProfile> ToAgentProfiles(List<CustomAgent> agents)
        => agents.ToDictionary(a => a.Name, a => new AgentProfile
        {
            Role = a.Role, Responsibilities = a.Responsibilities,
            Style = a.Style, MaxWords = a.MaxWords, Conclusion = a.IsSynthesizer
        });

    public static string BuildPersonProfile(NeuroresponseResult neuroResult,
        FullPersonalityScan? fullScan, List<ChemicalScore> hormones, List<ChemicalScore> peptides)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("NEUROTRANSMITTER WEIGHTS:");
        foreach (var w in neuroResult.NeurotransmitterWeights)
            sb.AppendLine($"  {w.Neurotransmitter}: {w.Weight:P0} ({w.TraitCount} traits)");

        if (fullScan?.Traits.Count > 0)
        {
            sb.AppendLine("\nPERSONALITY TRAITS:");
            foreach (var group in fullScan.Traits.GroupBy(t => t.Neurotransmitter ?? "Unknown"))
            {
                sb.AppendLine($"  [{group.Key}]:");
                foreach (var trait in group)
                    sb.AppendLine($"    - {trait.Topic}: {trait.Explanation}");
            }
        }

        if (neuroResult.TopMatchingTraits.Count > 0)
        {
            sb.AppendLine("\nMOST RELEVANT TRAITS FOR THIS MESSAGE:");
            foreach (var t in neuroResult.TopMatchingTraits)
                sb.AppendLine($"  - {t.Topic} ({t.Neurotransmitter}, {t.Similarity:P0})");
        }

        if (hormones.Count > 0)
            sb.AppendLine($"\nHORMONES: {string.Join(", ", hormones.Select(h => $"{h.Name} ({h.TraitCount} traits)"))}");
        if (peptides.Count > 0)
            sb.AppendLine($"\nPEPTIDES: {string.Join(", ", peptides.Select(p => $"{p.Name} ({p.TraitCount} traits)"))}");

        return sb.ToString().Trim();
    }
}
