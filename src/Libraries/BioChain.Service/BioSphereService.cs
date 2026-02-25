using BioChain.AnalysisFramework;
using BioChain.Repository;

namespace BioChain.Service;

public class BioSphereService(
    PersonService personService,
    ObservationRepository observationRepo,
    DimensionService dimensionService,
    DimensionDefinitionsService dimDefs,
    SignalRepository signalRepo,
    ActiveLoopRepository loopRepo,
    TrajectoryRepository trajectoryRepo)
{
    private IReadOnlyDictionary<string, string>? _signalLabels;

    private async Task<IReadOnlyDictionary<string, string>> GetSignalLabelsAsync()
    {
        if (_signalLabels is not null) return _signalLabels;
        var signals = await signalRepo.ListAsync();
        _signalLabels = signals.ToDictionary(
            s => s.Key, s => s.Label, StringComparer.OrdinalIgnoreCase);
        return _signalLabels;
    }

    public async Task<BioSphereResponse?> GetDashboardAsync(string person)
    {
        var personId = await personService.FindAsync(person);
        if (personId is null) return null;

        var signalCounts = await observationRepo.GetSignalCountsAsync(person);
        if (signalCounts.Count == 0)
            return new BioSphereResponse(person, DateTime.UtcNow.ToString("o"),
                [], [], [], [], [], [], [], [], []);

        var signalLabels = await GetSignalLabelsAsync();
        var dimensions = await dimensionService.ScoreAsync(person, ScoringMode.Private);
        var timeline = await observationRepo.GetTimelineAsync(person);
        var loops = await loopRepo.GetForPersonAsync(personId.Value);
        var trajectories = await trajectoryRepo.GetActiveAsync(personId.Value);

        // ── Signal Profile ──
        var totalObs = signalCounts.Sum(s => s.Count);
        var signalProfile = signalCounts
            .OrderByDescending(s => s.Count)
            .Take(12)
            .Select(s =>
            {
                var pct = totalObs > 0 ? (int)(s.Count * 100.0 / totalObs) : 0;
                var trend = ComputeTrend(s.Signal, timeline);
                var state = pct > 60 ? "↑↑" : pct > 40 ? "↑" : pct > 20 ? "≈" : "↓";
                return new BioSphereSignalProfile(
                    s.Signal,
                    signalLabels.TryGetValue(s.Signal, out var label) ? label : s.Signal,
                    pct,
                    state,
                    "",
                    trend);
            })
            .ToList();

        // ── Radar (from dimension scores) ──
        var radar = dimensions
            .Where(d => d.Section == "behavioral")
            .Take(8)
            .Select(d => new BioSphereRadarPoint(d.Name, (int)d.Score, 100))
            .ToList();

        // ── Trajectory (from timeline entries) ──
        var trajectory = timeline
            .GroupBy(t => t.CreatedAt.Date)
            .OrderBy(g => g.Key)
            .Take(20)
            .Select(g =>
            {
                var dict = new Dictionary<string, object>
                {
                    ["phase"] = g.Key.ToString("MMM dd"),
                    ["label"] = g.Key.ToString("yyyy-MM-dd")
                };
                foreach (var entry in g)
                    dict[entry.Signal] = entry.Intensity;
                return dict;
            })
            .ToList();

        // ── Active Loops ──
        var activeLoops = loops.Select(l => new BioSphereLoop(
            l.Name,
            l.LoopType,
            l.Status,
            l.Severity ?? "moderate",
            l.Formula,
            l.InvolvedSignals.Select(id => id.ToString()).ToList()
        )).ToList();

        // ── Cascades (populated when signal interaction data is available) ──
        var cascades = new List<BioSphereCascade>();

        // ── Gates (populated when gate instance data is available) ──
        var gates = new List<BioSphereGate>();

        // ── Region Heatmap (populated when region-tagged observations are available) ──
        var regionHeatmap = new List<Dictionary<string, object>>();

        // ── Failure Modes (from active loops) ──
        var failureModes = loops
            .Where(l => l.FailureMode is not null)
            .GroupBy(l => l.FailureMode!)
            .Select(g => new BioSphereFailureMode(
                g.Key,
                g.Count(),
                g.Max(l => l.Severity ?? "moderate"),
                GetSeverityColor(g.Max(l => l.Severity ?? "moderate"))
            ))
            .ToList();

        // ── Lifecycle (from trajectories) ──
        var lifecycle = trajectories.Select(t => new BioSphereLifecycleStage(
            t.Name,
            75,
            50,
            t.Status == "active"
        )).ToList();

        return new BioSphereResponse(
            person,
            timeline.Count > 0 ? timeline.Max(t => t.CreatedAt).ToString("o") : DateTime.UtcNow.ToString("o"),
            signalProfile,
            radar,
            trajectory,
            activeLoops,
            cascades,
            gates,
            regionHeatmap,
            failureModes,
            lifecycle
        );
    }

    private static string ComputeTrend(string signal, List<ObservationRepository.TimelineEntry> timeline)
    {
        var entries = timeline.Where(t => t.Signal == signal).OrderBy(t => t.CreatedAt).ToList();
        if (entries.Count < 2) return "stable";
        var first = entries.Take(entries.Count / 2).Average(e => e.Intensity);
        var second = entries.Skip(entries.Count / 2).Average(e => e.Intensity);
        return second > first * 1.1 ? "increasing" : second < first * 0.9 ? "declining" : "stable";
    }

    private static string GetSeverityColor(string severity) => severity switch
    {
        "critical" => "#f87171",
        "high" => "#fb923c",
        "moderate" => "#fbbf24",
        _ => "#6b7084"
    };
}

// ── DTOs ──

public record BioSphereResponse(
    string Person,
    string LastAnalysis,
    List<BioSphereSignalProfile> SignalProfile,
    List<BioSphereRadarPoint> Radar,
    List<Dictionary<string, object>> Trajectory,
    List<BioSphereLoop> Loops,
    List<BioSphereCascade> Cascades,
    List<BioSphereGate> Gates,
    List<Dictionary<string, object>> RegionHeatmap,
    List<BioSphereFailureMode> FailureModes,
    List<BioSphereLifecycleStage> Lifecycle);

public record BioSphereSignalProfile(
    string Signal, string Label, int Value, string State, string Region, string Trend);

public record BioSphereRadarPoint(string Dim, int Value, int FullMark);

public record BioSphereLoop(
    string Name, string Type, string Status, string Severity, string Formula, List<string> Signals);

public record BioSphereCascade(string Source, List<BioSphereCascadeTarget> Targets);
public record BioSphereCascadeTarget(string Name, int Impact);

public record BioSphereGate(string Gate, string Instance, string Formula, string Status);

public record BioSphereFailureMode(string Name, int Size, string Severity, string Color);

public record BioSphereLifecycleStage(string Stage, int Healthy, int Current, bool Vulnerable);
