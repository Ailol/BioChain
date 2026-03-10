# BioChain v6 — DELTA (Δ) Pipeline

## Model: Δ@R0→Δ@R1→Δ@R2→Δ@R3

The adaptation layer. Cross-cutting plasticity at every rank.
Exists from second observation (requires two BASE snapshots to compute difference).

```
┌─────────────────────────────────────────────────────────┐
│              DELTA PIPELINE (between ticks)               │
│                                                          │
│  Δ@R0  SCALAR PLASTICITY   signal self-adaptation        │
│       ↓ (scalar activity triggers vector change)         │
│  Δ@R1  VECTOR PLASTICITY   structural changes            │
│       ↓ (vector changes trigger matrix change)           │
│  Δ@R2  MATRIX PLASTICITY   protocol rewriting            │
│       ↓ (matrix patterns trigger tensor change)          │
│  Δ@R3  TENSOR PLASTICITY   cross-connective rewiring     │
│                                                          │
│  Each level's plasticity driven by the level below.      │
│  Evaluated BETWEEN ticks. Deferred. Never instantaneous. │
│  Requires τ (timescale) on every operation.              │
└─────────────────────────────────────────────────────────┘
```

---

## When to use

* Second or subsequent observation of a system
* Comparing two BASE snapshots taken at different times
* Treatment tracking ("what changed since intervention?")
* "What is changing and at what speed?"
* Requires: at least one prior BASE analysis

---

## What it produces

A change map across all four ranks:

* Which signals adapted their own properties (Δ@R0)
* Which structural units physically remodeled (Δ@R1)
* Which pairwise protocols rewrote (Δ@R2)
* Which cross-connective conditionals shifted (Δ@R3)
* Each with an observed or estimated timescale

---

## Prerequisites

The Δ pipeline is parasitic on BASE. It cannot exist alone.
Every Δ operator references entities that must exist in the BASE stack:

* Δ@R0 triggers reference R0 signal nodes
* Δ@R1 triggers reference R0/R1 entities
* Δ@R2 targets reference R2 ⊲ protocols
* Δ@R3 targets reference R3 ⊗ conditionals

---

## Operator: Δ@R`<n>`

```
Δ@Rn: {TRIGGER} ≫ {TARGET(property:before→after)} [τ:TIMESCALE]

TRIGGER:   a node reference with state condition (from BASE)
TARGET:    the entity being changed, with property transition
τ:         mandatory timescale — how long the trigger must persist
           before the change fires
```

### Timescale units

```
ms   milliseconds
s    seconds
min  minutes
h    hours
d    days
wk   weeks
mo   months
yr   years
```

---

## Δ@R0 — Scalar Self-Adaptation

The signal changes its own signal properties.
The most primitive plasticity. A synapse that fired slightly less eagerly next time.

### What changes:

```
release     vesicle release rate         norm→depleted, norm→enhanced
baseline    tonic resting level          norm→low, norm→high, low→lower
synthesis   enzyme production rate       norm→up, norm→down
reuptake    transporter density/speed    norm→down (more signal lingers)
                                         norm→up (faster clearance)
```

### Characteristic timescale: milliseconds to weeks

### Examples:

```
// Vesicle depletion: high-frequency firing exhausts release machinery
Δ@R0: {L.nt:DA[↑↑]@NAc} ≫ {V:DA_ves(release:norm→depleted)@NAc} [τ:200ms]

// Tonic baseline drift: sustained low serotonin lowers resting level
Δ@R0: {L.nt:5HT[↓]@DRN} ≫ {L.nt:5HT(baseline:norm→low)@DRN} [τ:2wk]

// Compensatory synthesis: depletion triggers enzyme upregulation
Δ@R0: {L.nt:DA[↓↓]@VTA} ≫ {E:TH(activity:norm→up)@VTA} [τ:6h]

// Reuptake adaptation: SSRI exposure reduces transporter density
Δ@R0: {L.nt:5HT[↑]@DRN} ≫ {T:SERT(dens:norm→down)@DRN} [τ:3wk]

// Hormonal self-regulation: sustained cortisol reduces CRH sensitivity
Δ@R0: {L.h:CORT[↑↑]@ADR} ≫ {L.h:CRH(release:norm→reduced)@PVN} [τ:48h]
```

### Key principle:

