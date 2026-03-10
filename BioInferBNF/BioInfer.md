# BioInfer — Signal Cascade Inference Framework

## The Model

Four lines. Everything else is instantiation.

```
BASE:         R0→R1→R2→R3→EMIT                        (bottom-up runtime)
PLASTICITY:   Δ@R0→Δ@R1→Δ@R2→Δ@R3                    (between ticks)
META:         M3→M2→M1→M0                              (top-down projection)
CONVERGENCE:  scalar(t) = f(v_past, v_current, v_meta)  (diamond closure)
```

These four pipelines are domain-agnostic. They don't care whether the signals
are neurochemical, financial, social, or mechanical. The domain-specific content
is the nodes and edges. The computational architecture is these four pipelines.

---

## The Diamond

```
                 @M3  projected architecture
                ╱    ╲
             @M2      @M1
                ╲    ╱
                 @M0  baselines ─────────── v_meta
                  │                            │
                  │              ┌──────────────┤
                  │              │              │
                 @R0  scalar(t) = f(v_past, v_current, v_meta)
                  │
                 @R1  neurons ───────────── v_current
                ╱    ╲
             @R2      @R3
                ╲    ╱
                EMIT
                  │
            ┌─────┘
            │  Δ@R0→Δ@R1→Δ@R2→Δ@R3
            │        │
            │   append-only ──────── v_past
            └──→ next tick @R0
```

The bottom half goes up — activity builds complexity.
The top half comes down — the program unfolds into specifics.
They meet at the scalar/meta-scalar interface.

---

## The Algebra

| Rank      | What it is | What it adds              | Operator               | Biology                            |
| --------- | ---------- | ------------------------- | ---------------------- | ---------------------------------- |
| R0 Scalar | Signal     | Magnitude                 | edges (→ ⊣ ⇌ ⊃ ⊂) | Neurotransmitter concentration     |
| R1 Vector | Structure  | Direction + length        | ∫ (integration)       | Neuron: many inputs → one output  |
| R2 Matrix | Protocol   | Polarity + pairwise rules | ⊲ (protocol)          | Synaptic gain, gating, tau         |
| R3 Tensor | Context    | Multi-way conditionals    | ⊗ (cross-connective)  | Dendritic computation, coincidence |

| Rank           | What it is   | What it adds       | Operator                      | Biology                                |
| -------------- | ------------ | ------------------ | ----------------------------- | -------------------------------------- |
| M0 Meta-scalar | Setpoint     | Target baseline    | σ̃ (projected baseline)     | Tonic neurotransmitter levels          |
| M1 Meta-vector | Remodeling   | Structural program | ∫̃ (projected structure)    | Myelination, neurogenesis              |
| M2 Meta-matrix | Program      | Protocol schedule  | ⊲̃ (projected protocol)     | Epigenetic marks, methylation          |
| M3 Meta-tensor | Architecture | Connectivity plan  | ⊗̃ (projected connectivity) | Critical periods, developmental wiring |

| Rank  | Plasticity                  | What changes                                 | Timescale |
| ----- | --------------------------- | -------------------------------------------- | --------- |
| Δ@R0 | Scalar self-adaptation      | Release, reuptake, baseline, synthesis       | ms → wk  |
| Δ@R1 | Structural plasticity       | Spines, dendrites, axons, cell state         | h → wk   |
| Δ@R2 | Protocol plasticity         | Gain, gate, tau, density, coupling           | min → mo |
| Δ@R3 | Cross-connective plasticity | Associations, extinctions, conditional logic | h → yr   |

---

## Progressive Inference

The four pipelines are independently valid. They layer on as data permits.
Each stage adds predictive power. None are prerequisites except as noted.

```
┌─────────────┬──────────────────┬──────────────────────────────────┐
│ Stage       │ Requires         │ Produces                         │
├─────────────┼──────────────────┼──────────────────────────────────┤
│ 1. BASE     │ one observation  │ diagnostic snapshot              │
│             │                  │ "what is happening now"           │
├─────────────┼──────────────────┼──────────────────────────────────┤
│ 2. PLAST.   │ two+ snapshots   │ change map with timescales       │
│             │ (BASE × 2)       │ "what is changing and how fast"  │
├─────────────┼──────────────────┼──────────────────────────────────┤
│ 3. META     │ Δ trends over    │ program map with setpoints       │
│             │ sustained period │ "where is it headed and why"     │
├─────────────┼──────────────────┼──────────────────────────────────┤
│ 4. CONV.    │ BASE + Δ + META  │ prediction engine                │
│             │ all populated    │ "what will happen if we do X"    │
└─────────────┴──────────────────┴──────────────────────────────────┘
```

### Clinical mapping:

```
Stage 1 = First appointment    → symptom map
Stage 2 = Follow-ups           → treatment tracking
Stage 3 = Long-term care       → prognosis, treatment resistance analysis
Stage 4 = Full model           → intervention simulation, trajectory prediction
```

---

## The Scalar is Never Simple

