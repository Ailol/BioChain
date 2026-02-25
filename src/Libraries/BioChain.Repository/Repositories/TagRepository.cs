using Microsoft.EntityFrameworkCore;
using BioChain.Repository.Entities;

namespace BioChain.Repository;

public class TagRepository(IDbContextFactory<PersonalityDbContext> factory)
{
    public async Task<TagEntity?> GetByKeyAsync(string key)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Tags.FirstOrDefaultAsync(t => t.Key == key);
    }

    public async Task<List<TagEntity>> GetByTypeAsync(string tagType)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Tags.Where(t => t.TagType == tagType).ToListAsync();
    }

    public async Task TagEntityAsync(int tagId, string entityType, string entityId,
        string? severity = null, string? confidence = null, string? notes = null)
    {
        await using var db = await factory.CreateDbContextAsync();
        var existing = await db.EntityTags
            .FirstOrDefaultAsync(e => e.TagId == tagId && e.EntityType == entityType && e.EntityId == entityId);

        if (existing is not null)
        {
            existing.Severity = severity ?? existing.Severity;
            existing.Confidence = confidence ?? existing.Confidence;
            existing.Notes = notes ?? existing.Notes;
        }
        else
        {
            db.EntityTags.Add(new EntityTagEntity
            {
                TagId = tagId,
                EntityType = entityType,
                EntityId = entityId,
                Severity = severity,
                Confidence = confidence,
                Notes = notes,
                CreatedAt = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();
    }

    public async Task<List<EntityTagEntity>> GetTagsForEntityAsync(string entityType, string entityId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.EntityTags
            .Where(e => e.EntityType == entityType && e.EntityId == entityId)
            .ToListAsync();
    }
}
