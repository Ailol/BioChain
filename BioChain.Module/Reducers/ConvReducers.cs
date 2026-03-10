using SpacetimeDB;

namespace BioChain.Module;

public static partial class Reducers
{
    [SpacetimeDB.Reducer]
    public static void AddConv(ReducerContext ctx,
        uint programId, byte kind, string signal,
        string vPast, string vCurrent, string vMeta,
        byte diagnosis, string prediction, string body)
    {
        ctx.Db.conv.Insert(new Conv
        {
            ProgramId = programId,
            Kind = (ConvFlagKind)kind,
            Signal = signal,
            VPast = vPast,
            VCurrent = vCurrent,
            VMeta = vMeta,
            Diagnosis = (ConvergenceDiag)diagnosis,
            Prediction = prediction,
            Body = body,
        });
    }
}
