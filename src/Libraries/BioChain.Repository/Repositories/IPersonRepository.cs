using BioChain.Repository.Entities;

namespace BioChain.Repository.Repositories;

public interface IPersonRepository
{
    Task<PersonEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PersonEntity?> GetByOwnerAndNameAsync(string ownerId, string name, CancellationToken ct = default);
    Task<List<PersonEntity>> GetByOwnerAsync(string ownerId, CancellationToken ct = default);
    Task<bool> HasAccessAsync(Guid personId, string userId, CancellationToken ct = default);
    Task<PersonEntity> CreateAsync(PersonEntity entity, CancellationToken ct = default);
    Task<PersonEntity> UpdateAsync(PersonEntity entity, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
