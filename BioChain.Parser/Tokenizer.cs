namespace BioChain.Parser;

// ── Token types ──────────────────────────────────────────────────────────────

public enum TokenKind
{
    // Sections
    Domain,         // @domain:
    R0, R1, R2, R3, // @R0, @R1, @R2, @R3
    Delta,          // @Δ
    M0, M1, M2, M3, // @M0..@M3
    Phase,          // #phase_name

    // Operators
    Arrow,          // →
    Inhibit,        // ⊣
    Bidirectional,  // ⇌
    Amplify,        // ⊃
    Attenuate,      // ⊂
    Modulate,       // ~>
    Transcribe,     // =>
    Transport,      // |>
    StrongArrow,    // →!
    StrongInhibit,  // ⊣!
    Reverse,        // ←
    Integrate,      // ∫
    Protocol,       // ⊲
    Tensor,         // ⊗
    Implies,        // ⟹
    And,            // ∧
    Or,             // ∨
    Not,            // ¬
    Root,           // ⊙
    Terminal,       // ⊘
    Merge,          // &
    GateOpen,       // ?
    RingOpen,       // «
    RingClose,      // »
    BranchOpen,     // (
    BranchClose,    // )
    BracketOpen,    // [
    BracketClose,   // ]
    BraceOpen,      // {
    BraceClose,     // }

    // Post-sections
    Conservation,   // Σ∇·
    Composite,      // ◈
    Dysreg,         // ⚡
    Convergence,    // ∮
    Trajectory,     // ⊳

    // Values
    NodeRef,        // {TYPE:CODE[STATE]@REGION}
    Identifier,     // bare word
    Number,         // 0.8, -0.3
    String,         // key:val
    Colon,          // :
    Comma,          // ,
    Equals,         // =
    Newline,
    Eof,
}

public record Token(TokenKind Kind, string Value, int Line, int Col);

// ── Tokenizer ────────────────────────────────────────────────────────────────

public static class Tokenizer
{
    public static List<Token> Tokenize(string input)
    {
        // TODO: walk chars, emit tokens
        // Key patterns:
        //   @domain: → Domain token + rest of line
        //   @R0..@R3 → section tokens
        //   @Δ → Delta section
        //   @M0..@M3 → MetaRank sections
        //   #name → Phase token
        //   {TYPE:CODE[STATE]@REGION FIELD} → NodeRef (balanced braces)
        //   Σ∇·, ◈, ⚡, ∮, ⊳ → post-section tokens
        //   Unicode operators: → ⊣ ⇌ ⊃ ⊂ ∫ ⊲ ⊗ ⟹ ∧ ∨ ¬ ⊙ ⊘ « »
        throw new NotImplementedException();
    }
}