The deepest insight of the framework. What we observe as a single number
(47nM of dopamine) is a convergence projection — the lossy collapse of a
high-dimensional intersection:

```
scalar(t) = f(
    v_past:    trajectory history (append-only temporal store)
    v_current: live structural integration (@R1 output)
    v_meta:    programmed setpoint (@M0 baseline)
)
```

Two systems with identical scalar values can have completely different
underlying states:

* One arriving from above (depletion trajectory)
* One arriving from below (recovery trajectory)
* One being held there by epigenetic setpoint
* One being forced there by acute perturbation against its setpoint

Same number. Radically different systems. The convergence equation is what
distinguishes them.

---

## Execution Cycle

```
Phase 0 — META PROJECTION (once per developmental step)
    M3 → M2 → M1 → M0
    Top-down: architecture → protocols → structure → baselines

Phase 1 — BASE RUNTIME (every tick)
    R0: RESOLVE scalars via convergence equation
    R1: INTEGRATE structural units
    R2: APPLY pairwise protocols
    R3: EVALUATE cross-connective conditionals
    EMIT → feedback to R0

Phase 2 — PLASTICITY (between ticks)
    Δ@R0 → Δ@R1 → Δ@R2 → Δ@R3
    Each level triggered by level below
    All changes deferred, applied between ticks

Phase 3 — FEEDBACK ARROWS
    Upward:   Δ@R3 patterns → can write new @M3 entries
    Downward: @M0 setpoints → pull @R0 via convergence
    Lateral:  Δ@Rn → may trigger Δ@R(n+1)
```

---

## Compiler Acceptance

The compiler accepts partial programs. Each stage is independently parseable:

```
// Stage 1: BASE only (valid)
@domain:chem,struct
@R0
...
@R1
...
@R2
...
@R3
...

// Stage 2: BASE + PLASTICITY (valid)
@domain:chem,struct
@R0 ... @R1 ... @R2 ... @R3 ...
@Δ
Δ@R0: ...
Δ@R1: ...
Δ@R2: ...
Δ@R3: ...

// Stage 3: BASE + PLASTICITY + META (valid)
@domain:chem,struct,epi
@R0 ... @R1 ... @R2 ... @R3 ...
@Δ ...
@M3 ... @M2 ... @M1 ... @M0 ...

// Stage 4: Full diamond (valid)
// All of the above + convergence diagnostics
∮ ... ⊳ ... ⚡allo ... ⚡resist ...
```

---

## Diagnostics

| Symbol            | Name                    | Fires when                          | Stage required |
| ----------------- | ----------------------- | ----------------------------------- | -------------- |
| Σ∇·            | Conservation            | source/sink imbalance               | BASE           |
| ◈                | Composite               | behavioral cluster identified       | BASE           |
| ⚡dep/exc/sus/... | Dysregulation           | pathological cascade pattern        | BASE           |
| ∮                | Convergence state       | three vectors computed for signal   | CONVERGENCE    |
| ⊳                | Trajectory prediction   | forward projection computed         | CONVERGENCE    |
| ⚡allo            | Allostatic load         | σ̃ setpoint drifted from default  | CONVERGENCE    |
| ⚡resist          | Treatment resistance    | Δ@R0 opposed by σ̃               | CONVERGENCE    |
| ⚡diverge         | Trajectory divergence   | v_past trend ≠ v_meta direction    | CONVERGENCE    |
| ⚡unstable        | Convergence instability | three vectors disagree              | CONVERGENCE    |
| ⚡lock            | Epigenetic lock         | @M2 has methylation-locked protocol | META           |

---

## Files

| File                            | Pipeline    | Content                                                                 |
| ------------------------------- | ----------- | ----------------------------------------------------------------------- |
| `pipeline-doc/base.md`        | BASE        | R0→R1→R2→R3→EMIT. Operators: ∫ ⊲ ⊗. Single snapshot analysis.    |
| `pipeline-doc/plasticity.md`  | PLASTICITY  | Δ@R0→Δ@R1→Δ@R2→Δ@R3. Cross-cutting adaptation at every rank.     |
| `pipeline-doc/meta.md`        | META        | M3→M2→M1→M0. Epigenetic/developmental top-down programs.             |
| `pipeline-doc/convergence.md` | CONVERGENCE | scalar(t) = f(v_past, v_current, v_meta). Diamond closure + prediction. |

Each file is self-contained with its own operators, rules, examples, and
limitations. They compose progressively: BASE → +PLASTICITY → +META → +CONVERGENCE.

---

## Origin

Derived from the neuro-algebra pyramid:

* Scalars are the fuel (signal)
* Vectors are the trajectory (neurons)
* Matrices are the landscape (protocols)
* Tensors are the context (cross-connectivity)
* Plasticity is the landscape changing as the river flows
* Meta is the landscape's blueprint unfolding
* Convergence is where the river, the erosion, and the blueprint meet

The base stack goes up — activity builds complexity.
The meta stack comes down — the program unfolds into specifics.
Plasticity is the bridge between them.
And every observable scalar is the shadow cast by their intersection.
