# BioChain v6 — META Pipeline

## Model: M3→M2→M1→M0

The program. Top-down, developmental/epigenetic.
Exists from sustained observation — emerges when Δ values show consistent trajectories.

```
┌──────────────────────────────────────────────────────────────┐
│              META PIPELINE (developmental timescale)          │
│                                                              │
│  @M3 META-TENSOR   projected connectivity architecture       │
│       ↓ (architecture decomposes into protocol programs)     │
│  @M2 META-MATRIX   protocol change programs                  │
│       ↓ (protocol programs drive structural remodeling)      │
│  @M1 META-VECTOR   structural remodeling programs            │
│       ↓ (structural programs set baseline targets)           │
│  @M0 META-SCALAR   baseline setpoints / biome re-adaptation  │
│       ↓                                                      │
│       ╰──→ feeds into BASE @R0 via convergence equation      │
│                                                              │
│  Operates on developmental timescale (not every tick).       │
│  Program-driven, not activity-driven.                        │
│  The system's intention, not its reaction.                   │
└──────────────────────────────────────────────────────────────┘
```

---

## When to use

* Sustained observation reveals trending Δ values (weeks to months)
* Developmental context is known (age, life stage, epigenetic history)
* Chronic conditions where the system has been *reprogrammed*
* "Where is this system headed?"
* "Why does this system keep returning to this state despite intervention?"
* Requires: BASE + Δ history showing consistent patterns

---

## What it produces

A top-down program that explains:

* What the system's architecture is *supposed to* look like (@M3)
* Which protocol changes are scheduled to serve that architecture (@M2)
* Which structural remodeling programs are active (@M1)
* What "normal" means for each signal in this specific system (@M0)

---

## Prerequisites

META is inferred from Δ patterns over time. It cannot be directly observed —
it's the *explanation* for why Δ values trend the way they do.

The inference chain:

```
1. Multiple BASE snapshots over time
2. Δ values computed between snapshots
3. Δ values show consistent direction (not random fluctuation)
4. Consistent Δ direction implies an attractor → that attractor is @M0
5. @M0 setpoints imply structural targets → that's @M1
6. Structural targets imply protocol programs → that's @M2
7. Protocol programs imply architectural plan → that's @M3
```

---

## @M3 — META-TENSOR: Projected Connectivity Architecture

The grand plan. Which brain regions should connect to which, what the overall
topology is supposed to look like, critical period timing, aging trajectories.

This is genetics + accumulated epigenetic modifications determining the
*intended shape* of the network.

### Operator: ⊗̃

```
⊗̃[WINDOW]( ARCHITECTURAL_TARGET )

WINDOW:
  age range:    0yr–5yr, 12yr–25yr, 60yr–∞
  condition:    after:EVENT, after:EVENT:DURATION
  cumulative:   cumulative:EVENT_TYPE

ARCHITECTURAL_TARGET:
  {REGION→REGION}:conn:PROGRAM

PROGRAM:
  plastic          connection is modifiable
  strengthen       connection being reinforced
  refine           connection being pruned to essentials
  gradual_decline  connection slowly weakening
  dominance_shift  one pathway overtaking another
```

### Examples:

```
@M3
// Critical period: visual cortex cross-hemisphere plasticity window
⊗̃[0yr–5yr]( {V1.L→V1.R}:conn:plastic )

// Adolescent prefrontal integration
⊗̃[12yr–25yr]( {PFC→AMY}:conn:strengthen, {PFC→NAc}:conn:refine )

// Stress-induced architectural shift: amygdala dominates over PFC
⊗̃[after:CORT.chronic:1yr]( {AMY→PFC}:conn:dominance_shift )

// Aging: hippocampal-prefrontal decline
⊗̃[60yr–∞]( {HPC→PFC}:conn:gradual_decline )

// Trauma: fear circuit hyper-connectivity
⊗̃[after:trauma_event]( {AMY→PAG}:conn:strengthen, {AMY→HPA}:conn:strengthen )
```

### Key principle:

@M3 is the *reason* lower meta levels do what they do. If @M3 projects
"strengthen AMY→PFC," then @M2 will schedule the protocol changes needed to
make that happen, @M1 will schedule the structural growth, and @M0 will
adjust the baselines to support it. The architecture drives everything below.

---

## @M2 — META-MATRIX: Protocol Change Programs

