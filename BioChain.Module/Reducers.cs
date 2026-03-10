using SpacetimeDB;

namespace BioChain.Module;

public static partial class Reducers
{
    // ── Program lifecycle ────────────────────────────────────────────────────

    [SpacetimeDB.Reducer]
    public static void CreateProgram(ReducerContext ctx, string subjectId, string label, string domains)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        ctx.Db.program.Insert(new Program
        {
            SubjectId = subjectId,
            Label = label,
            Domains = domains,
            Stage = 1,
            CreatedAt = now,
            UpdatedAt = now,
        });
    }

    [SpacetimeDB.Reducer]
    public static void SetProgramStage(ReducerContext ctx, uint programId, byte stage)
    {
        var p = ctx.Db.program.Id.Find(programId)
            ?? throw new Exception($"Program {programId} not found");
        p.Stage = stage;
        p.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        ctx.Db.program.Id.Update(p);
    }

    // ── Node CRUD ────────────────────────────────────────────────────────────

    [SpacetimeDB.Reducer]
    public static uint AddNode(ReducerContext ctx,
        uint programId, byte rank, byte domain,
        string typeSub, string code, byte state, float value, float delta,
        string region, string props, string fieldOps,
        bool isRoot, bool isTerminal)
    {
        var node = ctx.Db.node.Insert(new Node
        {
            ProgramId = programId,
            Rank = (Rank)rank,
            Domain = (NodeDomain)domain,
            TypeSub = typeSub,
            Code = code,
            State = (SignalState)state,
            Value = value,
            Delta = delta,
            Region = region,
            Props = props,
            FieldOps = fieldOps,
            IsRoot = isRoot,
            IsTerminal = isTerminal,
        });
        return node.Id;
    }

    [SpacetimeDB.Reducer]
    public static void UpdateNodeState(ReducerContext ctx, uint nodeId, byte state, float value)
    {
        var n = ctx.Db.node.Id.Find(nodeId)
            ?? throw new Exception($"Node {nodeId} not found");
        n.State = (SignalState)state;
        n.Value = value;
        ctx.Db.node.Id.Update(n);
    }

    // ── Edge CRUD ────────────────────────────────────────────────────────────

    [SpacetimeDB.Reducer]
    public static uint AddEdge(ReducerContext ctx,
        uint programId, uint sourceId, uint targetId,
        byte op, byte rank, string gateCondition, string label)
    {
        var edge = ctx.Db.edge.Insert(new Edge
        {
            ProgramId = programId,
            SourceId = sourceId,
            TargetId = targetId,
            Op = (EdgeOp)op,
            Rank = (Rank)rank,
            GateCondition = gateCondition,
            Label = label,
        });
        return edge.Id;
    }

    // ── Integration (R1) ─────────────────────────────────────────────────────

    [SpacetimeDB.Reducer]
    public static void AddIntegration(ReducerContext ctx,
        uint programId, uint unitNodeId,
        string inputs, string output, byte activation, string activationParam)
    {
        ctx.Db.integration.Insert(new Integration
        {
            ProgramId = programId,
            UnitNodeId = unitNodeId,
            Inputs = inputs,
            Output = output,
            Activation = (ActivationMode)activation,
            ActivationParam = activationParam,
        });
    }

    // ── Protocol (R2) ────────────────────────────────────────────────────────

    [SpacetimeDB.Reducer]
    public static void AddProtocol(ReducerContext ctx,
        uint programId, uint sourceId, uint targetEdgeId,
        float gain, byte pol, string tau, string gate, byte coupling, float pr)
    {
        ctx.Db.protocol.Insert(new Protocol
        {
            ProgramId = programId,
            SourceId = sourceId,
            TargetEdgeId = targetEdgeId,
            Gain = gain,
            Pol = (Polarity)pol,
            Tau = tau,
            Gate = gate,
            Coupling = (CouplingType)coupling,
            Pr = pr,
        });
    }

    // ── Tensor (R3) ──────────────────────────────────────────────────────────

    [SpacetimeDB.Reducer]
    public static void AddTensor(ReducerContext ctx,
        uint programId, string conditions, string logic,
        string effect, string effectTarget, string effectAction)
    {
        ctx.Db.tensor.Insert(new Tensor
        {
            ProgramId = programId,
            Conditions = conditions,
            Logic = logic,
            Effect = effect,
            EffectTarget = effectTarget,
            EffectAction = effectAction,
        });
    }

    // ── Diagnostics ──────────────────────────────────────────────────────────

    [SpacetimeDB.Reducer]
    public static void AddDiag(ReducerContext ctx,
        uint programId, byte kind, string code, string body)
    {
        ctx.Db.diag.Insert(new Diag
        {
            ProgramId = programId,
            Kind = (DiagKind)kind,
            Code = code,
            Body = body,
        });
    }

    // ── DeltaOp (plasticity) ─────────────────────────────────────────────────

    [SpacetimeDB.Reducer]
    public static void AddDeltaOp(ReducerContext ctx,
        uint programId, byte rank,
        string target, string rule, string timescale, string trigger)
    {
        ctx.Db.delta_op.Insert(new DeltaOp
        {
            ProgramId = programId,
            Rank = (Rank)rank,
            Target = target,
            Rule = rule,
            Timescale = timescale,
            Trigger = trigger,
        });
    }

    // ── DeltaLog (append-only v_past) ────────────────────────────────────────

    [SpacetimeDB.Reducer]
    public static void AppendDeltaLog(ReducerContext ctx,
        uint programId, uint nodeId, uint tick, float value)
    {
        ctx.Db.delta_log.Insert(new DeltaLog
        {
            ProgramId = programId,
            NodeId = nodeId,
            Tick = tick,
            Value = value,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        });
    }

    // ── MetaOp (M0..M3) ─────────────────────────────────────────────────────

    [SpacetimeDB.Reducer]
    public static void AddMetaOp(ReducerContext ctx,
        uint programId, byte rank,
        string target, string op, string spec, string window)
    {
        ctx.Db.meta_op.Insert(new MetaOp
        {
            ProgramId = programId,
            Rank = (MetaRank)rank,
            Target = target,
            Operator = op,
            Spec = spec,
            Window = window,
        });
    }

    // ── Convergence ──────────────────────────────────────────────────────────

    [SpacetimeDB.Reducer]
    public static void AddConv(ReducerContext ctx,
        uint programId, byte kind, string signal,
        string vPast, string vCurrent, string vMeta,
        byte diagnosis, string prediction, string body)
    {
        ctx.Db.conv.Insert(new Conv
        {
            ProgramId = programId,
            Kind = (ConvFlagKind)kind,
            Signal = signal,
            VPast = vPast,
            VCurrent = vCurrent,
            VMeta = vMeta,
            Diagnosis = (ConvergenceDiag)diagnosis,
            Prediction = prediction,
            Body = body,
        });
    }

    // ── Reconstruct BNF ──────────────────────────────────────────────────────

    [SpacetimeDB.Reducer]
    public static string Reconstruct(ReducerContext ctx, uint programId)
    {
        // TODO: walk tables for programId, emit BNF text
        // Nodes → @R0 chains, Integrations → @R1, Protocols → @R2, Tensors → @R3
        // DeltaOps → @Δ, MetaOps → @M3..@M0, Convs → ∮/⊳/⚡
        throw new NotImplementedException("Reconstruct not yet implemented");
    }

    // ── Engine Tick ──────────────────────────────────────────────────────────

    [SpacetimeDB.Reducer]
    public static void EngineTick(ReducerContext ctx, uint programId)
    {
        // Phase 1: BASE RUNTIME — resolve scalars, integrate, apply protocols, evaluate tensors
        // Phase 2: PLASTICITY — check Δ triggers, apply deferred changes
        // Phase 3: FEEDBACK — upward Δ@R3→M3, downward M0→R0, lateral Δ@Rn→Δ@R(n+1)
        // Phase 4: CONVERGENCE — compute ∮ for all roots, update ⊳ predictions
        // Each phase reads/writes tables directly via ctx.Db
        throw new NotImplementedException("EngineTick not yet implemented");
    }
}
