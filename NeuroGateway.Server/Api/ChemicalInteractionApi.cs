using NeuroGateway.Repository;

namespace NeuroGateway.Server.Api;

public static class ChemicalInteractionApi
{
    public static RouteGroupBuilder MapChemicalInteractionApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/chemical-interactions").WithTags("ChemicalInteractions");

        group.MapGet("/", async (ChemicalInteractionRepository repo) =>
        {
            var interactions = await repo.ListAsync();
            return Results.Ok(interactions.Select(i => new
            {
                i.Id,
                i.SourceKey, i.SourceLabel, i.SourceLayer,
                i.TargetKey, i.TargetLabel, i.TargetLayer,
                i.ModFactor, i.Mechanism, i.Notes
            }));
        });

        group.MapGet("/{chemical}", async (string chemical, ChemicalInteractionRepository repo) =>
        {
            var interactions = await repo.GetForChemicalAsync(chemical);
            return Results.Ok(interactions.Select(i => new
            {
                i.Id,
                i.SourceKey, i.SourceLabel, i.SourceLayer,
                i.TargetKey, i.TargetLabel, i.TargetLayer,
                i.ModFactor, i.Mechanism, i.Notes
            }));
        });

        group.MapPost("/", async (InteractionCreateRequest req, ChemicalInteractionRepository repo) =>
        {
            var entity = await repo.CreateAsync(req.SourceChemicalId, req.TargetChemicalId,
                req.ModFactor, req.Mechanism, req.Notes);
            return Results.Created($"/api/chemical-interactions/{entity.Id}",
                new { entity.Id });
        });

        group.MapPut("/{id:int}", async (int id, InteractionUpdateRequest req, ChemicalInteractionRepository repo) =>
        {
            var ok = await repo.UpdateAsync(id, req.ModFactor, req.Mechanism, req.Notes);
            return ok ? Results.NoContent() : Results.NotFound();
        });

        group.MapDelete("/{id:int}", async (int id, ChemicalInteractionRepository repo) =>
        {
            var ok = await repo.DeleteAsync(id);
            return ok ? Results.NoContent() : Results.NotFound();
        });

        return group;
    }

    public record InteractionCreateRequest(int SourceChemicalId, int TargetChemicalId,
        float ModFactor, string? Mechanism = null, string? Notes = null);

    public record InteractionUpdateRequest(float ModFactor, string? Mechanism = null, string? Notes = null);
}
