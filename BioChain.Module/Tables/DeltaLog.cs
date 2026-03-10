using SpacetimeDB;

namespace BioChain.Module;

[SpacetimeDB.Table(Name = "delta_log", Public = true)]
public partial struct DeltaLog
{
    [SpacetimeDB.PrimaryKey]
    [SpacetimeDB.AutoInc]
    public uint Id;

    public uint ProgramId;
    public uint NodeId;
    public uint Tick;
    public float Value;
    public long Timestamp; // unix ms
}
