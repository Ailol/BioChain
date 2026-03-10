using SpacetimeDB;

namespace BioChain.Module;

[SpacetimeDB.Table(Name = "conv", Public = true)]
public partial struct Conv
{
    [SpacetimeDB.PrimaryKey]
    [SpacetimeDB.AutoInc]
    public uint Id;

    public uint ProgramId;
    public ConvFlagKind Kind;
    public string Signal;     // "DA@NAc"
    public string VPast;      // "↓(drift:-0.02/wk)"
    public string VCurrent;   // "↓↓(∫VTA_DA:sub-threshold)"
    public string VMeta;      // "σ̃low"
    public ConvergenceDiag Diagnosis;
    public string Prediction; // "⊳(DA@NAc,+4wk)=↓↓ (...)"
    public string Body;       // raw flag body for non-∮ entries
}
