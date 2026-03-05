using Neo4j.Driver;

namespace BioChain.Kernel.Graph;

/// <summary>
/// Neo4j Graph Data Science wrappers for signal graph analysis.
/// Uses transient projections (project → stream → drop) to avoid persistent named graphs.
/// </summary>
public sealed class GdsAnalysis
{
    private readonly IDriver _driver;

    public GdsAnalysis(IDriver driver) => _driver = driver;

    public async Task<Dictionary<string, double>> PageRankAsync(Guid subjectId)
    {
        await using var session = _driver.AsyncSession();
        var result = await session.ExecuteReadAsync(async tx =>
        {
            var graphName = $"pr_{subjectId:N}";
            // Project graph
            await tx.RunAsync(@"
                CALL gds.graph.project($name,
                    {Signal: {properties: ['value', 'confidence']}},
                    {CAUSAL: {properties: ['gain'], orientation: 'NATURAL'}})",
                new { name = graphName });

            // Run PageRank
            var cursor = await tx.RunAsync(@"
                CALL gds.pageRank.stream($name, {maxIterations: 20, dampingFactor: 0.85})
                YIELD nodeId, score
                RETURN gds.util.asNode(nodeId).code AS code, score",
                new { name = graphName });
            var results = await cursor.ToListAsync(r => (r["code"].As<string>(), r["score"].As<double>()));

            // Drop graph
            await tx.RunAsync("CALL gds.graph.drop($name)", new { name = graphName });
            return results;
        });
        return result.ToDictionary(x => x.Item1, x => x.Item2);
    }

    public async Task<Dictionary<string, int>> LouvainAsync(Guid subjectId)
    {
        await using var session = _driver.AsyncSession();
        var result = await session.ExecuteReadAsync(async tx =>
        {
            var graphName = $"lv_{subjectId:N}";
            await tx.RunAsync(@"
                CALL gds.graph.project($name,
                    {Signal: {properties: ['value']}},
                    {CAUSAL: {orientation: 'UNDIRECTED'}})",
                new { name = graphName });

            var cursor = await tx.RunAsync(@"
                CALL gds.louvain.stream($name)
                YIELD nodeId, communityId
                RETURN gds.util.asNode(nodeId).code AS code, communityId",
                new { name = graphName });
            var results = await cursor.ToListAsync(r => (r["code"].As<string>(), r["communityId"].As<int>()));

            await tx.RunAsync("CALL gds.graph.drop($name)", new { name = graphName });
            return results;
        });
        return result.ToDictionary(x => x.Item1, x => x.Item2);
    }

    public async Task<double> ShortestPathAsync(Guid subjectId, string fromCode, string toCode)
    {
        await using var session = _driver.AsyncSession();
        return await session.ExecuteReadAsync(async tx =>
        {
            var graphName = $"sp_{subjectId:N}";
            await tx.RunAsync(@"
                CALL gds.graph.project($name,
                    'Signal',
                    {CAUSAL: {properties: ['gain']}})",
                new { name = graphName });

            // Get source and target node IDs
            var nodesCursor = await tx.RunAsync(@"
                MATCH (s:Signal {subject_id: $sid, code: $from}), (t:Signal {subject_id: $sid, code: $to})
                RETURN id(s) AS sourceId, id(t) AS targetId",
                new { sid = subjectId.ToString(), from = fromCode, to = toCode });
            var nodeRecord = await nodesCursor.SingleAsync();
            var sourceId = nodeRecord["sourceId"].As<long>();
            var targetId = nodeRecord["targetId"].As<long>();

            var cursor = await tx.RunAsync(@"
                CALL gds.shortestPath.dijkstra.stream($name, {
                    sourceNode: $source,
                    targetNode: $target,
                    relationshipWeightProperty: 'gain'
                })
                YIELD totalCost
                RETURN totalCost",
                new { name = graphName, source = sourceId, target = targetId });
            var cost = await cursor.SingleAsync(r => r["totalCost"].As<double>());

            await tx.RunAsync("CALL gds.graph.drop($name)", new { name = graphName });
            return cost;
        });
    }
}
