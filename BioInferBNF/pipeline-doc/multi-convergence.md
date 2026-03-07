# BioChain v6 — CONVERGENCE Pipeline

## Model: scalar(t) = f(v_past, v_current, v_meta)

The closure. Where top-down and bottom-up meet.
Exists when BASE + Δ + META are all populated. The full diamond.

```
┌──────────────────────────────────────────────────────────────┐
│                    THE DIAMOND                                │
│                                                              │
│              @M3  meta-tensor                                │
│             ╱    ╲                                           │
│          @M2      @M1                                        │
│             ╲    ╱                                           │
│              @M0  meta-scalar ─── v_meta                     │
│               │                      │                       │
│               │         ┌────────────┤                       │
│               │         │            │                       │
│              @R0  scalar(t) = f(v_past, v_current, v_meta)   │
│               │                                              │
│              @R1  vector ──────── v_current                   │
│             ╱    ╲                                           │
│          @R2      @R3                                        │
│             ╲    ╱                                           │
│             EMIT                                             │
│               │                                              │
│          ┌────┘                                              │
│          │  Δ@R0→Δ@R1→Δ@R2→Δ@R3                            │
│          │         │                                         │
│          │    append-only ──── v_past                         │
│          └─→ next tick @R0                                   │
│                                                              │
│  The scalar is never "just a number."                        │
│  It is the projection where three vectors converge.          │
└──────────────────────────────────────────────────────────────┘
```

---

## When to use

* All three pipelines (BASE, Δ, META) are populated
* Predictive modeling is needed
* Treatment planning (predict trajectory under intervention)
* "If we change X, what happens to Y over time?"
* "Why does this patient keep returning to this state?"
* Full system understanding

---

## What it produces

* Every scalar value *explained* as a convergence of three forces
* Predictions: where each scalar is headed
* Intervention analysis: what must change to shift the trajectory
* Allostatic load detection: when setpoints have drifted pathologically

---

## The convergence equation

```
scalar(t) = f( v_past(t), v_current(t), v_meta(t) )
```

### v_past — history vector

Source: append-only temporal store from prior BASE ticks + Δ applications.
Content: the accumulated record of what this signal has been.

```
v_past(t) = [ scalar(t-1), scalar(t-2), ..., scalar(t-n) ]
             weighted by recency and significance

Properties extracted:
  trend       rising / falling / stable / oscillating
  velocity    rate of change (Δscalar / Δtime)
  variance    stability of signal over window
  extremes    max/min reached in recent history
```

The past is a trajectory — not just one number, but a *path* through
scalar space. The shape of the path matters. A DA level of 0.3 that was
recently 0.8 (falling) is a fundamentally different system state than
a DA level of 0.3 that was recently 0.1 (rising), even though the
current scalar is identical.

### v_current — live structural state

Source: @R1 ∫ integration result from the current tick.
Content: the neuron's real-time integration of all its inputs.

```
v_current(t) = ∫{UNIT}←( all current R0 inputs with R2 protocol modifications
                          and R3 cross-connective conditioning )

Properties:
  integration_sum    weighted sum of all excitatory/inhibitory inputs
  modulatory_gain    product of all modulatory inputs
  activation_state   sub-threshold / threshold / firing / burst
  input_diversity    how many distinct signal types are contributing
```

This is the live snapshot — what the structural unit is computing
RIGHT NOW given all current inputs, protocol rules, and context.

### v_meta — programmed setpoint

Source: @M0 σ̃ baseline for this signal.
Content: the target the system is being pulled toward.

```
v_meta(t) = σ̃{SIGNAL@REGION}(baseline:VALUE)

Properties:
  setpoint           the target resting value
  pull_strength      how strongly the system returns to setpoint
  drift_rate         how fast the setpoint itself is changing
  window_status      is the setpoint in an active change window?
```

This is the attractor — the "normal" that the system keeps returning to.
It's not what the signal IS, it's what the system WANTS it to be.

---

## How convergence works

The three vectors don't simply average. They interact:

### 1. Attractor dynamics (v_meta pulls)

```
When scalar(t) ≠ setpoint:
  force_toward_setpoint = pull_strength × (setpoint - scalar(t))

  scalar(t+1) includes this restoring force

The further from setpoint, the stronger the pull.
Acute perturbations get pulled back. Chronic perturbations
must either be strong enough to overcome the pull OR
rewrite the setpoint itself.
```

