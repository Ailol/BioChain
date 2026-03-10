using SpacetimeDB;

namespace BioChain.Module;

[SpacetimeDB.Table(Name = "program", Public = true)]
public partial struct Program
{
    [SpacetimeDB.PrimaryKey]
    [SpacetimeDB.AutoInc]
    public uint Id;

    public string SubjectId;
    public string Label;
    public string Domains;       // comma-separated: "chem,struct,epi"
    public byte Stage;           // 1=BASE, 2=+PLAST, 3=+META, 4=+CONV
    public long CreatedAt;       // unix ms
    public long UpdatedAt;
}
