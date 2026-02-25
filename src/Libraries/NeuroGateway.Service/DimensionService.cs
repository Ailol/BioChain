using NeuroGateway.AnalysisFramework;
using NeuroGateway.Models;
using NeuroGateway.Repository;

namespace NeuroGateway.Service;

// Thin orchestrator: loads data from repositories, delegates scoring to AnalysisFramework.
public class DimensionService(
    ObservationRepository observationRepo,
    ShadowAnchorService shadowAnchor,
    DimensionDefinitionsService dimDefs)
{
    private const float DecayLambda = 0.01f; // used only by GetShadowMatrixAsync
    private const float SigmoidThreshold = 3f;

    public async Task<List<DimensionScore>> ScoreAsync(string person, ScoringMode mode = ScoringMode.Work)
    {
        var all = await dimDefs.GetAllAsync();
        var entries = await observationRepo.GetObservationEntriesAsync(person);
        var signalToLayer = await dimDefs.GetSignalToLayerAsync();

        // Map ObservationEntry -> SignalObservation (decouple algorithm from repository)
        var observations = entries
            .Select(e => new SignalObservation(
                e.Signal, e.Formula, e.StateText, e.CircuitsText,
                e.Embedding, e.Intensity, e.CreatedAt))
            .ToList();

        return await DimensionScorer.ScoreAsync(
            all, observations, mode, signalToLayer,
            shadowAnchor.EstimateLevelAsync);
    }

    // Shadow matrix: raw visualization of per-signal shadow levels (embedding-only).
    // Stays here because it's a specialized visualization endpoint, not core scoring.
    public async Task<ShadowMatrixResponse> GetShadowMatrixAsync(
        string person, ScoringMode mode = ScoringMode.Work)
    {
        var modeStr = mode == ScoringMode.Work ? "work" : "private";

        var all = await dimDefs.GetAllAsync();
        var signalToLayer = await dimDefs.GetSignalToLayerAsync();

        var dimensions = all.Select(d => d.Name).ToList();
        var signals = signalToLayer.Keys
            .OrderBy(c => signalToLayer[c] switch
            {
                "neurotransmitter" => 0, "hormone" => 1, "peptide" => 2, _ => 3
            })
            .ToList();

        var entries = await observationRepo.GetObservationEntriesAsync(person);
        if (entries.Count == 0)
            return new ShadowMatrixResponse(person, modeStr, [], dimensions, signals);

        var cells = new List<ShadowMatrixCell>();
        var now = DateTime.UtcNow;

        foreach (var dim in all)
        {
            // Get relevant signals from the dimension's signal affinity keys
            var relevantSignals = new HashSet<string>(
                dim.SignalAffinity.Keys, StringComparer.OrdinalIgnoreCase);

            var groups = entries
                .Where(e => relevantSignals.Contains(e.Signal))
                .GroupBy(e => e.Signal, StringComparer.OrdinalIgnoreCase);

            foreach (var group in groups)
            {
                var signal = group.Key;
                var layer = signalToLayer.TryGetValue(signal, out var l) ? l : "unknown";

                float weightedSum = 0, weightTotal = 0;
                foreach (var entry in group)
                {
                    var levelEmb = await shadowAnchor.EstimateLevelAsync(
                        dim.Name, modeStr, signal, entry.Embedding);
                    var daysSince = (float)(now - entry.CreatedAt).TotalDays;
                    var recency = MathF.Exp(-DecayLambda * daysSince);
                    weightedSum += levelEmb * recency;
                    weightTotal += recency;
                }

                var shadowLevel = weightTotal > 0 ? weightedSum / weightTotal : 3f;
                var confidence = LevelEstimator.Sigmoid(group.Count(), SigmoidThreshold);

                cells.Add(new ShadowMatrixCell(
                    dim.Name, dim.Section, signal, layer,
                    MathF.Round(shadowLevel, 2),
                    MathF.Round(confidence, 2),
                    group.Count()));
            }
        }

        return new ShadowMatrixResponse(person, modeStr, cells, dimensions, signals);
    }
}
