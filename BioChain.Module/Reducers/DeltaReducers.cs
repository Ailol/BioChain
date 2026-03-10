using SpacetimeDB;

namespace BioChain.Module;

public static partial class Reducers
{
    [SpacetimeDB.Reducer]
    public static void AddDeltaOp(ReducerContext ctx,
        uint programId, byte rank,
        string target, string rule, string timescale, string trigger)
    {
        ctx.Db.delta_op.Insert(new DeltaOp
        {
            ProgramId = programId,
            Rank = (Rank)rank,
            Target = target,
            Rule = rule,
            Timescale = timescale,
            Trigger = trigger,
        });
    }

    [SpacetimeDB.Reducer]
    public static void AppendDeltaLog(ReducerContext ctx,
        uint programId, uint nodeId, uint tick, float value)
    {
        ctx.Db.delta_log.Insert(new DeltaLog
        {
            ProgramId = programId,
            NodeId = nodeId,
            Tick = tick,
            Value = value,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        });
    }
}
