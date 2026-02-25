using System.Security.Claims;
using System.Text.Json;
using BioChain.Repository.Roles;

namespace BioChain.Server.Auth;

// Extracts application roles from Keycloak's realm_access JWT claim.
// Only returns roles that are valid AppRole values — ignores Keycloak
// built-in roles like "offline_access", "uma_authorization", etc.
public class KeycloakRoleProvider : IRoleProvider
{
    public Task<List<string>> ExtractRolesFromClaimsAsync(ClaimsPrincipal principal)
    {
        var roles = new List<string>();

        var realmAccess = principal.FindFirst("realm_access")?.Value;
        if (realmAccess is null) return Task.FromResult(roles);

        using var doc = JsonDocument.Parse(realmAccess);
        if (!doc.RootElement.TryGetProperty("roles", out var rolesElement))
            return Task.FromResult(roles);

        foreach (var role in rolesElement.EnumerateArray())
        {
            var roleName = role.GetString();
            if (roleName is not null && AppRole.IsValid(roleName))
                roles.Add(roleName);
        }

        return Task.FromResult(roles);
    }
}
