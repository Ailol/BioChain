using Microsoft.EntityFrameworkCore;

namespace NeuroGateway.Repository;

public class ProfileRepository(IDbContextFactory<PersonalityDbContext> factory)
{
    public async Task InsertAsync(int personalityId, int? analyzedDataId, string chemical,
        string reasoning, float modulationFactor, string? vectorLiteral)
    {
        await using var db = await factory.CreateDbContextAsync();
        if (vectorLiteral is null)
        {
            var entity = new Entities.BiochemicalProfileEntity
            {
                PersonalityId = personalityId,
                AnalyzedDataId = analyzedDataId,
                Chemical = chemical,
                Reasoning = reasoning,
                ModulationFactor = modulationFactor,
                CreatedAt = DateTime.UtcNow
            };
            db.BiochemicalProfiles.Add(entity);
            await db.SaveChangesAsync();
        }
        else
        {
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO biochemical_profile (personality_id, analyzed_data_id, chemical, reasoning, modulation_factor, embedding, created_at)
                VALUES (@p0, @p1, @p2, @p3, @p4, @p5::vector, NOW())
                """,
                new Npgsql.NpgsqlParameter("p0", personalityId),
                new Npgsql.NpgsqlParameter("p1", (object?)analyzedDataId ?? DBNull.Value),
                new Npgsql.NpgsqlParameter("p2", chemical),
                new Npgsql.NpgsqlParameter("p3", reasoning),
                new Npgsql.NpgsqlParameter("p4", modulationFactor),
                new Npgsql.NpgsqlParameter("p5", vectorLiteral));
        }
    }

    public async Task<List<(string Chemical, string Reasoning)>> GetByPersonAsync(string person)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.BiochemicalProfiles
            .Join(db.Personalities, bp => bp.PersonalityId, p => p.Id, (bp, p) => new { bp, p })
            .Join(db.Persons, x => x.p.PersonId, per => per.Id, (x, per) => new { x.bp, per })
            .Where(x => x.per.FirstName.ToLower() == person.ToLower())
            .Select(x => new { x.bp.Chemical, x.bp.Reasoning })
            .ToListAsync()
            .ContinueWith(t => t.Result.Select(x => (x.Chemical, x.Reasoning)).ToList());
    }

    public async Task<List<(string Chemical, int Count)>> GetChemicalCountsAsync(string person)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.BiochemicalProfiles
            .Join(db.Personalities, bp => bp.PersonalityId, p => p.Id, (bp, p) => new { bp, p })
            .Join(db.Persons, x => x.p.PersonId, per => per.Id, (x, per) => new { x.bp, per })
            .Where(x => x.per.FirstName.ToLower() == person.ToLower())
            .GroupBy(x => x.bp.Chemical)
            .Select(g => new { Chemical = g.Key, Count = g.Count() })
            .ToListAsync()
            .ContinueWith(t => t.Result.Select(x => (x.Chemical, x.Count)).ToList());
    }

    public async Task<List<string>> GetScoredReasoningsAsync(
        string person, string messageVector, string relationshipVector,
        float messageWeight, int topPerChemical)
    {
        await using var db = await factory.CreateDbContextAsync();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            WITH scored AS (
                SELECT bp.chemical, bp.reasoning,
                    (1 - (bp.embedding <=> @msgVec::vector)) * @msgW
                    + (1 - (bp.embedding <=> @relVec::vector)) * (1 - @msgW) AS score,
                    ROW_NUMBER() OVER (PARTITION BY bp.chemical ORDER BY
                        (1 - (bp.embedding <=> @msgVec::vector)) * @msgW
                        + (1 - (bp.embedding <=> @relVec::vector)) * (1 - @msgW) DESC
                    ) AS rn
                FROM biochemical_profile bp
                JOIN personality p ON p.id = bp.personality_id
                JOIN person per ON per.id = p.person_id
                WHERE lower(per.first_name) = lower(@person)
                  AND bp.embedding IS NOT NULL
            )
            SELECT reasoning FROM scored WHERE rn <= @topN ORDER BY score DESC
            """;
        AddParam(cmd, "msgVec", messageVector);
        AddParam(cmd, "relVec", relationshipVector);
        AddParam(cmd, "msgW", messageWeight);
        AddParam(cmd, "person", person);
        AddParam(cmd, "topN", topPerChemical);

        var results = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add(reader.GetString(0));
        return results;
    }

    private static void AddParam(System.Data.Common.DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }
}
