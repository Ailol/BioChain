using BioChain.Repository.Entities;

namespace BioChain.Repository.Repositories;

public interface IDataRepository
{
    Task<DataEntity?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<DataEntity>> GetByPersonAsync(Guid personId, CancellationToken ct = default);
    Task<List<DataEntity>> GetByPersonAndKindAsync(Guid personId, string kind, CancellationToken ct = default);
    Task<List<DataEntity>> GetUnanalyzedAsync(Guid personId, CancellationToken ct = default);
    Task<DataEntity> CreateAsync(DataEntity entity, CancellationToken ct = default);
    Task MarkAnalyzedAsync(int id, CancellationToken ct = default);
}
