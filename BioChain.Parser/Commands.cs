namespace BioChain.Parser;

// ── Output types — what the parser produces ──────────────────────────────────
// Each command maps 1:1 to a Module reducer call.
// The Agent reads these and calls the corresponding reducer.

public abstract record ParsedCommand;

public record SetDomains(string Domains) : ParsedCommand;

public record InsertNode(
    byte Rank,          // 0=R0, 1=R1, 2=R2, 3=R3
    byte Domain,        // chem=0, elec=1, meta=2, epi=3, struct=4
    string TypeSub,     // "L.nt", "R", "N.da", etc.
    string Code,        // "DA", "5HT", etc.
    byte State,         // maps to SignalState enum
    float Value,
    float Delta,
    string Region,
    string Props,       // JSON
    string FieldOps,
    bool IsRoot,
    bool IsTerminal
) : ParsedCommand;

public record InsertEdge(
    string SourceRef,   // "{L.nt:DA@VTA}" — resolved to ID by Agent
    string TargetRef,
    byte Op,            // maps to EdgeOp enum
    byte Rank,
    string GateCondition,
    string Label
) : ParsedCommand;

public record InsertIntegration(
    string UnitRef,     // R1 structural unit reference
    string Inputs,      // JSON array
    string Output,
    byte Activation,
    string ActivationParam
) : ParsedCommand;

public record InsertProtocol(
    string SourceRef,
    string TargetRef,   // edge or R1 input reference
    float Gain,
    byte Pol,
    string Tau,
    string Gate,
    byte Coupling,
    float Pr
) : ParsedCommand;

public record InsertTensor(
    string Conditions,  // JSON
    string Logic,       // "and"|"or"|"not"
    string Effect,
    string EffectTarget,
    string EffectAction
) : ParsedCommand;

public record InsertDiag(
    byte Kind,          // conservation=0, composite=1, dysreg=2
    string Code,
    string Body
) : ParsedCommand;

public record InsertDeltaOp(
    byte Rank,
    string Target,
    string Rule,
    string Timescale,
    string Trigger
) : ParsedCommand;

public record InsertMetaOp(
    byte Rank,          // M0=0..M3=3
    string Target,
    string Operator,    // "σ̃", "∫̃", "⊲̃", "⊗̃"
    string Spec,        // JSON
    string Window
) : ParsedCommand;

public record InsertConv(
    byte Kind,
    string Signal,
    string VPast,
    string VCurrent,
    string VMeta,
    byte Diagnosis,
    string Prediction,
    string Body
) : ParsedCommand;

// ── Parse result ─────────────────────────────────────────────────────────────

public record ParseResult(
    bool Success,
    List<ParsedCommand> Commands,
    List<string> Errors
)
{
    public static ParseResult Ok(List<ParsedCommand> commands) =>
        new(true, commands, []);

    public static ParseResult Fail(List<string> errors) =>
        new(false, [], errors);
}
