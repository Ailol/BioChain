using SpacetimeDB;

namespace BioChain.Module;

[SpacetimeDB.Table(Name = "delta_op", Public = true)]
public partial struct DeltaOp
{
    [SpacetimeDB.PrimaryKey]
    [SpacetimeDB.AutoInc]
    public uint Id;

    public uint ProgramId;
    public Rank Rank;
    public string Target;    // node/edge/protocol reference
    public string Rule;      // plasticity rule body
    public string Timescale; // "ms→wk", "h→yr", etc.
    public string Trigger;
}
