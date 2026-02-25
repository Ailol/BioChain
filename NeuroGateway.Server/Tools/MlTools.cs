using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using NeuroGateway.Service;

namespace NeuroGateway.Server.Tools;

[McpServerToolType]
public class MlTools(MlService mlService)
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    [McpServerTool(Name = "discover_behavioral_archetypes")]
    [Description("Discover behavioral archetypes in a person's biochemical observations using spectral clustering. " +
                 "Groups similar observation embeddings into clusters via spectral graph partitioning, " +
                 "revealing distinct behavioral modes and their relative frequencies.")]
    public async Task<string> DiscoverArchetypes(
        [Description("Person name")] string person,
        [Description("Number of clusters (default 4)")] int k = 4)
    {
        try
        {
            var result = await mlService.SpectralClusterAsync(person, k);
            return JsonSerializer.Serialize(result, Json);
        }
        catch (InvalidOperationException ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, Json);
        }
    }

    [McpServerTool(Name = "compute_topological_fingerprint")]
    [Description("Compute a topological fingerprint of a person's biochemical embedding space using persistent homology. " +
                 "Reveals structural features: connected components (Betti-0), loops (Betti-1), and persistence diagrams " +
                 "that characterize the shape of their personality manifold.")]
    public async Task<string> ComputeTopology(
        [Description("Person name")] string person)
    {
        try
        {
            var result = await mlService.TopologicalFingerprintAsync(person);
            return JsonSerializer.Serialize(result, Json);
        }
        catch (InvalidOperationException ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, Json);
        }
    }

    [McpServerTool(Name = "discover_latent_factors")]
    [Description("Discover latent personality factors using a Variational Autoencoder trained on all persons' " +
                 "24-dimensional scores. Compresses personality into a low-dimensional latent space, " +
                 "revealing the hidden factors that drive observable dimension scores.")]
    public async Task<string> DiscoverLatentFactors(
        [Description("Person name")] string person,
        [Description("Latent space dimensionality (default 8)")] int latentDim = 8,
        [Description("Training epochs (default 500)")] int epochs = 500)
    {
        try
        {
            var result = await mlService.VaeEncodeAsync(person, latentDim, epochs);
            return JsonSerializer.Serialize(result, Json);
        }
        catch (InvalidOperationException ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, Json);
        }
    }

    [McpServerTool(Name = "classify_personality_profiles")]
    [Description("Classify personality profiles across all persons using Latent Profile Analysis (Gaussian mixture model). " +
                 "Identifies distinct personality types from 24-dimensional scores. " +
                 "Shows which profile cluster the specified person belongs to and their membership probabilities.")]
    public async Task<string> ClassifyProfiles(
        [Description("Person name")] string person,
        [Description("Number of profiles (0 = auto-select via BIC)")] int k = 0)
    {
        try
        {
            var result = await mlService.LatentProfilesAsync(person, k);
            return JsonSerializer.Serialize(result, Json);
        }
        catch (InvalidOperationException ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, Json);
        }
    }

    [McpServerTool(Name = "map_personality_framework")]
    [Description("Map biochemical dimension scores to Big Five personality traits using Canonical Correlation Analysis. " +
                 "Finds shared latent structure between neurochemistry (24 dims) and trait psychology (5 traits). " +
                 "Returns canonical variates with cross-loading weights showing how biochemistry predicts personality.")]
    public async Task<string> MapFramework(
        [Description("Person name")] string person)
    {
        try
        {
            var result = await mlService.CanonicalCorrelationAsync(person);
            return JsonSerializer.Serialize(result, Json);
        }
        catch (InvalidOperationException ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, Json);
        }
    }

    [McpServerTool(Name = "predict_trajectory")]
    [Description("Predict personality trajectory using a Temporal Convolutional Network trained on a person's " +
                 "time-ordered biochemical observations. Captures temporal patterns, detects change points, " +
                 "and predicts the next state of their neurochemical profile.")]
    public async Task<string> PredictTrajectory(
        [Description("Person name")] string person,
        [Description("Training epochs (default 100)")] int epochs = 100)
    {
        try
        {
            var result = await mlService.TemporalPredictAsync(person, epochs);
            return JsonSerializer.Serialize(result, Json);
        }
        catch (InvalidOperationException ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, Json);
        }
    }
}
