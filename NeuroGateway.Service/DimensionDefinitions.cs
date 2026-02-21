using NeuroGateway.Repository;
using static NeuroGateway.AnalysisFramework.DimensionDefinitions;

namespace NeuroGateway.Service;

// DB-backed dimension definitions service. Loads chemicals, dimensions, and interactions
// from the database on first access, caches in memory.
// Call InvalidateCache() after CRUD mutations.
public class DimensionDefinitionsService(
    DimensionRepository dimRepo,
    ChemicalRepository chemRepo,
    ChemicalInteractionRepository interactionRepo)
{
    private IReadOnlyList<DimensionDef>? _all;
    private IReadOnlyDictionary<string, string>? _chemicalToLayer;
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
                a => a.ChemicalKey,
                a => a.Weight,
                StringComparer.OrdinalIgnoreCase),
            d.Dimension.WorkRelevance,
            d.Dimension.PrivateRelevance,
            d.Dimension.ArchetypeName,
            d.Dimension.ArchetypeEssence
        )).ToList();

        return _all;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetChemicalToLayerAsync()
    {
        if (_chemicalToLayer is not null) return _chemicalToLayer;

        var chemicals = await chemRepo.ListAsync();
        _chemicalToLayer = chemicals.ToDictionary(
            c => c.Key, c => c.Layer, StringComparer.OrdinalIgnoreCase);

        return _chemicalToLayer;
    }

    // Returns cached lookup: (sourceChemicalKey, targetChemicalKey) -> (modFactor, mechanism).
    // Keys are lowercased for consistent lookup.
    public async Task<IReadOnlyDictionary<(string Source, string Target), (float ModFactor, string? Mechanism)>> GetInteractionsAsync()
    {
        if (_interactions is not null) return _interactions;

        var raw = await interactionRepo.ListAsync();
        _interactions = raw.ToDictionary(
            i => (i.SourceKey.ToLowerInvariant(), i.TargetKey.ToLowerInvariant()),
            i => (i.ModFactor, i.Mechanism));

        return _interactions;
    }

    public void InvalidateCache()
    {
        _all = null;
        _chemicalToLayer = null;
        _interactions = null;
    }
}
