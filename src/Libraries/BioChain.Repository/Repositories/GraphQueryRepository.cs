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
        _connString = config.GetConnectionString("personality")
            ?? throw new InvalidOperationException("ConnectionStrings:personality is required");
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
}
