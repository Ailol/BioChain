using Pgvector;

namespace NeuroGateway.Repository.Entities;

public class ObservationEntity
{
    public int Id { get; set; }
    public Guid PersonId { get; set; }
    public int PersonalityId { get; set; }
    public Guid? AnalysisRunId { get; set; }
    public int? AnalyzedDataId { get; set; }

    // ── LAYER 8 CANONICAL FIELDS ──

    // SUBJECT
    public int SignalId { get; set; }
    public int? SubjectReceptorId { get; set; }
    public string? SubjectState { get; set; }        // [↑],[↓],[↑↑],[↓↓],[~],[≈],[⊘],[◭],[◊],[●]
    public string? SubjectDoseRange { get; set; }    // low, mid, high, excess

    // OPERATOR
    public string? Operator { get; set; }            // →, ⊣, ⊃, ⊂, ⊩, ⇌, ∥, ⊗, ≫, ≂, ⊘→

    // TARGET
    public int? TargetSignalId { get; set; }
    public int? TargetReceptorId { get; set; }
    public string? TargetState { get; set; }

    // @REGION
    public int? RegionId { get; set; }

    // (temporal)
    public string? Temporal { get; set; }            // acute, chronic, tonic, phasic, etc.

    // {gate}
    public int? GateInstanceId { get; set; }
    public string? GateFormula { get; set; }

    // <stage>
    public string? LifecycleStage { get; set; }      // syn, sto, rel, bnd, trd, eff, trm, mod

    // #confidence
    public string? Confidence { get; set; }          // explicit(●), strong(◐), weak(○), absent(∅)

    // ~context
    public string? Context { get; set; }             // academic, casual, diary, clinical, professional, chat

    // ── ANALYSIS-SPECIFIC ──
    public string? FailureMode { get; set; }         // depletion, resistance, sensitization, etc.
    public float? Intensity { get; set; }
    public int? PathwayId { get; set; }
    public int? CircuitId { get; set; }

    // ── FREETEXT (AI output, vector-searchable) ──
    public string? SignalsText { get; set; }
    public string Formula { get; set; } = "";
    public string? StateText { get; set; }
    public string? CircuitsText { get; set; }
    public string? Notes { get; set; }

    // ── EXTENSION ──
    public string Metadata { get; set; } = "{}";

    // ── EMBEDDING ──
    public Vector? Embedding { get; set; }

    public DateTime CreatedAt { get; set; }
}
