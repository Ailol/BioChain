using Microsoft.EntityFrameworkCore;
using BioChain.Repository.Entities;

namespace BioChain.Repository;

public class ObservationRepository(IDbContextFactory<PersonalityDbContext> factory, IUserContext userContext)
{
    private const string AccessCheck = """
        AND (per.owner_id = @userId
             OR EXISTS (SELECT 1 FROM person_share ps
                        WHERE ps.person_id = per.id
                        AND (ps.shared_with_user_id = @userId
                             OR ps.shared_with_email = @email)))
        """;

    public async Task InsertAsync(ObservationEntity entity)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.Observations.Add(entity);
        await db.SaveChangesAsync();
    }

    public async Task InsertWithEmbeddingAsync(ObservationEntity entity, string vectorLiteral)
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO observation (
                person_id, personality_id, analysis_run_id, analyzed_data_id,
                signal_id, subject_receptor_id, subject_state, subject_dose_range,
                operator, target_signal_id, target_receptor_id, target_state,
                region_id, temporal, gate_instance_id, gate_formula,
                lifecycle_stage, confidence, context,
                failure_mode, intensity, pathway_id, circuit_id,
                signals_text, formula, state_text, circuits_text, notes,
                metadata, embedding, created_at
            ) VALUES (
                @p0, @p1, @p2, @p3,
                @p4, @p5, @p6, @p7,
                @p8, @p9, @p10, @p11,
                @p12, @p13, @p14, @p15,
                @p16, @p17, @p18,
                @p19, @p20, @p21, @p22,
                @p23, @p24, @p25, @p26, @p27,
                @p28::jsonb, @p29::vector, NOW()
            )
            """,
            new Npgsql.NpgsqlParameter("p0", entity.PersonId),
            new Npgsql.NpgsqlParameter("p1", entity.PersonalityId),
            new Npgsql.NpgsqlParameter("p2", (object?)entity.AnalysisRunId ?? DBNull.Value),
            new Npgsql.NpgsqlParameter("p3", (object?)entity.AnalyzedDataId ?? DBNull.Value),
            new Npgsql.NpgsqlParameter("p4", entity.SignalId),
            new Npgsql.NpgsqlParameter("p5", (object?)entity.SubjectReceptorId ?? DBNull.Value),
            new Npgsql.NpgsqlParameter("p6", (object?)entity.SubjectState ?? DBNull.Value),
            new Npgsql.NpgsqlParameter("p7", (object?)entity.SubjectDoseRange ?? DBNull.Value),
            new Npgsql.NpgsqlParameter("p8", (object?)entity.Operator ?? DBNull.Value),
            new Npgsql.NpgsqlParameter("p9", (object?)entity.TargetSignalId ?? DBNull.Value),
            new Npgsql.NpgsqlParameter("p10", (object?)entity.TargetReceptorId ?? DBNull.Value),
            new Npgsql.NpgsqlParameter("p11", (object?)entity.TargetState ?? DBNull.Value),
            new Npgsql.NpgsqlParameter("p12", (object?)entity.RegionId ?? DBNull.Value),
            new Npgsql.NpgsqlParameter("p13", (object?)entity.Temporal ?? DBNull.Value),
            new Npgsql.NpgsqlParameter("p14", (object?)entity.GateInstanceId ?? DBNull.Value),
            new Npgsql.NpgsqlParameter("p15", (object?)entity.GateFormula ?? DBNull.Value),
            new Npgsql.NpgsqlParameter("p16", (object?)entity.LifecycleStage ?? DBNull.Value),
            new Npgsql.NpgsqlParameter("p17", (object?)entity.Confidence ?? DBNull.Value),
            new Npgsql.NpgsqlParameter("p18", (object?)entity.Context ?? DBNull.Value),
            new Npgsql.NpgsqlParameter("p19", (object?)entity.FailureMode ?? DBNull.Value),
            new Npgsql.NpgsqlParameter("p20", (object?)entity.Intensity ?? DBNull.Value),
            new Npgsql.NpgsqlParameter("p21", (object?)entity.PathwayId ?? DBNull.Value),
            new Npgsql.NpgsqlParameter("p22", (object?)entity.CircuitId ?? DBNull.Value),
            new Npgsql.NpgsqlParameter("p23", (object?)entity.SignalsText ?? DBNull.Value),
            new Npgsql.NpgsqlParameter("p24", entity.Formula),
            new Npgsql.NpgsqlParameter("p25", (object?)entity.StateText ?? DBNull.Value),
            new Npgsql.NpgsqlParameter("p26", (object?)entity.CircuitsText ?? DBNull.Value),
            new Npgsql.NpgsqlParameter("p27", (object?)entity.Notes ?? DBNull.Value),
            new Npgsql.NpgsqlParameter("p28", entity.Metadata),
            new Npgsql.NpgsqlParameter("p29", vectorLiteral));
    }

    public async Task<List<(string Signal, string Formula)>> GetByPersonAsync(string person)
    {
        await using var db = await factory.CreateDbContextAsync();
        var userId = userContext.UserId;
        var email = userContext.Email;
        return await db.Observations
            .Join(db.Personalities, o => o.PersonalityId, p => p.Id, (o, p) => new { o, p })
            .Join(db.Persons, x => x.p.PersonId, per => per.Id, (x, per) => new { x.o, per })
            .Join(db.Signals, x => x.o.SignalId, s => s.Id, (x, s) => new { x.o, x.per, s })
            .Where(x => x.per.FirstName.ToLower() == person.ToLower())
            .Where(x => x.per.OwnerId == userId
                || db.PersonShares.Any(ps => ps.PersonId == x.per.Id
                    && (ps.SharedWithUserId == userId
                        || (email != null && ps.SharedWithEmail == email))))
            .Select(x => new { Signal = x.s.Key, x.o.Formula })
            .ToListAsync()
            .ContinueWith(t => t.Result.Select(x => (x.Signal, x.Formula)).ToList());
    }

    public async Task<List<(string Signal, int Count)>> GetSignalCountsAsync(string person)
    {
        await using var db = await factory.CreateDbContextAsync();
        var userId = userContext.UserId;
        var email = userContext.Email;
        return await db.Observations
            .Join(db.Personalities, o => o.PersonalityId, p => p.Id, (o, p) => new { o, p })
            .Join(db.Persons, x => x.p.PersonId, per => per.Id, (x, per) => new { x.o, per })
            .Join(db.Signals, x => x.o.SignalId, s => s.Id, (x, s) => new { x.o, x.per, s })
            .Where(x => x.per.FirstName.ToLower() == person.ToLower())
            .Where(x => x.per.OwnerId == userId
                || db.PersonShares.Any(ps => ps.PersonId == x.per.Id
                    && (ps.SharedWithUserId == userId
                        || (email != null && ps.SharedWithEmail == email))))
            .GroupBy(x => x.s.Key)
            .Select(g => new { Signal = g.Key, Count = g.Count() })
            .ToListAsync()
            .ContinueWith(t => t.Result.Select(x => (x.Signal, x.Count)).ToList());
    }

    public async Task<List<ObservationEntry>> GetObservationEntriesAsync(string person)
    {
        await using var db = await factory.CreateDbContextAsync();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT s.key AS signal, o.formula, o.state_text, o.circuits_text, o.signals_text,
                   o.embedding::text, o.intensity, o.subject_state, o.confidence,
                   o.failure_mode, o.operator, o.temporal, o.created_at
            FROM observation o
            JOIN signal s ON s.id = o.signal_id
            JOIN personality p ON p.id = o.personality_id
            JOIN person per ON per.id = p.person_id
            WHERE lower(per.first_name) = lower(@person)
              AND o.embedding IS NOT NULL
              {AccessCheck}
            """;
        AddParam(cmd, "person", person);
        AddParam(cmd, "userId", userContext.UserId);
        AddParam(cmd, "email", (object?)userContext.Email ?? DBNull.Value);

        var results = new List<ObservationEntry>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new ObservationEntry(
                Signal: reader.GetString(0),
                Formula: reader.GetString(1),
                StateText: reader.IsDBNull(2) ? null : reader.GetString(2),
                CircuitsText: reader.IsDBNull(3) ? null : reader.GetString(3),
                SignalsText: reader.IsDBNull(4) ? null : reader.GetString(4),
                Embedding: VectorParser.Parse(reader.GetString(5)),
                Intensity: reader.IsDBNull(6) ? 1.0f : reader.GetFloat(6),
                SubjectState: reader.IsDBNull(7) ? null : reader.GetString(7),
                Confidence: reader.IsDBNull(8) ? null : reader.GetString(8),
                FailureMode: reader.IsDBNull(9) ? null : reader.GetString(9),
                Operator: reader.IsDBNull(10) ? null : reader.GetString(10),
                Temporal: reader.IsDBNull(11) ? null : reader.GetString(11),
                CreatedAt: reader.GetDateTime(12)));
        }
        return results;
    }

    public record ObservationEntry(
        string Signal, string Formula,
        string? StateText, string? CircuitsText, string? SignalsText,
        float[] Embedding, float Intensity,
        string? SubjectState, string? Confidence,
        string? FailureMode, string? Operator, string? Temporal,
        DateTime CreatedAt);

    public async Task<List<(int Id, string Formula)>> GetWithoutEmbeddingsAsync(string? person = null)
    {
        await using var db = await factory.CreateDbContextAsync();
        var userId = userContext.UserId;
        var email = userContext.Email;
        var query = db.Observations
            .Join(db.Personalities, o => o.PersonalityId, p => p.Id, (o, p) => new { o, p })
            .Join(db.Persons, x => x.p.PersonId, per => per.Id, (x, per) => new { x.o, per })
            .Where(x => x.o.Embedding == null)
            .Where(x => x.per.OwnerId == userId
                || db.PersonShares.Any(ps => ps.PersonId == x.per.Id
                    && (ps.SharedWithUserId == userId
                        || (email != null && ps.SharedWithEmail == email))));

        if (!string.IsNullOrEmpty(person))
            query = query.Where(x => x.per.FirstName.ToLower() == person.ToLower());

        return await query
            .Select(x => new { x.o.Id, x.o.Formula })
            .ToListAsync()
            .ContinueWith(t => t.Result.Select(x => (x.Id, x.Formula)).ToList());
    }

    public async Task UpdateEmbeddingAsync(int observationId, string vectorLiteral)
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE observation SET embedding = @p1::vector WHERE id = @p0",
            new Npgsql.NpgsqlParameter("p0", observationId),
            new Npgsql.NpgsqlParameter("p1", vectorLiteral));
    }

    public async Task<List<TimelineEntry>> GetTimelineAsync(string person)
    {
        await using var db = await factory.CreateDbContextAsync();
        var userId = userContext.UserId;
        var email = userContext.Email;
        return await db.Observations
            .Join(db.Personalities, o => o.PersonalityId, p => p.Id, (o, p) => new { o, p })
            .Join(db.Persons, x => x.p.PersonId, per => per.Id, (x, per) => new { x.o, per })
            .Join(db.Signals, x => x.o.SignalId, s => s.Id, (x, s) => new { x.o, x.per, s })
            .Where(x => x.per.FirstName.ToLower() == person.ToLower())
            .Where(x => x.per.OwnerId == userId
                || db.PersonShares.Any(ps => ps.PersonId == x.per.Id
                    && (ps.SharedWithUserId == userId
                        || (email != null && ps.SharedWithEmail == email))))
            .OrderBy(x => x.o.CreatedAt)
            .Select(x => new TimelineEntry(x.s.Key, x.o.Intensity ?? 0f, x.o.CreatedAt))
            .ToListAsync();
    }

    public record TimelineEntry(string Signal, float Intensity, DateTime CreatedAt);

    public async Task<List<string>> GetScoredFormulasAsync(
        string person, string messageVector, string relationshipVector,
        float messageWeight, int topPerSignal)
    {
        await using var db = await factory.CreateDbContextAsync();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            WITH scored AS (
                SELECT s.key AS signal, o.formula,
                    (1 - (o.embedding <=> @msgVec::vector)) * @msgW
                    + (1 - (o.embedding <=> @relVec::vector)) * (1 - @msgW) AS score,
                    ROW_NUMBER() OVER (PARTITION BY s.key ORDER BY
                        (1 - (o.embedding <=> @msgVec::vector)) * @msgW
                        + (1 - (o.embedding <=> @relVec::vector)) * (1 - @msgW) DESC
                    ) AS rn
                FROM observation o
                JOIN signal s ON s.id = o.signal_id
                JOIN personality p ON p.id = o.personality_id
                JOIN person per ON per.id = p.person_id
                WHERE lower(per.first_name) = lower(@person)
                  AND o.embedding IS NOT NULL
                  {AccessCheck}
            )
            SELECT formula FROM scored WHERE rn <= @topN ORDER BY score DESC
            """;
        AddParam(cmd, "msgVec", messageVector);
        AddParam(cmd, "relVec", relationshipVector);
        AddParam(cmd, "msgW", messageWeight);
        AddParam(cmd, "person", person);
        AddParam(cmd, "topN", topPerSignal);
        AddParam(cmd, "userId", userContext.UserId);
        AddParam(cmd, "email", (object?)userContext.Email ?? DBNull.Value);

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
