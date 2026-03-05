using BioChain.Repository.Entities;

namespace BioChain.Repository.Repositories;

public interface ISubjectRepository
{
    Task<SubjectEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<SubjectEntity?> GetByOwnerAndNameAsync(string ownerId, string name, CancellationToken ct = default);
    Task<List<SubjectEntity>> GetByOwnerAsync(string ownerId, CancellationToken ct = default);
    Task<bool> HasAccessAsync(Guid subjectId, string userId, CancellationToken ct = default);
    Task<SubjectEntity> CreateAsync(SubjectEntity entity, CancellationToken ct = default);
    Task<SubjectEntity> UpdateAsync(SubjectEntity entity, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
