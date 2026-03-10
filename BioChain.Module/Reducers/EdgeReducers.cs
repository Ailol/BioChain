using SpacetimeDB;

namespace BioChain.Module;

public static partial class Reducers
{
    [SpacetimeDB.Reducer]
    public static uint AddEdge(ReducerContext ctx,
        uint programId, uint sourceId, uint targetId,
        byte op, byte rank, string gateCondition, string label)
    {
        var edge = ctx.Db.edge.Insert(new Edge
        {
            ProgramId = programId,
            SourceId = sourceId,
            TargetId = targetId,
            Op = (EdgeOp)op,
            Rank = (Rank)rank,
            GateCondition = gateCondition,
            Label = label,
        });
        return edge.Id;
    }
}
