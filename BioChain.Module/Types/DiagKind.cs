using SpacetimeDB;

namespace BioChain.Module;

[SpacetimeDB.Type]
public enum DiagKind : byte
{
    Conservation, // Σ∇·
    Composite,    // ◈
    Dysreg,       // ⚡dep/exc/sus/...
}
