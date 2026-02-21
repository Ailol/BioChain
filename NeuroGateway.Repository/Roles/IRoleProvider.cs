using System.Security.Claims;

namespace NeuroGateway.Repository.Roles;

// Vendor-agnostic interface for extracting roles from an identity provider.
// Each IdP (Keycloak, Azure AD, Auth0) implements this adapter.
// The extracted roles are synced into the DB on first login.
public interface IRoleProvider
{
    // Extract application-relevant roles from the external identity claims
    Task<List<string>> ExtractRolesFromClaimsAsync(ClaimsPrincipal principal);
}
