using SpacetimeDB;

namespace BioChain.Module;

[SpacetimeDB.Table(Name = "diag", Public = true)]
public partial struct Diag
{
    [SpacetimeDB.PrimaryKey]
    [SpacetimeDB.AutoInc]
    public uint Id;

    public uint ProgramId;
    public DiagKind Kind;
    public string Code;  // "DA", "anhedonia", "dep"
    public string Body;  // "+1/−2", "DA@NAc+DA@VTA+...", "{chain}"
}
