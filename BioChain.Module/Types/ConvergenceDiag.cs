using SpacetimeDB;

namespace BioChain.Module;

[SpacetimeDB.Type]
public enum ConvergenceDiag : byte
{
    ConvergingLow,
    ConvergingHigh,
    ConvergingNorm,
    Divergent,
    Contested,
    Unstable,
    Locked,
    Breaking,
}
