using SpacetimeDB;

namespace BioChain.Module;

public static partial class Reducers
{
    [SpacetimeDB.Reducer]
    public static void AddTensor(ReducerContext ctx,
        uint programId, string conditions, string logic,
        string effect, string effectTarget, string effectAction)
    {
        ctx.Db.tensor.Insert(new Tensor
        {
            ProgramId = programId,
            Conditions = conditions,
            Logic = logic,
            Effect = effect,
            EffectTarget = effectTarget,
            EffectAction = effectAction,
        });
    }
}