This is the level pharmacology primarily operates on. SSRIs, SNRIs, dopamine agonists —
they all tweak scalar properties. Fast to engage, but the meta-setpoint often drags
the system back (which is why SSRIs take weeks — you need Δ@R0 to cascade upward
into Δ@R2 and eventually @M2 before lasting change occurs).

---

## Δ@R1 — Vector Structural Plasticity

The structural unit changes its own physical architecture.
The container changes shape.

### What changes:

```
spines      dendritic spine density      norm→increased, norm→reduced, weak→pruned
dendrite    dendritic tree extent        norm→retracted, norm→expanded
axon        axonal projection            norm→sprouted, norm→retracted
myelin      myelination state            incomplete→complete, norm→degraded
state       cell functional state        surveilling→activated (microglia)
                                         resting→reactive (astrocyte)
volume      overall cell volume          norm→reduced, norm→hypertrophied
```

### Characteristic timescale: hours to weeks

### Examples:

```
// Spine growth from sustained kinase activity
Δ@R1: {K:CaMKII[↑]@HPC} ≫ {N.pyr:HPC_PYR(spines:norm→increased)@HPC} [τ:24h]

// Stress-induced dendritic retraction in PFC
Δ@R1: {L.h:CORT[↑↑]@ADR} ≫ {N.pyr:PFC_PYR(dendrite:norm→retracted)@PFC} [τ:2wk]

// BDNF-driven axonal sprouting
Δ@R1: {L.p:BDNF[↑↑]@HPC} ≫ {N.pyr:HPC_PYR(axon:norm→sprouted)@HPC} [τ:7d]

// Microglial activation from neuroinflammation
Δ@R1: {L.ni:TNFα[↑]@CNS} ≫ {N.glia.mg:MG(state:surveilling→activated)@HPC} [τ:48h]

// Exercise-induced neurogenesis
Δ@R1: {L.p:BDNF[↑↑]@HPC} ≫ {N.gran:DG(neurogenesis:low→increased)@HPC} [τ:2wk]
```

### Key principle:

Vector plasticity is why "use it or lose it" is literal. Active neurons grow spines,
extend dendrites, strengthen their structural presence. Silent neurons get pruned.
The structural unit's physical shape IS its computational capability — more spines
means more integration inputs, which means a richer vector.

---

## Δ@R2 — Matrix Protocol Plasticity

The pairwise rules rewrite themselves based on activity patterns.
LTP, LTD, receptor sensitization/desensitization, gain shifts, tau adaptation.

### What changes:

```
gain        transfer gain                ×1.0→×1.5 (LTP), ×1.0→×0.6 (LTD)
gate        gating state                 open→desens, open→closed, closed→open
tau         time constant                fast:5ms→slow:50ms
pr          release probability          0.8→0.3
dens        receptor density             norm→up, norm→down
st          receptor state               act→des, des→supersens, surf→intern
coup        coupling efficiency          Gs→Gs.weak
```

### Characteristic timescale: minutes to months

### Examples:

```
// LTP: CaMKII-driven gain increase
Δ@R2: {K:CaMKII[↑]@HPC} ≫ {⊲:GLU→AMPA@HPC(gain:×1.0→×1.5)} [τ:30min]

// LTD: phosphatase-driven gain decrease
Δ@R2: {Ph:PP1[↑]@HPC} ≫ {⊲:GLU→AMPA@HPC(gain:×1.0→×0.6)} [τ:30min]

// Receptor desensitization from sustained agonist
Δ@R2: {L.nt:DA[↑↑]@NAc} ≫ {⊲:DA→D2@NAc(gate:open→desens)} [τ:1h]

// Receptor upregulation from chronic depletion
Δ@R2: {L.nt:DA[↓↓]@NAc} ≫ {⊲:DA→D2@NAc(gain:×1.0→×1.8)} [τ:2wk]

// Stress slows protocol temporal dynamics
Δ@R2: {L.h:CORT[↑↑]@ADR} ≫ {⊲:GLU→NMDA@HPC(tau:fast:5ms→slow:50ms)} [τ:1wk]

// BDNF loss weakens trophic support protocol
Δ@R2: {L.p:BDNF[↓]@HPC} ≫ {⊲:BDNF→TrkB@HPC(gain:×1.0→×0.4)} [τ:2wk]
```

### Key principle:

This is where learning lives. Every memory, every skill, every conditioned response
is a Δ@R2 operation that changed the gain, gate, or timing of a pairwise protocol.
Therapy works here too — cognitive reappraisal is systematically rewriting protocol
gains in PFC→AMY pathways.

