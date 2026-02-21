using Microsoft.EntityFrameworkCore;

namespace NeuroGateway.Repository;

public class ProfileRepository(IDbContextFactory<PersonalityDbContext> factory, IUserContext userContext)
{
    // SQL fragment for owner-or-shared access check
    private const string AccessCheck = """
        AND (per.owner_id = @userId
             OR EXISTS (SELECT 1 FROM person_share ps
                        WHERE ps.person_id = per.id
                        AND (ps.shared_with_user_id = @userId
                             OR ps.shared_with_email = @email)))
        """;

    public async Task InsertAsync(int personalityId, int? analyzedDataId, string chemical,
        string reasoning, float intensityFactor, string? vectorLiteral)
    {
        await using var db = await factory.CreateDbContextAsync();
        if (vectorLiteral is null)
        {
            var entity = new Entities.ChemicalObservationEntity
            {
                PersonalityId = personalityId,
                AnalyzedDataId = analyzedDataId,
                Chemical = chemical,
                Reasoning = reasoning,
                IntensityFactor = intensityFactor,
                CreatedAt = DateTime.UtcNow
            };
            db.ChemicalObservations.Add(entity);
            await db.SaveChangesAsync();
        }
        else
        {
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO chemical_observation (personality_id, analyzed_data_id, chemical, reasoning, intensity_factor, embedding, created_at)
                VALUES (@p0, @p1, @p2, @p3, @p4, @p5::vector, NOW())
                """,
                new Npgsql.NpgsqlParameter("p0", personalityId),
                new Npgsql.NpgsqlParameter("p1", (object?)analyzedDataId ?? DBNull.Value),
                new Npgsql.NpgsqlParameter("p2", chemical),
                new Npgsql.NpgsqlParameter("p3", reasoning),
                new Npgsql.NpgsqlParameter("p4", intensityFactor),
                new Npgsql.NpgsqlParameter("p5", vectorLiteral));
        }
    }

    public async Task<List<(string Chemical, string Reasoning)>> GetByPersonAsync(string person)
    {
        await using var db = await factory.CreateDbContextAsync();
        var userId = userContext.UserId;
        var email = userContext.Email;
        return await db.ChemicalObservations
            .Join(db.Personalities, bp => bp.PersonalityId, p => p.Id, (bp, p) => new { bp, p })
            .Join(db.Persons, x => x.p.PersonId, per => per.Id, (x, per) => new { x.bp, per })
            .Where(x => x.per.FirstName.ToLower() == person.ToLower())
            .Where(x => x.per.OwnerId == userId
                || db.PersonShares.Any(s => s.PersonId == x.per.Id
                    && (s.SharedWithUserId == userId
                        || (email != null && s.SharedWithEmail == email))))
            .Select(x => new { x.bp.Chemical, x.bp.Reasoning })
            .ToListAsync()
            .ContinueWith(t => t.Result.Select(x => (x.Chemical, x.Reasoning)).ToList());
    }

    public async Task<List<(string Chemical, int Count)>> GetChemicalCountsAsync(string person)
    {
        await using var db = await factory.CreateDbContextAsync();
        var userId = userContext.UserId;
        var email = userContext.Email;
        return await db.ChemicalObservations
            .Join(db.Personalities, bp => bp.PersonalityId, p => p.Id, (bp, p) => new { bp, p })
            .Join(db.Persons, x => x.p.PersonId, per => per.Id, (x, per) => new { x.bp, per })
            .Where(x => x.per.FirstName.ToLower() == person.ToLower())
            .Where(x => x.per.OwnerId == userId
                || db.PersonShares.Any(s => s.PersonId == x.per.Id
                    && (s.SharedWithUserId == userId
                        || (email != null && s.SharedWithEmail == email))))
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
        cmd.CommandText = $"""
            WITH scored AS (
                SELECT bp.chemical, bp.reasoning,
                    (1 - (bp.embedding <=> @msgVec::vector)) * @msgW
                    + (1 - (bp.embedding <=> @relVec::vector)) * (1 - @msgW) AS score,
                    ROW_NUMBER() OVER (PARTITION BY bp.chemical ORDER BY
                        (1 - (bp.embedding <=> @msgVec::vector)) * @msgW
                        + (1 - (bp.embedding <=> @relVec::vector)) * (1 - @msgW) DESC
                    ) AS rn
                FROM chemical_observation bp
                JOIN personality p ON p.id = bp.personality_id
                JOIN person per ON per.id = p.person_id
                WHERE lower(per.first_name) = lower(@person)
                  AND bp.embedding IS NOT NULL
                  {AccessCheck}
            )
            SELECT reasoning FROM scored WHERE rn <= @topN ORDER BY score DESC
            """;
        AddParam(cmd, "msgVec", messageVector);
        AddParam(cmd, "relVec", relationshipVector);
        AddParam(cmd, "msgW", messageWeight);
        AddParam(cmd, "person", person);
        AddParam(cmd, "topN", topPerChemical);
        AddParam(cmd, "userId", userContext.UserId);
        AddParam(cmd, "email", (object?)userContext.Email ?? DBNull.Value);

        var results = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add(reader.GetString(0));
        return results;
    }

    public async Task<List<(string Chemical, string Reasoning, float Similarity, DateTime CreatedAt)>>
        GetSimilarReasoningsAsync(string person, string vectorLiteral, int topK)
    {
        await using var db = await factory.CreateDbContextAsync();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT bp.chemical, bp.reasoning,
                   1 - (bp.embedding <=> @dimVec::vector) AS similarity,
                   bp.created_at
            FROM chemical_observation bp
            JOIN personality p ON p.id = bp.personality_id
            JOIN person per ON per.id = p.person_id
            WHERE lower(per.first_name) = lower(@person)
              AND bp.embedding IS NOT NULL
              {AccessCheck}
            ORDER BY bp.embedding <=> @dimVec::vector
            LIMIT @topK
            """;
        AddParam(cmd, "dimVec", vectorLiteral);
        AddParam(cmd, "person", person);
        AddParam(cmd, "topK", topK);
        AddParam(cmd, "userId", userContext.UserId);
        AddParam(cmd, "email", (object?)userContext.Email ?? DBNull.Value);

        var results = new List<(string, string, float, DateTime)>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add((
                reader.GetString(0),
                reader.GetString(1),
                reader.GetFloat(2),
                reader.GetDateTime(3)));
        return results;
    }

    public async Task<List<(string Chemical, float[] Embedding, DateTime CreatedAt)>>
        GetAllEmbeddingsAsync(string person)
    {
        await using var db = await factory.CreateDbContextAsync();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT bp.chemical, bp.embedding::text, bp.created_at
            FROM chemical_observation bp
            JOIN personality p ON p.id = bp.personality_id
            JOIN person per ON per.id = p.person_id
            WHERE lower(per.first_name) = lower(@person)
              AND bp.embedding IS NOT NULL
              {AccessCheck}
            """;
        AddParam(cmd, "person", person);
        AddParam(cmd, "userId", userContext.UserId);
        AddParam(cmd, "email", (object?)userContext.Email ?? DBNull.Value);

        var results = new List<(string, float[], DateTime)>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var chemical = reader.GetString(0);
            var embeddingStr = reader.GetString(1);
            var embedding = ParseVector(embeddingStr);
            var createdAt = reader.GetDateTime(2);
            results.Add((chemical, embedding, createdAt));
        }
        return results;
    }

    public async Task<List<ProfileEntry>> GetProfileEntriesAsync(string person)
    {
        await using var db = await factory.CreateDbContextAsync();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT bp.chemical, bp.reasoning, bp.embedding::text, bp.intensity_factor, bp.created_at
            FROM chemical_observation bp
            JOIN personality p ON p.id = bp.personality_id
            JOIN person per ON per.id = p.person_id
            WHERE lower(per.first_name) = lower(@person)
              AND bp.embedding IS NOT NULL
              {AccessCheck}
            """;
        AddParam(cmd, "person", person);
        AddParam(cmd, "userId", userContext.UserId);
        AddParam(cmd, "email", (object?)userContext.Email ?? DBNull.Value);

        var results = new List<ProfileEntry>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new ProfileEntry(
                reader.GetString(0),
                reader.GetString(1),
                ParseVector(reader.GetString(2)),
                reader.GetFloat(3),
                reader.GetDateTime(4)));
        }
        return results;
    }

    public record ProfileEntry(string Chemical, string Reasoning, float[] Embedding, float IntensityFactor, DateTime CreatedAt);

    public async Task<List<TimelineEntry>> GetTimelineAsync(string person)
    {
        await using var db = await factory.CreateDbContextAsync();
        var userId = userContext.UserId;
        var email = userContext.Email;
        return await db.ChemicalObservations
            .Join(db.Personalities, bp => bp.PersonalityId, p => p.Id, (bp, p) => new { bp, p })
            .Join(db.Persons, x => x.p.PersonId, per => per.Id, (x, per) => new { x.bp, per })
            .Where(x => x.per.FirstName.ToLower() == person.ToLower())
            .Where(x => x.per.OwnerId == userId
                || db.PersonShares.Any(s => s.PersonId == x.per.Id
                    && (s.SharedWithUserId == userId
                        || (email != null && s.SharedWithEmail == email))))
            .OrderBy(x => x.bp.CreatedAt)
            .Select(x => new TimelineEntry(x.bp.Chemical, x.bp.IntensityFactor, x.bp.CreatedAt))
            .ToListAsync();
    }

    public record TimelineEntry(string Chemical, float IntensityFactor, DateTime CreatedAt);

    private static float[] ParseVector(string vectorStr)
    {
        var trimmed = vectorStr.Trim('[', ']');
        var parts = trimmed.Split(',');
        var result = new float[parts.Length];
        for (var i = 0; i < parts.Length; i++)
            result[i] = float.Parse(parts[i], System.Globalization.CultureInfo.InvariantCulture);
        return result;
    }

    public async Task<List<(int Id, string Reasoning)>> GetWithoutEmbeddingsAsync(string? person = null)
    {
        await using var db = await factory.CreateDbContextAsync();
        var userId = userContext.UserId;
        var email = userContext.Email;
        var query = db.ChemicalObservations
            .Join(db.Personalities, bp => bp.PersonalityId, p => p.Id, (bp, p) => new { bp, p })
            .Join(db.Persons, x => x.p.PersonId, per => per.Id, (x, per) => new { x.bp, per })
            .Where(x => x.bp.Embedding == null)
            .Where(x => x.per.OwnerId == userId
                || db.PersonShares.Any(s => s.PersonId == x.per.Id
                    && (s.SharedWithUserId == userId
                        || (email != null && s.SharedWithEmail == email))));

        if (!string.IsNullOrEmpty(person))
            query = query.Where(x => x.per.FirstName.ToLower() == person.ToLower());

        return await query
            .Select(x => new { x.bp.Id, x.bp.Reasoning })
            .ToListAsync()
            .ContinueWith(t => t.Result.Select(x => (x.Id, x.Reasoning)).ToList());
    }

    public async Task UpdateEmbeddingAsync(int profileId, string vectorLiteral)
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE chemical_observation SET embedding = @p1::vector WHERE id = @p0",
            new Npgsql.NpgsqlParameter("p0", profileId),
            new Npgsql.NpgsqlParameter("p1", vectorLiteral));
    }

    private static void AddParam(System.Data.Common.DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }
}
