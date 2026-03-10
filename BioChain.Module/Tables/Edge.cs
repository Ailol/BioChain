using SpacetimeDB;

namespace BioChain.Module;

[SpacetimeDB.Table(Name = "edge", Public = true)]
public partial struct Edge
{
    [SpacetimeDB.PrimaryKey]
    [SpacetimeDB.AutoInc]
    public uint Id;

    public uint ProgramId;
    public uint SourceId;
    public uint TargetId;
    public EdgeOp Op;
    public Rank Rank;
    public string GateCondition; // for gated edges: "{COND>=STATE}"
    public string Label;
}
