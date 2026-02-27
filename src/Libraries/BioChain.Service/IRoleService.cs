using System.Security.Claims;

namespace BioChain.Service;

public interface IRoleService
{
    Task<IReadOnlyList<string>> GetUserRolesAsync(string userId, CancellationToken ct = default);
    Task SetUserRoleAsync(string userId, string? email, string role, CancellationToken ct = default);
    Task SetUserRolesAsync(string userId, string? email, List<string> roles, CancellationToken ct = default);
    Task SyncFromProviderAsync(string userId, string? email, ClaimsPrincipal principal, CancellationToken ct = default);
    Task<List<UserOverview>> GetAllUsersAsync(CancellationToken ct = default);
}

public record UserOverview(string UserId, string? Email, List<string> Roles, DateTimeOffset UpdatedAt);
