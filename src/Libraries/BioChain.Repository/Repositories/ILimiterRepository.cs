using BioChain.Repository.Entities;

namespace BioChain.Repository.Repositories;

public interface ILimiterRepository
{
    Task<LimiterEntity?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<LimiterEntity?> GetByCodeAsync(Guid personId, string code, CancellationToken ct = default);
    Task<List<LimiterEntity>> GetByPersonAsync(Guid personId, CancellationToken ct = default);
    Task<List<LimiterEntity>> GetBottlenecksAsync(Guid personId, CancellationToken ct = default);
    Task<LimiterEntity> UpsertAsync(LimiterEntity entity, CancellationToken ct = default);
}
