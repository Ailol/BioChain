using BioChain.Repository.Entities;

namespace BioChain.Repository.Repositories;

public interface IAnalysisRepository
{
    Task<AnalysisEntity?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<AnalysisEntity>> GetByPersonAsync(Guid subjectId, CancellationToken ct = default);
    Task<List<AnalysisEntity>> GetGlobalAsync(CancellationToken ct = default);
    Task<AnalysisEntity> CreateAsync(AnalysisEntity entity, CancellationToken ct = default);
    Task<List<AnalysisEntity>> GetByModuleTagAsync(int moduleId, string tag, CancellationToken ct = default);
}
