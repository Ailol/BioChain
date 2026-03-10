using SpacetimeDB;

namespace BioChain.Module;

public static partial class Reducers
{
    [SpacetimeDB.Reducer]
    public static void AddDiag(ReducerContext ctx,
        uint programId, byte kind, string code, string body)
    {
        ctx.Db.diag.Insert(new Diag
        {
            ProgramId = programId,
            Kind = (DiagKind)kind,
            Code = code,
            Body = body,
        });
    }
}
