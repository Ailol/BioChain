using SpacetimeDB;

namespace BioChain.Module;

public static partial class Reducers
{
    [SpacetimeDB.Reducer]
    public static void CreateProgram(ReducerContext ctx, string subjectId, string label, string domains)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        ctx.Db.program.Insert(new Program
        {
            SubjectId = subjectId,
            Label = label,
            Domains = domains,
            Stage = 1,
            CreatedAt = now,
            UpdatedAt = now,
        });
    }

    [SpacetimeDB.Reducer]
    public static void SetProgramStage(ReducerContext ctx, uint programId, byte stage)
    {
        var p = ctx.Db.program.Id.Find(programId)
            ?? throw new Exception($"Program {programId} not found");
        p.Stage = stage;
        p.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        ctx.Db.program.Id.Update(p);
    }
}
