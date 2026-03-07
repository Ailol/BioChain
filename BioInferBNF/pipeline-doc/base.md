# BioChain v6 — BASE Pipeline

## Model: R0→R1→R2→R3→EMIT

The runtime. Bottom-up, activity-driven. Single snapshot analysis.
Exists from first observation. No history required.

```
┌─────────────────────────────────────────────────┐
│              BASE RUNTIME PIPELINE               │
│                                                  │
│  @R0 SCALAR  →  signal values (raw quantities)   │
│       ↓                                          │
│  @R1 VECTOR  →  structural integration           │
│       ↓                                          │
│  @R2 MATRIX  →  pairwise protocol application    │
│       ↓                                          │
│  @R3 TENSOR  →  cross-connective conditioning    │
│       ↓                                          │
│     EMIT     →  scalar outputs feed back to R0   │
│                                                  │
│  Each rank operates ON the output of rank below. │
│  Single pass. One tick. Snapshot.                 │
└─────────────────────────────────────────────────┘
```

---

## When to use

* First observation of a system
* Symptom mapping from a description
* "What is happening right now?"
* No prior data exists
* Quick diagnostic snapshot

---

## What it produces

A complete signal cascade map at a single moment in time:

* Which signals are active and at what levels
* Which structural units are integrating what
* Which protocols govern the connections
* Which cross-connective conditions are met or blocked

---

## Domains

```
@domain: declares which node types are available.
All domains interleave freely within each rank.

chem:   L.nt L.h L.p L.cb L.ni L.ns R Gp 2m K Ph NR TF G T E V
elec:   E.v E.lf E.gj Ch Ch.vg Ch.mec Ch.trp
meta:   M.atp M.glc M.ros M.o2 Mt
epi:    Cr Ep.me Ep.ac Ep.mi
struct: N.pyr N.da N.5ht N.gaba N.gran N.glia N.glia.mg N.glia.as
```

---

## @R0 — SCALAR: Signal Values

The base layer. Concentrations, voltages, metabolite levels.
In BASE-only mode, scalars are measured/inferred values (not convergence projections — that requires META).

### Node

```
{TYPE.SUB:CODE[STATE]@REGION FIELD_OPS}

STATE: [↑↑|↑|≈|↓|↓↓|~|⊘|●] optional numeric: [↑:0.8] optional delta: [↑:0.8 Δ+0.3]
PROPS: (key:val,key:val)
LOC:   @REGION
```

### Edges

```
→     activates/produces        ⊣     inhibits/suppresses
⇌     bidirectional             ⊃     amplifies (gain>1)
⊂     attenuates (gain<1)       ~>    modulates (indirect)
=>    transcribes/expresses      |>    transports/clears
→!    strong activate            ⊣!    strong inhibit
←     reverse direction
```

### Structures

```
GATED:    {A}→?{COND>=STATE}{B}
BRANCH:   {A}(→{B} ⊣{C})
MERGE:    {A}&{B}→{C}
RING:     {X ∇×1⁺}«1⁺→...→{Y}»1
BIND:     {R:X(coup:Y)@R}?{L.x:Z}→{Gp:Y}→{2m:...}
ROOT:     ⊙{NODE} (must have Δ≠0)
TERMINAL: →⊘ (metabolized)
```

### Mandatory cascades

```
Intracellular: L.x→R→Gp→2m→K (never skip between extracellular signals)
Steroid path:  L.h→NR→TF→G (bypasses Gp/2m)
Ionotropic:    R(coup:ion)→2m directly
```

### Field operators

```
∇→R         gradient toward R
∇·+  ∇·−   source / sink
∇×n⁺ ∇×n⁻  feedback ring n
∇²syn ∇²vol synaptic / volume transmission
-∇φ:X@R    potential-driven
```

---

## @R1 — VECTOR: Structural Units

The integrators. Neurons, glia, immune cells.
Receive multiple R0 scalar inputs → hold multidimensional state → emit scalar output.

### Operator: ∫

