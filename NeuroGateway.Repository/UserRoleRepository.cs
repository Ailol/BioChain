using Microsoft.EntityFrameworkCore;
using NeuroGateway.Repository.Entities;

namespace NeuroGateway.Repository;

public class UserRoleRepository(IDbContextFactory<PersonalityDbContext> _factory)
{
    public async Task<List<string>> GetRolesAsync(string userId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.UserRoles
            .Where(r => r.UserId == userId && r.IsActive)
            .Select(r => r.Role)
            .ToListAsync();
    }

    public async Task SetRoleAsync(string userId, string? email, string role)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var existing = await db.UserRoles
            .FirstOrDefaultAsync(r => r.UserId == userId && r.Role == role);

        if (existing is not null)
        {
            existing.IsActive = true;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            db.UserRoles.Add(new UserRoleEntity
            {
                UserId = userId,
                Email = email,
                Role = role,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync();
    }

    public async Task SetRolesAsync(string userId, string? email, List<string> roles)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var currentRoles = await db.UserRoles
            .Where(r => r.UserId == userId)
            .ToListAsync();

        // Deactivate roles not in the new set
        foreach (var existing in currentRoles)
        {
            existing.IsActive = roles.Contains(existing.Role);
            existing.UpdatedAt = DateTime.UtcNow;
        }

        // Add new roles that don't exist yet
        var existingRoleNames = currentRoles.Select(r => r.Role).ToHashSet();
        foreach (var role in roles.Where(r => !existingRoleNames.Contains(r)))
        {
            db.UserRoles.Add(new UserRoleEntity
            {
                UserId = userId,
                Email = email,
                Role = role,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync();
    }

    public async Task<bool> HasAnyRoleAsync(string userId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.UserRoles.AnyAsync(r => r.UserId == userId && r.IsActive);
    }

    // Returns all distinct users with their active roles (for admin overview)
    public async Task<List<UserRoleSummary>> GetAllUsersWithRolesAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.UserRoles
            .Where(r => r.IsActive)
            .GroupBy(r => new { r.UserId, r.Email })
            .Select(g => new UserRoleSummary
            {
                UserId = g.Key.UserId,
                Email = g.Key.Email,
                Roles = g.Select(r => r.Role).ToList(),
                UpdatedAt = g.Max(r => r.UpdatedAt),
            })
            .OrderBy(u => u.Email ?? u.UserId)
            .ToListAsync();
    }
}

public class UserRoleSummary
{
    public string UserId { get; set; } = "";
    public string? Email { get; set; }
    public List<string> Roles { get; set; } = [];
    public DateTime UpdatedAt { get; set; }
}
