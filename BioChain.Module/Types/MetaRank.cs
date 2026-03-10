using SpacetimeDB;

namespace BioChain.Module;

[SpacetimeDB.Type]
public enum MetaRank : byte
{
    M0, // Meta-scalar — setpoints
    M1, // Meta-vector — remodeling
    M2, // Meta-matrix — program
    M3, // Meta-tensor — architecture
}