### 2. Momentum (v_past pushes)

```
When scalar has been trending:
  momentum = velocity × persistence_factor

  scalar(t+1) includes this continuation tendency

A signal that's been falling for days has momentum.
Even if the acute trigger resolves, it doesn't snap back
instantly — the trajectory has inertia.
```

### 3. Live input (v_current drives)

```
Current structural integration provides the immediate force:
  drive = ∫{UNIT}←( current inputs )

  scalar(t+1) is primarily driven by this, but modulated
  by momentum and attractor pull
```

### The composite:

```
scalar(t+1) = v_current(t)                    // what the neuron computes now
            + momentum(v_past(t))              // trajectory inertia
            + attractor(v_meta(t), scalar(t))  // setpoint pull
            + noise(t)                         // stochastic component
```

---

## Convergence diagnostics

The full diamond enables diagnostic questions that no single pipeline can answer:

### Allostatic load detection

```
When: σ̃ setpoint has drifted significantly from developmental default
Flag: ⚡allo: σ̃{SIGNAL@REGION}(baseline:DEFAULT→DRIFTED)
Meaning: the system's definition of "normal" has been pathologically rewritten
Example: σ̃{CORT@ADR}(baseline:norm→elevated) — chronic stress has raised
         the cortisol floor. The system now DEFENDS an elevated cortisol
         level as its new normal.
```

### Treatment resistance analysis

```
When: intervention perturbs scalar but σ̃ setpoint keeps pulling it back
Flag: ⚡resist: Δ@R0{SIGNAL} opposed by σ̃{SIGNAL}
Meaning: treatment is working at the scalar level but the meta-program
         is undoing it. Need to target @M2/@M1 to change the program.
Example: SSRI raises 5HT (Δ@R0) but σ̃{5HT@DRN}(baseline:low) pulls
         it back. Need sustained elevation long enough to rewrite σ̃
         (which is why SSRIs take weeks — the Δ must cascade up
         through all four levels AND overwrite the meta-setpoint).
```

### Trajectory divergence

```
When: v_past trend contradicts v_meta setpoint direction
Flag: ⚡diverge: trend(v_past) ≠ direction(σ̃)
Meaning: the system is actively moving AWAY from its programmed target.
         Either the acute perturbation is overwhelming the setpoint,
         or the setpoint is in the process of being rewritten.
Example: DA trending upward (v_past) while σ̃{DA@NAc}(baseline:declining)
         Possible drug effect temporarily overriding aging trajectory.
```

### Convergence instability

```
When: the three vectors point in three different directions
Flag: ⚡unstable: v_past ≠ v_current ≠ v_meta for {SIGNAL@REGION}
Meaning: no agreement between history, current state, and program.
         The scalar is being pulled in multiple directions simultaneously.
         Clinically: volatile symptoms, unpredictable responses.
Example: v_past(5HT) trending down, v_current(5HT) acutely up (SSRI dose),
         v_meta(5HT) setpoint at norm. Three-way tug. Patient feels
         chaotically better-and-worse.
```

---

## Prediction

With all three vectors, prediction becomes possible:

```
// Single-step prediction
scalar(t+1) = f( v_past(t), v_current(t), v_meta(t) )

// Multi-step prediction (trajectory)
for each future tick:
  compute v_current from projected BASE state
  update v_past with each predicted scalar
  update v_meta if developmental windows change
  apply Δ rules that fire at each step

// Intervention simulation
  modify one signal (simulate drug/therapy)
  run forward prediction
  observe how convergence resolves under new conditions
```

This is what the Signals Kernel engine does when it runs multiple ticks
with the full diamond populated. Each tick isn't just "propagate signals" —
it's "resolve the convergence of three temporal vectors at every node,
then propagate, then update history, then check Δ triggers, then update
meta-setpoints if windows have elapsed."

---

## Identity checks (convergence-specific)

