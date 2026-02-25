using NeuroGateway.AnalysisFramework;
using NeuroGateway.Repository;

namespace NeuroGateway.Service;

// DB-backed dimension definitions service. Loads signals, dimensions, and interactions
// from the database on first access, caches in memory.
// Call InvalidateCache() after CRUD mutations.
public class DimensionDefinitionsService(
    DimensionRepository dimRepo,
    SignalRepository signalRepo,
    SignalInteractionRepository interactionRepo)
{
    private IReadOnlyList<DimensionDef>? _all;
    private IReadOnlyDictionary<string, string>? _signalToLayer;
    private IReadOnlyDictionary<(string Source, string Target), (float ModFactor, string? Mechanism)>? _interactions;

    public async Task<IReadOnlyList<DimensionDef>> GetAllAsync()
    {
        if (_all is not null) return _all;

        var dims = await dimRepo.ListWithAffinitiesAsync();
        _all = dims.Select(d => new DimensionDef(
            d.Dimension.Name,
            d.Dimension.Section,
            d.Dimension.Category,
            d.Dimension.Description,
            d.Affinities.ToDictionary(
                a => a.SignalKey,
                a => a.Weight,
                StringComparer.OrdinalIgnoreCase),
            d.Dimension.WorkRelevance,
            d.Dimension.PrivateRelevance,
            d.Dimension.ArchetypeName,
            d.Dimension.ArchetypeEssence
        )).ToList();

        return _all;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetSignalToLayerAsync()
    {
        if (_signalToLayer is not null) return _signalToLayer;

        var signals = await signalRepo.ListAsync();
        _signalToLayer = signals.ToDictionary(
            c => c.Key, c => c.Layer, StringComparer.OrdinalIgnoreCase);

        return _signalToLayer;
    }

    // Returns cached lookup: (sourceSignalKey, targetSignalKey) -> (modFactor, mechanism).
    // Keys are lowercased for consistent lookup.
    public async Task<IReadOnlyDictionary<(string Source, string Target), (float ModFactor, string? Mechanism)>> GetInteractionsAsync()
    {
        if (_interactions is not null) return _interactions;

        var raw = await interactionRepo.ListAsync();
        _interactions = raw.ToDictionary(
            i => (i.SourceKey.ToLowerInvariant(), i.TargetKey.ToLowerInvariant()),
            i => (i.ModFactor ?? 1f, i.Mechanism));

        return _interactions;
    }

    public void InvalidateCache()
    {
        _all = null;
        _signalToLayer = null;
        _interactions = null;
    }
}
