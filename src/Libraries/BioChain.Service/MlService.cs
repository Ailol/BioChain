using BioChain.Repository;

namespace BioChain.Service;

/// <summary>
/// ML algorithms — deferred. Methods throw NotImplementedException until fresh ML implementations are created.
/// </summary>
public class MlService(
    ObservationRepository observationRepo,
    PersonRepository personRepo,
    DimensionService dimensionService)
{
    public Task<SpectralResult> SpectralClusterAsync(string person, int k = 4)
        => throw new NotImplementedException("ML algorithms are being redesigned for v6 schema.");

    public Task<TopologicalFingerprint> TopologicalFingerprintAsync(string person)
        => throw new NotImplementedException("ML algorithms are being redesigned for v6 schema.");

    public Task<VaePersonResult> VaeEncodeAsync(string person, int latentDim = 8, int epochs = 500)
        => throw new NotImplementedException("ML algorithms are being redesigned for v6 schema.");

    public Task<LpaPersonResult> LatentProfilesAsync(string person, int k = 0)
        => throw new NotImplementedException("ML algorithms are being redesigned for v6 schema.");

    public Task<CcaPersonResult> CanonicalCorrelationAsync(string person)
        => throw new NotImplementedException("ML algorithms are being redesigned for v6 schema.");

    public Task<TcnPersonResult> TemporalPredictAsync(string person, int epochs = 100)
        => throw new NotImplementedException("ML algorithms are being redesigned for v6 schema.");
}

// ── Result DTOs (kept for API compatibility) ─────────────────────────────────

public sealed record SpectralResult(int K, List<int> Assignments, List<float[]> Centroids);
public sealed record TopologicalFingerprint(List<(float Birth, float Death)> H0, List<(float Birth, float Death)> H1);
public sealed record VaePersonResult(string Person, VaeLatentFactors Factors, VaeTrainResult Training, int TotalPersons);
public sealed record VaeLatentFactors(float[] Mean, float[] LogVar, float[] Sampled);
public sealed record VaeTrainResult(float FinalLoss, int Epochs);
public sealed record LpaPersonResult(string Person, LpaResult Result, LpaMembership? PersonMembership, List<string> PersonNames);
public sealed record LpaResult(int K, List<LpaMembership> Memberships);
public sealed record LpaMembership(int Cluster, float Probability, float[] Responsibilities);
public sealed record CcaPersonResult(string Person, CcaResult Result, float[]? PersonProjectedA, float[]? PersonProjectedB, List<string> PersonNames);
public sealed record CcaResult(float[] Correlations, List<float[]> ProjectedA, List<float[]> ProjectedB);
public sealed record TcnPersonResult(string Person, TcnResult Result, float TrainingLoss, List<string> SignalOrder, List<DateTime> Dates);
public sealed record TcnResult(List<float[]> Predictions, float[] FinalPrediction);
