using Microsoft.EntityFrameworkCore;
using NeuroGateway.Repository.Entities;

namespace NeuroGateway.Repository;

public class SignalInteractionRepository(IDbContextFactory<PersonalityDbContext> factory)
{
    public record InteractionDto(
        int Id,
        string SourceKey, string SourceLabel, string SourceLayer,
        string TargetKey, string TargetLabel, string TargetLayer,
        string Operator, float? ModFactor, string? Mechanism,
        string? Temporal, string? RegionKey);

    public async Task<List<InteractionDto>> ListAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.SignalInteractions
            .Join(db.Signals, i => i.SourceSignalId, s => s.Id, (i, s) => new { i, Source = s })
            .Join(db.Signals, x => x.i.TargetSignalId, s => s.Id, (x, t) => new { x.i, x.Source, Target = t })
            .GroupJoin(db.BrainRegions, x => x.i.RegionId, r => r.Id, (x, regions) => new { x, regions })
            .SelectMany(x => x.regions.DefaultIfEmpty(), (x, region) => new InteractionDto(
                x.x.i.Id,
                x.x.Source.Key, x.x.Source.Label, x.x.Source.Layer,
                x.x.Target.Key, x.x.Target.Label, x.x.Target.Layer,
                x.x.i.Operator, x.x.i.ModFactor, x.x.i.Mechanism,
                x.x.i.Temporal, region != null ? region.Key : null))
            .ToListAsync();
    }

    public async Task<List<InteractionDto>> GetForSignalAsync(string signalKey)
    {
        await using var db = await factory.CreateDbContextAsync();
        var signalId = await db.Signals
            .Where(s => s.Key == signalKey)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync();

        if (signalId is null) return [];

        return await db.SignalInteractions
            .Where(i => i.SourceSignalId == signalId || i.TargetSignalId == signalId)
            .Join(db.Signals, i => i.SourceSignalId, s => s.Id, (i, s) => new { i, Source = s })
            .Join(db.Signals, x => x.i.TargetSignalId, s => s.Id, (x, t) => new { x.i, x.Source, Target = t })
            .GroupJoin(db.BrainRegions, x => x.i.RegionId, r => r.Id, (x, regions) => new { x, regions })
            .SelectMany(x => x.regions.DefaultIfEmpty(), (x, region) => new InteractionDto(
                x.x.i.Id,
                x.x.Source.Key, x.x.Source.Label, x.x.Source.Layer,
                x.x.Target.Key, x.x.Target.Label, x.x.Target.Layer,
                x.x.i.Operator, x.x.i.ModFactor, x.x.i.Mechanism,
                x.x.i.Temporal, region != null ? region.Key : null))
            .ToListAsync();
    }

    /// <summary>
    /// Get all interactions as a lookup for the analysis framework.
    /// Key: (sourceSignalId, targetSignalId), Value: (modFactor, operator)
    /// </summary>
    public async Task<Dictionary<(int, int), (float ModFactor, string Operator)>> GetInteractionMapAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.SignalInteractions
            .Where(i => i.ModFactor != null)
            .ToDictionaryAsync(
                i => (i.SourceSignalId, i.TargetSignalId),
                i => (i.ModFactor!.Value, i.Operator));
    }
}
