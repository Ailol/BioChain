using NeuroGateway.Service;

namespace NeuroGateway.Server.Api;

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

        // Delete all MBTI + Big Five prototype embeddings from DB and clear
        // in-memory caches. Next classification call will regenerate them.
        group.MapPost("/reembed-prototypes", async (MbtiService mbti, BigFiveService bigFive) =>
        {
            var mbtiDeleted = await mbti.ReembedAsync();
            var bigFiveDeleted = await bigFive.ReembedAsync();
            var total = mbtiDeleted + bigFiveDeleted;
            return Results.Ok(new
            {
                mbti_deleted = mbtiDeleted,
                bigfive_deleted = bigFiveDeleted,
                total,
                message = $"Cleared {mbtiDeleted} MBTI + {bigFiveDeleted} Big Five prototype embeddings. They will regenerate on next classification."
            });
        });

        return group;
    }
}

public record BackfillRequest(string? Person = null);
