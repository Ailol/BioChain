using BioChain.Repository;
using BioChain.Repository.Repositories;
using BioChain.Repository.Roles;
using BioChain.Service;

namespace BioChain.Server.Api;

public static class AuthApi
{
    public static RouteGroupBuilder MapAuthApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        // Returns current user info + roles from DB
        group.MapGet("/me", async (IUserContext ctx, IRoleService roleSvc) =>
        {
            var roles = await roleSvc.GetUserRolesAsync(ctx.UserId);
            var hasSelectedRole = roles.Count > 0;
            return Results.Ok(new
            {
                userId = ctx.UserId,
                email = ctx.Email,
                roles,
                hasSelectedRole,
            });
        });

        // Set or change user role (e.g. from role selection page)
        group.MapPost("/set-role", async (SetRoleRequest req, IUserContext ctx, IRoleService roleSvc) =>
        {
            if (!AppRole.IsValid(req.Role))
                return Results.BadRequest(new { error = $"Invalid role: {req.Role}" });

            await roleSvc.SetUserRoleAsync(ctx.UserId, ctx.Email, req.Role);
            return Results.Ok(new { role = req.Role });
        });

        // Sync IdP claims → DB (called on first login)
        group.MapPost("/sync-roles", async (HttpContext http, IUserContext ctx, IRoleService roleSvc) =>
        {
            await roleSvc.SyncFromProviderAsync(ctx.UserId, ctx.Email, http.User);
            return Results.Ok();
        });

        // Resolve pending shares (moved from inline in Program.cs)
        group.MapPost("/resolve-shares", async (IPersonShareRepository shareRepo, IUserContext ctx) =>
        {
            var email = ctx.Email;
            if (email is not null)
                await shareRepo.ResolveSharesAsync(ctx.UserId, email);
            return Results.Ok();
        });

        // ── Admin endpoints ───────────────────────────────────────────────────

        // List all users with their roles (admin overview table)
        group.MapGet("/admin/users", async (IUserContext ctx, IRoleService roleSvc) =>
        {
            if (!ctx.HasRole("admin"))
                return Results.Forbid();

            var users = await roleSvc.GetAllUsersAsync();
            return Results.Ok(new
            {
                users = users.Select(u => new
                {
                    u.UserId,
                    u.Email,
                    u.Roles,
                    updatedAt = u.UpdatedAt.ToString("o"),
                }),
            });
        });

        // Set roles for any user (admin only)
        group.MapPost("/admin/set-roles", async (AdminSetRolesRequest req, IUserContext ctx, IRoleService roleSvc) =>
        {
            if (!ctx.HasRole("admin"))
                return Results.Forbid();

            var invalid = req.Roles.Where(r => !AppRole.IsValid(r)).ToList();
            if (invalid.Count > 0)
                return Results.BadRequest(new { error = $"Invalid roles: {string.Join(", ", invalid)}" });

            await roleSvc.SetUserRolesAsync(req.UserId, req.Email, req.Roles);
            return Results.Ok(new { userId = req.UserId, roles = req.Roles });
        });

        return group;
    }
}

public record SetRoleRequest(string Role);
public record AdminSetRolesRequest(string UserId, string? Email, List<string> Roles);
