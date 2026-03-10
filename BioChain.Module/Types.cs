using SpacetimeDB;

namespace BioChain.Module;

// ── Enums ────────────────────────────────────────────────────────────────────

[SpacetimeDB.Type]
public enum Rank : byte
{
    R0, // Scalar — signal values
    R1, // Vector — structural integration
    R2, // Matrix — pairwise protocols
    R3, // Tensor — cross-connective
}

[SpacetimeDB.Type]
public enum MetaRank : byte
{
    M0, // Meta-scalar — setpoints
    M1, // Meta-vector — remodeling
    M2, // Meta-matrix — program
    M3, // Meta-tensor — architecture
}

[SpacetimeDB.Type]
public enum NodeDomain : byte
{
    Chem,
    Elec,
    Meta,
    Epi,
    Struct,
}

[SpacetimeDB.Type]
public enum SignalState : byte
{
    UpUp,   // ↑↑
    Up,     // ↑
    Norm,   // ≈
    Down,   // ↓
    DownDown, // ↓↓
    Osc,    // ~
    Null,   // ⊘
    Active, // ●
}

[SpacetimeDB.Type]
public enum EdgeOp : byte
{
    Activate,       // →
    Inhibit,        // ⊣
    Bidirectional,  // ⇌
    Amplify,        // ⊃
    Attenuate,      // ⊂
    Modulate,       // ~>
    Transcribe,     // =>
    Transport,      // |>
    StrongActivate, // →!
    StrongInhibit,  // ⊣!
    Reverse,        // ←
}

[SpacetimeDB.Type]
public enum Polarity : byte { Exc, Inh, Mod }

[SpacetimeDB.Type]
public enum CouplingType : byte { Syn, Vol, Gap, Para }

[SpacetimeDB.Type]
public enum TransmissionMode : byte { Synaptic, Volume }

[SpacetimeDB.Type]
public enum ActivationMode : byte { Threshold, Rate, Burst, Tonic }

[SpacetimeDB.Type]
public enum DiagKind : byte
{
    Conservation,   // Σ∇·
    Composite,      // ◈
    Dysreg,         // ⚡dep/exc/sus/...
}

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

[SpacetimeDB.Type]
public enum ConvFlagKind : byte
{
    Allostatic,         // ⚡allo
    Resistance,         // ⚡resist
    TrajectoryDiverge,  // ⚡diverge
    Instability,        // ⚡unstable
    EpigeneticLock,     // ⚡lock
    DeltaCascade,       // ⚡cascade
}
