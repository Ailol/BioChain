using NeuroGateway.Service;

namespace NeuroGateway.Server.Api;

public static class AnalyzeApi
{
    public static RouteGroupBuilder MapAnalyzeApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/analyze").WithTags("Analyze");

        group.MapPost("/chat", async (ChatAnalyzeRequest req, NeuroService svc) =>
        {
            var result = await svc.ChatRespondAsync(
                req.Person, req.Text, req.Relationship, req.ProjectedRelationship, req.Save);
            return Results.Ok(new
            {
                person = req.Person,
                sourceType = "chat",
                decisions = result.Decisions.Select(d => new { d.Chemical, d.Reasoning }),
                synthesis = result.Synthesis,
                layerResponses = result.LayerResponses,
                suggestedResponse = result.SuggestedResponse
            });
        });

        group.MapPost("/work", async (WorkAnalyzeRequest req, NeuroService svc) =>
        {
            var result = await svc.WorkAnalyzeAsync(req.Person, req.Text, req.Relationship, req.Save);
            return Results.Ok(new
            {
                person = req.Person,
                sourceType = "work",
                decisionsCount = result.Decisions.Count,
                decisions = result.Decisions.Select(d => new { d.Chemical, d.Reasoning }),
                synthesis = result.Synthesis
            });
        });

        group.MapPost("/journal", async (JournalAnalyzeRequest req, NeuroService svc) =>
        {
            var result = await svc.JournalAnalyzeAsync(req.Person, req.Text, req.Save);
            return Results.Ok(new
            {
                person = req.Person,
                sourceType = "journal",
                decisionsCount = result.Decisions.Count,
                decisions = result.Decisions.Select(d => new { d.Chemical, d.Reasoning }),
                synthesis = result.Synthesis
            });
        });

        return group;
    }
}

public record ChatAnalyzeRequest(
    string Person,
    string Text,
    string? Relationship = null,
    string? ProjectedRelationship = null,
    bool Save = true);

public record WorkAnalyzeRequest(
    string Person,
    string Text,
    string? Relationship = null,
    bool Save = true);

public record JournalAnalyzeRequest(
    string Person,
    string Text,
    bool Save = true);
