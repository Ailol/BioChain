using BioChain.Repository.Entities;

namespace BioChain.Repository.Repositories;

public interface IModuleRepository
{
    Task<ModuleEntity?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<ModuleEntity>> GetBySubjectAsync(Guid subjectId, CancellationToken ct = default);
    Task<ModuleEntity?> GetCurrentByCodeAsync(Guid subjectId, string code, CancellationToken ct = default);
    Task<List<ModuleEntity>> GetByAgentTypeAsync(Guid subjectId, string agentType, CancellationToken ct = default);
    Task<List<ModuleEntity>> GetByNamespaceAsync(Guid subjectId, string ns, CancellationToken ct = default);
    Task<ModuleEntity> CreateAsync(ModuleEntity entity, CancellationToken ct = default);
    Task UpdatePropertiesAsync(int moduleId, string propertiesJson, CancellationToken ct = default);
}
