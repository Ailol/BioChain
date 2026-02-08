using Npgsql;
using Models;

namespace Repository;

/// <summary>
/// Data access layer for all personality-related database operations.
/// Pure SQL/Npgsql — no business logic, no LLM calls, no embedding generation.
/// </summary>
public class PersonalityRepository
{
    private readonly string _connectionString;

    public PersonalityRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    private async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        return conn;
    }

    // ===== Person Methods =====

    public async Task<List<string>> ListPersonsAsync()
    {
        const string sql = "SELECT name FROM person ORDER BY name";
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        var persons = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            persons.Add(reader.GetString(0));
        return persons;
    }

    public async Task<bool> CreatePersonAsync(string name)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "INSERT INTO person (name) VALUES (@name) ON CONFLICT DO NOTHING", conn);
        cmd.Parameters.AddWithValue("name", name.ToLowerInvariant());
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task EnsurePersonExistsAsync(string name)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "INSERT INTO person (name) VALUES (@name) ON CONFLICT DO NOTHING", conn);
        cmd.Parameters.AddWithValue("name", name.ToLowerInvariant());
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<bool> PersonExistsAsync(string name)
    {
        const string sql = "SELECT 1 FROM person WHERE LOWER(name) = LOWER(@name) LIMIT 1";
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("name", name);
        return await cmd.ExecuteScalarAsync() != null;
    }

    public async Task<List<string>> FindSimilarPersonsAsync(string search)
    {
        const string sql = """
            SELECT name, similarity(LOWER(name), LOWER(@search)) as sim
            FROM person
            WHERE similarity(LOWER(name), LOWER(@search)) > 0.3
            ORDER BY sim DESC
            LIMIT 5;
        """;

        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("search", search);

        var suggestions = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            suggestions.Add(reader.GetString(0));

        return suggestions;
    }

    // ===== Personality Trait Methods =====

    public async Task<PersonalityResult> GetPersonalityAsync(string person)
    {
        const string sql = """
            SELECT p.topic, p.explanation, nt.name, pr.name
            FROM personality p
            JOIN person pr ON pr.id = p.person_id
            JOIN neurotransmitter nt ON nt.id = p.neurotransmitter_id
            WHERE LOWER(pr.name) = LOWER(@name) ORDER BY p.topic;
        """;

        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("name", person);

        var traits = new List<Trait>();
        string? matchedName = null;
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            traits.Add(new Trait(reader.GetString(0), reader.GetString(1), reader.GetString(2)));
            matchedName ??= reader.GetString(3);
        }

        if (traits.Count > 0)
            return new PersonalityResult(new PersonalityProfile(matchedName!, traits));

        var suggestions = await FindSimilarPersonsAsync(person);
        return new PersonalityResult(null, suggestions.Count > 0 ? suggestions : null);
    }

    /// <summary>
    /// Get raw trait embedding vectors for a person (for computing hormone/peptide scores).
    /// </summary>
    public async Task<List<float[]>> GetTraitEmbeddingsAsync(string person)
    {
        const string sql = """
            SELECT p.embedding::TEXT
            FROM personality p
            JOIN person pr ON pr.id = p.person_id
            WHERE LOWER(pr.name) = LOWER(@name) AND p.embedding IS NOT NULL
        """;

        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("name", person);

        var embeddings = new List<float[]>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var embedding = FromPostgresVector(reader.GetString(0));
            if (embedding != null)
                embeddings.Add(embedding);
        }

        return embeddings;
    }

    /// <summary>
    /// Get trait embeddings with full metadata (topic, explanation, NT, embedding) for vector analysis.
    /// </summary>
    public async Task<List<TraitWithEmbedding>> GetTraitEmbeddingsWithMetadataAsync(string person)
    {
        const string sql = """
            SELECT p.topic, p.explanation, nt.name as neurotransmitter, p.embedding::TEXT
            FROM personality p
            JOIN person pr ON pr.id = p.person_id
            JOIN neurotransmitter nt ON nt.id = p.neurotransmitter_id
            WHERE LOWER(pr.name) = LOWER(@name) AND p.embedding IS NOT NULL
        """;

        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("name", person);

        var results = new List<TraitWithEmbedding>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var embedding = FromPostgresVector(reader.GetString(3));
            if (embedding != null)
                results.Add(new TraitWithEmbedding(reader.GetString(0), reader.GetString(1), reader.GetString(2), embedding));
        }

        return results;
    }

    /// <summary>
    /// Get raw name + embedding pairs for hormone or peptide table (for heatmap analysis).
    /// </summary>
    public async Task<List<(string Name, float[] Embedding)>> GetTargetEmbeddingsAsync(string table)
    {
        if (table is not "hormone" and not "peptide")
            throw new ArgumentException("Table must be 'hormone' or 'peptide'", nameof(table));

        var targets = new List<(string Name, float[] Embedding)>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            $"SELECT name, embedding::TEXT FROM {table} WHERE embedding IS NOT NULL", conn);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var embedding = FromPostgresVector(reader.GetString(1));
            if (embedding != null)
                targets.Add((reader.GetString(0), embedding));
        }

        return targets;
    }

    /// <summary>
    /// Search personality traits by vector similarity (pgvector cosine distance).
    /// Returns raw trait data with similarity scores.
    /// </summary>
    public async Task<List<(string Topic, string Explanation, string Neurotransmitter, double Similarity)>> GetSimilarTraitsAsync(
        string person, string embeddingVector, int limit = 20)
    {
        const string sql = """
            SELECT p.topic, p.explanation, nt.name as neurotransmitter,
                   1 - (p.embedding <=> @embedding::vector) as similarity
            FROM personality p
            JOIN person pr ON pr.id = p.person_id
            JOIN neurotransmitter nt ON nt.id = p.neurotransmitter_id
            WHERE LOWER(pr.name) = LOWER(@person) AND p.embedding IS NOT NULL
            ORDER BY p.embedding <=> @embedding::vector
            LIMIT @limit;
        """;

        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("embedding", embeddingVector);
        cmd.Parameters.AddWithValue("person", person);
        cmd.Parameters.AddWithValue("limit", limit);

        var results = new List<(string, string, string, double)>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add((
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetDouble(3)
            ));
        }

        return results;
    }

    /// <summary>
    /// Match a person by name (exact) or by embedding similarity fallback.
    /// Returns (matchedName, matchMethod) where matchMethod is "name", "embedding", "name_fallback", or "name_unverified".
    /// </summary>
    public async Task<(string Person, string MatchedBy)> MatchPersonAsync(string? personName, string? embeddingVector)
    {
        if (!string.IsNullOrWhiteSpace(personName))
        {
            var exists = await PersonExistsAsync(personName);
            if (exists) return (personName, "name");
        }

        if (!string.IsNullOrWhiteSpace(embeddingVector))
        {
            var bestMatch = await FindBestPersonByEmbeddingAsync(embeddingVector);
            if (bestMatch != null)
                return (bestMatch, string.IsNullOrWhiteSpace(personName) ? "embedding" : "name_fallback");
        }

        if (!string.IsNullOrWhiteSpace(personName))
            return (personName, "name_unverified");

        throw new InvalidOperationException("Could not identify person. Please provide a name or ensure profiles exist.");
    }

    /// <summary>
    /// Find best matching person by embedding similarity across all traits.
    /// </summary>
    public async Task<string?> FindBestPersonByEmbeddingAsync(string embeddingVector)
    {
        const string sql = """
            SELECT pr.name, MIN(p.embedding <=> @embedding::vector) as min_dist
            FROM personality p
            JOIN person pr ON pr.id = p.person_id
            WHERE p.embedding IS NOT NULL
            GROUP BY pr.name
            ORDER BY min_dist
            LIMIT 1;
        """;

        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("embedding", embeddingVector);

        await using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? reader.GetString(0) : null;
    }

    public async Task UpsertPersonalityTraitAsync(
        string person, string neurotransmitter, string topic, string explanation, string? embeddingVector)
    {
        var sql = embeddingVector != null
            ? """
                INSERT INTO personality (person_id, neurotransmitter_id, topic, explanation, embedding)
                SELECT p.id, nt.id, @topic, @expl, @embedding::vector
                FROM person p, neurotransmitter nt WHERE LOWER(p.name) = LOWER(@person) AND nt.name = @nt
                ON CONFLICT (person_id, neurotransmitter_id, topic)
                DO UPDATE SET explanation = EXCLUDED.explanation, embedding = EXCLUDED.embedding;
              """
            : """
                INSERT INTO personality (person_id, neurotransmitter_id, topic, explanation)
                SELECT p.id, nt.id, @topic, @expl
                FROM person p, neurotransmitter nt WHERE LOWER(p.name) = LOWER(@person) AND nt.name = @nt
                ON CONFLICT (person_id, neurotransmitter_id, topic)
                DO UPDATE SET explanation = EXCLUDED.explanation;
              """;

        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("topic", topic);
        cmd.Parameters.AddWithValue("expl", explanation);
        cmd.Parameters.AddWithValue("person", person);
        cmd.Parameters.AddWithValue("nt", neurotransmitter);
        if (embeddingVector != null)
            cmd.Parameters.AddWithValue("embedding", embeddingVector);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateTraitEmbeddingByContentAsync(string person, string neurotransmitter, string topic, string embeddingVector)
    {
        const string sql = """
            UPDATE personality SET embedding = @embedding::vector
            WHERE person_id = (SELECT id FROM person WHERE LOWER(name) = LOWER(@person))
              AND neurotransmitter_id = (SELECT id FROM neurotransmitter WHERE name = @nt)
              AND topic = @topic;
        """;

        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("embedding", embeddingVector);
        cmd.Parameters.AddWithValue("person", person);
        cmd.Parameters.AddWithValue("nt", neurotransmitter);
        cmd.Parameters.AddWithValue("topic", topic);
        await cmd.ExecuteNonQueryAsync();
    }

    // ===== Embedding Backfill Methods =====

    public async Task<List<(int Id, string Topic, string Explanation)>> GetTraitsWithoutEmbeddingsAsync(string? person = null)
    {
        var filter = person != null ? "AND LOWER(pr.name) = LOWER(@name)" : "";
        var sql = $"""
            SELECT p.id, p.topic, p.explanation, pr.name
            FROM personality p
            JOIN person pr ON pr.id = p.person_id
            WHERE p.embedding IS NULL
            {filter}
        """;

        await using var conn = await OpenConnectionAsync();
        var traits = new List<(int Id, string Topic, string Explanation)>();
        await using var cmd = new NpgsqlCommand(sql, conn);
        if (person != null)
            cmd.Parameters.AddWithValue("name", person);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            traits.Add((reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));

        return traits;
    }

    public async Task UpdateTraitEmbeddingAsync(int id, string embeddingVector)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "UPDATE personality SET embedding = @embedding::vector WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("embedding", embeddingVector);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<(string Table, int Id, string Name, string Description)>> GetItemsWithoutEmbeddingsAsync()
    {
        var items = new List<(string Table, int Id, string Name, string Description)>();

        await using var conn = await OpenConnectionAsync();
        foreach (var table in new[] { "hormone", "peptide" })
        {
            var sql = $"SELECT id, name, description FROM {table} WHERE description IS NOT NULL AND embedding IS NULL";
            await using var cmd = new NpgsqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                items.Add((table, reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));
        }

        return items;
    }

    public async Task UpdateItemEmbeddingAsync(string table, int id, string embeddingVector)
    {
        if (table is not "hormone" and not "peptide")
            throw new ArgumentException("Table must be 'hormone' or 'peptide'", nameof(table));

        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            $"UPDATE {table} SET embedding = @embedding::vector WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("embedding", embeddingVector);
        await cmd.ExecuteNonQueryAsync();
    }

    // ===== Hormone/Peptide Vector Scoring =====

    /// <summary>
    /// Compute similarity scores between a person's trait embeddings and hormone/peptide embeddings.
    /// Reads vectors from DB and computes cosine similarity in-memory.
    /// </summary>
    public async Task<List<Interaction>> ComputeVectorScoresAsync(string table, List<float[]> traitEmbeddings)
    {
        if (table is not "hormone" and not "peptide")
            throw new ArgumentException("Table must be 'hormone' or 'peptide'", nameof(table));

        var targets = new List<(string Name, float[] Embedding)>();
        await using var conn = await OpenConnectionAsync();
        await using (var cmd = new NpgsqlCommand(
            $"SELECT name, embedding::TEXT FROM {table} WHERE embedding IS NOT NULL", conn))
        {
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var embedding = FromPostgresVector(reader.GetString(1));
                if (embedding != null)
                    targets.Add((reader.GetString(0), embedding));
            }
        }

        if (targets.Count == 0) return [];

        var results = new List<Interaction>();
        foreach (var (name, targetEmbedding) in targets)
        {
            var similarities = traitEmbeddings
                .Select(te => CosineSimilarity(te, targetEmbedding))
                .OrderByDescending(s => s)
                .Take(5)
                .ToList();

            var score = (float)Math.Clamp(similarities.Average(), 0, 1);
            results.Add(new Interaction(name, score));
        }

        return results.OrderByDescending(r => r.Strength).ToList();
    }

    // ===== Agent Group Methods =====

    public async Task<Guid> CreateAgentGroupAsync(string personName, string groupName, List<CustomAgent> agents)
    {
        await using var conn = await OpenConnectionAsync();

        // Ensure person exists
        await using (var ensureCmd = new NpgsqlCommand(
            "INSERT INTO person (name) VALUES (@name) ON CONFLICT DO NOTHING", conn))
        {
            ensureCmd.Parameters.AddWithValue("name", personName.ToLowerInvariant());
            await ensureCmd.ExecuteNonQueryAsync();
        }

        // Get person ID
        Guid personId;
        await using (var getCmd = new NpgsqlCommand(
            "SELECT id FROM person WHERE LOWER(name) = LOWER(@name)", conn))
        {
            getCmd.Parameters.AddWithValue("name", personName);
            var result = await getCmd.ExecuteScalarAsync();
            if (result == null) throw new InvalidOperationException($"Person '{personName}' not found");
            personId = (Guid)result;
        }

        // Create or update agent group
        Guid groupId;
        const string groupSql = """
            INSERT INTO agent_group (person_id, name) VALUES (@personId, @groupName)
            ON CONFLICT (person_id, name) DO UPDATE SET created_at = NOW()
            RETURNING id;
        """;
        await using (var groupCmd = new NpgsqlCommand(groupSql, conn))
        {
            groupCmd.Parameters.AddWithValue("personId", personId);
            groupCmd.Parameters.AddWithValue("groupName", groupName);
            groupId = (Guid)(await groupCmd.ExecuteScalarAsync())!;
        }

        // Delete existing agents for this group (to allow regeneration)
        await using (var delCmd = new NpgsqlCommand("DELETE FROM agent WHERE group_id = @groupId", conn))
        {
            delCmd.Parameters.AddWithValue("groupId", groupId);
            await delCmd.ExecuteNonQueryAsync();
        }

        // Insert agents
        for (int i = 0; i < agents.Count; i++)
        {
            var agent = agents[i];
            const string agentSql = """
                INSERT INTO agent (group_id, name, role, responsibilities, style, max_words, is_synthesizer, sort_order)
                VALUES (@groupId, @name, @role, @responsibilities, @style, @maxWords, @isSynthesizer, @sortOrder);
            """;
            await using var agentCmd = new NpgsqlCommand(agentSql, conn);
            agentCmd.Parameters.AddWithValue("groupId", groupId);
            agentCmd.Parameters.AddWithValue("name", agent.Name);
            agentCmd.Parameters.AddWithValue("role", agent.Role);
            agentCmd.Parameters.AddWithValue("responsibilities", agent.Responsibilities.ToArray());
            agentCmd.Parameters.AddWithValue("style", agent.Style);
            agentCmd.Parameters.AddWithValue("maxWords", agent.MaxWords);
            agentCmd.Parameters.AddWithValue("isSynthesizer", agent.IsSynthesizer);
            agentCmd.Parameters.AddWithValue("sortOrder", i);
            await agentCmd.ExecuteNonQueryAsync();
        }

        return groupId;
    }

    public async Task<List<CustomAgentGroup>> ListAgentGroupsAsync()
    {
        const string sql = """
            SELECT ag.id, p.name as person_name, ag.name as group_name, ag.created_at,
                   COUNT(a.id) as agent_count,
                   ARRAY_AGG(a.name ORDER BY a.sort_order) as agent_names
            FROM agent_group ag
            JOIN person p ON p.id = ag.person_id
            LEFT JOIN agent a ON a.group_id = ag.id
            GROUP BY ag.id, p.name, ag.name, ag.created_at
            ORDER BY ag.created_at DESC;
        """;

        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);

        var groups = new List<CustomAgentGroup>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var agentNames = reader.IsDBNull(5) ? [] : ((string[])reader.GetValue(5)).ToList();
            groups.Add(new CustomAgentGroup(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetDateTime(3),
                reader.GetInt32(4),
                agentNames
            ));
        }

        return groups;
    }

    public async Task<CustomAgentGroupDetail?> GetAgentGroupAsync(string personName, string? groupName = null)
    {
        var effectiveGroupName = groupName ?? personName;

        const string sql = """
            SELECT ag.id, p.name as person_name, ag.name as group_name, ag.created_at
            FROM agent_group ag
            JOIN person p ON p.id = ag.person_id
            WHERE LOWER(p.name) = LOWER(@personName) AND LOWER(ag.name) = LOWER(@groupName);
        """;

        await using var conn = await OpenConnectionAsync();

        Guid groupId;
        string matchedPersonName, matchedGroupName;
        DateTime createdAt;

        await using (var cmd = new NpgsqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("personName", personName);
            cmd.Parameters.AddWithValue("groupName", effectiveGroupName);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            groupId = reader.GetGuid(0);
            matchedPersonName = reader.GetString(1);
            matchedGroupName = reader.GetString(2);
            createdAt = reader.GetDateTime(3);
        }

        // Get agents
        const string agentsSql = """
            SELECT name, role, responsibilities, style, max_words, is_synthesizer
            FROM agent WHERE group_id = @groupId ORDER BY sort_order;
        """;

        var agents = new List<CustomAgent>();
        await using (var agentsCmd = new NpgsqlCommand(agentsSql, conn))
        {
            agentsCmd.Parameters.AddWithValue("groupId", groupId);
            await using var agentsReader = await agentsCmd.ExecuteReaderAsync();
            while (await agentsReader.ReadAsync())
            {
                agents.Add(new CustomAgent(
                    agentsReader.GetString(0),
                    agentsReader.GetString(1),
                    ((string[])agentsReader.GetValue(2)).ToList(),
                    agentsReader.GetString(3),
                    agentsReader.GetInt32(4),
                    agentsReader.GetBoolean(5)
                ));
            }
        }

        return new CustomAgentGroupDetail(groupId, matchedPersonName, matchedGroupName, createdAt, agents);
    }

    public async Task<bool> DeleteAgentGroupAsync(string personName, string? groupName = null)
    {
        var effectiveGroupName = groupName ?? personName;

        const string sql = """
            DELETE FROM agent_group ag
            USING person p
            WHERE ag.person_id = p.id
              AND LOWER(p.name) = LOWER(@personName)
              AND LOWER(ag.name) = LOWER(@groupName);
        """;

        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("personName", personName);
        cmd.Parameters.AddWithValue("groupName", effectiveGroupName);

        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    // ===== Private Helpers =====

    private static float[]? FromPostgresVector(string? vectorString)
    {
        if (string.IsNullOrWhiteSpace(vectorString))
            return null;

        var trimmed = vectorString.Trim('[', ']', '(', ')');
        var parts = trimmed.Split(',');

        try
        {
            return parts.Select(p => float.Parse(p.Trim())).ToArray();
        }
        catch
        {
            return null;
        }
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a == null || b == null || a.Length != b.Length || a.Length == 0)
            return 0;

        double dotProduct = 0, magnitudeA = 0, magnitudeB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        magnitudeA = Math.Sqrt(magnitudeA);
        magnitudeB = Math.Sqrt(magnitudeB);

        if (magnitudeA == 0 || magnitudeB == 0)
            return 0;

        return dotProduct / (magnitudeA * magnitudeB);
    }
}
