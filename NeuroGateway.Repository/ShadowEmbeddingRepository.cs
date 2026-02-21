using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace NeuroGateway.Repository;

public class ShadowEmbeddingRepository(IDbContextFactory<PersonalityDbContext> factory)
{
    /// <summary>
    /// Load all persisted shadow embeddings into memory.
    /// </summary>
    public async Task<Dictionary<(string Dim, string Mode, string Chem, int Level), float[]>> LoadAllAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT dimension, mode, chemical, level, embedding::text FROM shadow_embedding";

        var result = new Dictionary<(string, string, string, int), float[]>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var key = (reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3));
            result[key] = ParseVector(reader.GetString(4));
        }
        return result;
    }

    // Load only rows matching a specific mode and level
    public async Task<Dictionary<(string Dim, string Chem), float[]>> LoadByModeAsync(string mode, int level)
    {
        await using var db = await factory.CreateDbContextAsync();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT dimension, chemical, embedding::text FROM shadow_embedding WHERE mode = @m AND level = @l";
        AddParam(cmd, "m", mode);
        AddParam(cmd, "l", level);

        var result = new Dictionary<(string, string), float[]>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result[(reader.GetString(0), reader.GetString(1))] = ParseVector(reader.GetString(2));
        return result;
    }

    /// <summary>
    /// Persist a batch of shadow embeddings. Skips duplicates via ON CONFLICT DO NOTHING.
    /// </summary>
    public async Task SaveBatchAsync(List<(string Dim, string Mode, string Chem, int Level, float[] Embedding)> entries)
    {
        if (entries.Count == 0) return;

        await using var db = await factory.CreateDbContextAsync();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();

        // Batch insert in chunks of 50 to avoid overly large SQL
        const int chunkSize = 50;
        for (var i = 0; i < entries.Count; i += chunkSize)
        {
            var chunk = entries.Skip(i).Take(chunkSize).ToList();
            await using var cmd = conn.CreateCommand();

            var values = new List<string>();
            var paramIndex = 0;

            foreach (var (dim, mode, chem, level, embedding) in chunk)
            {
                var vectorLiteral = "[" + string.Join(",",
                    embedding.Select(v => v.ToString(CultureInfo.InvariantCulture))) + "]";

                values.Add($"(@p{paramIndex}, @p{paramIndex + 1}, @p{paramIndex + 2}, @p{paramIndex + 3}, @p{paramIndex + 4}::vector)");

                AddParam(cmd, $"p{paramIndex}", dim);
                AddParam(cmd, $"p{paramIndex + 1}", mode);
                AddParam(cmd, $"p{paramIndex + 2}", chem);
                AddParam(cmd, $"p{paramIndex + 3}", level);
                AddParam(cmd, $"p{paramIndex + 4}", vectorLiteral);

                paramIndex += 5;
            }

            cmd.CommandText = $"""
                INSERT INTO shadow_embedding (dimension, mode, chemical, level, embedding)
                VALUES {string.Join(", ", values)}
                ON CONFLICT (dimension, mode, chemical, level) DO NOTHING
                """;

            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task<int> CountAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.ShadowEmbeddings.CountAsync();
    }

    // Delete all rows for a specific mode (e.g. "mbti_chem", "bigfive_chem")
    public async Task<int> DeleteByModeAsync(string mode)
    {
        await using var db = await factory.CreateDbContextAsync();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM shadow_embedding WHERE mode = @m";
        AddParam(cmd, "m", mode);
        return await cmd.ExecuteNonQueryAsync();
    }

    // Widen the level CHECK constraint to allow prototype version numbers > 5.
    // The original init.sql had CHECK (level BETWEEN 1 AND 5) which blocks
    // MBTI/BigFive prototype embeddings that use version as the level value.
    public async Task MigrateLevelConstraintAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.check_constraints
                    WHERE constraint_name = 'shadow_embedding_level_check'
                      AND check_clause LIKE '%5%'
                ) THEN
                    ALTER TABLE shadow_embedding DROP CONSTRAINT shadow_embedding_level_check;
                    ALTER TABLE shadow_embedding ADD CONSTRAINT shadow_embedding_level_check CHECK (level BETWEEN 1 AND 100);
                END IF;
            END $$;
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    private static void AddParam(System.Data.Common.DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }

    private static float[] ParseVector(string vectorStr)
    {
        var trimmed = vectorStr.Trim('[', ']');
        var parts = trimmed.Split(',');
        var result = new float[parts.Length];
        for (var i = 0; i < parts.Length; i++)
            result[i] = float.Parse(parts[i], CultureInfo.InvariantCulture);
        return result;
    }
}
