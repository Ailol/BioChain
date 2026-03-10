using SpacetimeDB;

namespace BioChain.Module;

[SpacetimeDB.Type]
public enum SignalState : byte
{
    UpUp,     // ↑↑
    Up,       // ↑
    Norm,     // ≈
    Down,     // ↓
    DownDown, // ↓↓
    Osc,      // ~
    Null,     // ⊘
    Active,   // ●
}
