using BioChain.Repository.Entities;

namespace BioChain.Repository.Repositories;

public interface IProtocolRepository
{
    Task<ProtocolEntity?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<ProtocolEntity>> GetByPersonAsync(Guid subjectId, CancellationToken ct = default);
    Task<List<ProtocolEntity>> GetGlobalAsync(CancellationToken ct = default);
    Task<ProtocolEntity> CreateAsync(ProtocolEntity entity, CancellationToken ct = default);
    Task<List<ProtocolEntity>> GetByModuleTagAsync(int moduleId, string tag, CancellationToken ct = default);
}
