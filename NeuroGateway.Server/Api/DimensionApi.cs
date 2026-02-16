using NeuroGateway.Repository;

namespace NeuroGateway.Server.Api;

public static class DimensionMasterApi
{
    public static RouteGroupBuilder MapDimensionMasterApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dimensions").WithTags("Dimensions");

        group.MapGet("/", async (DimensionRepository repo) =>
        {
            var dims = await repo.ListWithAffinitiesAsync();
            return Results.Ok(dims.Select(d => new
            {
                d.Dimension.Id,
                d.Dimension.Name,
                d.Dimension.Section,
                d.Dimension.Category,
                d.Dimension.Description,
                d.Dimension.WorkRelevance,
                d.Dimension.PrivateRelevance,
                d.Dimension.SortOrder,
                Affinities = d.Affinities.Select(a => new { ChemicalKey = a.ChemicalKey, a.Weight })
            }));
        });

        group.MapPost("/", async (DimensionCreateRequest req, DimensionRepository repo) =>
        {
            var entity = await repo.CreateAsync(req.Name, req.Section, req.Category,
                req.Description, req.WorkRelevance, req.PrivateRelevance, req.SortOrder);
            return Results.Created($"/api/dimensions/{entity.Id}",
                new { entity.Id, entity.Name });
        });

        group.MapPut("/{id:int}", async (int id, DimensionCreateRequest req, DimensionRepository repo) =>
        {
            var ok = await repo.UpdateAsync(id, req.Name, req.Section, req.Category,
                req.Description, req.WorkRelevance, req.PrivateRelevance, req.SortOrder);
            return ok ? Results.NoContent() : Results.NotFound();
        });

        group.MapDelete("/{id:int}", async (int id, DimensionRepository repo) =>
        {
            var ok = await repo.DeleteAsync(id);
            return ok ? Results.NoContent() : Results.NotFound();
        });

        group.MapPut("/{id:int}/affinities", async (int id, AffinityRequest req, DimensionRepository repo) =>
        {
            await repo.SetAffinityAsync(id, req.ChemicalId, req.Weight);
            return Results.NoContent();
        });

        group.MapDelete("/{dimensionId:int}/affinities/{chemicalId:int}",
            async (int dimensionId, int chemicalId, DimensionRepository repo) =>
            {
                var ok = await repo.RemoveAffinityAsync(dimensionId, chemicalId);
                return ok ? Results.NoContent() : Results.NotFound();
            });

        return group;
    }

    public record DimensionCreateRequest(string Name, string Section, string Category,
        string Description, float WorkRelevance, float PrivateRelevance, int SortOrder);

    public record AffinityRequest(int ChemicalId, float Weight);
}
