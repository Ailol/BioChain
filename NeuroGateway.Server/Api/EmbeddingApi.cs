using NeuroGateway.Service;

namespace NeuroGateway.Server.Api;

public static class EmbeddingApi
{
    public static RouteGroupBuilder MapEmbeddingApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/embeddings").WithTags("Embeddings");

        group.MapPost("/backfill", async (BackfillRequest? req, EmbeddingService svc) =>
        {
            var count = await svc.BackfillAsync(req?.Person);
            return Results.Ok(new
            {
                embeddingsGenerated = count,
                message = count > 0 ? $"Generated {count} embedding(s)" : "No entries pending embeddings"
            });
        });

        return group;
    }
}

public record BackfillRequest(string? Person = null);
