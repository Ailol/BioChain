using BioChain.Service;

namespace BioChain.Server.Api;

public static class EmbeddingApi
{
    public static RouteGroupBuilder MapEmbeddingApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/embeddings").WithTags("Embeddings");

        group.MapPost("/backfill", async (BackfillRequest? req, EmbeddingService svc) =>
        {
            var (adCount, profileCount) = await svc.BackfillAsync(req?.Person);
            var total = adCount + profileCount;
            return Results.Ok(new
            {
                analyzed_data_embeddings = adCount,
                profile_embeddings = profileCount,
                total,
                message = total > 0
                    ? $"Generated {adCount} analyzed_data + {profileCount} profile embedding(s)"
                    : "No entries pending embeddings"
            });
        });

        return group;
    }
}

public record BackfillRequest(string? Person = null);
