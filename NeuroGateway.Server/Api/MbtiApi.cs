using NeuroGateway.Service;

namespace NeuroGateway.Server.Api;

public static class MbtiApi
{
    public static RouteGroupBuilder MapMbtiApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/mbti").WithTags("MBTI");

        // MBTI personality type classification via embedding similarity
        group.MapGet("/{person}", async (string person, ProfileAnalysisService svc) =>
        {
            var result = await svc.GetMbtiAsync(person);
            return Results.Ok(result);
        });

        return group;
    }
}
