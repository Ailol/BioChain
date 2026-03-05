using System.Text.Json;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;

namespace BioChain.Kernel.Graph;

/// <summary>
/// Neo4j implementation of <see cref="IGraphStore"/>.
/// Receives pre-built JSON from PG export_graph_json() and replaces
/// the full graph for a subject in a single write transaction.
/// </summary>
public sealed class Neo4jGraphStore : IGraphStore
{
    private readonly IDriver _neo4j;
    private readonly ILogger<Neo4jGraphStore> _logger;

    public Neo4jGraphStore(IDriver neo4j, ILogger<Neo4jGraphStore> logger)
    {
        _neo4j = neo4j;
        _logger = logger;
    }

    public async Task SyncPersonAsync(Guid subjectId, string graphJson, CancellationToken ct = default)
    {
        using var doc = JsonDocument.Parse(graphJson);
        var root = doc.RootElement;
        var pid = subjectId.ToString();
        var nodes = root.GetProperty("nodes");
        var edges = root.GetProperty("edges");

        var nodeCount = 0;
        var edgeCount = 0;

        await using var session = _neo4j.AsyncSession();
        await session.ExecuteWriteAsync(async tx =>
        {
            // DELETE all nodes for this subject (DETACH removes relationships too)
            await tx.RunAsync(
                "MATCH (n {subject_id: $pid}) DETACH DELETE n",
                new { pid });

            // CREATE nodes — use 'label' (PascalCase) for Neo4j label,
            // nid = 'kind:id' for unique identification (signals share codes across regions)
            foreach (var node in nodes.EnumerateArray())
            {
                var kind = node.GetProperty("kind").GetString()!;
                var label = node.TryGetProperty("label", out var lb) ? lb.GetString()! : kind;
                var id = node.GetProperty("id").GetInt32();
                var nid = $"{kind}:{id}";
                var code = node.GetProperty("code").GetString()!;
                var state = node.TryGetProperty("state", out var s) ? s.GetString() : null;
                var props = node.TryGetProperty("properties", out var p) && p.ValueKind != JsonValueKind.Null
                    ? p.ToString() : null;

                await tx.RunAsync(
                    "CALL apoc.create.node([$label], {subject_id: $pid, nid: $nid, kind: $kind, code: $code, state: $state, properties: $props}) YIELD node RETURN node",
                    new { label, kind, pid, nid, code, state, props });
                nodeCount++;
            }

            // CREATE relationships — match by nid (kind:id) for uniqueness
            foreach (var edge in edges.EnumerateArray())
            {
                var source = edge.GetProperty("source").GetString()!;
                var target = edge.GetProperty("target").GetString()!;
                var sourceType = edge.GetProperty("source_type").GetString() ?? "signal";
                var targetType = edge.GetProperty("target_type").GetString() ?? "signal";
                var opClass = edge.GetProperty("class").GetString() ?? "causal";
                var op = edge.TryGetProperty("operator", out var o) ? o.GetString() : null;
                var eProps = edge.TryGetProperty("properties", out var ep) && ep.ValueKind != JsonValueKind.Null
                    ? ep.ToString() : null;

                // Gate activation properties
                var gateId = edge.TryGetProperty("gate_id", out var gi) && gi.ValueKind != JsonValueKind.Null
                    ? (int?)gi.GetInt32() : null;
                var gateType = edge.TryGetProperty("gate_type", out var gt) && gt.ValueKind != JsonValueKind.Null
                    ? gt.GetString() : null;
                var gateActive = edge.TryGetProperty("gate_active", out var ga) && ga.ValueKind != JsonValueKind.Null
                    ? (bool?)ga.GetBoolean() : null;

                // Use source_id/target_id (nid) for precise matching when available,
                // fall back to kind+code for backward compat
                var sourceNid = edge.TryGetProperty("source_id", out var si) && si.ValueKind != JsonValueKind.Null
                    ? $"{sourceType}:{si.GetInt32()}" : null;
                var targetNid = edge.TryGetProperty("target_id", out var ti) && ti.ValueKind != JsonValueKind.Null
                    ? $"{targetType}:{ti.GetInt32()}" : null;

                string cypher;
                object parameters;

                if (sourceNid != null && targetNid != null)
                {
                    cypher = """
                        MATCH (a {subject_id: $pid, nid: $sourceNid})
                        MATCH (b {subject_id: $pid, nid: $targetNid})
                        CALL apoc.create.relationship(a, $relType, {operator: $op, properties: $eProps, gate_id: $gateId, gate_type: $gateType, gate_active: $gateActive}, b) YIELD rel
                        RETURN rel
                        """;
                    parameters = new { pid, sourceNid, targetNid, relType = opClass.ToUpperInvariant(), op, eProps, gateId, gateType, gateActive };
                }
                else
                {
                    // Fallback: match by kind+code (may produce Cartesian if codes aren't unique)
                    cypher = """
                        MATCH (a {subject_id: $pid, kind: $sourceType, code: $source})
                        MATCH (b {subject_id: $pid, kind: $targetType, code: $target})
                        CALL apoc.create.relationship(a, $relType, {operator: $op, properties: $eProps, gate_id: $gateId, gate_type: $gateType, gate_active: $gateActive}, b) YIELD rel
                        RETURN rel
                        """;
                    parameters = new { pid, source, target, sourceType, targetType, relType = opClass.ToUpperInvariant(), op, eProps, gateId, gateType, gateActive };
                }

                await tx.RunAsync(cypher, parameters);
                edgeCount++;
            }
        });

        _logger.LogDebug("[Neo4jGraphStore] Synced subject {SubjectId}: {Nodes} nodes, {Edges} edges",
            subjectId, nodeCount, edgeCount);
    }

    public async Task DeletePersonAsync(Guid subjectId, CancellationToken ct = default)
    {
        var pid = subjectId.ToString();
        await using var session = _neo4j.AsyncSession();
        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(
                "MATCH (n {subject_id: $pid}) DETACH DELETE n",
                new { pid });
        });

        _logger.LogDebug("[Neo4jGraphStore] Deleted subject {SubjectId}", subjectId);
    }

    public async Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        try
        {
            await using var session = _neo4j.AsyncSession();
            await session.ExecuteReadAsync(async tx =>
            {
                var result = await tx.RunAsync("RETURN 1");
                await result.ConsumeAsync();
            });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
