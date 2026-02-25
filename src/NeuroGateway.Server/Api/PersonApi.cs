using NeuroGateway.AnalysisFramework;
using NeuroGateway.Repository;
using NeuroGateway.Service;

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
            var counts = await profileSvc.GetSignalCountsAsync(name);
            var profiles = await profileSvc.GetProfileAsync(name);
            return Results.Ok(new
            {
                person = name,
                communicationStyle = style,
                signalCounts = counts.Select(c => new { c.Signal, c.Count }),
                profiles = profiles.Select(p => new { p.Signal, p.Formula })
            });
        });

        group.MapGet("/{name}/style", async (string name, ProfileService profileSvc) =>
        {
            var style = await profileSvc.GetCommunicationStyleAsync(name);
            return Results.Ok(new { person = name, communicationStyle = style });
        });

        group.MapGet("/{name}/signals", async (string name, ProfileService profileSvc) =>
        {
            var counts = await profileSvc.GetSignalCountsAsync(name);
            return Results.Ok(new
            {
                person = name,
                signals = counts.Select(c => new { c.Signal, c.Count })
            });
        });

        group.MapGet("/{name}/profile/timeline", async (string name, ObservationRepository obsRepo) =>
        {
            var entries = await obsRepo.GetTimelineAsync(name);
            return Results.Ok(new
            {
                person = name,
                entries = entries.Select(e => new
                {
                    e.Signal,
                    e.Intensity,
                    createdAt = e.CreatedAt.ToString("o")
                })
            });
        });

        // Share a person with another user by email
        group.MapPost("/{name}/share", async (string name, SharePersonRequest req, PersonRepository personRepo, PersonShareRepository shareRepo) =>
        {
            var personId = await personRepo.GetIdAsync(name);
            if (personId is null) return Results.NotFound(new { error = "Person not found" });
            await shareRepo.ShareAsync(personId.Value, req.Email);
            return Results.Ok(new { shared = true });
        });

        // Unshare a person (email as query param — DELETE doesn't support inferred body)
        group.MapDelete("/{name}/share", async (string name, string email, PersonRepository personRepo, PersonShareRepository shareRepo) =>
        {
            var personId = await personRepo.GetIdAsync(name);
            if (personId is null) return Results.NotFound(new { error = "Person not found" });
            await shareRepo.UnshareAsync(personId.Value, email);
            return Results.Ok(new { unshared = true });
        });

        // List shares for a person
        group.MapGet("/{name}/shares", async (string name, PersonRepository personRepo, PersonShareRepository shareRepo) =>
        {
            var personId = await personRepo.GetIdAsync(name);
            if (personId is null) return Results.NotFound(new { error = "Person not found" });
            var shares = await shareRepo.ListSharesAsync(personId.Value);
            return Results.Ok(new { shares = shares.Select(s => new { s.Email, sharedAt = s.SharedAt.ToString("o") }) });
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
public record SharePersonRequest(string Email);
