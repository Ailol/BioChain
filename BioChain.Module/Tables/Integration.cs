using SpacetimeDB;

namespace BioChain.Module;

[SpacetimeDB.Table(Name = "integration", Public = true)]
public partial struct Integration
{
    [SpacetimeDB.PrimaryKey]
    [SpacetimeDB.AutoInc]
    public uint Id;

    public uint ProgramId;
    public uint UnitNodeId;
    public string Inputs;          // JSON: [{"ref":"DA@VTA","weight":"+0.7"},...]
    public string Output;          // "DA@VTA"
    public ActivationMode Activation;
    public string ActivationParam; // "thr:-45mV" or ""
}
