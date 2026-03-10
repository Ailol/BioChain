using SpacetimeDB;

namespace BioChain.Module;

[SpacetimeDB.Table(Name = "tensor", Public = true)]
public partial struct Tensor
{
    [SpacetimeDB.PrimaryKey]
    [SpacetimeDB.AutoInc]
    public uint Id;

    public uint ProgramId;
    public string Conditions;    // JSON: [{"ref":"GLU@HPC","op":">=","state":"↑"},...]
    public string Logic;         // "and" | "or" | "not"
    public string Effect;        // "{R:NMDA@HPC}:pass"
    public string EffectTarget;
    public string EffectAction;  // "pass"|"block"|"amplify:1.5"|"switch:TARGET"
}
