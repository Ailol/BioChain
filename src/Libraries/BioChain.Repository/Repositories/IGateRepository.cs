using BioChain.Repository.Entities;

namespace BioChain.Repository.Repositories;

public interface IGateRepository
{
    Task<GateEntity?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<GateEntity?> GetByCodeAsync(Guid personId, string code, CancellationToken ct = default);
    Task<List<GateEntity>> GetByPersonAsync(Guid personId, CancellationToken ct = default);
    Task<List<GateEntity>> GetByTypeAsync(Guid personId, string type, CancellationToken ct = default);
    Task<GateEntity> UpsertAsync(GateEntity entity, CancellationToken ct = default);
}
