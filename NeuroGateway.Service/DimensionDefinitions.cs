using NeuroGateway.Repository;
using static NeuroGateway.AnalysisFramework.DimensionDefinitions;

namespace NeuroGateway.Service;

/// <summary>
/// DB-backed dimension definitions service. Loads chemicals and dimensions from the database
/// on first access, caches in memory. Call <see cref="InvalidateCache"/> after CRUD mutations.
/// </summary>
public class DimensionDefinitionsService(DimensionRepository dimRepo, ChemicalRepository chemRepo)
{
    private IReadOnlyList<DimensionDef>? _all;
    private IReadOnlyDictionary<string, string>? _chemicalToLayer;

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
            d.Dimension.PrivateRelevance
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

    public void InvalidateCache()
    {
        _all = null;
        _chemicalToLayer = null;
    }
}
