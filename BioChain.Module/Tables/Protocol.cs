using SpacetimeDB;

namespace BioChain.Module;

[SpacetimeDB.Table(Name = "protocol", Public = true)]
public partial struct Protocol
{
    [SpacetimeDB.PrimaryKey]
    [SpacetimeDB.AutoInc]
    public uint Id;

    public uint ProgramId;
    public uint SourceId;
    public uint TargetEdgeId;
    public float Gain;
    public Polarity Pol;
    public string Tau;           // "fast:2ms", "slow:500ms", "tonic:∞"
    public string Gate;          // "{CORT>=↑}" or "open"
    public CouplingType Coupling;
    public float Pr;             // release probability 0.0–1.0
}
