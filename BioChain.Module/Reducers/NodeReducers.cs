using SpacetimeDB;

namespace BioChain.Module;

public static partial class Reducers
{
    [SpacetimeDB.Reducer]
    public static uint AddNode(ReducerContext ctx,
        uint programId, byte rank, byte domain,
        string typeSub, string code, byte state, float value, float delta,
        string region, string props, string fieldOps,
        bool isRoot, bool isTerminal)
    {
        var node = ctx.Db.node.Insert(new Node
        {
            ProgramId = programId,
            Rank = (Rank)rank,
            Domain = (NodeDomain)domain,
            TypeSub = typeSub,
            Code = code,
            State = (SignalState)state,
            Value = value,
            Delta = delta,
            Region = region,
            Props = props,
            FieldOps = fieldOps,
            IsRoot = isRoot,
            IsTerminal = isTerminal,
        });
        return node.Id;
    }

    [SpacetimeDB.Reducer]
    public static void UpdateNodeState(ReducerContext ctx, uint nodeId, byte state, float value)
    {
        var n = ctx.Db.node.Id.Find(nodeId)
            ?? throw new Exception($"Node {nodeId} not found");
        n.State = (SignalState)state;
        n.Value = value;
        ctx.Db.node.Id.Update(n);
    }
}
