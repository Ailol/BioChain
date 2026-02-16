using NeuroGateway.Repository;
using NeuroGateway.Service;
using static NeuroGateway.AnalysisFramework.DimensionDefinitions;

namespace NeuroGateway.Server.Api;

public static class PersonApi
{
    public static RouteGroupBuilder MapPersonApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/persons").WithTags("Persons");

        group.MapGet("/", async (PersonService svc) =>
        {
            var persons = await svc.ListAsync();
            return Results.Ok(new { persons });
        });

        group.MapPost("/", async (CreatePersonRequest req, PersonService svc) =>
        {
            var (personId, personalityId) = await svc.EnsureAsync(req.Name);
            return Results.Ok(new { personId, personalityId });
        });

        group.MapGet("/{name}/profile", async (string name, ProfileService profileSvc) =>
        {
            var style = await profileSvc.GetCommunicationStyleAsync(name);
            var counts = await profileSvc.GetChemicalCountsAsync(name);
            var profiles = await profileSvc.GetProfileAsync(name);
            return Results.Ok(new
            {
                person = name,
                communicationStyle = style,
                chemicalCounts = counts.Select(c => new { c.Chemical, c.Count }),
                profiles = profiles.Select(p => new { p.Chemical, p.Reasoning })
            });
        });

        group.MapGet("/{name}/style", async (string name, ProfileService profileSvc) =>
        {
            var style = await profileSvc.GetCommunicationStyleAsync(name);
            return Results.Ok(new { person = name, communicationStyle = style });
        });

        group.MapGet("/{name}/chemicals", async (string name, ProfileService profileSvc) =>
        {
            var counts = await profileSvc.GetChemicalCountsAsync(name);
            return Results.Ok(new
            {
                person = name,
                chemicals = counts.Select(c => new { c.Chemical, c.Count })
            });
        });

        group.MapGet("/{name}/profile/timeline", async (string name, ProfileRepository profileRepo) =>
        {
            var entries = await profileRepo.GetTimelineAsync(name);
            return Results.Ok(new
            {
                person = name,
                entries = entries.Select(e => new
                {
                    e.Chemical,
                    e.IntensityFactor,
                    createdAt = e.CreatedAt.ToString("o")
                })
            });
        });

        return group;
    }

    public static RouteGroupBuilder MapDimensionApi(this RouteGroupBuilder group)
    {
        group.MapGet("/{name}/dimensions", async (string name, string? mode, DimensionService dimSvc) =>
        {
            var scoringMode = mode?.ToLowerInvariant() == "private" ? ScoringMode.Private : ScoringMode.Work;
            var scores = await dimSvc.ScoreAsync(name, scoringMode);
            return Results.Ok(new
            {
                person = name,
                mode = scoringMode.ToString().ToLowerInvariant(),
                behavioral = scores.Where(s => s.Section == "work"),
                personal = scores.Where(s => s.Section == "private")
            });
        });

        group.MapGet("/{name}/shadow-matrix", async (string name, string? mode, DimensionService dimSvc) =>
        {
            var scoringMode = mode?.ToLowerInvariant() == "private" ? ScoringMode.Private : ScoringMode.Work;
            var matrix = await dimSvc.GetShadowMatrixAsync(name, scoringMode);
            return Results.Ok(matrix);
        });

        return group;
    }
}

public record CreatePersonRequest(string Name);
