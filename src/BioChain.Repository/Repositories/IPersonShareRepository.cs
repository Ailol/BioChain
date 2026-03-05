using BioChain.Repository.Entities;

namespace BioChain.Repository.Repositories;

public interface IPersonShareRepository
{
    Task<List<PersonShareEntity>> GetByPersonAsync(Guid subjectId, CancellationToken ct = default);
    Task<List<PersonShareEntity>> GetByUserAsync(string userId, CancellationToken ct = default);
    Task<PersonShareEntity> CreateAsync(PersonShareEntity entity, CancellationToken ct = default);
    Task DeleteAsync(Guid subjectId, string sharedWithEmail, CancellationToken ct = default);
    Task ResolveSharesAsync(string userId, string email, CancellationToken ct = default);
}
