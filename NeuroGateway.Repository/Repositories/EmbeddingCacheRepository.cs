using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace NeuroGateway.Repository;

public class EmbeddingCacheRepository(IDbContextFactory<PersonalityDbContext> factory)
{
    /// <summary>
    /// Load all cached embeddings of a given type.
    /// </summary>
    public async Task<Dictionary<string, float[]>> LoadByTypeAsync(string cacheType)
    {
        await using var db = await factory.CreateDbContextAsync();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT lookup_key, embedding::text FROM embedding_cache WHERE cache_type = @t";
        AddParam(cmd, "t", cacheType);

        var result = new Dictionary<string, float[]>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result[reader.GetString(0)] = VectorParser.Parse(reader.GetString(1));
        return result;
    }

    /// <summary>
    /// Load all embeddings matching type and a key prefix pattern.
    /// </summary>
    public async Task<Dictionary<string, float[]>> LoadByTypePrefixAsync(string cacheType, string keyPrefix)
    {
        await using var db = await factory.CreateDbContextAsync();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT lookup_key, embedding::text FROM embedding_cache WHERE cache_type = @t AND lookup_key LIKE @prefix";
        AddParam(cmd, "t", cacheType);
        AddParam(cmd, "prefix", keyPrefix + "%");

        var result = new Dictionary<string, float[]>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result[reader.GetString(0)] = VectorParser.Parse(reader.GetString(1));
        return result;
    }

    /// <summary>
    /// Persist a batch of embeddings. Skips duplicates via ON CONFLICT DO NOTHING.
    /// </summary>
    public async Task SaveBatchAsync(string cacheType, int? domainId,
        List<(string LookupKey, string? Label, float[] Embedding)> entries)
    {
        if (entries.Count == 0) return;

        await using var db = await factory.CreateDbContextAsync();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();

        const int chunkSize = 50;
        for (var i = 0; i < entries.Count; i += chunkSize)
        {
            var chunk = entries.Skip(i).Take(chunkSize).ToList();
            await using var cmd = conn.CreateCommand();

            var values = new List<string>();
            var paramIndex = 0;

            foreach (var (lookupKey, label, embedding) in chunk)
            {
                var vectorLiteral = "[" + string.Join(",",
                    embedding.Select(v => v.ToString(CultureInfo.InvariantCulture))) + "]";

                values.Add($"(@p{paramIndex}, @p{paramIndex + 1}, @p{paramIndex + 2}, @p{paramIndex + 3}::vector, @p{paramIndex + 4})");

                AddParam(cmd, $"p{paramIndex}", cacheType);
                AddParam(cmd, $"p{paramIndex + 1}", lookupKey);
                AddParam(cmd, $"p{paramIndex + 2}", (object?)label ?? DBNull.Value);
                AddParam(cmd, $"p{paramIndex + 3}", vectorLiteral);
                AddParam(cmd, $"p{paramIndex + 4}", (object?)domainId ?? DBNull.Value);

                paramIndex += 5;
            }

            cmd.CommandText = $"""
                INSERT INTO embedding_cache (cache_type, lookup_key, label, embedding, domain_id)
                VALUES {string.Join(", ", values)}
                ON CONFLICT (cache_type, lookup_key) DO NOTHING
                """;

            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task<int> CountByTypeAsync(string cacheType)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.EmbeddingCache.CountAsync(e => e.CacheType == cacheType);
    }

    public async Task<int> DeleteByTypeAsync(string cacheType)
    {
        await using var db = await factory.CreateDbContextAsync();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM embedding_cache WHERE cache_type = @t";
        AddParam(cmd, "t", cacheType);
        return await cmd.ExecuteNonQueryAsync();
    }

    private static void AddParam(System.Data.Common.DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }
}
