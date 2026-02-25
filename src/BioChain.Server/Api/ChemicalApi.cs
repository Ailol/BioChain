using BioChain.Repository;

namespace BioChain.Server.Api;

public static class SignalApi
{
    public static RouteGroupBuilder MapSignalApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/signals").WithTags("Signals");

        group.MapGet("/", async (SignalRepository repo) =>
        {
            var signals = await repo.ListAsync();
            return Results.Ok(signals.Select(s => new
            {
                s.Id, s.Key, s.Label, s.Layer, s.Code, s.Unit
            }));
        });

        group.MapGet("/by-layer/{layer}", async (string layer, SignalRepository repo) =>
        {
            var signals = await repo.ListByLayerAsync(layer);
            return Results.Ok(signals.Select(s => new
            {
                s.Id, s.Key, s.Label, s.Layer, s.Code, s.Unit
            }));
        });

        group.MapGet("/{key}", async (string key, SignalRepository repo) =>
        {
            var signal = await repo.GetByKeyAsync(key);
            return signal is null
                ? Results.NotFound()
                : Results.Ok(new { signal.Id, signal.Key, signal.Label, signal.Layer, signal.Code, signal.Unit });
        });

        return group;
    }
}
