using BioChain.Repository.Repositories;

namespace BioChain.Server.Api;

public static class QuestionnaireApi
{
    public static RouteGroupBuilder MapQuestionnaireApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/questionnaire").WithTags("Questionnaire");

        // List all questions with options (for local preview / building the UI) — public
        group.MapGet("/questions", async (IQuestionnaireRepository repo) =>
        {
            var items = await repo.GetAllItemsAsync();
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

        return group;
    }
}
