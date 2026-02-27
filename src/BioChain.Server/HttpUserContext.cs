using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using BioChain.Repository;
using BioChain.Repository.Repositories;
using BioChain.Repository.Roles;

namespace BioChain.Server;

public class HttpUserContext(
    IHttpContextAccessor httpContextAccessor,
    IHostEnvironment env,
    IUserRoleRepository roleRepo
) : IUserContext
{
    public string UserId
    {
        get
        {
            var sub = httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
                ?? httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (sub is not null) return sub;

            // In Development, fall back to a fixed dev user for anonymous access
            if (env.IsDevelopment()) return "dev-user";
            throw new InvalidOperationException("No authenticated user found");
        }
    }

    public string? Email =>
        httpContextAccessor.HttpContext?.User?.FindFirst("email")?.Value
        ?? httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Email)?.Value
        ?? (env.IsDevelopment() ? "dev@ailo.no" : null);

    public IReadOnlyList<string> Roles
    {
        get
        {
            // Dev fallback: dev-user gets admin (all access)
            if (env.IsDevelopment() && UserId == "dev-user")
                return ["admin"];

            return roleRepo.GetRolesAsync(UserId).GetAwaiter().GetResult();
        }
    }

    public bool HasRole(string role)
    {
        var effective = AppRole.ExpandEffective(Roles);
        return effective.Contains(role);
    }
}

public class FixedUserContext(
    string userId,
    string? email = null,
    IReadOnlyList<string>? roles = null
) : IUserContext
{
    public string UserId => userId;
    public string? Email => email;
    public IReadOnlyList<string> Roles => roles ?? ["admin"];
    public bool HasRole(string role) => AppRole.ExpandEffective(Roles).Contains(role);
}

// Extracts realm_access.roles from Keycloak JWT into standard Role claims
// so ASP.NET RequireRole / [Authorize(Roles = "...")] works out of the box.
// Kept for reference — DbRolesClaimsTransformation is now the active transformer.
public class KeycloakRolesClaimsTransformation : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var identity = principal.Identity as ClaimsIdentity;
        if (identity is null) return Task.FromResult(principal);

        var realmAccess = principal.FindFirst("realm_access")?.Value;
        if (realmAccess is null) return Task.FromResult(principal);

        using var doc = JsonDocument.Parse(realmAccess);
        if (!doc.RootElement.TryGetProperty("roles", out var rolesElement))
            return Task.FromResult(principal);

        foreach (var role in rolesElement.EnumerateArray())
        {
            var roleName = role.GetString();
            if (roleName is not null && !identity.HasClaim(ClaimTypes.Role, roleName))
                identity.AddClaim(new Claim(ClaimTypes.Role, roleName));
        }

        return Task.FromResult(principal);
    }
}
