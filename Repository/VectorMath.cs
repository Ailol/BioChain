using Pgvector;

namespace Repository;

/// <summary>
/// Static vector math utilities — cosine similarity and type conversion helpers.
/// </summary>
public static class VectorMath
{
    /// <summary>
    /// Compute cosine similarity between two float arrays.
    /// Returns 0 if either array is null, empty, or mismatched in length.
    /// </summary>
    public static double CosineSimilarity(float[] a, float[] b)
    {
        if (a == null || b == null || a.Length != b.Length || a.Length == 0)
            return 0;

        double dotProduct = 0, magnitudeA = 0, magnitudeB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        magnitudeA = Math.Sqrt(magnitudeA);
        magnitudeB = Math.Sqrt(magnitudeB);

        if (magnitudeA == 0 || magnitudeB == 0)
            return 0;

        return dotProduct / (magnitudeA * magnitudeB);
    }

    /// <summary>
    /// Convert a Pgvector.Vector to float[] for DTO return types.
    /// Returns null if the input is null.
    /// </summary>
    public static float[]? ToFloatArray(Vector? vector) => vector?.ToArray();
}
