using SpacetimeDB;

namespace BioChain.Module;

[SpacetimeDB.Table(Name = "node", Public = true)]
public partial struct Node
{
    [SpacetimeDB.PrimaryKey]
    [SpacetimeDB.AutoInc]
    public uint Id;

    public uint ProgramId;
    public Rank Rank;
    public NodeDomain Domain;
    public string TypeSub;       // "L.nt", "R", "N.da", "K", etc.
    public string Code;          // "DA", "5HT", "CRH", "VTA_DA", etc.
    public SignalState State;
    public float Value;          // numeric state (0.0–1.0)
    public float Delta;          // Δ perturbation
    public string Region;        // "VTA", "DRN", "PVN", etc.
    public string Props;         // JSON: {"coup":"Gs","st":"down"}
    public string FieldOps;      // "∇→NAc ∇²syn"
    public bool IsRoot;          // ⊙
    public bool IsTerminal;      // ⊘
}
