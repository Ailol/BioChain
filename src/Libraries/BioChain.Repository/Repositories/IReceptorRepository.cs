using BioChain.Repository.Entities;

namespace BioChain.Repository.Repositories;

public interface IReceptorRepository
{
    Task<ReceptorEntity?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<ReceptorEntity?> GetByCodeAsync(Guid personId, string code, CancellationToken ct = default);
    Task<List<ReceptorEntity>> GetByPersonAsync(Guid personId, CancellationToken ct = default);
    Task<List<ReceptorEntity>> GetBySignalAsync(int signalId, CancellationToken ct = default);
    Task<ReceptorEntity> UpsertAsync(ReceptorEntity entity, CancellationToken ct = default);
}
