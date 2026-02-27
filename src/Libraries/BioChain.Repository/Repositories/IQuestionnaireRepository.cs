using BioChain.Repository.Entities;

namespace BioChain.Repository.Repositories;

public interface IQuestionnaireRepository
{
    // Items
    Task<List<QuestionnaireItemEntity>> GetAllItemsAsync(CancellationToken ct = default);
    Task<QuestionnaireItemEntity?> GetItemByIdAsync(int id, CancellationToken ct = default);

    // Questionnaires
    Task<QuestionnaireEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<QuestionnaireEntity?> GetByTokenAsync(string token, CancellationToken ct = default);
    Task<List<QuestionnaireEntity>> GetByPersonAsync(Guid personId, CancellationToken ct = default);
    Task<QuestionnaireEntity> CreateAsync(QuestionnaireEntity entity, CancellationToken ct = default);
    Task<QuestionnaireEntity> UpdateAsync(QuestionnaireEntity entity, CancellationToken ct = default);

    // Answers
    Task<List<QuestionnaireAnswerEntity>> GetAnswersAsync(Guid questionnaireId, CancellationToken ct = default);
    Task AddAnswerAsync(QuestionnaireAnswerEntity answer, CancellationToken ct = default);
}
