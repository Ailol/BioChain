using BioChain.Repository.Data;
using BioChain.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace BioChain.Repository.Repositories;

public class QuestionnaireRepository(BioChainDbContext db) : IQuestionnaireRepository
{
    // Items
    public Task<List<QuestionnaireItemEntity>> GetAllItemsAsync(CancellationToken ct = default)
        => db.QuestionnaireItems.OrderBy(i => i.SortOrder).ThenBy(i => i.Label).ToListAsync(ct);

    public Task<QuestionnaireItemEntity?> GetItemByIdAsync(int id, CancellationToken ct = default)
        => db.QuestionnaireItems.FirstOrDefaultAsync(i => i.Id == id, ct);

    // Questionnaires
    public Task<QuestionnaireEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Questionnaires.Include(q => q.Answers).FirstOrDefaultAsync(q => q.Id == id, ct);

    public Task<QuestionnaireEntity?> GetByTokenAsync(string token, CancellationToken ct = default)
        => db.Questionnaires.Include(q => q.Answers).FirstOrDefaultAsync(q => q.Token == token, ct);

    public Task<List<QuestionnaireEntity>> GetByPersonAsync(Guid personId, CancellationToken ct = default)
        => db.Questionnaires.Where(q => q.PersonId == personId).OrderByDescending(q => q.CreatedAt).ToListAsync(ct);

    public async Task<QuestionnaireEntity> CreateAsync(QuestionnaireEntity entity, CancellationToken ct = default)
    {
        db.Questionnaires.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<QuestionnaireEntity> UpdateAsync(QuestionnaireEntity entity, CancellationToken ct = default)
    {
        db.Questionnaires.Update(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }

    // Answers
    public Task<List<QuestionnaireAnswerEntity>> GetAnswersAsync(Guid questionnaireId, CancellationToken ct = default)
        => db.QuestionnaireAnswers
            .Include(a => a.Item)
            .Where(a => a.QuestionnaireId == questionnaireId)
            .ToListAsync(ct);

    public async Task AddAnswerAsync(QuestionnaireAnswerEntity answer, CancellationToken ct = default)
    {
        db.QuestionnaireAnswers.Add(answer);
        await db.SaveChangesAsync(ct);
    }
}
