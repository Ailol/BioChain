using SpacetimeDB;

namespace BioChain.Module;

[SpacetimeDB.Type]
public enum NodeDomain : byte
{
    Chem,
    Elec,
    Meta,
    Epi,
    Struct,
}
