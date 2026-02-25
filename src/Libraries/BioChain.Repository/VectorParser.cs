using System.Globalization;

namespace BioChain.Repository;

// Parse pgvector text representation "[0.1,0.2,...]" into float[].
// Shared by all repositories that read vector columns.
public static class VectorParser
{
    public static float[] Parse(string vectorStr)
    {
        var trimmed = vectorStr.Trim('[', ']');
        var parts = trimmed.Split(',');
        var result = new float[parts.Length];
        for (var i = 0; i < parts.Length; i++)
            result[i] = float.Parse(parts[i], CultureInfo.InvariantCulture);
        return result;
    }
}
