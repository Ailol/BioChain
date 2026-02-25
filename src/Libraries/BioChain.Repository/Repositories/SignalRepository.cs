using Microsoft.EntityFrameworkCore;
using BioChain.Repository.Entities;

namespace BioChain.Repository;

public class SignalRepository(IDbContextFactory<PersonalityDbContext> factory)
{
    public async Task<List<SignalEntity>> ListAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Signals.OrderBy(s => s.Id).ToListAsync();
    }

    public async Task<List<SignalEntity>> ListByLayerAsync(string layer)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Signals.Where(s => s.Layer == layer).OrderBy(s => s.Id).ToListAsync();
    }

    public async Task<SignalEntity?> GetByKeyAsync(string key)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Signals.FirstOrDefaultAsync(s => s.Key == key);
    }

    public async Task<SignalEntity?> GetByCodeAsync(string code)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Signals.FirstOrDefaultAsync(s => s.Code == code);
    }

    public async Task<Dictionary<string, int>> GetKeyToIdMapAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Signals.ToDictionaryAsync(s => s.Key, s => s.Id);
    }

    public async Task<Dictionary<string, int>> GetCodeToIdMapAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Signals.ToDictionaryAsync(s => s.Code, s => s.Id);
    }
}