Epigenetic programs that schedule which protocols change and when.
Methylation marks, histone modifications, miRNA regulation — all the machinery
that locks or unlocks plasticity rules on a developmental timeline.

### Operator: ⊲̃

```
⊲̃[WINDOW]( PROTOCOL_TARGET )

PROTOCOL_TARGET:
  {⊲:EDGE@REGION}[property:before→after]
```

### Examples:

```
@M2
// Puberty: GABA balance shifts in PFC
⊲̃[11yr–16yr]( {⊲:GABA→GABA-A@PFC}[gain:×0.8→×1.2] )

// Chronic stress writes methylation lock on hippocampal plasticity
⊲̃[after:CORT.chronic:3mo]( {⊲:GLU→NMDA@HPC}[gate:open→methylation_locked] )

// SSRI epigenetic unlocking (why SSRIs take weeks to work fully)
⊲̃[after:5HT.sustained_elevation:3wk]( {⊲:BDNF→TrkB@HPC}[gate:locked→open] )

// Aging: progressive loss of DA protocol sensitivity
⊲̃[50yr–∞]( {⊲:DA→D1@PFC}[gain:×1.0→×0.7] )

// Early life adversity: locks stress protocols into high-gain mode
⊲̃[after:early_adversity]( {⊲:CRH→CRH-R1@PIT}[gain:×1.0→×1.5, gate:open→epigenetic_fixed] )
```

### Key principle:

@M2 is where chronic conditions become  *locked in* . A protocol change from Δ@R2
is reversible — remove the trigger and it can revert. But when Δ@R2 persists
long enough, it gets written into @M2 as an epigenetic mark. Now it persists
even after the trigger resolves. This is the mechanism of treatment resistance —
the disease isn't just a current state, it's been written into the program.

---

## @M1 — META-VECTOR: Structural Remodeling Programs

Programs that reshape physical structural units over developmental time.
Myelination schedules, programmed cell death, neurogenesis rates, microglial
pruning programs.

### Operator: ∫̃

```
∫̃[WINDOW]( STRUCTURAL_TARGET )

STRUCTURAL_TARGET:
  {UNIT@REGION}(property:PROGRAM)

PROGRAM:
  myelin:incomplete→complete       myelination
  survival:if_integrated           programmed cell death rule
  death:if_silent                  pruning of unused neurons
  neurogenesis:rate:VALUE          ongoing cell birth rate
  volume:norm→reduced              regional volume change
```

### Examples:

```
@M1
// Myelination: PFC last to complete (not until mid-20s)
∫̃[0yr–25yr]( {N.pyr:PFC_PYR@PFC}(myelin:incomplete→complete) )

// Programmed apoptosis: over-produced neurons die if unintegrated
∫̃[0yr–2yr]( {N.*@CORTEX}(survival:if_integrated, death:if_silent) )

// Hippocampal neurogenesis: ongoing but declining with age
∫̃[0yr–∞]( {N.gran:DG@HPC}(neurogenesis:rate:decreasing) )

// Chronic stress: hippocampal volume reduction program
∫̃[after:CORT.chronic:6mo]( {N.pyr:HPC_PYR@HPC}(volume:norm→reduced) )

// Exercise intervention: neurogenesis rate increase
∫̃[after:BDNF.sustained_high:4wk]( {N.gran:DG@HPC}(neurogenesis:rate:increased) )
```

### Key principle:

@M1 is why some changes take months to manifest and months to reverse.
Structural remodeling is slow, expensive, and persistent. A hippocampus that
has lost volume from chronic stress doesn't regrow overnight — the ∫̃ program
needs to be overwritten by a sustained counter-signal (exercise, treatment,
environmental enrichment) before the structural target changes.

---

## @M0 — META-SCALAR: Baseline Setpoints

The bottom of the meta stack. Defines what "normal" is for each signal
in this specific system at this developmental stage.

The runtime scalar (@R0) is always being pulled toward the @M0 setpoint.
Acute signals perturb away. The setpoint pulls back. Disease = the attractor
itself has drifted.

### Operator: σ̃

```
σ̃[WINDOW]( BASELINE_TARGET )

BASELINE_TARGET:
  {SIGNAL@REGION}(baseline:before→after)
```

### Examples:

