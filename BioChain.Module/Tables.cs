using SpacetimeDB;

namespace BioChain.Module;

// ── Program — top-level container for a BioInfer analysis ────────────────────

[SpacetimeDB.Table(Name = "program", Public = true)]
public partial struct Program
{
    [SpacetimeDB.PrimaryKey]
    [SpacetimeDB.AutoInc]
    public uint Id;

    public string SubjectId;
    public string Label;
    public string Domains;       // comma-separated: "chem,struct,epi"
    public byte Stage;           // 1=BASE, 2=+PLAST, 3=+META, 4=+CONV
    public long CreatedAt;       // unix ms
    public long UpdatedAt;
}

// ── Node — any entity at any rank ────────────────────────────────────────────

[SpacetimeDB.Table(Name = "node", Public = true)]
public partial struct Node
{
    [SpacetimeDB.PrimaryKey]
    [SpacetimeDB.AutoInc]
    public uint Id;

    public uint ProgramId;
    public Rank Rank;            // R0..R3
    public NodeDomain Domain;
    public string TypeSub;       // "L.nt", "R", "N.da", "K", etc.
    public string Code;          // "DA", "5HT", "CRH", "VTA_DA", etc.
    public SignalState State;
    public float Value;          // numeric state (0.0–1.0)
    public float Delta;          // Δ perturbation
    public string Region;        // "VTA", "DRN", "PVN", etc.
    public string Props;         // JSON: {"coup":"Gs","st":"down"}
    public string FieldOps;      // "∇→NAc ∇²syn"
    public bool IsRoot;          // ⊙
    public bool IsTerminal;      // ⊘
}

// ── Edge — relationship between nodes ────────────────────────────────────────

[SpacetimeDB.Table(Name = "edge", Public = true)]
public partial struct Edge
{
    [SpacetimeDB.PrimaryKey]
    [SpacetimeDB.AutoInc]
    public uint Id;

    public uint ProgramId;
    public uint SourceId;
    public uint TargetId;
    public EdgeOp Op;
    public Rank Rank;            // which rank this edge belongs to
    public string GateCondition; // for gated edges: "{COND>=STATE}"
    public string Label;         // optional annotation
}

// ── Integration — R1 ∫ declarations ──────────────────────────────────────────

[SpacetimeDB.Table(Name = "integration", Public = true)]
public partial struct Integration
{
    [SpacetimeDB.PrimaryKey]
    [SpacetimeDB.AutoInc]
    public uint Id;

    public uint ProgramId;
    public uint UnitNodeId;      // the R1 structural unit node
    public string Inputs;        // JSON array: [{"ref":"DA@VTA","weight":"+0.7"},...]
    public string Output;        // "DA@VTA"
    public ActivationMode Activation;
    public string ActivationParam; // "thr:-45mV" or ""
}

// ── Protocol — R2 ⊲ declarations ─────────────────────────────────────────────

[SpacetimeDB.Table(Name = "protocol", Public = true)]
public partial struct Protocol
{
    [SpacetimeDB.PrimaryKey]
    [SpacetimeDB.AutoInc]
    public uint Id;

    public uint ProgramId;
    public uint SourceId;
    public uint TargetEdgeId;    // the R0 edge or R1 input being modified
    public float Gain;           // ×0.6, ×1.4
    public Polarity Pol;
    public string Tau;           // "fast:2ms", "slow:500ms", "tonic:∞"
    public string Gate;          // "{CORT>=↑}" or "open"
    public CouplingType Coupling;
    public float Pr;             // release probability 0.0–1.0
}

// ── Tensor — R3 ⊗ declarations ───────────────────────────────────────────────

[SpacetimeDB.Table(Name = "tensor", Public = true)]
public partial struct Tensor
{
    [SpacetimeDB.PrimaryKey]
    [SpacetimeDB.AutoInc]
    public uint Id;

    public uint ProgramId;
    public string Conditions;    // JSON: [{"ref":"GLU@HPC","op":">=","state":"↑"},...]
    public string Logic;         // "and" | "or" | "not"
    public string Effect;        // "{R:NMDA@HPC}:pass"
    public string EffectTarget;  // node reference
    public string EffectAction;  // "pass"|"block"|"amplify:1.5"|"switch:TARGET"
}

// ── Diag — post-section diagnostics (Σ∇·, ◈, ⚡) ────────────────────────────

[SpacetimeDB.Table(Name = "diag", Public = true)]
public partial struct Diag
{
    [SpacetimeDB.PrimaryKey]
    [SpacetimeDB.AutoInc]
    public uint Id;

    public uint ProgramId;
    public DiagKind Kind;
    public string Code;          // "DA", "anhedonia", "dep"
    public string Body;          // "+1/−2", "DA@NAc+DA@VTA+...", "{chain}"
}

// ── DeltaOp — plasticity operations (Δ@R0..Δ@R3) ────────────────────────────

[SpacetimeDB.Table(Name = "delta_op", Public = true)]
public partial struct DeltaOp
{
    [SpacetimeDB.PrimaryKey]
    [SpacetimeDB.AutoInc]
    public uint Id;

    public uint ProgramId;
    public Rank Rank;            // which rank this Δ targets
    public string Target;        // node/edge/protocol reference
    public string Rule;          // the plasticity rule body
    public string Timescale;     // "ms→wk", "h→yr", etc.
    public string Trigger;       // what triggers this Δ
}

// ── DeltaLog — append-only history for v_past ────────────────────────────────

[SpacetimeDB.Table(Name = "delta_log", Public = true)]
public partial struct DeltaLog
{
    [SpacetimeDB.PrimaryKey]
    [SpacetimeDB.AutoInc]
    public uint Id;

    public uint ProgramId;
    public uint NodeId;
    public uint Tick;
    public float Value;
    public long Timestamp;       // unix ms
}

// ── MetaOp — meta pipeline entries (M0..M3) ──────────────────────────────────

[SpacetimeDB.Table(Name = "meta_op", Public = true)]
public partial struct MetaOp
{
    [SpacetimeDB.PrimaryKey]
    [SpacetimeDB.AutoInc]
    public uint Id;

    public uint ProgramId;
    public MetaRank Rank;        // M0..M3
    public string Target;        // signal/structure/protocol/architecture ref
    public string Operator;      // "σ̃", "∫̃", "⊲̃", "⊗̃"
    public string Spec;          // JSON: {"baseline":"norm","pull":0.8,...}
    public string Window;        // developmental window: "active|closed|scheduled"
}

// ── Conv — convergence diagnostics (∮, ⊳, ⚡allo, etc.) ──────────────────────

[SpacetimeDB.Table(Name = "conv", Public = true)]
public partial struct Conv
{
    [SpacetimeDB.PrimaryKey]
    [SpacetimeDB.AutoInc]
    public uint Id;

    public uint ProgramId;
    public ConvFlagKind Kind;
    public string Signal;        // "DA@NAc"
    public string VPast;         // "↓(drift:-0.02/wk)"
    public string VCurrent;      // "↓↓(∫VTA_DA:sub-threshold)"
    public string VMeta;         // "σ̃low"
    public ConvergenceDiag Diagnosis;
    public string Prediction;    // "⊳(DA@NAc,+4wk)=↓↓ (...)"
    public string Body;          // raw flag body for non-∮ entries
}
