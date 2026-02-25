using BioChain.Service;

namespace BioChain.Server.Api;

public static class BioSphereApi
{
    public static RouteGroupBuilder MapBioSphereApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/biosphere").WithTags("BioSphere");

        // Full BioSphere dashboard — aggregates signal profile, radar, trajectory,
        // loops, cascades, gates, region heatmap, failure modes, lifecycle stages
        group.MapGet("/{person}", async (string person, BioSphereService svc) =>
        {
            var result = await svc.GetDashboardAsync(person);
            return result is null
                ? Results.NotFound(new { error = $"Person '{person}' not found" })
                : Results.Ok(result);
        });

        return group;
    }
}
