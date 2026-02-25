using Microsoft.EntityFrameworkCore;
using BioChain.Repository.Entities;

namespace BioChain.Repository;

public class AnalysisRunRepository(IDbContextFactory<PersonalityDbContext> factory)
{
    public async Task<AnalysisRunEntity> CreateAsync(Guid personId, int analysisTypeId,
        string? triggeredBy = null, Guid? parentRunId = null, int[]? inputDataIds = null)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = new AnalysisRunEntity
        {
            PersonId = personId,
            AnalysisTypeId = analysisTypeId,
            Status = "pending",
            TriggeredBy = triggeredBy,
            ParentRunId = parentRunId,
            InputDataIds = inputDataIds,
            CreatedAt = DateTime.UtcNow
        };
        db.AnalysisRuns.Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }

    public async Task StartAsync(Guid runId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = await db.AnalysisRuns.FindAsync(runId);
        if (entity is null) return;
        entity.Status = "running";
        entity.StartedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task CompleteAsync(Guid runId, string? summaryJson = null)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = await db.AnalysisRuns.FindAsync(runId);
        if (entity is null) return;
        entity.Status = "completed";
        entity.CompletedAt = DateTime.UtcNow;
        entity.Summary = summaryJson;
        await db.SaveChangesAsync();
    }

    public async Task FailAsync(Guid runId, string error)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = await db.AnalysisRuns.FindAsync(runId);
        if (entity is null) return;
        entity.Status = "failed";
        entity.CompletedAt = DateTime.UtcNow;
        entity.Error = error;
        await db.SaveChangesAsync();
    }

    public async Task<List<AnalysisRunEntity>> GetForPersonAsync(Guid personId, int? limit = null)
    {
        await using var db = await factory.CreateDbContextAsync();
        var query = db.AnalysisRuns
            .Where(r => r.PersonId == personId)
            .OrderByDescending(r => r.CreatedAt);
        if (limit.HasValue)
            return await query.Take(limit.Value).ToListAsync();
        return await query.ToListAsync();
    }
}
