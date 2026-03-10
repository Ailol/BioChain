using SpacetimeDB;

namespace BioChain.Module;

public static partial class Reducers
{
    [SpacetimeDB.Reducer]
    public static void AddMetaOp(ReducerContext ctx,
        uint programId, byte rank,
        string target, string op, string spec, string window)
    {
        ctx.Db.meta_op.Insert(new MetaOp
        {
            ProgramId = programId,
            Rank = (MetaRank)rank,
            Target = target,
            Operator = op,
            Spec = spec,
            Window = window,
        });
    }
}
