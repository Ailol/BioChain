using NeuroGateway.AgentFramework;
using NeuroGateway.AgentFramework.Algorithms;
using NeuroGateway.Repository;

namespace NeuroGateway.Service;

/// <summary>
/// Shared per-layer estimation + dual-vector scoring pipeline.
/// All 3 paths (NeuroService, ProfessionalService, DataService) inject this
/// instead of reimplementing estimation.
///
/// Dual-vector scoring: each reasoning row is scored against BOTH the message embedding
/// and the layer's estimated relationship embedding separately, then combined with weighting.
/// Per-chemical coverage guarantee (top N per chemical) + temporal freshness boost.
/// </summary>
public class ProfileScoringService(
    ProfileRepository profileRepo,
    ContextEmbeddingCache contextCache)
{
    public record LayerEstimation(string? NtEstimate, string? HormoneEstimate, string? PeptideEstimate);
    public record BlendedProfiles(string NtProfile, string HormoneProfile, string PeptideProfile);

    /// <summary>
    /// Per-layer estimation + dual-vector scoring pipeline.
    /// dimensionName: "relationship" for neurorespond, "career" for professional, null for raw input.
    /// inputEmbedding: embedded user message (null = use unranked fallback).
    /// </summary>
    public async Task<(LayerEstimation Estimates, BlendedProfiles Profiles)> EstimateAndScoreAsync(
        string person, string? dimensionName, float[]? inputEmbedding)
    {
        // 1. Get per-layer reasoning embeddings (cluster representatives)
        var ntTexts = await profileRepo.GetLayerEmbeddingTextsAsync(person, "neurotransmitter_profile");
        var hTexts = await profileRepo.GetLayerEmbeddingTextsAsync(person, "hormone_profile");
        var pTexts = await profileRepo.GetLayerEmbeddingTextsAsync(person, "peptide_profile");

        var ntEmbs = ParseEmbeddings(ntTexts);
        var hEmbs = ParseEmbeddings(hTexts);
        var pEmbs = ParseEmbeddings(pTexts);

        // 2. Per-layer centroid → estimate via dimension (or null if no dimension)
        string? ntEst = null, hEst = null, pEst = null;
        if (dimensionName != null && contextCache.HasDimension(dimensionName))
        {
            ntEst = ntEmbs.Count > 0
                ? contextCache.FindClosest(dimensionName, VectorAlgorithms.MeanPool(ntEmbs))?.Name
                : null;
            hEst = hEmbs.Count > 0
                ? contextCache.FindClosest(dimensionName, VectorAlgorithms.MeanPool(hEmbs))?.Name
                : null;
            pEst = pEmbs.Count > 0
                ? contextCache.FindClosest(dimensionName, VectorAlgorithms.MeanPool(pEmbs))?.Name
                : null;
        }

        // 3. Score reasoning rows per layer with dual-vector scoring
        var ntProfile = await GetDualScoredProfile(person, "neurotransmitter",
            inputEmbedding, dimensionName, ntEst);
        var hProfile = await GetDualScoredProfile(person, "hormone",
            inputEmbedding, dimensionName, hEst);
        var pProfile = await GetDualScoredProfile(person, "peptide",
            inputEmbedding, dimensionName, pEst);

        return (new LayerEstimation(ntEst, hEst, pEst), new BlendedProfiles(ntProfile, hProfile, pProfile));
    }

    /// <summary>
    /// Score a single layer using dual-vector SQL function.
    /// Passes message embedding + relationship embedding separately — no information loss from blending.
    /// Falls back to unranked profile when no embeddings available.
    /// </summary>
    private async Task<string> GetDualScoredProfile(string person, string layer,
        float[]? messageEmbedding, string? dimensionName, string? estimatedLabel)
    {
        if (messageEmbedding == null)
        {
            // Map layer name to table names for fallback
            var (profileTable, chemicalTable, chemicalFk) = GetTableNames(layer);
            var rows = await profileRepo.GetUnrankedLayerProfileAsync(person, profileTable, chemicalTable, chemicalFk);
            return rows.Count > 0 ? string.Join("\n", rows) : $"No {layer} profile data yet";
        }

        // Get relationship embedding for this layer's estimate
        float[]? relationshipEmb = null;
        if (dimensionName != null && estimatedLabel != null)
            relationshipEmb = contextCache.GetEmbedding(dimensionName, estimatedLabel);

        // If no relationship embedding available, use message embedding for both vectors
        // (dual scoring degrades gracefully to single-vector scoring)
        var msgVector = VectorAlgorithms.ToPostgresVector(messageEmbedding);
        var relVector = relationshipEmb != null && relationshipEmb.Length == messageEmbedding.Length
            ? VectorAlgorithms.ToPostgresVector(relationshipEmb)
            : msgVector;

        var scored = await profileRepo.GetDualScoredLayerProfileAsync(
            person, layer, msgVector, relVector,
            messageWeight: 0.6f, topPerChemical: 1);

        return scored.Count > 0 ? string.Join("\n", scored) : $"No {layer} profile data yet";
    }

    private static (string profileTable, string chemicalTable, string chemicalFk) GetTableNames(string layer) =>
        layer switch
        {
            "neurotransmitter" => ("neurotransmitter_profile", "neurotransmitter", "neurotransmitter_id"),
            "hormone" => ("hormone_profile", "hormone", "hormone_id"),
            "peptide" => ("peptide_profile", "peptide", "peptide_id"),
            _ => throw new ArgumentException($"Unknown layer: {layer}")
        };

    private static List<float[]> ParseEmbeddings(List<string> texts) =>
        texts.Select(VectorAlgorithms.ParsePostgresVector)
             .Where(v => v != null)
             .Select(v => v!)
             .ToList();
}
