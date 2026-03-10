using SpacetimeDB;

namespace BioChain.Module;

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
