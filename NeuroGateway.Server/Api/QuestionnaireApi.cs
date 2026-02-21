using NeuroGateway.Repository;
using NeuroGateway.Service;

namespace NeuroGateway.Server.Api;

public static class QuestionnaireApi
{
    public static RouteGroupBuilder MapQuestionnaireApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/questionnaire").WithTags("Questionnaire");

        // List all questions with options (for local preview / building the UI) — public
        group.MapGet("/questions", async (QuestionnaireRepository repo) =>
        {
            var items = await repo.ListItemsAsync();
            var questions = items
                .GroupBy(i => i.SortOrder)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    sortOrder = g.Key,
                    scenario = g.First().Scenario,
                    isInverted = g.First().IsInverted,
                    options = g.Select(i => new { i.Id, i.Label, text = i.OptionText }).ToList()
                });
            return Results.Ok(new { questions });
        }).AllowAnonymous();

        // Create a questionnaire for a person (returns the shareable token) — requires auth
        group.MapPost("/", async (CreateQuestionnaireRequest req, QuestionnaireService svc) =>
        {
            var token = await svc.CreateAsync(req.PersonName);
            return Results.Ok(new { token });
        }).RequireAuthorization();

        // Load questionnaire by token (questions + person + status) — public
        group.MapGet("/{token}", async (string token, QuestionnaireService svc) =>
        {
            var view = await svc.GetByTokenAsync(token);
            if (view is null) return Results.NotFound(new { error = "Questionnaire not found" });
            return Results.Ok(view);
        }).AllowAnonymous();

        // Submit answers and trigger the agent analysis pipeline (batch) — public
        group.MapPost("/{token}/submit", async (string token, SubmitQuestionnaireRequest req, QuestionnaireService svc) =>
        {
            await svc.SubmitAndScoreAsync(token, req.SelectedItemIds);
            return Results.Ok(new { status = "completed" });
        }).AllowAnonymous();

        // Submit a single answer and run targeted agents — public
        group.MapPost("/{token}/answer", async (string token, SubmitSingleAnswerRequest req, QuestionnaireService svc) =>
        {
            var result = await svc.SubmitSingleAnswerAsync(token, req.ItemId);
            return Results.Ok(result);
        }).AllowAnonymous();

        return group;
    }
}

public record CreateQuestionnaireRequest(string PersonName);
public record SubmitQuestionnaireRequest(List<int> SelectedItemIds);
public record SubmitSingleAnswerRequest(int ItemId);
