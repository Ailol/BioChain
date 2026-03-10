using SpacetimeDB;

namespace BioChain.Module;

[SpacetimeDB.Table(Name = "meta_op", Public = true)]
public partial struct MetaOp
{
    [SpacetimeDB.PrimaryKey]
    [SpacetimeDB.AutoInc]
    public uint Id;

    public uint ProgramId;
    public MetaRank Rank;
    public string Target;   // signal/structure/protocol/architecture ref
    public string Operator;  // "σ̃", "∫̃", "⊲̃", "⊗̃"
    public string Spec;     // JSON: {"baseline":"norm","pull":0.8,...}
    public string Window;   // "active|closed|scheduled"
}
