namespace NeuroGateway.AnalysisFramework;

public sealed record InternalSimulationState(IReadOnlyDictionary<string, float> Levels);

public sealed record InternalSimulationResult(
    List<InternalSimulationState> Trajectory,
    IReadOnlyDictionary<string, float> Initial
);

// One-person forward projection: takes a 27-float profile → runs internal dynamics
// for N steps → predicts where the chemistry is heading. No cross-person coupling.
public static class InternalDynamicsSimulator
{
    private const int DefaultSteps = 50;
    private const float DefaultStepScale = 0.02f;
    private const float MaxDeltaPerStep = 0.05f;

    public static InternalSimulationResult Simulate(
        IReadOnlyDictionary<string, float> profile,
        IReadOnlyDictionary<
            (string Source, string Target),
            (float ModFactor, string? Mechanism)
        > interactions,
        IReadOnlyDictionary<string, int> obsCounts,
        IReadOnlyDictionary<string, float> variances,
        float stepScale = DefaultStepScale,
        int steps = DefaultSteps
    )
    {
        var initial = new Dictionary<string, float>(profile);
        var levels = new Dictionary<string, float>(profile);
        var trajectory = new List<InternalSimulationState>(steps);

        for (var step = 0; step < steps; step++)
        {
            // Snapshot-based: compute all deltas from current state before applying
            var deltas = new Dictionary<string, float>();

            foreach (var ((source, target), (modFactor, _)) in interactions)
            {
                if (!levels.TryGetValue(source, out var sourceLevel))
                    continue;
                if (!levels.TryGetValue(target, out var targetLevel))
                    continue;

                var shift = sourceLevel * modFactor * stepScale;

                // Apply resistance: chemicals near optimal with lots of consistent data are hard to shift
                var optimalCenter = ChemicalConstants.PopulationRanges.TryGetValue(
                    target,
                    out var range
                )
                    ? range.Center
                    : 0.5f;

                obsCounts.TryGetValue(target, out var obsCount);
                variances.TryGetValue(target, out var variance);

                shift = ResistanceEngine.ApplyResistance(
                    targetLevel,
                    optimalCenter,
                    obsCount,
                    variance,
                    shift
                );

                if (!deltas.TryGetValue(target, out var existing))
                    existing = 0f;
                deltas[target] = existing + shift;
            }

            // Per-step delta clamping: cap total shift per chemical to prevent
            // popular targets (cortisol with 8+ inbound) from swinging wildly
            foreach (var chemical in deltas.Keys)
            {
                var clampedDelta = Math.Clamp(deltas[chemical], -MaxDeltaPerStep, MaxDeltaPerStep);
                levels[chemical] = Math.Clamp(
                    levels.GetValueOrDefault(chemical) + clampedDelta,
                    0f,
                    1f
                );
            }

            trajectory.Add(new InternalSimulationState(new Dictionary<string, float>(levels)));
        }

        return new InternalSimulationResult(trajectory, initial);
    }
}
