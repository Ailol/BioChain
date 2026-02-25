namespace BioChain.ML.OLD;

/// <summary>
/// Shared linear algebra primitives used by all 6 algorithms.
/// Pure C# — no external dependencies.
/// </summary>
public static class LinearAlgebra
{
    // ── Vector ops ──────────────────────────────────────────────

    public static float Dot(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        float d = 0;
        for (var i = 0; i < a.Length; i++) d += a[i] * b[i];
        return d;
    }

    public static float Norm(ReadOnlySpan<float> v)
    {
        float n = 0;
        for (var i = 0; i < v.Length; i++) n += v[i] * v[i];
        return MathF.Sqrt(n);
    }

    public static float CosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        float dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }
        var denom = MathF.Sqrt(na) * MathF.Sqrt(nb);
        return denom > 1e-10f ? dot / denom : 0f;
    }

    public static void Normalize(Span<float> v)
    {
        var n = Norm(v);
        if (n > 1e-10f) for (var i = 0; i < v.Length; i++) v[i] /= n;
    }

    public static float[] Add(float[] a, float[] b)
    {
        var r = new float[a.Length];
        for (var i = 0; i < a.Length; i++) r[i] = a[i] + b[i];
        return r;
    }

    public static float[] Sub(float[] a, float[] b)
    {
        var r = new float[a.Length];
        for (var i = 0; i < a.Length; i++) r[i] = a[i] - b[i];
        return r;
    }

    public static float[] Scale(float[] v, float s)
    {
        var r = new float[v.Length];
        for (var i = 0; i < v.Length; i++) r[i] = v[i] * s;
        return r;
    }

    public static float[] MeanPool(IReadOnlyList<float[]> vectors)
    {
        if (vectors.Count == 0) return [];
        var dim = vectors[0].Length;
        var result = new float[dim];
        foreach (var v in vectors)
            for (var i = 0; i < dim; i++) result[i] += v[i];
        for (var i = 0; i < dim; i++) result[i] /= vectors.Count;
        return result;
    }

    // ── Matrix ops ──────────────────────────────────────────────

    /// <summary>Row-major matrix multiply: C = A * B. A is (m×p), B is (p×n).</summary>
    public static float[][] MatMul(float[][] A, float[][] B, int m, int p, int n)
    {
        var C = NewMatrix(m, n);
        for (var i = 0; i < m; i++)
            for (var k = 0; k < p; k++)
            {
                var aik = A[i][k];
                for (var j = 0; j < n; j++)
                    C[i][j] += aik * B[k][j];
            }
        return C;
    }

    /// <summary>Transpose a (rows × cols) matrix.</summary>
    public static float[][] Transpose(float[][] M, int rows, int cols)
    {
        var T = NewMatrix(cols, rows);
        for (var i = 0; i < rows; i++)
            for (var j = 0; j < cols; j++)
                T[j][i] = M[i][j];
        return T;
    }

    public static float[][] NewMatrix(int rows, int cols)
    {
        var m = new float[rows][];
        for (var i = 0; i < rows; i++) m[i] = new float[cols];
        return m;
    }

    public static float[][] Identity(int n)
    {
        var m = NewMatrix(n, n);
        for (var i = 0; i < n; i++) m[i][i] = 1f;
        return m;
    }

    // ── Eigen / SVD helpers ─────────────────────────────────────

    /// <summary>
    /// Power iteration with deflation to find top-k eigenvectors of symmetric matrix M.
    /// Returns (eigenvalues[k], eigenvectors[n][k]).
    /// </summary>
    public static (float[] values, float[][] vectors) TopKEigen(float[][] M, int n, int k, int maxIter = 300)
    {
        var rng = new Random(42);
        var values = new float[k];
        var vectors = NewMatrix(n, k);
        var found = new List<float[]>();

        for (var eig = 0; eig < k; eig++)
        {
            var v = new float[n];
            for (var i = 0; i < n; i++) v[i] = (float)(rng.NextDouble() - 0.5);
            Normalize(v);

            float eigenvalue = 0;
            for (var iter = 0; iter < maxIter; iter++)
            {
                // Mv
                var mv = new float[n];
                for (var i = 0; i < n; i++)
                {
                    float sum = 0;
                    for (var j = 0; j < n; j++) sum += M[i][j] * v[j];
                    mv[i] = sum;
                }

                // Deflate
                foreach (var prev in found)
                {
                    var d = Dot(mv, prev);
                    for (var i = 0; i < n; i++) mv[i] -= d * prev[i];
                }

                eigenvalue = Norm(mv);
                Normalize(mv);

                var diff = 0f;
                for (var i = 0; i < n; i++) diff += (mv[i] - v[i]) * (mv[i] - v[i]);
                v = mv;
                if (diff < 1e-8f) break;
            }

            found.Add(v);
            values[eig] = eigenvalue;
            for (var i = 0; i < n; i++) vectors[i][eig] = v[i];
        }

        return (values, vectors);
    }

    /// <summary>
    /// Thin SVD via eigen-decomposition of A^T A.
    /// A is (m × n), returns (U[m×k], S[k], V[n×k]).
    /// </summary>
    public static (float[][] U, float[] S, float[][] V) ThinSvd(float[][] A, int m, int n, int k)
    {
        k = Math.Min(k, Math.Min(m, n));
        var At = Transpose(A, m, n);
        var AtA = MatMul(At, A, n, m, n);

        var (eigenvalues, V) = TopKEigen(AtA, n, k);

        var S = new float[k];
        for (var i = 0; i < k; i++)
            S[i] = MathF.Sqrt(MathF.Max(0, eigenvalues[i]));

        // U = A V S^{-1}
        var AV = MatMul(A, V, m, n, k);
        var U = NewMatrix(m, k);
        for (var i = 0; i < m; i++)
            for (var j = 0; j < k; j++)
                U[i][j] = S[j] > 1e-10f ? AV[i][j] / S[j] : 0f;

        return (U, S, V);
    }

    // ── K-means (reusable) ──────────────────────────────────────

    public static (int[] assignments, float[][] centroids) KMeans(
        float[][] data, int n, int dim, int k, int maxIter = 100, int seed = 42)
    {
        var rng = new Random(seed);
        var centroids = new float[k][];

        // K-means++ init
        centroids[0] = (float[])data[rng.Next(n)].Clone();
        for (var c = 1; c < k; c++)
        {
            var dists = new float[n];
            float total = 0;
            for (var i = 0; i < n; i++)
            {
                var minD = float.MaxValue;
                for (var j = 0; j < c; j++)
                    minD = MathF.Min(minD, SqDist(data[i], centroids[j], dim));
                dists[i] = minD;
                total += minD;
            }

            var r = (float)(rng.NextDouble() * total);
            float cum = 0;
            for (var i = 0; i < n; i++)
            {
                cum += dists[i];
                if (cum >= r) { centroids[c] = (float[])data[i].Clone(); break; }
            }
            centroids[c] ??= (float[])data[rng.Next(n)].Clone();
        }

        var assignments = new int[n];
        for (var iter = 0; iter < maxIter; iter++)
        {
            var changed = false;
            for (var i = 0; i < n; i++)
            {
                var best = 0;
                var bestD = SqDist(data[i], centroids[0], dim);
                for (var c = 1; c < k; c++)
                {
                    var d = SqDist(data[i], centroids[c], dim);
                    if (d < bestD) { bestD = d; best = c; }
                }
                if (assignments[i] != best) { assignments[i] = best; changed = true; }
            }
            if (!changed) break;

            var counts = new int[k];
            var sums = new float[k][];
            for (var c = 0; c < k; c++) sums[c] = new float[dim];
            for (var i = 0; i < n; i++)
            {
                counts[assignments[i]]++;
                for (var d = 0; d < dim; d++) sums[assignments[i]][d] += data[i][d];
            }
            for (var c = 0; c < k; c++)
            {
                if (counts[c] == 0) continue;
                for (var d = 0; d < dim; d++) centroids[c][d] = sums[c][d] / counts[c];
            }
        }

        return (assignments, centroids);
    }

    public static float SqDist(ReadOnlySpan<float> a, ReadOnlySpan<float> b, int dim)
    {
        float d = 0;
        for (var i = 0; i < dim; i++) d += (a[i] - b[i]) * (a[i] - b[i]);
        return d;
    }

    // ── Statistics ───────────────────────────────────────────────

    public static float Mean(ReadOnlySpan<float> v)
    {
        float s = 0;
        for (var i = 0; i < v.Length; i++) s += v[i];
        return s / v.Length;
    }

    public static float StdDev(ReadOnlySpan<float> v, float mean)
    {
        float s = 0;
        for (var i = 0; i < v.Length; i++) s += (v[i] - mean) * (v[i] - mean);
        return MathF.Sqrt(s / v.Length);
    }

    public static float Sigmoid(float x) => 1f / (1f + MathF.Exp(-x));

    public static float Tanh(float x) => MathF.Tanh(x);

    public static float ReLU(float x) => MathF.Max(0, x);

    public static float[] Softmax(float[] logits)
    {
        var max = logits[0];
        for (var i = 1; i < logits.Length; i++) if (logits[i] > max) max = logits[i];
        var exp = new float[logits.Length];
        float sum = 0;
        for (var i = 0; i < logits.Length; i++) { exp[i] = MathF.Exp(logits[i] - max); sum += exp[i]; }
        for (var i = 0; i < logits.Length; i++) exp[i] /= sum;
        return exp;
    }

    /// <summary>Gaussian log-probability for a single value.</summary>
    public static float GaussianLogPdf(float x, float mean, float variance)
    {
        var diff = x - mean;
        return -0.5f * (MathF.Log(2f * MathF.PI * variance) + diff * diff / variance);
    }
}
