using Microsoft.EntityFrameworkCore;

namespace BioChain.Repository;

public class AnalyzedDataRepository(IDbContextFactory<PersonalityDbContext> factory)
{
    public async Task<int> InsertAsync(Guid personId, string content, string? sourceType, string? sourceUri)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entity = new Entities.AnalyzedDataEntity
        {
            PersonId = personId,
            Content = content,
            SourceType = sourceType,
            SourceUri = sourceUri,
            CreatedAt = DateTime.UtcNow
        };
        db.AnalyzedData.Add(entity);
        await db.SaveChangesAsync();
        return entity.Id;
    }

    public async Task UpdateEmbeddingAsync(int id, string vectorLiteral)
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE analyzed_data SET embedding = @p1::vector WHERE id = @p2",
            new Npgsql.NpgsqlParameter("p1", vectorLiteral),
            new Npgsql.NpgsqlParameter("p2", id));
    }

    public async Task<(string Content, float Similarity)?> FindNearestAsync(Guid personId, string vectorLiteral)
    {
        await using var db = await factory.CreateDbContextAsync();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT content, 1 - (embedding <=> @vec::vector) AS sim
            FROM analyzed_data
            WHERE person_id = @pid AND embedding IS NOT NULL
            ORDER BY embedding <=> @vec::vector
            LIMIT 1
            """;
        AddParam(cmd, "vec", vectorLiteral);
        AddParam(cmd, "pid", personId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        return (reader.GetString(0), reader.GetFloat(1));
    }

    public async Task<List<(int Id, string Content)>> GetWithoutEmbeddingsAsync(string? person)
    {
        await using var db = await factory.CreateDbContextAsync();
        var query = db.AnalyzedData.Where(a => a.Embedding == null);

        if (person is not null)
        {
            var personId = await db.Persons
                .Where(p => p.FirstName.ToLower() == person.ToLower())
                .Select(p => p.Id)
                .FirstOrDefaultAsync();
            if (personId == Guid.Empty) return [];
            query = query.Where(a => a.PersonId == personId);
        }

        return await query
            .Select(a => new { a.Id, a.Content })
            .ToListAsync()
            .ContinueWith(t => t.Result.Select(x => (x.Id, x.Content)).ToList());
    }

    private static void AddParam(System.Data.Common.DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }
}
