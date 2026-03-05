using BioChain.Repository.Entities;

namespace BioChain.Repository.Repositories;

public interface IGateRepository
{
    Task<GateEntity?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<GateEntity?> GetCurrentByCodeAsync(Guid subjectId, string code, CancellationToken ct = default);
    Task<List<GateEntity>> GetByPersonAsync(Guid subjectId, CancellationToken ct = default);
    Task<List<GateEntity>> GetByTypeAsync(Guid subjectId, string type, CancellationToken ct = default);
    Task<GateEntity> CreateAsync(GateEntity entity, CancellationToken ct = default);
}