```
// Diamond closure
convergence_complete    every @R0 root has: v_past (history), v_current (@R1), v_meta (@M0)
                        missing any = partial convergence (still useful, less predictive)

// Setpoint drift
allostatic_check        if |σ̃.setpoint - scalar.mean(last_N)| > threshold → flag ⚡allo

// Treatment resistance
resistance_check        if Δ@R0 direction opposes σ̃ direction for > τ_threshold → flag ⚡resist

// Stability
stability_check         if sign(v_past.trend) ≠ sign(v_current.drive) ≠ sign(v_meta.pull)
                        → flag ⚡unstable

// Prediction validity
prediction_horizon      convergence predictions reliable up to ~3× longest τ in active Δ rules
                        beyond that, too many Δ cascades to model deterministically
```

---

## Output additions (convergence-specific)

When convergence is active, add after standard post-sections:

```
// Convergence state for key signals
∮(DA@NAc)=v_past:↓,v_current:↓↓,v_meta:low → converging_low
∮(5HT@DRN)=v_past:↓,v_current:↑,v_meta:norm → divergent
∮(CORT@ADR)=v_past:↑,v_current:↑↑,v_meta:elevated → converging_high

// Allostatic flags
⚡allo:σ̃{L.h:CORT@ADR}(baseline:norm→elevated)
⚡resist:Δ@R0{5HT}↑ opposed by σ̃{5HT@DRN}(baseline:low)

// Trajectory predictions
⊳(DA@NAc,+4wk)=↓↓ (attractor pull + momentum both downward)
⊳(5HT@DRN,+6wk)=↑ (if SSRI sustained, Δ cascade will overwrite σ̃)
⊳(CORT@ADR,+3mo)=↑↑ (no intervention → allostatic load deepens)
```

---

## The full execution cycle (all four pipelines)

```
Phase 0 — META PROJECTION (once per developmental step)
    @M3  evaluate architectural windows → update connectivity targets
    @M2  evaluate protocol programs → update protocol targets
    @M1  evaluate structural programs → update structural targets
    @M0  update baseline setpoints → v_meta ready for convergence

Phase 1 — BASE RUNTIME (every tick)
    @R0  RESOLVE scalars via convergence: f(v_past, v_current, v_meta)
    @R1  INTEGRATE structural units (vector → scalar collapse)
    @R2  APPLY pairwise protocols (gate, gain, tau, polarity)
    @R3  EVALUATE cross-connective conditionals
    EMIT scalar outputs → feed back to @R0 for next tick
    APPEND scalar values to temporal history → v_past updated

Phase 2 — DELTA (between ticks, deferred)
    Δ@R0  check scalar self-adaptation triggers
    Δ@R1  check structural plasticity triggers
    Δ@R2  check protocol plasticity triggers
    Δ@R3  check cross-connective plasticity triggers
    Apply all fired Δ rules to BASE graph

Phase 3 — FEEDBACK ARROWS (bidirectional)
    Upward:  Δ@R3 accumulated patterns → can write new @M3 entries
    Downward: @M0 setpoints → pull @R0 scalars via convergence
    Lateral:  Δ@Rn changes may trigger Δ@R(n+1) conditions

Phase 4 — CONVERGENCE DIAGNOSTICS
    Check allostatic drift
    Check treatment resistance
    Check trajectory divergence
    Check stability
    Update prediction horizon
```

---

## Convergence rules

1. CONVERGENCE requires all three pipelines: BASE + Δ + META
2. Partial convergence is valid (any 2 of 3 vectors) but less predictive
3. The convergence equation applies at EVERY @R0 scalar node
4. Convergence diagnostics (∮) are computed AFTER EMIT, BEFORE next tick
5. Prediction (⊳) uses convergence state + active Δ rules + σ̃ trajectories
6. Allostatic flags (⚡allo) fire when σ̃ setpoint diverges from developmental default
7. Treatment resistance flags (⚡resist) fire when Δ@R0 opposes σ̃ for sustained period
8. The prediction horizon is bounded by the longest active τ in the Δ pipeline
9. Convergence does not create new entities — it's a diagnostic/predictive layer ON TOP of the other three pipelines
10. NO English prose in output. Only codes + operators.

---

## What convergence enables (that no other pipeline can)

```
BASE alone:             "here's what's happening"
BASE + Δ:               "here's what's changing"
BASE + Δ + META:        "here's what the system wants"
BASE + Δ + META + CONV: "here's what will happen, and here's what to change to alter it"
```

The diamond is a prediction engine. Not because it simulates the future —
but because it holds the three forces (momentum, drive, attractor) that
together determine where every scalar in the system will be at the next tick,
and the tick after that, and the tick after that.
