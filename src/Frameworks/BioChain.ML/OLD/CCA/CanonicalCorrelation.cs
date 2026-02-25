using BioChain.ML.OLD;

namespace BioChain.ML.OLD.CCA;

/// <summary>
/// Canonical Correlation Analysis — maps biochemical dimension scores to known personality frameworks.
/// Finds linear combinations of Set A (24 biochemical dims) that maximally correlate with
/// Set B (Big Five / MBTI / Attachment axes).
/// Uses SVD on the cross-covariance matrix.
/// </summary>
public static class CanonicalCorrelationAnalyzer
{
    public record CanonicalVariate(int Index, float Correlation, float[] LoadingsA, float[] LoadingsB);

    public record CcaResult(
        List<CanonicalVariate> Variates,
        float[][] ProjectedA,   // N × k canonical scores for biochemical dims
        float[][] ProjectedB,   // N × k canonical scores for personality framework
        float TotalCorrelation);

    /// <summary>
    /// Compute CCA between two variable sets.
    /// </summary>
    /// <param name="A">N observations × dA dimensions (biochemical scores)</param>
    /// <param name="B">N observations × dB dimensions (personality framework scores)</param>
    /// <param name="k">Number of canonical variates to extract (max = min(dA, dB))</param>
    public static CcaResult Compute(IReadOnlyList<float[]> A, IReadOnlyList<float[]> B, int k = 0)
    {
        var n = A.Count;
        var dA = A[0].Length;
        var dB = B[0].Length;
        k = k > 0 ? Math.Min(k, Math.Min(dA, dB)) : Math.Min(dA, dB);

        // 1. Center both sets
        var (centeredA, meanA) = Center(A, n, dA);
        var (centeredB, meanB) = Center(B, n, dB);

        // 2. Compute covariance matrices
        var Caa = Covariance(centeredA, centeredA, n, dA, dA);
        var Cbb = Covariance(centeredB, centeredB, n, dB, dB);
        var Cab = Covariance(centeredA, centeredB, n, dA, dB);

        // 3. Regularize (ridge) for numerical stability
        Regularize(Caa, dA, 1e-4f);
        Regularize(Cbb, dB, 1e-4f);

        // 4. Compute Caa^{-1/2} and Cbb^{-1/2} via eigen-decomposition
        var CaaInvSqrt = InverseSqrt(Caa, dA);
        var CbbInvSqrt = InverseSqrt(Cbb, dB);

        // 5. Form T = Caa^{-1/2} * Cab * Cbb^{-1/2}
        var temp = LinearAlgebra.MatMul(CaaInvSqrt, Cab, dA, dA, dB);
        var T = LinearAlgebra.MatMul(temp, CbbInvSqrt, dA, dB, dB);

        // 6. SVD of T → canonical correlations and directions
        var (U, S, V) = LinearAlgebra.ThinSvd(T, dA, dB, k);

        // 7. Transform back to original space
        // WA = Caa^{-1/2} * U, WB = Cbb^{-1/2} * V
        var WA = LinearAlgebra.MatMul(CaaInvSqrt, U, dA, dA, k);
        var WB = LinearAlgebra.MatMul(CbbInvSqrt, V, dB, dB, k);

        // 8. Project data
        var projA = LinearAlgebra.MatMul(centeredA, WA, n, dA, k);
        var projB = LinearAlgebra.MatMul(centeredB, WB, n, dB, k);

        // 9. Build variates
        var variates = new List<CanonicalVariate>(k);
        float totalCorr = 0;
        for (var i = 0; i < k; i++)
        {
            var loadA = new float[dA];
            var loadB = new float[dB];
            for (var j = 0; j < dA; j++) loadA[j] = WA[j][i];
            for (var j = 0; j < dB; j++) loadB[j] = WB[j][i];
            variates.Add(new CanonicalVariate(i, S[i], loadA, loadB));
            totalCorr += S[i];
        }

        return new CcaResult(variates, projA, projB, totalCorr);
    }

    /// <summary>
    /// Map new biochemical scores to personality framework predictions using trained CCA variates.
    /// </summary>
    public static float[] Predict(float[] biochemScores, CcaResult trainedCca, float[] meanA, float[] meanB)
    {
        var dA = biochemScores.Length;
        var k = trainedCca.Variates.Count;

        // Center
        var centered = new float[dA];
        for (var i = 0; i < dA; i++) centered[i] = biochemScores[i] - meanA[i];

        // Project to canonical space via loadings A
        var canonical = new float[k];
        for (var v = 0; v < k; v++)
        {
            float sum = 0;
            var loadings = trainedCca.Variates[v].LoadingsA;
            for (var i = 0; i < dA; i++) sum += centered[i] * loadings[i];
            canonical[v] = sum;
        }

        // Inverse-project via loadings B + add mean
        var dB = trainedCca.Variates[0].LoadingsB.Length;
        var predicted = new float[dB];
        for (var j = 0; j < dB; j++)
        {
            float sum = 0;
            for (var v = 0; v < k; v++)
                sum += canonical[v] * trainedCca.Variates[v].Correlation * trainedCca.Variates[v].LoadingsB[j];
            predicted[j] = sum + meanB[j];
        }

        return predicted;
    }

    // ── Helpers ──────────────────────────────────────────────────

    private static (float[][] centered, float[] mean) Center(IReadOnlyList<float[]> data, int n, int d)
    {
        var mean = new float[d];
        for (var i = 0; i < n; i++)
            for (var j = 0; j < d; j++) mean[j] += data[i][j];
        for (var j = 0; j < d; j++) mean[j] /= n;

        var centered = LinearAlgebra.NewMatrix(n, d);
        for (var i = 0; i < n; i++)
            for (var j = 0; j < d; j++)
                centered[i][j] = data[i][j] - mean[j];

        return (centered, mean);
    }

    private static float[][] Covariance(float[][] A, float[][] B, int n, int dA, int dB)
    {
        var At = LinearAlgebra.Transpose(A, n, dA);
        var cov = LinearAlgebra.MatMul(At, B, dA, n, dB);
        for (var i = 0; i < dA; i++)
            for (var j = 0; j < dB; j++)
                cov[i][j] /= (n - 1);
        return cov;
    }

    private static void Regularize(float[][] M, int d, float ridge)
    {
        for (var i = 0; i < d; i++) M[i][i] += ridge;
    }

    /// <summary>Compute M^{-1/2} via eigendecomposition: M = V D V^T → M^{-1/2} = V D^{-1/2} V^T</summary>
    private static float[][] InverseSqrt(float[][] M, int d)
    {
        var (eigenValues, eigenVectors) = LinearAlgebra.TopKEigen(M, d, d);

        // D^{-1/2}
        var dInvSqrt = new float[d];
        for (var i = 0; i < d; i++)
            dInvSqrt[i] = eigenValues[i] > 1e-6f ? 1f / MathF.Sqrt(eigenValues[i]) : 0f;

        // V * diag(D^{-1/2}) * V^T
        var result = LinearAlgebra.NewMatrix(d, d);
        for (var i = 0; i < d; i++)
            for (var j = 0; j < d; j++)
            {
                float sum = 0;
                for (var k = 0; k < d; k++)
                    sum += eigenVectors[i][k] * dInvSqrt[k] * eigenVectors[j][k];
                result[i][j] = sum;
            }

        return result;
    }
}