```
@M0
// Developmental DA setpoint: increases through adolescence
σ̃[0yr–20yr]( {L.nt:DA@NAc}(baseline:low→norm) )

// Chronic stress drifts serotonin setpoint
σ̃[after:CORT.chronic:6mo]( {L.nt:5HT@DRN}(baseline:norm→low) )

// Menopause: estrogen baseline decline
σ̃[45yr–55yr]( {L.h:E2@OVR}(baseline:norm→low) )

// Allostatic load: accumulated stress raises cortisol floor
σ̃[cumulative:stress_events]( {L.h:CORT@ADR}(baseline:norm→elevated) )

// Recovery: sustained intervention restores DA setpoint
σ̃[after:treatment.sustained:6mo]( {L.nt:DA@NAc}(baseline:low→norm) )

// Aging: progressive decline in multiple baselines
σ̃[60yr–∞]( {L.nt:DA@VTA}(baseline:norm→declining) )
σ̃[60yr–∞]( {L.nt:ACh@NBM}(baseline:norm→declining) )
```

### Key principle:

@M0 is the answer to "why does this patient's serotonin keep going back to low
even after treatment?" The treatment perturbs the scalar (Δ@R0), but the
meta-setpoint (σ̃) keeps pulling it back. Lasting treatment must change the
setpoint itself — which requires going up through @M1 and @M2 to rewrite
the structural and protocol programs that maintain the setpoint.

This is also where individual differences in drug response live. Two patients
with identical current serotonin levels (same @R0 scalar) but different σ̃
setpoints will respond completely differently to the same SSRI — one is being
pushed further from their setpoint, the other is being pulled toward it.

---

## Meta downward cascade

The META pipeline unfolds top-down:

```
@M3 (architectural plan)
  ↓ architecture requires specific protocol configurations
@M2 (protocol programs)
  ↓ protocol programs require structural support
@M1 (structural remodeling)
  ↓ structural changes set what baselines are physically sustainable
@M0 (baseline setpoints)
  ↓ setpoints feed into BASE via convergence equation
```

Example cascade:

```
1. @M3: after chronic stress, projects AMY→PFC dominance shift
2. @M2: schedules methylation of plasticity genes in PFC, gain increase in AMY circuits
3. @M1: programs PFC dendritic retraction, AMY dendritic expansion
4. @M0: sets DA@PFC baseline low (less prefrontal drive), NE@LC baseline high (more arousal)
5. BASE: runtime scalars now pulled toward these new setpoints
```

---

## Meta ↔ Δ interaction

The META and Δ pipelines interact bidirectionally:

```
Δ→META (bottom-up writing):
  Sustained Δ patterns get written into META as programs.
  If Δ@R2 shows persistent gain reduction in HPC for 3 months,
  @M2 acquires a new ⊲̃ entry locking that gain.

META→Δ (top-down constraint):
  META setpoints constrain what Δ can achieve.
  If σ̃ says DA baseline is "low", then Δ@R0 trying to raise DA
  will be continuously opposed by the setpoint attractor.
  The Δ must be strong enough and sustained enough to
  rewrite the σ̃ itself (which takes much longer).
```

---

## Output order

```
@M3    meta-tensor (projected architecture)
⊗̃ ...

@M2    meta-matrix (protocol programs)
⊲̃ ...

@M1    meta-vector (structural programs)
∫̃ ...

@M0    meta-scalar (baseline setpoints)
σ̃ ...
```

---

## META rules

1. META is OPTIONAL — include only when input implies chronic/developmental context
2. META operates on developmental timescale (not every tick)
3. σ̃ ONLY in @M0. ∫̃ ONLY in @M1. ⊲̃ ONLY in @M2. ⊗̃ ONLY in @M3.
4. Every META operator requires a WINDOW (age range, condition, or cumulative)
5. META targets should reference existing BASE entities
6. ⊲̃→⊲ consistency: @M2 targets must map to existing @R2 protocols
7. ∫̃→∫ consistency: @M1 targets must map to existing @R1 units
8. ⊗̃→⊗ consistency: @M3 targets must map to existing @R3 conditionals
9. Every R0 root signal SHOULD have a σ̃ baseline in @M0 (warn if missing)
10. META without BASE is invalid — META is a program FOR the runtime
11. META without Δ is possible but rare — usually inferred from Δ trends
12. NO English prose. Only codes + operators.

---

## Limitations without convergence

* Setpoints exist but aren't formally integrated with runtime scalars
* BASE runs independently of META pulls (no attractor dynamics)
* Useful for describing the program, but prediction requires CONVERGENCE
