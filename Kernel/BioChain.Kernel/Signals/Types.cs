using Apache.Arrow;
using Apache.Arrow.Types;

namespace BioChain.Kernel.Signals;

// ──────────────────────── DATA TYPES ────────────────────────

// Loaded from Postgres per tick (3 DISTINCT ON queries)
public sealed record SignalRow(int Id, string Code, string? Region, string State,
    double Value, double Baseline, double Confidence, string Distribution,
    double TauMinMs, double TauMaxMs, double RangeLow, double RangeHigh);

public sealed record EdgeRow(int Id, int SourceId, int TargetId,
    string Operator, string OperatorClass,
    double Gain, double NoiseSigma, string TransferFn,
    int DelayMs, double? ClampLo, double? ClampHi, int? GateId, int? ToolId, bool Active);

public sealed record GateRow(int Id, string Code, string Type,
    double? Threshold, string? Expression, double? Probability, bool Latched,
    string? Prompt, string? Model, string? ParseMap, string? Fallback,
    int? TimeoutMs, int? CacheMs);

/// <summary>LLM-defined computed signal: expression evaluated each tick, output tracked in FormulaOutputs.</summary>
public sealed record FormulaRule(string OutputCode, string Expression, double DecayRate = 1.0);

// Tick IO
public abstract record Input
{
    public sealed record Inject(string SignalCode, double Value, double Confidence = 1.0) : Input;
    public sealed record GateResult(int GateId, bool Fired, double Confidence = 1.0) : Input;
    public sealed record ToolResult(string ToolCode, Dictionary<string, double> Outputs) : Input;
}

public sealed record TickResult(
    RecordBatch SignalState,               // Arrow columnar: all signal values post-tick
    ProtocolEntry[] Protocol,              // append-only writes
    SideEffect[] Pending,                  // for Wolverine dispatch
    KernelEvt[] Events,                    // observable output
    bool Stable,
    int CascadeDepth,
    long TickNumber);

public sealed record ProtocolEntry(string Tag, string Code, string Content, double? Confidence);

public abstract record SideEffect
{
    public sealed record LlmGate(int GateId, string Prompt, string Model,
        string? ParseMap, string? Fallback, int TimeoutMs, bool Cache) : SideEffect;
    public sealed record ToolInvoke(string ToolCode, string Invoke,
        string[] InputCodes, string[] OutputCodes,
        int TimeoutMs, int RetryCount, string? Fallback) : SideEffect;
}

public abstract record KernelEvt
{
    public sealed record SignalChange(string Code, double Old, double New, double Conf) : KernelEvt;
    public sealed record GateFire(int Id, string Type) : KernelEvt;
    public sealed record GateBlock(int Id, string Type) : KernelEvt;
    public sealed record CascadeStep(int Depth) : KernelEvt;
    public sealed record ConstraintSolved(string Expr) : KernelEvt;
    public sealed record ConstraintViolated(string Expr) : KernelEvt;
    public sealed record FailActive(string Type, string Code) : KernelEvt;
    public sealed record FailResolved(string Type, string Code) : KernelEvt;
    public sealed record EvalStable(long Tick) : KernelEvt;
}

// ──────────────────────── ARROW SIGNAL STATE ────────────────────────

/// <summary>
/// Arrow-backed columnar signal state. Each field is a contiguous double[] array.
/// Decay and Propagate phases operate on these arrays with vectorized access patterns.
/// </summary>
public sealed class SignalColumns
{
    public string[] Codes { get; }           // signal code index (row → code)
    public double[] Values { get; }          // current values — THE hot array
    public double[] Baselines { get; }
    public double[] Confidences { get; }
    public double[] TauMinMs { get; }
    public double[] RangeLow { get; }
    public double[] RangeHigh { get; }
    public Dictionary<string, int> Index { get; }  // code → row index

    public int Count => Codes.Length;

    public SignalColumns(SignalRow[] rows)
    {
        var n = rows.Length;
        Codes = new string[n];
        Values = new double[n];
        Baselines = new double[n];
        Confidences = new double[n];
        TauMinMs = new double[n];
        RangeLow = new double[n];
        RangeHigh = new double[n];
        Index = new(n);

        for (int i = 0; i < n; i++)
        {
            Codes[i] = rows[i].Code;
            Values[i] = rows[i].Value;
            Baselines[i] = rows[i].Baseline;
            Confidences[i] = rows[i].Confidence;
            TauMinMs[i] = rows[i].TauMinMs;
            RangeLow[i] = rows[i].RangeLow;
            RangeHigh[i] = rows[i].RangeHigh;
            Index[rows[i].Code] = i;
        }
    }

    /// <summary>Export as Arrow RecordBatch for zero-copy streaming.</summary>
    public RecordBatch ToRecordBatch()
    {
        var schema = new Schema.Builder()
            .Field(new Field("code", StringType.Default, false))
            .Field(new Field("value", DoubleType.Default, false))
            .Field(new Field("baseline", DoubleType.Default, false))
            .Field(new Field("confidence", DoubleType.Default, false))
            .Build();

        var codeBuilder = new StringArray.Builder();
        var valueBuilder = new DoubleArray.Builder();
        var baselineBuilder = new DoubleArray.Builder();
        var confBuilder = new DoubleArray.Builder();

        for (int i = 0; i < Count; i++)
        {
            codeBuilder.Append(Codes[i]);
            valueBuilder.Append(Values[i]);
            baselineBuilder.Append(Baselines[i]);
            confBuilder.Append(Confidences[i]);
        }

        return new RecordBatch(schema, [codeBuilder.Build(), valueBuilder.Build(),
            baselineBuilder.Build(), confBuilder.Build()], Count);
    }
}

// ──────────────────────── TICK CONTEXT ────────────────────────

/// <summary>
/// Mutable context threaded through all 9 phases.
/// Phases read and mutate this directly — no immutable copying on the hot path.
/// </summary>
public sealed class TickCtx
{
    public required SignalColumns Signals { get; init; }
    public required EdgeRow[] Edges { get; init; }
    public required GateRow[] Gates { get; init; }
    public required int[][] TopoLevels { get; init; }  // pre-computed from Compiler
    public long TickNumber { get; set; }
    public int CascadeDepth { get; set; }
    public bool Stable { get; set; }
    public double TickIntervalMs { get; init; } = 100;

    // Output accumulators
    public List<KernelEvt> Events { get; } = [];
    public List<SideEffect> Pending { get; } = [];
    public List<ProtocolEntry> Protocol { get; } = [];

    // Resolved side effects from last tick (injected by Agent.cs)
    public List<Input> ResolvedInputs { get; } = [];

    // Adaptation: per-edge gain modulation (starts at 1.0, decays with activation)
    public double[]? EdgeGainMod { get; set; }

    // LLM-defined formulas evaluated each tick
    public List<FormulaRule> Formulas { get; } = [];

    // Formula outputs: computed signal values (virtual signals, not in main array)
    public Dictionary<string, double> FormulaOutputs { get; } = new(StringComparer.OrdinalIgnoreCase);
}
