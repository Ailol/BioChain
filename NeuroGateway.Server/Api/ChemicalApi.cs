using NeuroGateway.Repository;

namespace NeuroGateway.Server.Api;

public static class ChemicalApi
{
    public static RouteGroupBuilder MapChemicalApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/chemicals").WithTags("Chemicals");

        group.MapGet("/", async (ChemicalRepository repo) =>
        {
            var chemicals = await repo.ListAsync();
            return Results.Ok(chemicals.Select(c => new
            {
                c.Id, c.Key, c.Label, c.Layer
            }));
        });

        group.MapGet("/{key}", async (string key, ChemicalRepository repo) =>
        {
            var chemical = await repo.GetByKeyAsync(key);
            return chemical is null
                ? Results.NotFound()
                : Results.Ok(new { chemical.Id, chemical.Key, chemical.Label, chemical.Layer });
        });

        group.MapPost("/", async (ChemicalCreateRequest req, ChemicalRepository repo) =>
        {
            var entity = await repo.CreateAsync(req.Key, req.Label, req.Layer);
            return Results.Created($"/api/chemicals/{entity.Key}",
                new { entity.Id, entity.Key, entity.Label, entity.Layer });
        });

        group.MapPut("/{id:int}", async (int id, ChemicalCreateRequest req, ChemicalRepository repo) =>
        {
            var ok = await repo.UpdateAsync(id, req.Key, req.Label, req.Layer);
            return ok ? Results.NoContent() : Results.NotFound();
        });

        group.MapDelete("/{id:int}", async (int id, ChemicalRepository repo) =>
        {
            var ok = await repo.DeleteAsync(id);
            return ok ? Results.NoContent() : Results.NotFound();
        });

        return group;
    }

    public record ChemicalCreateRequest(string Key, string Label, string Layer);
}