---

## Δ@R3 — Tensor Cross-Connective Plasticity

The multi-way conditional logic rewires. New associations form, old ones extinguish,
the conditions under which context matters change.

### What changes:

```
New ⊗ conditionals created (association formation)
Existing ⊗ effects strengthened or weakened
Conditions added or dropped from existing ⊗
Entire ⊗ rules dissolved (extinction)
```

### Characteristic timescale: hours to years

### Examples:

```
// Fear conditioning: tone + context creates new conditional
Δ@R3: ⊗({tone@AMY}>=↑ ∧ {context@HPC}>=↑)
     ≫ ⊗({tone@AMY}>=↑ ∧ {context@HPC}>=↑)⟹{NE→AMY.fear}:amplify:2.0 [τ:1h]

// Extinction: repeated tone without shock weakens conditional
Δ@R3: ⊗({tone@AMY}>=↑ ∧ ¬{pain@AMY}>=↑)
     ≫ ⊗({tone@AMY}>=↑)⟹{NE→AMY.fear}:amplify:2.0→1.0 [τ:5d]

// Stress simplifies three-factor rule by dropping a condition
Δ@R3: {L.h:CORT[↑↑]@ADR}
     ≫ ⊗({CaMKII@NAc}>=↑ ∧ {DA@NAc}>=↑)⟹modify:drop_condition({DA@NAc}) [τ:1mo]

// Chronic stress hardens plasticity block
Δ@R3: ⊗({CORT@ADR}>=↑↑ ∧ {BDNF@HPC}>=↓)
     ≫ ⊗({CORT@ADR}>=↑↑ ∧ {BDNF@HPC}>=↓)⟹{AMPA@HPC}:block→permanent_block [τ:3mo]
```

### Key principle:

This is where personality and disposition live. Your characteristic responses to
complex situations — the multi-factor conditionals that determine how you react
when multiple things are happening simultaneously — are Δ@R3 products accumulated
over a lifetime. Trauma is a Δ@R3 that wrote a very strong conditional with a
very low threshold that resists extinction.

---

## Cascading triggers (the upward arrow)

The defining feature of the Δ pipeline: each level's changes can trigger
plasticity at the level above.

```
Δ@R0 (scalar adapts)
  ↓ sustained scalar change triggers...
Δ@R1 (structural remodeling)
  ↓ structural changes trigger...
Δ@R2 (protocol rewriting)
  ↓ protocol patterns trigger...
Δ@R3 (cross-connective rewiring)
```

Example cascade:

```
1. Δ@R0: SSRI blocks SERT → 5HT lingers longer [τ:hours]
2. Δ@R0: sustained 5HT elevation → autoreceptor exposure [τ:days]
3. Δ@R2: 5HT1A desensitization → autoreceptor gate closes [τ:1-2wk]
4. Δ@R1: increased 5HT tone → BDNF expression → spine growth [τ:2-4wk]
5. Δ@R2: new spines create new protocol entries [τ:4-6wk]
6. Δ@R3: restored plasticity enables new cross-connective rules [τ:6-8wk]
```

This is why SSRIs take 6-8 weeks. The scalar intervention (Δ@R0) must cascade
upward through every plasticity level before the cross-connective patterns
(Δ@R3) that constitute "feeling better" actually change.

---

## Δ output order

```
@Δ

Δ@R0: ...    // scalar self-adaptation
Δ@R0: ...

Δ@R1: ...    // vector structural plasticity
Δ@R1: ...

Δ@R2: ...    // matrix protocol plasticity
Δ@R2: ...

Δ@R3: ...    // tensor cross-connective plasticity
Δ@R3: ...
```

---

## Δ rules

1. Every Δ requires τ — no instantaneous plasticity
2. Δ@Rn triggers come primarily from R(n-1) activity
3. Δ is evaluated AFTER all base ranks, BETWEEN ticks
4. Δ targets must exist in the BASE stack
5. Multiple Δ on same target at same rank: last τ wins (engine warns)
6. Δ is deferred — graph mutations happen between ticks, not during
7. Δ without BASE is invalid — Δ pipeline requires BASE pipeline
8. NO English prose. Only codes + operators.

---

## Limitations without other pipelines

* No trajectory prediction (no META = no setpoints to compare against)
* No convergence (can't compute f(v_past, v_current, v_meta) without META)
* Change tracking only — "things are changing" but not "where is it headed"
* Useful for monitoring and treatment response, not prognosis
