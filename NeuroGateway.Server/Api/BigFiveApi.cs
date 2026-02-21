using NeuroGateway.Service;

namespace NeuroGateway.Server.Api;

public static class BigFiveApi
{
    public static RouteGroupBuilder MapBigFiveApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/bigfive").WithTags("BigFive");

        // Big Five (OCEAN) personality trait classification via embedding similarity
        group.MapGet("/{person}", async (string person, ProfileAnalysisService svc) =>
        {
            var result = await svc.GetBigFiveAsync(person);
            return Results.Ok(result);
        });

        return group;
    }
}
