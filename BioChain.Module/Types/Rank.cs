using SpacetimeDB;

namespace BioChain.Module;

[SpacetimeDB.Type]
public enum Rank : byte
{
    R0, // Scalar — signal values
    R1, // Vector — structural integration
    R2, // Matrix — pairwise protocols
    R3, // Tensor — cross-connective
}
