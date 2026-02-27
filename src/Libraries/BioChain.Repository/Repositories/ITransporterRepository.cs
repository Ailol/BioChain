using BioChain.Repository.Entities;

namespace BioChain.Repository.Repositories;

public interface ITransporterRepository
{
    Task<TransporterEntity?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<TransporterEntity?> GetByCodeAsync(Guid personId, string code, CancellationToken ct = default);
    Task<List<TransporterEntity>> GetByPersonAsync(Guid personId, CancellationToken ct = default);
    Task<List<TransporterEntity>> GetBySignalAsync(int signalId, CancellationToken ct = default);
    Task<TransporterEntity> UpsertAsync(TransporterEntity entity, CancellationToken ct = default);
}
