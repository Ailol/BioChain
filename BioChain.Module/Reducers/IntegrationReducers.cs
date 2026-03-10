using SpacetimeDB;

namespace BioChain.Module;

public static partial class Reducers
{
    [SpacetimeDB.Reducer]
    public static void AddIntegration(ReducerContext ctx,
        uint programId, uint unitNodeId,
        string inputs, string output, byte activation, string activationParam)
    {
        ctx.Db.integration.Insert(new Integration
        {
            ProgramId = programId,
            UnitNodeId = unitNodeId,
            Inputs = inputs,
            Output = output,
            Activation = (ActivationMode)activation,
            ActivationParam = activationParam,
        });
    }
}
