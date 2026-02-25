using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using BioChain.Repository;

namespace BioChain.Server.Auth;

// Reads roles from DB (source of truth) and adds them as standard Role claims
// so ASP.NET RequireRole / policy-based authorization works automatically.
// Replaces KeycloakRolesClaimsTransformation — DB takes precedence over JWT.
public class DbRolesClaimsTransformation(UserRoleRepository _roleRepo) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var identity = principal.Identity as ClaimsIdentity;
        if (identity is null) return principal;

        var userId =
            principal.FindFirst("sub")?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return principal;

        var roles = await _roleRepo.GetRolesAsync(userId);
        foreach (var role in roles)
        {
            if (!identity.HasClaim(ClaimTypes.Role, role))
                identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        return principal;
    }
}
