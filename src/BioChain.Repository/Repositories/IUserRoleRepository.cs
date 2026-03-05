using BioChain.Repository.Entities;

namespace BioChain.Repository.Repositories;

public interface IUserRoleRepository
{
    Task<List<UserRoleEntity>> GetByUserAsync(string userId, CancellationToken ct = default);
    Task<bool> HasRoleAsync(string userId, string role, CancellationToken ct = default);
    Task<UserRoleEntity> AssignAsync(UserRoleEntity entity, CancellationToken ct = default);
    Task RevokeAsync(string userId, string role, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetRolesAsync(string userId, CancellationToken ct = default);
    Task<List<UserRoleEntity>> GetAllActiveAsync(CancellationToken ct = default);
}
