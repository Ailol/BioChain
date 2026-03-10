using SpacetimeDB;

namespace BioChain.Module;

[SpacetimeDB.Type]
public enum ConvFlagKind : byte
{
    Allostatic,        // ⚡allo
    Resistance,        // ⚡resist
    TrajectoryDiverge, // ⚡diverge
    Instability,       // ⚡unstable
    EpigeneticLock,    // ⚡lock
    DeltaCascade,      // ⚡cascade
}
