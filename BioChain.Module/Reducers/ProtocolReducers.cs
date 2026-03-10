using SpacetimeDB;

namespace BioChain.Module;

public static partial class Reducers
{
    [SpacetimeDB.Reducer]
    public static void AddProtocol(ReducerContext ctx,
        uint programId, uint sourceId, uint targetEdgeId,
        float gain, byte pol, string tau, string gate, byte coupling, float pr)
    {
        ctx.Db.protocol.Insert(new Protocol
        {
            ProgramId = programId,
            SourceId = sourceId,
            TargetEdgeId = targetEdgeId,
            Gain = gain,
            Pol = (Polarity)pol,
            Tau = tau,
            Gate = gate,
            Coupling = (CouplingType)coupling,
            Pr = pr,
        });
    }
}
