using NeuroGateway.Service;

namespace NeuroGateway.Server.Api;

public static class PersonalSphereApi
{
    public static RouteGroupBuilder MapPersonalSphereApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/personal-sphere").WithTags("PersonalSphere");

        // Full PersonalSphere dashboard — uses layer agents LLM to generate
        // personal insights, deep patterns, leverage points, strengths,
        // system radar, and energy curve
        group.MapGet("/{person}", async (string person, PersonalSphereService svc) =>
        {
            var result = await svc.GetInsightsAsync(person);
            return result is null
                ? Results.NotFound(new { error = $"Person '{person}' not found" })
                : Results.Ok(result);
        });

        return group;
    }
}
