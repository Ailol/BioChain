using Microsoft.Extensions.Configuration;
using Npgsql;

namespace BioChain.Repository.Repositories;

/// <summary>
/// Executes raw PostgreSQL functions for gate evaluation and graph serialisation.
/// Each call opens its own short-lived connection (same pattern as the original inline code).
/// </summary>
public sealed class GraphQueryRepository : IGraphQueryRepository
{
    private readonly string _connString;

    public GraphQueryRepository(IConfiguration config)
    {
        _connString = config.GetConnectionString("biochain")
            ?? throw new InvalidOperationException("ConnectionStrings:biochain is required");
    }

    public async Task<bool> EvaluateGateAsync(int gateId, Guid subjectId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT evaluate_gate($1, $2)", conn);
        cmd.Parameters.AddWithValue(gateId);
        cmd.Parameters.AddWithValue(subjectId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is true;
    }

    public async Task<string> SerializeProfileDslAsync(Guid subjectId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);

        await using (var refreshCmd = new NpgsqlCommand("SELECT refresh_graph()", conn))
            await refreshCmd.ExecuteNonQueryAsync(ct);

        await using var cmd = new NpgsqlCommand("SELECT serialize_profile_dsl($1)", conn);
        cmd.Parameters.AddWithValue(subjectId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result?.ToString() ?? "(empty graph)";
    }

    public async Task<string> ExportGraphJsonAsync(Guid subjectId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);

        await using (var refreshCmd = new NpgsqlCommand("SELECT refresh_graph()", conn))
            await refreshCmd.ExecuteNonQueryAsync(ct);

        await using var cmd = new NpgsqlCommand("SELECT export_graph_json($1)::text", conn);
        cmd.Parameters.AddWithValue(subjectId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result?.ToString() ?? "{}";
    }

    public async Task<List<FeedbackLoopRow>> FindFeedbackLoopsAsync(Guid subjectId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT loop_path, operators, is_positive FROM find_feedback_loops($1, true)", conn);
        cmd.Parameters.AddWithValue(subjectId);

        var rows = new List<FeedbackLoopRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new FeedbackLoopRow(
                reader.GetFieldValue<string[]>(0),
                reader.GetFieldValue<string[]>(1),
                reader.GetBoolean(2)));
        }
        return rows;
    }

    public async Task<List<DysregCascadeRow>> FindDysregCascadesAsync(Guid subjectId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT root_code, dysreg_type, cascade_depth, affected_path FROM find_dysreg_cascades($1, 5, true)", conn);
        cmd.Parameters.AddWithValue(subjectId);

        var rows = new List<DysregCascadeRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new DysregCascadeRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetFieldValue<string[]>(3)));
        }
        return rows;
    }

    public async Task<List<RegionActivityRow>> GetRegionActivityAsync(Guid subjectId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(@"
            SELECT entity_id, region_id, region_code, full_name, system,
                   computed_activity,
                   signal_count, signals_elevated, signals_depleted,
                   receptor_count, receptors_impaired, dysreg_count
            FROM v_region_activity WHERE entity_id = $1", conn);
        cmd.Parameters.AddWithValue(subjectId);

        var rows = new List<RegionActivityRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new RegionActivityRow(
                reader.GetGuid(0), reader.GetInt32(1),
                reader.GetString(2), reader.IsDBNull(3) ? "" : reader.GetString(3),
                reader.IsDBNull(4) ? "" : reader.GetString(4),
                reader.IsDBNull(5) ? "unknown" : reader.GetString(5),
                reader.GetInt64(6), reader.GetInt64(7), reader.GetInt64(8),
                reader.GetInt64(9), reader.GetInt64(10), reader.GetInt64(11)));
        }
        return rows;
    }

    public async Task<List<BindEntryRow>> GetBindEntriesAsync(Guid subjectId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(@"
            SELECT id, bind_expr, status
            FROM analysis
            WHERE entity_id = $1 AND tag = 'BIND' AND bind_expr IS NOT NULL
            ORDER BY seq", conn);
        cmd.Parameters.AddWithValue(subjectId);

        var rows = new List<BindEntryRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new BindEntryRow(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2)));
        }
        return rows;
    }
}
