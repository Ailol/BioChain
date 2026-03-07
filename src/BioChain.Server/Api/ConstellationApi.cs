using BioChain.Repository;
using BioChain.Repository.Repositories;
using BioChain.Service;

namespace BioChain.Server.Api;

public static class ConstellationApi
{
    public static RouteGroupBuilder MapConstellationApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/constellation").WithTags("Constellation");

        // Fast graph data (DB-only): nodes, edges, communities, loops, cascades, bridges, geometry
        group.MapGet("/graph/{subjectId:guid}", async (Guid subjectId, IConstellationService svc,
            IUserContext ctx, ISubjectRepository subjects, CancellationToken ct) =>
        {
            if (!await subjects.HasAccessAsync(subjectId, ctx.UserId))
                return Results.Forbid();

            var result = await svc.GetGraphAsync(subjectId, ct);
            return Results.Ok(result);
        });

        // LLM-powered deep analysis: narratives, contradictions, compensators, motifs, etc.
        group.MapPost("/analyze/{subjectId:guid}", async (Guid subjectId, IConstellationService svc,
            IUserContext ctx, ISubjectRepository subjects, CancellationToken ct) =>
        {
            if (!await subjects.HasAccessAsync(subjectId, ctx.UserId))
                return Results.Forbid();

            var result = await svc.AnalyzeAsync(subjectId, ct);
            return Results.Ok(result);
        });

        return group;
    }
}
