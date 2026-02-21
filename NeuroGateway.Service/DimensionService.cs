using NeuroGateway.AnalysisFramework;
using NeuroGateway.Models;
using NeuroGateway.Repository;
using static NeuroGateway.AnalysisFramework.DimensionDefinitions;

namespace NeuroGateway.Service;

// Thin orchestrator: loads data from repositories, delegates scoring to AnalysisFramework.
public class DimensionService(
    ProfileRepository profileRepo,
    ShadowAnchorService shadowAnchor,
    DimensionDefinitionsService dimDefs)
{
    private const float DecayLambda = 0.01f; // used only by GetShadowMatrixAsync
    private const float SigmoidThreshold = 3f;

    private readonly DimensionScoring _scoring = new();

    public async Task<List<DimensionScore>> ScoreAsync(string person, ScoringMode mode = ScoringMode.Work)
    {
        var all = await dimDefs.GetAllAsync();
        var entries = await profileRepo.GetProfileEntriesAsync(person);
        var chemicalToLayer = await dimDefs.GetChemicalToLayerAsync();
        var interactions = await dimDefs.GetInteractionsAsync();

        // Map ProfileEntry -> ChemicalObservation (decouple algorithm from repository)
        var observations = entries
            .Select(e => new ChemicalObservation(e.Chemical, e.Reasoning, e.Embedding, e.IntensityFactor, e.CreatedAt))
            .ToList();

        return await _scoring.ScoreAllAsync(
            all, observations, mode, chemicalToLayer, interactions,
            shadowAnchor.EstimateLevelAsync);
    }

    // Shadow matrix: raw visualization of per-chemical shadow levels (embedding-only).
    // Stays here because it's a specialized visualization endpoint, not core scoring.
    public async Task<ShadowMatrixResponse> GetShadowMatrixAsync(
        string person, ScoringMode mode = ScoringMode.Work)
    {
        var modeStr = mode == ScoringMode.Work ? "work" : "private";

        var all = await dimDefs.GetAllAsync();
        var chemicalToLayer = await dimDefs.GetChemicalToLayerAsync();

        var dimensions = all.Select(d => d.Name).ToList();
        var chemicals = chemicalToLayer.Keys
            .OrderBy(c => chemicalToLayer[c] switch
            {
                "neurotransmitter" => 0, "hormone" => 1, "peptide" => 2, _ => 3
            })
            .ToList();

        var entries = await profileRepo.GetProfileEntriesAsync(person);
        if (entries.Count == 0)
            return new ShadowMatrixResponse(person, modeStr, [], dimensions, chemicals);

        var cells = new List<ShadowMatrixCell>();
        var now = DateTime.UtcNow;

        foreach (var dim in all)
        {
            var shadowChemicals = ShadowProfileLoader.GetChemicalsForDimension(dim.Name, modeStr);
            var relevantChemicals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in shadowChemicals) relevantChemicals.Add(c);
            foreach (var c in dim.ChemicalAffinity.Keys) relevantChemicals.Add(c);

            var groups = entries
                .Where(e => relevantChemicals.Contains(e.Chemical))
                .GroupBy(e => e.Chemical, StringComparer.OrdinalIgnoreCase);

            foreach (var group in groups)
            {
                var chemical = group.Key;
                var layer = chemicalToLayer.TryGetValue(chemical, out var l) ? l : "unknown";

                float weightedSum = 0, weightTotal = 0;
                foreach (var entry in group)
                {
                    var levelEmb = await shadowAnchor.EstimateLevelAsync(
                        dim.Name, modeStr, chemical, entry.Embedding);
                    var daysSince = (float)(now - entry.CreatedAt).TotalDays;
                    var recency = MathF.Exp(-DecayLambda * daysSince);
                    weightedSum += levelEmb * recency;
                    weightTotal += recency;
                }

                var shadowLevel = weightTotal > 0 ? weightedSum / weightTotal : 3f;
                var confidence = LevelEstimator.Sigmoid(group.Count(), SigmoidThreshold);

                cells.Add(new ShadowMatrixCell(
                    dim.Name, dim.Section, chemical, layer,
                    MathF.Round(shadowLevel, 2),
                    MathF.Round(confidence, 2),
                    group.Count()));
            }
        }

        return new ShadowMatrixResponse(person, modeStr, cells, dimensions, chemicals);
    }
}
