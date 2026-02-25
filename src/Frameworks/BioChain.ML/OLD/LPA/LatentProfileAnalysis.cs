using BioChain.ML.OLD;

namespace BioChain.ML.OLD.LPA;

/// <summary>
/// Latent Profile Analysis via Gaussian Mixture Model with EM algorithm.
/// Input: 24 dimension scores. Output: probabilistic personality type memberships.
/// Each profile is a Gaussian with its own mean and diagonal covariance per dimension.
/// </summary>
public static class LatentProfileAnalyzer
{
    public record Profile(int Id, string Label, float[] Mean, float[] Variance, float MixingWeight, int PrimaryCount);

    public record Membership(int Index, int PrimaryProfile, float[] Probabilities);

    public record LpaResult(
        List<Profile> Profiles,
        List<Membership> Memberships,
        float LogLikelihood,
        float Bic,
        int Iterations);

    /// <summary>
    /// Fit a Gaussian mixture model to dimension score data.
    /// </summary>
    /// <param name="data">N observations × D dimensions</param>
    /// <param name="k">Number of latent profiles</param>
    /// <param name="maxIter">Max EM iterations</param>
    /// <param name="tol">Convergence tolerance on log-likelihood</param>
    public static LpaResult Fit(
        IReadOnlyList<float[]> data,
        int k = 4,
        int maxIter = 200,
        float tol = 1e-4f)
    {
        var n = data.Count;
        var d = data[0].Length;
        if (n < k) k = Math.Max(1, n);

        // Initialize with k-means
        var (initAssign, initCentroids) = LinearAlgebra.KMeans(
            data.Select(x => x).ToArray(), n, d, k);

        // Mixing weights
        var pi = new float[k];
        var counts = new int[k];
        foreach (var a in initAssign) counts[a]++;
        for (var c = 0; c < k; c++) pi[c] = (float)counts[c] / n;

        // Means
        var mu = new float[k][];
        for (var c = 0; c < k; c++) mu[c] = (float[])initCentroids[c].Clone();

        // Diagonal covariances (init from data variance)
        var sigma = new float[k][];
        for (var c = 0; c < k; c++)
        {
            sigma[c] = new float[d];
            for (var j = 0; j < d; j++) sigma[c][j] = 100f; // initial broad variance
        }

        // Responsibilities: gamma[i][c] = P(profile c | observation i)
        var gamma = new float[n][];
        for (var i = 0; i < n; i++) gamma[i] = new float[k];

        float prevLL = float.NegativeInfinity;
        var iter = 0;

        for (iter = 0; iter < maxIter; iter++)
        {
            // ── E-step: compute responsibilities ────────────────
            float logLik = 0;
            for (var i = 0; i < n; i++)
            {
                var logProbs = new float[k];
                float maxLog = float.NegativeInfinity;

                for (var c = 0; c < k; c++)
                {
                    float lp = MathF.Log(pi[c] + 1e-10f);
                    for (var j = 0; j < d; j++)
                        lp += LinearAlgebra.GaussianLogPdf(data[i][j], mu[c][j], sigma[c][j]);
                    logProbs[c] = lp;
                    if (lp > maxLog) maxLog = lp;
                }

                // Log-sum-exp for numerical stability
                float sumExp = 0;
                for (var c = 0; c < k; c++) sumExp += MathF.Exp(logProbs[c] - maxLog);
                var logTotal = maxLog + MathF.Log(sumExp);
                logLik += logTotal;

                for (var c = 0; c < k; c++)
                    gamma[i][c] = MathF.Exp(logProbs[c] - logTotal);
            }

            // Convergence check
            if (MathF.Abs(logLik - prevLL) < tol) { iter++; break; }
            prevLL = logLik;

            // ── M-step: update parameters ───────────────────────
            for (var c = 0; c < k; c++)
            {
                float nk = 0;
                for (var i = 0; i < n; i++) nk += gamma[i][c];
                nk = MathF.Max(nk, 1e-6f);

                pi[c] = nk / n;

                // Update mean
                for (var j = 0; j < d; j++)
                {
                    float sum = 0;
                    for (var i = 0; i < n; i++) sum += gamma[i][c] * data[i][j];
                    mu[c][j] = sum / nk;
                }

                // Update variance (diagonal)
                for (var j = 0; j < d; j++)
                {
                    float sum = 0;
                    for (var i = 0; i < n; i++)
                    {
                        var diff = data[i][j] - mu[c][j];
                        sum += gamma[i][c] * diff * diff;
                    }
                    sigma[c][j] = MathF.Max(sum / nk, 1e-4f); // floor to prevent collapse
                }
            }
        }

        // Final log-likelihood
        float finalLL = 0;
        for (var i = 0; i < n; i++)
        {
            float logSum = float.NegativeInfinity;
            for (var c = 0; c < k; c++)
            {
                float lp = MathF.Log(pi[c] + 1e-10f);
                for (var j = 0; j < d; j++)
                    lp += LinearAlgebra.GaussianLogPdf(data[i][j], mu[c][j], sigma[c][j]);
                logSum = LogSumExp(logSum, lp);
            }
            finalLL += logSum;
        }

        // BIC = -2 * LL + numParams * ln(n)
        var numParams = k * (1 + 2 * d) - 1; // k mixing weights + k*(d means + d variances)
        var bic = -2f * finalLL + numParams * MathF.Log(n);

        // Build results
        var profiles = new List<Profile>(k);
        var memberships = new List<Membership>(n);

        var primaryCounts = new int[k];
        for (var i = 0; i < n; i++)
        {
            var best = 0;
            for (var c = 1; c < k; c++)
                if (gamma[i][c] > gamma[i][best]) best = c;
            primaryCounts[best]++;
            memberships.Add(new Membership(i, best, (float[])gamma[i].Clone()));
        }

        for (var c = 0; c < k; c++)
            profiles.Add(new Profile(c, $"profile_{c}", mu[c], sigma[c], pi[c], primaryCounts[c]));

        return new LpaResult(profiles, memberships, finalLL, bic, iter);
    }

    /// <summary>
    /// Select optimal k by comparing BIC across range.
    /// </summary>
    public static (int optimalK, List<(int k, float bic)> scores) SelectK(
        IReadOnlyList<float[]> data, int minK = 2, int maxK = 6)
    {
        var scores = new List<(int k, float bic)>();
        for (var k = minK; k <= maxK; k++)
        {
            var result = Fit(data, k);
            scores.Add((k, result.Bic));
        }

        var best = scores.MinBy(s => s.bic);
        return (best.k, scores);
    }

    private static float LogSumExp(float a, float b)
    {
        if (float.IsNegativeInfinity(a)) return b;
        if (float.IsNegativeInfinity(b)) return a;
        var max = MathF.Max(a, b);
        return max + MathF.Log(MathF.Exp(a - max) + MathF.Exp(b - max));
    }
}
