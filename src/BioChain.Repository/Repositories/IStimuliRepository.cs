using BioChain.Repository.Entities;

namespace BioChain.Repository.Repositories;

public interface IStimuliRepository
{
    Task<StimuliEntity?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<StimuliEntity>> GetBySubjectAsync(Guid subjectId, CancellationToken ct = default);
    Task<List<StimuliEntity>> GetBySubjectAndKindAsync(Guid subjectId, string kind, CancellationToken ct = default);
    Task<List<StimuliEntity>> GetUnanalyzedAsync(Guid subjectId, CancellationToken ct = default);
    Task<StimuliEntity> CreateAsync(StimuliEntity entity, CancellationToken ct = default);
    Task MarkAnalyzedAsync(int id, CancellationToken ct = default);
}
