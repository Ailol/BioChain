using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using BioChain.Repository.Entities;

namespace BioChain.Repository;

public class QuestionnaireRepository(IDbContextFactory<PersonalityDbContext> factory)
{
    // Load all questionnaire items grouped by sort_order for rendering.
    public async Task<List<QuestionnaireItemEntity>> ListItemsAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.QuestionnaireItems
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.Label)
            .ToListAsync();
    }

    // Create a new questionnaire instance for a person, returning the shareable token.
    public async Task<(Guid Id, string Token)> CreateAsync(Guid personId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var entity = new QuestionnaireEntity
        {
            PersonId = personId,
            Token = token,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };
        db.Questionnaires.Add(entity);
        await db.SaveChangesAsync();
        return (entity.Id, entity.Token);
    }

    // Load a questionnaire by its shareable token, including person name.
    public async Task<(QuestionnaireEntity Questionnaire, string PersonName)?> GetByTokenAsync(string token)
    {
        await using var db = await factory.CreateDbContextAsync();
        var q = await db.Questionnaires.FirstOrDefaultAsync(x => x.Token == token);
        if (q is null) return null;

        var person = await db.Persons.FirstOrDefaultAsync(p => p.Id == q.PersonId);
        return (q, person?.FirstName ?? "Unknown");
    }

    // Save selected answers and mark the questionnaire as completed.
    public async Task SaveAnswersAsync(Guid questionnaireId, List<int> selectedItemIds)
    {
        await using var db = await factory.CreateDbContextAsync();
        var now = DateTime.UtcNow;

        foreach (var itemId in selectedItemIds)
        {
            db.QuestionnaireAnswers.Add(new QuestionnaireAnswerEntity
            {
                QuestionnaireId = questionnaireId,
                ItemId = itemId,
                CreatedAt = now
            });
        }

        var questionnaire = await db.Questionnaires.FirstAsync(q => q.Id == questionnaireId);
        questionnaire.Status = "completed";
        questionnaire.CompletedAt = now;

        await db.SaveChangesAsync();
    }

    // Load all items with a flag indicating which were selected (for formatting the analysis text).
    public async Task<List<(QuestionnaireItemEntity Item, bool Selected)>> GetAnsweredItemsAsync(Guid questionnaireId)
    {
        await using var db = await factory.CreateDbContextAsync();

        var selectedIds = await db.QuestionnaireAnswers
            .Where(a => a.QuestionnaireId == questionnaireId)
            .Select(a => a.ItemId)
            .ToHashSetAsync();

        var items = await db.QuestionnaireItems
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.Label)
            .ToListAsync();

        return items.Select(i => (i, selectedIds.Contains(i.Id))).ToList();
    }

    // Save a single answer, update questionnaire status, return answered count.
    public async Task<int> SaveSingleAnswerAsync(Guid questionnaireId, int itemId)
    {
        await using var db = await factory.CreateDbContextAsync();

        db.QuestionnaireAnswers.Add(new QuestionnaireAnswerEntity
        {
            QuestionnaireId = questionnaireId,
            ItemId = itemId,
            CreatedAt = DateTime.UtcNow
        });

        var questionnaire = await db.Questionnaires.FirstAsync(q => q.Id == questionnaireId);
        var answeredCount = await db.QuestionnaireAnswers
            .CountAsync(a => a.QuestionnaireId == questionnaireId) + 1; // +1 for the one we're adding

        if (answeredCount >= 18)
        {
            questionnaire.Status = "completed";
            questionnaire.CompletedAt = DateTime.UtcNow;
        }
        else if (questionnaire.Status == "pending")
        {
            questionnaire.Status = "in_progress";
        }

        await db.SaveChangesAsync();
        return answeredCount;
    }

    // Get sort_orders already answered (for resume on page reload).
    public async Task<List<int>> GetAnsweredSortOrdersAsync(Guid questionnaireId)
    {
        await using var db = await factory.CreateDbContextAsync();

        return await db.QuestionnaireAnswers
            .Where(a => a.QuestionnaireId == questionnaireId)
            .Join(db.QuestionnaireItems, a => a.ItemId, i => i.Id, (a, i) => i.SortOrder)
            .OrderBy(s => s)
            .ToListAsync();
    }

    // Get the 3 options for a single question by the selected item's sort_order.
    public async Task<List<(QuestionnaireItemEntity Item, bool Selected)>> GetSingleQuestionItemsAsync(
        Guid questionnaireId, int itemId)
    {
        await using var db = await factory.CreateDbContextAsync();

        var targetItem = await db.QuestionnaireItems.FirstAsync(i => i.Id == itemId);
        var sortOrder = targetItem.SortOrder;

        var options = await db.QuestionnaireItems
            .Where(i => i.SortOrder == sortOrder)
            .OrderBy(i => i.Label)
            .ToListAsync();

        return options.Select(i => (i, i.Id == itemId)).ToList();
    }
}
