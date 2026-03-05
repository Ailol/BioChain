using System.Security.Claims;
using BioChain.Repository.Entities;
using BioChain.Repository.Repositories;
using BioChain.Repository.Roles;

namespace BioChain.Service;

public class RoleService(IUserRoleRepository repo) : IRoleService
{
    public async Task<IReadOnlyList<string>> GetUserRolesAsync(string userId, CancellationToken ct = default)
    {
        var entities = await repo.GetByUserAsync(userId, ct);
        return entities.Select(r => r.Role).ToList();
    }

    public async Task SetUserRoleAsync(string userId, string? email, string role, CancellationToken ct = default)
    {
        // Revoke all existing, assign the new one
        var existing = await repo.GetByUserAsync(userId, ct);
        foreach (var e in existing.Where(e => e.Role != role))
            await repo.RevokeAsync(userId, e.Role, ct);

        await repo.AssignAsync(new UserRoleEntity { UserId = userId, Email = email, Role = role }, ct);
    }

    public async Task SetUserRolesAsync(string userId, string? email, List<string> roles, CancellationToken ct = default)
    {
        var existing = await repo.GetByUserAsync(userId, ct);

        // Revoke roles not in the new set
        foreach (var e in existing.Where(e => !roles.Contains(e.Role)))
            await repo.RevokeAsync(userId, e.Role, ct);

        // Assign new roles
        foreach (var role in roles)
            await repo.AssignAsync(new UserRoleEntity { UserId = userId, Email = email, Role = role }, ct);
    }

    public async Task SyncFromProviderAsync(string userId, string? email, ClaimsPrincipal principal,
        CancellationToken ct = default)
    {
        var idpRoles = principal.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .Where(AppRole.IsValid)
            .ToList();

        if (idpRoles.Count > 0)
            await SetUserRolesAsync(userId, email, idpRoles, ct);
    }

    public async Task<List<UserOverview>> GetAllUsersAsync(CancellationToken ct = default)
    {
        var all = await repo.GetAllActiveAsync(ct);
        return all
            .GroupBy(r => r.UserId)
            .Select(g => new UserOverview(
                g.Key,
                g.FirstOrDefault(r => r.Email is not null)?.Email,
                g.Select(r => r.Role).ToList(),
                g.Max(r => r.UpdatedAt)))
            .ToList();
    }
}
