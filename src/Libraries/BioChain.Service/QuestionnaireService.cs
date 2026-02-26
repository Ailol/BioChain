using System.Text;
using BioChain.Repository;

namespace BioChain.Service;

public class QuestionnaireService(
    QuestionnaireRepository questionnaireRepo,
    PersonRepository personRepo,
    AnalysisQueueService analysisQueue)
{
    // Create a new questionnaire for a person by name.
    public async Task<string> CreateAsync(string personName)
    {
        var personId = await personRepo.GetIdAsync(personName)
            ?? throw new InvalidOperationException($"Person '{personName}' not found");

        var (_, token) = await questionnaireRepo.CreateAsync(personId);
        return token;
    }

    // Load questionnaire by token for the frontend (includes answered sort orders for resume).
    public async Task<QuestionnaireView?> GetByTokenAsync(string token)
    {
        var result = await questionnaireRepo.GetByTokenAsync(token);
        if (result is not { } r) return null;

        var (q, personName) = r;
        var items = await questionnaireRepo.ListItemsAsync();
        var answeredSortOrders = await questionnaireRepo.GetAnsweredSortOrdersAsync(q.Id);

        var questions = items
            .GroupBy(i => i.SortOrder)
            .OrderBy(g => g.Key)
            .Select(g => new QuestionnaireQuestionView(
                g.Key,
                g.First().Scenario,
                g.First().IsInverted,
                g.Select(i => new QuestionnaireOptionView(i.Id, i.Label, i.OptionText)).ToList()))
            .ToList();

        return new QuestionnaireView(q.Id, personName, q.Status, questions, answeredSortOrders);
    }

    // Submit a single answer and run targeted agents for that question's chemicals.
    public async Task<SingleAnswerResult> SubmitSingleAnswerAsync(string token, int itemId)
    {
        var (q, personName) = await questionnaireRepo.GetByTokenAsync(token)
            ?? throw new InvalidOperationException("Questionnaire not found");
        if (q.Status is not ("pending" or "in_progress"))
            throw new InvalidOperationException("Questionnaire already completed");

        // Validate sort_order not already answered
        var answeredSortOrders = await questionnaireRepo.GetAnsweredSortOrdersAsync(q.Id);
        var items = await questionnaireRepo.ListItemsAsync();
        var targetItem = items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException($"Invalid item ID: {itemId}");
        if (answeredSortOrders.Contains(targetItem.SortOrder))
            throw new InvalidOperationException($"Question {targetItem.SortOrder} already answered");

        // Save the single answer
        var answeredCount = await questionnaireRepo.SaveSingleAnswerAsync(q.Id, itemId);
        var isComplete = answeredCount >= 18;

        // Format single question for targeted analysis
        var questionItems = await questionnaireRepo.GetSingleQuestionItemsAsync(q.Id, itemId);
        var text = FormatSingleQuestionForAnalysis(personName, targetItem.SortOrder, questionItems);

        // Run only targeted agents (primary + secondary signals)
        var targetSignals = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { targetItem.PrimarySignal };
        if (targetItem.SecondarySignal is not null)
            targetSignals.Add(targetItem.SecondarySignal);

        // Enqueue for background processing — returns immediately
        await analysisQueue.EnqueueAsync(new AnalysisWorkItem(
            personName, text,
            SourceType: "questionnaire",
            Save: true,
            TargetSignals: targetSignals));

        return new SingleAnswerResult(answeredCount, 18, isComplete, targetItem.SortOrder);
    }

    // Submit answers and run through the agent analysis pipeline (batch — kept for backwards compat).
    public async Task SubmitAndScoreAsync(string token, List<int> selectedItemIds)
    {
        var (q, personName) = await questionnaireRepo.GetByTokenAsync(token)
            ?? throw new InvalidOperationException("Questionnaire not found");
        if (q.Status != "pending")
            throw new InvalidOperationException("Questionnaire already completed");

        // Validate: exactly 18 answers with distinct sort_orders
        var items = await questionnaireRepo.ListItemsAsync();
        var itemLookup = items.ToDictionary(i => i.Id);
        var sortOrders = new HashSet<int>();
        foreach (var id in selectedItemIds)
        {
            if (!itemLookup.TryGetValue(id, out var item))
                throw new InvalidOperationException($"Invalid item ID: {id}");
            if (!sortOrders.Add(item.SortOrder))
                throw new InvalidOperationException($"Duplicate answer for question {item.SortOrder}");
        }
        if (sortOrders.Count != 18)
            throw new InvalidOperationException($"Expected 18 answers, got {sortOrders.Count}");

        // Save answers
        await questionnaireRepo.SaveAnswersAsync(q.Id, selectedItemIds);

        // Format structured text for the agent pipeline
        var answeredItems = await questionnaireRepo.GetAnsweredItemsAsync(q.Id);
        var text = FormatForAnalysis(personName, answeredItems);

        // Enqueue for background processing (all 27 agents)
        await analysisQueue.EnqueueAsync(new AnalysisWorkItem(
            personName, text,
            SourceType: "questionnaire",
            Save: true,
            TargetSignals: null));
    }

    // Format a single question + answer for targeted agent analysis.
    private static string FormatSingleQuestionForAnalysis(
        string personName,
        int sortOrder,
        List<(Repository.Entities.QuestionnaireItemEntity Item, bool Selected)> questionItems)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"NeuroTriangulate-18 Single Question for {personName}:");
        sb.AppendLine();

        var question = questionItems.First().Item;
        var selected = questionItems.First(a => a.Selected);
        var rejected = questionItems.Where(a => !a.Selected).ToList();

        sb.AppendLine($"Q{sortOrder}: {question.Scenario}");
        sb.AppendLine($"  Selected: {selected.Item.OptionText} (primary: {selected.Item.PrimarySignal}, secondary: {selected.Item.SecondarySignal ?? "none"})");

        if (rejected.Count > 0)
        {
            var rejectedTexts = rejected.Select(r => r.Item.OptionText);
            sb.AppendLine($"  Rejected: {string.Join("; ", rejectedTexts)}");
        }

        if (question.IsInverted)
            sb.AppendLine("  [INVERTED: selection indicates overactivation of mapped chemicals]");

        return sb.ToString();
    }

    // Format questionnaire answers as a structured text block for agent analysis (batch).
    private static string FormatForAnalysis(
        string personName,
        List<(Repository.Entities.QuestionnaireItemEntity Item, bool Selected)> answeredItems)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"NeuroTriangulate-18 Questionnaire Results for {personName}:");
        sb.AppendLine();

        var grouped = answeredItems
            .GroupBy(a => a.Item.SortOrder)
            .OrderBy(g => g.Key);

        foreach (var group in grouped)
        {
            var question = group.First().Item;
            var selected = group.First(a => a.Selected);
            var rejected = group.Where(a => !a.Selected).ToList();

            sb.AppendLine($"Q{question.SortOrder}: {question.Scenario}");
            sb.AppendLine($"  Selected: {selected.Item.OptionText} (primary: {selected.Item.PrimarySignal}, secondary: {selected.Item.SecondarySignal ?? "none"})");

            if (rejected.Count > 0)
            {
                var rejectedTexts = rejected.Select(r => r.Item.OptionText);
                sb.AppendLine($"  Rejected: {string.Join("; ", rejectedTexts)}");
            }

            if (question.IsInverted)
                sb.AppendLine("  [INVERTED: selection indicates overactivation of mapped chemicals]");

            sb.AppendLine();
        }

        return sb.ToString();
    }
}

// View models returned by the service
public record QuestionnaireView(Guid Id, string PersonName, string Status, List<QuestionnaireQuestionView> Questions, List<int> AnsweredSortOrders);
public record QuestionnaireQuestionView(int SortOrder, string Scenario, bool IsInverted, List<QuestionnaireOptionView> Options);
public record QuestionnaireOptionView(int Id, string Label, string Text);
public record SingleAnswerResult(int AnsweredCount, int TotalQuestions, bool IsComplete, int QuestionSortOrder);
