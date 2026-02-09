using Pgvector;

namespace Repository;

public static class VectorMath
{
    public static float[]? ToFloatArray(Vector? vector) => vector?.ToArray();
}
