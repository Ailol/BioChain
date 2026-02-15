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

        return group;
    }

    public static RouteGroupBuilder MapDimensionApi(this RouteGroupBuilder group)
    {
        group.MapGet("/{name}/dimensions", async (string name, DimensionService dimSvc) =>
        {
            var scores = await dimSvc.ScoreAsync(name);
            return Results.Ok(new { person = name, dimensions = scores });
        });

        group.MapPost("/{name}/dimensions/query", async (string name, DimensionQueryRequest req, DimensionService dimSvc) =>
        {
            var score = await dimSvc.ScoreCustomAsync(name, req.Query);
            return Results.Ok(new { person = name, dimension = score });
        });

        return group;
    }
}

public record CreatePersonRequest(string Name);
public record DimensionQueryRequest(string Query);
