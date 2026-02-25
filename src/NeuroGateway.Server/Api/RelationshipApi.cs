using NeuroGateway.Repository;

namespace NeuroGateway.Server.Api;

public static class RelationshipApi
{
    public static RouteGroupBuilder MapRelationshipApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/relationships").WithTags("Relationships");

        group.MapGet("/", async (RelationshipRepository repo) =>
        {
            var types = await repo.ListAsync();
            return Results.Ok(new
            {
                relationshipTypes = types.Select(t => new { t.Name, t.Description })
            });
        });

        return group;
    }
}
