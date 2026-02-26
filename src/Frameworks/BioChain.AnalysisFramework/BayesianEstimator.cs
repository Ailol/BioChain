using BioChain.Models;

namespace BioChain.AnalysisFramework;

/// <summary>
/// Bayesian online posterior estimation over 5 signal levels using Dirichlet-Categorical model.
/// Pure computation — no DB, no DI. Works from observation #1 (no time-series required).
/// </summary>
public static class BayesianEstimator
{
    private const int NumLevels = 5;
    private static readonly float MaxEntropy = MathF.Log(NumLevels);

    /// <summary>
    /// Estimate posterior distribution over 5 levels from a sequence of level estimates.
    /// Levels should be ordered newest-first; exponential halving decay is applied.
    /// </summary>
    /// <param name="levels">Observed levels (1-5 continuous), newest first</param>
    /// <returns>Posterior distribution with MAP, entropy, and surprise</returns>
    public static BayesianPosterior EstimatePosterior(ReadOnlySpan<float> levels)
    {
        // Uniform Dirichlet prior
        Span<float> alpha = stackalloc float[NumLevels];
        for (var i = 0; i < NumLevels; i++) alpha[i] = 1f;

        // Accumulate pseudo-counts with halving decay (newest observations weighted most)
        for (var i = 0; i < levels.Length; i++)
        {
            var level = Math.Clamp(levels[i], 1f, 5f);
            var weight = MathF.Pow(0.5f, i); // halving decay

            // Distribute weight across nearest levels (soft assignment)
            var lower = (int)MathF.Floor(level) - 1; // 0-indexed
            var upper = Math.Min(lower + 1, NumLevels - 1);
            lower = Math.Max(lower, 0);

            var frac = level - MathF.Floor(level);
            alpha[lower] += weight * (1f - frac);
            if (upper != lower)
                alpha[upper] += weight * frac;
        }

        // Compute posterior statistics
        var alphaSum = 0f;
        for (var i = 0; i < NumLevels; i++) alphaSum += alpha[i];

        var mapLevel = 0f;
        var mapProb = 0f;
        var meanLevel = 0f;
        var entropy = 0f;

        for (var i = 0; i < NumLevels; i++)
        {
            var prob = alpha[i] / alphaSum;
            var levelValue = i + 1f; // 1-indexed

            meanLevel += levelValue * prob;

            if (prob > mapProb)
            {
                mapProb = prob;
                mapLevel = levelValue;
            }

            if (prob > 1e-10f)
                entropy -= prob * MathF.Log(prob);
        }

        // Surprise: KL divergence from uniform prior (measures how much data has taught us)
        var uniformProb = 1f / NumLevels;
        var surprise = 0f;
        for (var i = 0; i < NumLevels; i++)
        {
            var prob = alpha[i] / alphaSum;
            if (prob > 1e-10f)
                surprise += prob * MathF.Log(prob / uniformProb);
        }

        var confidence = 1f - entropy / MaxEntropy;
        var alphaArray = new float[NumLevels];
        alpha.CopyTo(alphaArray);

        var interpretation = BuildInterpretation(mapLevel, confidence, levels.Length);

        return new BayesianPosterior(
            alphaArray, mapLevel, meanLevel, entropy, surprise, confidence, interpretation);
    }

    /// <summary>
    /// Compute surprise (KL divergence) for a single new observation against existing posterior.
    /// High KL = this observation is unexpected given the current belief.
    /// </summary>
    public static float ComputeSurprise(float newLevel, float[] currentAlpha)
    {
        var alphaSum = currentAlpha.Sum();
        var level = Math.Clamp(newLevel, 1f, 5f);
        var idx = Math.Clamp((int)MathF.Round(level) - 1, 0, NumLevels - 1);

        // Probability of this level under current posterior
        var prob = currentAlpha[idx] / alphaSum;

        // Surprise = -log(prob) (self-information / surprisal)
        return prob > 1e-10f ? -MathF.Log(prob) : 10f;
    }

    private static string BuildInterpretation(float mapLevel, float confidence, int dataPoints)
    {
        var levelDesc = mapLevel switch
        {
            <= 1.5f => "very low",
            <= 2.5f => "low",
            <= 3.5f => "moderate",
            <= 4.5f => "elevated",
            _ => "very high"
        };

        var confDesc = confidence switch
        {
            >= 0.7f => "highly confident",
            >= 0.4f => "moderately confident",
            _ => "uncertain"
        };

        return dataPoints < 3
            ? $"{confDesc} {levelDesc} (limited data: {dataPoints} observations)"
            : $"{confDesc} {levelDesc}";
    }
}
