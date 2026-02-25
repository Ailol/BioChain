using Microsoft.EntityFrameworkCore;
using BioChain.Repository.Entities;

namespace BioChain.Repository;

public class ProfileSnapshotRepository(IDbContextFactory<PersonalityDbContext> factory)
{
    /// <summary>
    /// Call the stored function to refresh profile snapshots for a personality.
    /// </summary>
    public async Task RefreshAsync(int personalityId)
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.ExecuteSqlRawAsync(
            "SELECT refresh_profile_snapshot(@p0)",
            new Npgsql.NpgsqlParameter("p0", personalityId));
    }

    public async Task<List<ProfileSnapshotEntity>> GetForPersonAsync(Guid personId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.ProfileSnapshots
            .Where(s => s.PersonId == personId)
            .OrderBy(s => s.SignalId)
            .ToListAsync();
    }

    public async Task<List<ProfileSnapshotEntity>> GetForPersonalityAsync(int personalityId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.ProfileSnapshots
            .Where(s => s.PersonalityId == personalityId)
            .OrderBy(s => s.SignalId)
            .ToListAsync();
    }

    public async Task<ProfileSnapshotEntity?> GetForSignalAsync(int personalityId, int signalId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.ProfileSnapshots
            .FirstOrDefaultAsync(s => s.PersonalityId == personalityId && s.SignalId == signalId);
    }
}