```
∫{UNIT:CODE@REGION}←( INPUT:WEIGHT, ... )→OUTPUT:ACTIVATION

Weight signs:
  +  excitatory (adds to sum)
  −  inhibitory (subtracts from sum)
  ×  modulatory (multiplies the integrated sum)

Activation modes:
  thr:VALUE  threshold — fires when sum exceeds value
  rate       continuous rate coding
  burst      burst firing mode
  tonic      steady background firing
```

### Rules

* ∫ sources must reference R0 signal nodes
* ∫ output feeds back into R0 as new scalar emission
* Every R0 signal SHOULD originate from an ∫ output or root ⊙

---

## @R2 — MATRIX: Pairwise Protocols

The rules between structural units. Polarity, gain, gating, time constants.
Defines HOW signals transfer, not WHAT they are.

### Operator: ⊲

```
{SOURCE}⊲{TARGET}[PROTOCOL_SPEC]

Protocol properties:
  gain    transfer gain           ×0.6, ×1.4
  pol     polarity                exc | inh | mod
  tau     time constant           fast:2ms | slow:500ms | tonic:∞
  gate    gating condition        {COND>=STATE} | open | closed
  coup    coupling type           syn | vol | gap | para
  pr      release probability     0.0–1.0
  rev     reversal potential      −70mV, 0mV
```

### Rules

* ⊲ source: signal node (R0) or structural unit (R1)
* ⊲ target: R0 edge or R1 integration input
* Multiple ⊲ same target: gains multiply, gates AND, taus follow slowest
* Evaluated AFTER @R1 integration

---

## @R3 — TENSOR: Cross-Connective Protocols

Multi-way interactions. Signal passage depends on context from MULTIPLE other connections simultaneously.

### Operator: ⊗

```
⊗( COND ∧ COND )⟹EFFECT          // AND — simultaneous
⊗( COND ∨ COND )⟹EFFECT          // OR — either
⊗( ¬COND )⟹EFFECT                 // NOT — absence

COND:   {NODE_REF}>=STATE
EFFECT: {NODE_REF}:pass | block | amplify:VAL | switch:TARGET
```

### Rules

* ⊗ conditions reference R0 states or R1 unit states
* ⊗ effects modify R0 edges, R1 outputs, or R2 protocols
* ∧ requires simultaneity within timing window (default: same tick)
* Evaluated AFTER R2, BEFORE final EMIT

---

## EMIT

Scalar outputs from R3-conditioned pipeline feed back into R0 for next tick.
In BASE-only mode (single snapshot), EMIT is the final output — no feedback loop.

---

## Post-sections

```
Σ∇·(CODE)=+n/−m    conservation summary
◈name=X@R+Y@R       behavioral composite (read-only terminal)
⚡type:{chain}       dysregulation flag
```

---

## Output order

```
@domain:chem,struct
#phase_name
Δ declarations

@R0   chains, branches, rings, clearance
@R1   ∫ integration declarations
@R2   ⊲ protocol declarations
@R3   ⊗ conditional declarations

Σ∇·   conservation
◈     composites
⚡    dysregs
```

---

## BASE-only rules

1. Every non-⊙ non-⊘ node: ≥1 incoming AND ≥1 outgoing edge in @R0
2. All @R0 chains connect — no orphans
3. Same {TYPE:CODE@REGION} across lines/ranks = same node
4. Intracellular cascade mandatory between extracellular signals
5. ∫ ONLY in @R1. ⊲ ONLY in @R2. ⊗ ONLY in @R3.
6. ⊲ target must reference existing R0 edge or R1 input
7. ⊗ conditions must reference existing R0/R1 states
8. L subclass MANDATORY — never bare L:
9. ∇²syn or ∇²vol on all ligand nodes
10. Δ on all ⊙ root nodes
11. No Δ block, no META block — those are separate pipeline layers
12. NO English prose. Only codes + operators.

---

## Limitations without other pipelines

* No temporal dynamics (no Δ = no change tracking)
* No prediction (no META = no trajectory)
* No convergence (scalar is measured, not projected)
* Snapshot only — useful but static
