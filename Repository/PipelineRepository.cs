using Microsoft.EntityFrameworkCore;
using Models;

namespace Repository;

/// <summary>
/// Data access for pipeline and layer tables.
/// Pipelines are per-person, optionally scoped to a relationship type.
/// </summary>
public class PipelineRepository(IDbContextFactory<PersonalityDbContext> factory)
{
    public async Task<PipelineInfo?> GetPipelineAsync(Guid personId, string name)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var pipeline = await ctx.Pipelines
            .Include(p => p.RelationshipType)
            .Include(p => p.Layers.OrderBy(l => l.SortOrder))
                .ThenInclude(l => l.Agent)
            .FirstOrDefaultAsync(p => p.PersonId == personId && p.Name.ToLower() == name.ToLower());

        return pipeline == null ? null : MapToInfo(pipeline);
    }

    public async Task<int> CreatePipelineAsync(Guid personId, string name, int? relationshipTypeId, string? description)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var ids = await ctx.Database.SqlQueryRaw<int>("""
            INSERT INTO pipeline (name, person_id, relationship_type_id, description)
            VALUES (@p0, @p1, @p2, @p3)
            ON CONFLICT (person_id, name) DO UPDATE SET
                relationship_type_id = EXCLUDED.relationship_type_id,
                description = EXCLUDED.description,
                updated_at = NOW()
            RETURNING id AS "Value"
        """, name, personId, relationshipTypeId as object ?? DBNull.Value, description as object ?? DBNull.Value).ToListAsync();

        return ids.FirstOrDefault();
    }

    public async Task<int> AddLayerAsync(int pipelineId, string name, int agentId, int sortOrder, bool isSynthesizer)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var ids = await ctx.Database.SqlQueryRaw<int>("""
            INSERT INTO layer (pipeline_id, name, agent_id, sort_order, is_synthesizer)
            VALUES (@p0, @p1, @p2, @p3, @p4)
            ON CONFLICT (pipeline_id, sort_order) DO UPDATE SET
                name = EXCLUDED.name,
                agent_id = EXCLUDED.agent_id,
                is_synthesizer = EXCLUDED.is_synthesizer
            RETURNING id AS "Value"
        """, pipelineId, name, agentId, sortOrder, isSynthesizer).ToListAsync();

        return ids.FirstOrDefault();
    }

    public async Task<List<PipelineInfo>> ListPipelinesAsync(Guid personId)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var pipelines = await ctx.Pipelines
            .Include(p => p.RelationshipType)
            .Include(p => p.Layers.OrderBy(l => l.SortOrder))
                .ThenInclude(l => l.Agent)
            .Where(p => p.PersonId == personId)
            .OrderBy(p => p.Name)
            .ToListAsync();

        return pipelines.Select(MapToInfo).ToList();
    }

    private static PipelineInfo MapToInfo(Entities.Pipeline p) => new(
        p.Id,
        p.Name,
        p.PersonId,
        p.RelationshipType?.Name,
        p.Description,
        p.IsActive,
        p.Layers.OrderBy(l => l.SortOrder).Select(l => new LayerInfo(
            l.Id,
            l.Name,
            l.AgentId,
            l.Agent.Name,
            l.SortOrder,
            l.IsSynthesizer
        )).ToList()
    );
}
