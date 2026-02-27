using BioChain.Repository.Entities;

namespace BioChain.Repository.Repositories;

public interface IInterfaceRepository
{
    Task<InterfaceEntity?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<InterfaceEntity?> GetByCodeAsync(Guid personId, string code, CancellationToken ct = default);
    Task<List<InterfaceEntity>> GetByPersonAsync(Guid personId, CancellationToken ct = default);
    Task<List<InterfaceEntity>> GetActiveAsync(Guid personId, CancellationToken ct = default);
    Task<InterfaceEntity> UpsertAsync(InterfaceEntity entity, CancellationToken ct = default);
}
