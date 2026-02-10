using Pgvector;

namespace NeuroGateway.Repository;

public static class VectorMath
{
    public static float[]? ToFloatArray(Vector? vector) => vector?.ToArray();
}
