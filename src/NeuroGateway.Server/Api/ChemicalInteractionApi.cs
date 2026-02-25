using NeuroGateway.Repository;

namespace NeuroGateway.Server.Api;

public static class SignalInteractionApi
{
    public static RouteGroupBuilder MapSignalInteractionApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/signal-interactions").WithTags("SignalInteractions");

        group.MapGet("/", async (SignalInteractionRepository repo) =>
        {
            var interactions = await repo.ListAsync();
            return Results.Ok(interactions.Select(i => new
            {
                i.Id,
                i.SourceKey, i.SourceLabel, i.SourceLayer,
                i.TargetKey, i.TargetLabel, i.TargetLayer,
                i.Operator, i.ModFactor, i.Mechanism,
                i.Temporal, i.RegionKey
            }));
        });

        group.MapGet("/{signalKey}", async (string signalKey, SignalInteractionRepository repo) =>
        {
            var interactions = await repo.GetForSignalAsync(signalKey);
            return Results.Ok(interactions.Select(i => new
            {
                i.Id,
                i.SourceKey, i.SourceLabel, i.SourceLayer,
                i.TargetKey, i.TargetLabel, i.TargetLayer,
                i.Operator, i.ModFactor, i.Mechanism,
                i.Temporal, i.RegionKey
            }));
        });

        return group;
    }
}
