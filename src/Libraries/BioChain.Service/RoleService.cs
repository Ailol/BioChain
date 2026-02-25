using System.Security.Claims;
using BioChain.Repository;
using BioChain.Repository.Roles;

namespace BioChain.Service;

public interface IRoleService
{
    Task<List<string>> GetUserRolesAsync(string userId);
    Task<HashSet<string>> GetEffectiveRolesAsync(string userId);
    Task SetUserRoleAsync(string userId, string? email, string role);
    Task SetUserRolesAsync(string userId, string? email, List<string> roles);
    Task SyncFromProviderAsync(string userId, string? email, ClaimsPrincipal principal);
    Task<bool> HasCompletedRoleSelectionAsync(string userId);
    Task<List<UserRoleSummary>> GetAllUsersAsync();
}

public class RoleService(UserRoleRepository _roleRepo, IRoleProvider _provider) : IRoleService
{
    // Returns raw DB roles (not expanded)
    public Task<List<string>> GetUserRolesAsync(string userId) =>
        _roleRepo.GetRolesAsync(userId);

    // Returns expanded effective roles (both → {work, private}, admin → all)
    public async Task<HashSet<string>> GetEffectiveRolesAsync(string userId)
    {
        var roles = await _roleRepo.GetRolesAsync(userId);
        return AppRole.ExpandEffective(roles);
    }

    // Set a single role for the user (validates against AppRole.All)
    public Task SetUserRoleAsync(string userId, string? email, string role)
    {
        if (!AppRole.IsValid(role))
            throw new ArgumentException($"Invalid role: {role}");
        return _roleRepo.SetRoleAsync(userId, email, role);
    }

    // Set multiple roles at once (admin use — replaces all existing roles)
    public Task SetUserRolesAsync(string userId, string? email, List<string> roles)
    {
        var invalid = roles.Where(r => !AppRole.IsValid(r)).ToList();
        if (invalid.Count > 0)
            throw new ArgumentException($"Invalid roles: {string.Join(", ", invalid)}");
        return _roleRepo.SetRolesAsync(userId, email, roles);
    }

    // Sync from IdP on first login only — if user has no DB roles yet,
    // extract from claims and store. After that, DB takes precedence.
    public async Task SyncFromProviderAsync(
        string userId,
        string? email,
        ClaimsPrincipal principal
    )
    {
        var hasRoles = await _roleRepo.HasAnyRoleAsync(userId);
        if (hasRoles) return; // DB already has roles, skip sync

        var providerRoles = await _provider.ExtractRolesFromClaimsAsync(principal);
        if (providerRoles.Count > 0)
            await _roleRepo.SetRolesAsync(userId, email, providerRoles);
    }

    // True if user has at least one active role in DB
    public Task<bool> HasCompletedRoleSelectionAsync(string userId) =>
        _roleRepo.HasAnyRoleAsync(userId);

    // All users with roles (admin overview)
    public Task<List<UserRoleSummary>> GetAllUsersAsync() =>
        _roleRepo.GetAllUsersWithRolesAsync();
}
