using Microsoft.EntityFrameworkCore;
using NeuroGateway.Repository.Entities;

namespace NeuroGateway.Repository;

public class AnalysisTypeRepository(IDbContextFactory<PersonalityDbContext> factory)
{
    public async Task<List<AnalysisTypeEntity>> GetActiveAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.AnalysisTypes
            .Where(a => a.IsActive)
            .OrderBy(a => a.SortOrder)
            .ToListAsync();
    }

    public async Task<AnalysisTypeEntity?> GetByKeyAsync(string key)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.AnalysisTypes.FirstOrDefaultAsync(a => a.Key == key);
    }

    public async Task<List<AnalysisDimensionEntity>> GetDimensionsAsync(int analysisTypeId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.AnalysisDimensions
            .Where(d => d.AnalysisTypeId == analysisTypeId)
            .OrderBy(d => d.SortOrder)
            .ToListAsync();
    }

    public async Task<List<(AnalysisTypeEntity Type, List<AnalysisDimensionEntity> Dimensions)>> GetAllWithDimensionsAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var types = await db.AnalysisTypes
            .Where(a => a.IsActive)
            .OrderBy(a => a.SortOrder)
            .ToListAsync();

        var allDimensions = await db.AnalysisDimensions
            .OrderBy(d => d.SortOrder)
            .ToListAsync();

        return types.Select(t => (t, allDimensions.Where(d => d.AnalysisTypeId == t.Id).ToList())).ToList();
    }
}
