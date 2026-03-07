using System.Security.Cryptography;
using System.Text;
using BioChain.Repository;
using BioChain.Repository.Entities;
using BioChain.Repository.Linking;
using BioChain.Repository.Repositories;
using BioChain.Service;

namespace BioChain.Server.Api;

public static class QuestionnaireApi
{
    public static RouteGroupBuilder MapQuestionnaireApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/questionnaire").WithTags("Questionnaire");

        // List all questions with options (for building the UI) — public
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
                    options = g.Select(i => new { i.Id, i.Label, text = i.OptionText }).ToList()
                });
            return Results.Ok(new { questions });
        }).AllowAnonymous();

        // Submit completed questionnaire → analyze each question in parallel via biochain-engine
        group.MapPost("/submit", async (
            QuestionnaireSubmitRequest req,
            IQuestionnaireRepository repo,
            ISubjectRepository persons,
            IComponentLinker linker,
            IServiceScopeFactory scopeFactory,
            IUserContext ctx) =>
        {
            if (!await persons.HasAccessAsync(req.SubjectId, ctx.UserId))
                return Results.Forbid();

            if (req.Answers is not { Count: > 0 })
                return Results.BadRequest("No answers provided.");

            var selectedItemIds = req.Answers.ToDictionary(a => a.SortOrder, a => a.SelectedItemId);
            var sortOrders = selectedItemIds.Keys.ToHashSet();

            if (sortOrders.Count != req.Answers.Count)
                return Results.BadRequest("Duplicate sort orders.");

            // Load all items for the answered questions (all options per question)
            var allItems = await repo.GetItemsBySortOrdersAsync(sortOrders);
            if (allItems.Count == 0)
                return Results.BadRequest("No matching questions found.");

            // Create questionnaire record
            var questionnaire = await repo.CreateAsync(new QuestionnaireEntity
            {
                SubjectId = req.SubjectId,
                Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
                Status = "completed",
                CompletedAt = DateTimeOffset.UtcNow,
            });

            // Store answers
            var answerEntities = req.Answers.Select(a => new QuestionnaireAnswerEntity
            {
                QuestionnaireId = questionnaire.Id,
                ItemId = a.SelectedItemId,
            });
            await repo.AddAnswersAsync(answerEntities);

            // Analyze each question in parallel — each task gets its own DI scope (DbContext)
            var questions = allItems
                .GroupBy(i => i.SortOrder)
                .OrderBy(g => g.Key)
                .Select(g => (SortOrder: g.Key, Items: g.ToList()))
                .ToList();

            var results = new AnalyzeResult[questions.Count];

            await Parallel.ForEachAsync(
                questions.Select((q, idx) => (q, idx)),
                new ParallelOptions { MaxDegreeOfParallelism = 3 },
                async (item, ct) =>
                {
                    var (question, idx) = item;
                    var selectedId = selectedItemIds.GetValueOrDefault(question.SortOrder);
                    var analysisText = BuildQuestionAnalysisText(question.Items, selectedId);

                    await using var scope = scopeFactory.CreateAsyncScope();
                    var analyze = scope.ServiceProvider.GetRequiredService<AnalyzeService>();
                    results[idx] = await analyze.AnalyzeAsync(req.SubjectId, analysisText, "psych", ct);
                });

            var totalAnalyses = results.Sum(r => r.AnalysesStored);
            var totalLines = results.Sum(r => r.LinesTotal);
            var stimuliIds = results.Select(r => r.StimuliId).ToList();

            // Post-pass: connect orphaned signals to nearest same-region signal
            await linker.ConnectOrphanedSignalsAsync(req.SubjectId);

            return Results.Ok(new QuestionnaireSubmitResponse(
                questionnaire.Id,
                stimuliIds,
                totalAnalyses,
                totalLines));
        });

        return group;
    }

    /// <summary>
    /// Build analysis text for a single question.
    /// Includes the SELECTED option and all REJECTED options.
    /// The biochain-engine infers neurochemical signals from behavioral descriptions.
    /// </summary>
    private static string BuildQuestionAnalysisText(
        List<QuestionnaireItemEntity> items,
        int selectedItemId)
    {
        var sb = new StringBuilder();
        var scenario = items.First().Scenario;
        sb.AppendLine($"Psychological assessment question: {scenario}");
        sb.AppendLine();

        foreach (var item in items.OrderBy(i => i.Label))
        {
            var tag = item.Id == selectedItemId ? "SELECTED" : "REJECTED";
            sb.AppendLine($"{tag}: {item.OptionText}");
        }

        return sb.ToString();
    }
}

public record QuestionnaireSubmitRequest(
    Guid SubjectId,
    List<QuestionnaireAnswer> Answers);

public record QuestionnaireAnswer(int SortOrder, int SelectedItemId);

public record QuestionnaireSubmitResponse(
    Guid QuestionnaireId,
    List<int> StimuliIds,
    int AnalysesStored,
    int LinesTotal);
