using BioChain.Repository.Data;
using BioChain.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace BioChain.Repository.Repositories;

public class UserRoleRepository(BioChainDbContext db) : IUserRoleRepository
{
    public Task<List<UserRoleEntity>> GetByUserAsync(string userId, CancellationToken ct = default)
        => db.UserRoles.Where(r => r.UserId == userId && r.IsActive).ToListAsync(ct);

    public Task<bool> HasRoleAsync(string userId, string role, CancellationToken ct = default)
        => db.UserRoles.AnyAsync(r => r.UserId == userId && r.Role == role && r.IsActive, ct);

    public async Task<UserRoleEntity> AssignAsync(UserRoleEntity entity, CancellationToken ct = default)
    {
        var existing = await db.UserRoles
            .FirstOrDefaultAsync(r => r.UserId == entity.UserId && r.Role == entity.Role, ct);
        if (existing is null)
        {
            entity.IsActive = true;
            entity.CreatedAt = DateTimeOffset.UtcNow;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            db.UserRoles.Add(entity);
            await db.SaveChangesAsync(ct);
            return entity;
        }
        existing.IsActive = true;
        existing.Email = entity.Email;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return existing;
    }

    public async Task RevokeAsync(string userId, string role, CancellationToken ct = default)
    {
        await db.UserRoles
            .Where(r => r.UserId == userId && r.Role == role)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.IsActive, false)
                .SetProperty(r => r.UpdatedAt, DateTimeOffset.UtcNow), ct);
    }

    public async Task<IReadOnlyList<string>> GetRolesAsync(string userId, CancellationToken ct = default)
    {
        var entities = await GetByUserAsync(userId, ct);
        return entities.Select(r => r.Role).ToList();
    }

    public Task<List<UserRoleEntity>> GetAllActiveAsync(CancellationToken ct = default)
        => db.UserRoles.Where(r => r.IsActive).ToListAsync(ct);
}
