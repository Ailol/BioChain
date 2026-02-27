using BioChain.Repository.Entities;

namespace BioChain.Repository.Repositories;

public interface ISignalRepository
{
    Task<SignalEntity?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<SignalEntity?> GetByCodeAsync(Guid personId, string code, string? region = null, CancellationToken ct = default);
    Task<List<SignalEntity>> GetByPersonAsync(Guid personId, CancellationToken ct = default);
    Task<List<SignalEntity>> GetByTypeAsync(Guid personId, string type, CancellationToken ct = default);
    Task<SignalEntity> UpsertAsync(SignalEntity entity, CancellationToken ct = default);
}
