using NeuroGateway.Models;
using NeuroGateway.Repository;

namespace NeuroGateway.Service;

/// <summary>
/// Evaluates dimension scoring quality across all persons in the database.
/// With the shadow-anchored algorithm there are no weights to optimize —
/// calibration now measures discrimination, consistency, and confidence quality.
/// </summary>
public class CalibrationService(
    PersonRepository personRepo,
    DimensionService dimensionService)
{
    public record CalibrationReport(
        int PersonCount,
        DateTime Timestamp,
        QualityMetrics Metrics,
        List<DimensionDiagnostic> Dimensions);

    public record QualityMetrics(
        double MeanConfidence,
        double MeanConsistency,
        double MeanSpread,
        double Discrimination,
        double CoverageRatio);

    public record DimensionDiagnostic(
        string Name,
        string Section,
        double MeanScore,
        double StdDev,
        double MeanConfidence,
        double MeanConsistency,
        int TotalEvidence,
        double Discrimination);

    /// <summary>
    /// Score all persons and compute quality diagnostics for the shadow-anchored scoring.
    /// </summary>
    public async Task<CalibrationReport> RunDiagnosticsAsync()
    {
        var persons = await personRepo.ListAsync();
        var allScores = new Dictionary<string, List<DimensionScore>>();

        foreach (var person in persons)
        {
            var scores = await dimensionService.ScoreAsync(person);
            if (scores.Any(s => s.EvidenceCount > 0))
                allScores[person] = scores;
        }

        if (allScores.Count == 0)
            return new CalibrationReport(0, DateTime.UtcNow,
                new QualityMetrics(0, 0, 0, 0, 0), []);

        // Per-dimension diagnostics
        var dimensions = allScores.Values.First().Select(d => d.Name).ToList();
        var diagnostics = new List<DimensionDiagnostic>();

        var allSpreads = new List<double>();
        var allDiscriminations = new List<double>();

        foreach (var dim in dimensions)
        {
            var dimScores = allScores.Values
                .Select(scores => scores.FirstOrDefault(s => s.Name == dim))
                .Where(s => s is not null)
                .ToList();

            var scores = dimScores.Select(s => (double)s!.Score).ToList();
            var confidences = dimScores.Select(s => (double)s!.Confidence).ToList();
            var consistencies = dimScores.Select(s => (double)s!.Consistency).ToList();
            var totalEvidence = dimScores.Sum(s => s!.EvidenceCount);

            var mean = scores.Average();
            var stddev = scores.Count > 1
                ? Math.Sqrt(scores.Sum(s => (s - mean) * (s - mean)) / scores.Count)
                : 0;

            // Discrimination: how well this dimension separates persons
            // Higher stddev = better discrimination
            var discrimination = Math.Min(stddev / 30.0, 1.0);

            allSpreads.Add(stddev);
            allDiscriminations.Add(discrimination);

            diagnostics.Add(new DimensionDiagnostic(
                dim,
                dimScores.First()!.Section,
                Math.Round(mean, 1),
                Math.Round(stddev, 1),
                Math.Round(confidences.Average(), 3),
                Math.Round(consistencies.Average(), 3),
                totalEvidence,
                Math.Round(discrimination, 3)));
        }

        // Overall metrics
        var allConfidences = allScores.Values
            .SelectMany(scores => scores.Select(s => (double)s.Confidence))
            .ToList();
        var allConsistencies = allScores.Values
            .SelectMany(scores => scores.Select(s => (double)s.Consistency))
            .ToList();

        // Coverage: fraction of dimensions with any evidence across all persons
        var totalDimSlots = allScores.Count * dimensions.Count;
        var coveredSlots = allScores.Values
            .SelectMany(scores => scores)
            .Count(s => s.EvidenceCount > 0);

        var metrics = new QualityMetrics(
            Math.Round(allConfidences.Average(), 3),
            Math.Round(allConsistencies.Average(), 3),
            Math.Round(allSpreads.Average(), 1),
            Math.Round(allDiscriminations.Average(), 3),
            Math.Round((double)coveredSlots / totalDimSlots, 3));

        return new CalibrationReport(
            allScores.Count,
            DateTime.UtcNow,
            metrics,
            diagnostics);
    }
}
