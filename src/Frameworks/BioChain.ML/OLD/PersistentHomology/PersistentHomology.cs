using BioChain.ML.OLD;

namespace BioChain.ML.OLD.PersistentHomology;

/// <summary>
/// Persistent homology on embedding vectors → structural fingerprints.
/// Builds a Vietoris-Rips filtration from pairwise distances, tracks connected components (H0)
/// and loops (H1) as the distance threshold grows.
/// Input: embedding vectors. Output: persistence diagram, Betti numbers, topological fingerprint.
/// </summary>
public static class PersistentHomologyComputer
{
    /// <summary>A feature born at birth and dying at death. Dimension 0 = component, 1 = loop.</summary>
    public record PersistenceInterval(int Dimension, float Birth, float Death)
    {
        public float Persistence => Death - Birth;
    }

    public record TopologicalFingerprint(
        List<PersistenceInterval> Diagram,
        int Betti0,     // connected components at max scale
        int Betti1,     // 1-cycles (loops) at max scale
        float[] PersistenceLandscape,   // vectorized summary
        float TotalPersistenceH0,
        float TotalPersistenceH1,
        float MaxPersistenceH0,
        float MaxPersistenceH1);

    /// <summary>
    /// Compute persistent homology up to dimension 1.
    /// </summary>
    /// <param name="embeddings">N embedding vectors</param>
    /// <param name="maxEdgeFraction">Fraction of max distance to use as filtration ceiling (0-1)</param>
    /// <param name="landscapeResolution">Number of bins for persistence landscape vector</param>
    public static TopologicalFingerprint Compute(
        IReadOnlyList<float[]> embeddings,
        float maxEdgeFraction = 1.0f,
        int landscapeResolution = 50)
    {
        var n = embeddings.Count;
        if (n <= 1)
            return new TopologicalFingerprint([], 1, 0, new float[landscapeResolution], 0, 0, 0, 0);

        // 1. Pairwise distance matrix (1 - cosine similarity)
        var edges = BuildSortedEdges(embeddings, n);
        var maxDist = edges.Count > 0 ? edges[^1].Distance * maxEdgeFraction : 1f;

        // 2. Union-Find for H0 (connected components)
        var h0Intervals = ComputeH0(edges, n, maxDist);

        // 3. H1 (loops) via edge-cycle detection
        var h1Intervals = ComputeH1(edges, n, maxDist);

        var diagram = new List<PersistenceInterval>();
        diagram.AddRange(h0Intervals);
        diagram.AddRange(h1Intervals);

        // Betti numbers at max scale
        var betti0 = h0Intervals.Count(i => i.Death >= maxDist);
        var betti1 = h1Intervals.Count(i => i.Death >= maxDist);

        // Persistence statistics
        var totalH0 = h0Intervals.Sum(i => i.Persistence);
        var totalH1 = h1Intervals.Sum(i => i.Persistence);
        var maxH0 = h0Intervals.Count > 0 ? h0Intervals.Max(i => i.Persistence) : 0f;
        var maxH1 = h1Intervals.Count > 0 ? h1Intervals.Max(i => i.Persistence) : 0f;

        // Persistence landscape (vectorized topological summary)
        var landscape = ComputeLandscape(diagram, maxDist, landscapeResolution);

        return new TopologicalFingerprint(diagram, betti0, betti1, landscape, totalH0, totalH1, maxH0, maxH1);
    }

    private record Edge(int I, int J, float Distance);

    private static List<Edge> BuildSortedEdges(IReadOnlyList<float[]> embeddings, int n)
    {
        var edges = new List<Edge>(n * (n - 1) / 2);
        for (var i = 0; i < n; i++)
            for (var j = i + 1; j < n; j++)
            {
                var sim = LinearAlgebra.CosineSimilarity(embeddings[i], embeddings[j]);
                edges.Add(new Edge(i, j, 1f - sim));
            }
        edges.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        return edges;
    }

    /// <summary>H0: track connected components via union-find as edges are added.</summary>
    private static List<PersistenceInterval> ComputeH0(List<Edge> edges, int n, float maxDist)
    {
        var parent = new int[n];
        var rank = new int[n];
        var birthTime = new float[n]; // all born at distance 0
        for (var i = 0; i < n; i++) parent[i] = i;

        var intervals = new List<PersistenceInterval>();

        foreach (var e in edges)
        {
            if (e.Distance > maxDist) break;

            var ri = Find(parent, e.I);
            var rj = Find(parent, e.J);
            if (ri == rj) continue;

            // Merge: younger component dies (the one born later, or smaller rank)
            int survivor, dying;
            if (rank[ri] >= rank[rj]) { survivor = ri; dying = rj; }
            else { survivor = rj; dying = ri; }

            parent[dying] = survivor;
            if (rank[ri] == rank[rj]) rank[survivor]++;

            // Component dies at this edge distance
            intervals.Add(new PersistenceInterval(0, birthTime[dying], e.Distance));
        }

        // Remaining components persist to infinity (capped at maxDist)
        var seen = new HashSet<int>();
        for (var i = 0; i < n; i++)
        {
            var r = Find(parent, i);
            if (seen.Add(r))
                intervals.Add(new PersistenceInterval(0, birthTime[r], maxDist));
        }

        return intervals;
    }

    /// <summary>
    /// H1: detect 1-cycles. When adding an edge that connects two already-connected vertices,
    /// a loop is born. Track loop deaths by checking when triangles fill the loop.
    /// Simplified approach: loops born when edges close cycles, die when shortest
    /// triangulating edge is added.
    /// </summary>
    private static List<PersistenceInterval> ComputeH1(List<Edge> edges, int n, float maxDist)
    {
        var parent = new int[n];
        for (var i = 0; i < n; i++) parent[i] = i;
        var rank = new int[n];

        // Adjacency for triangle detection
        var adj = new HashSet<long>();
        var intervals = new List<PersistenceInterval>();
        var pendingLoops = new List<(float birth, int i, int j)>();

        foreach (var e in edges)
        {
            if (e.Distance > maxDist) break;

            var ri = Find(parent, e.I);
            var rj = Find(parent, e.J);

            if (ri == rj)
            {
                // Edge closes a cycle → loop born
                pendingLoops.Add((e.Distance, e.I, e.J));
            }
            else
            {
                // Union
                if (rank[ri] >= rank[rj]) { parent[rj] = ri; if (rank[ri] == rank[rj]) rank[ri]++; }
                else parent[ri] = rj;
            }

            adj.Add(EdgeKey(e.I, e.J));

            // Check if any pending loop is now filled by a triangle
            for (var p = pendingLoops.Count - 1; p >= 0; p--)
            {
                var (birth, li, lj) = pendingLoops[p];
                var filled = false;
                // A loop (li, lj) is filled if there exists vertex k adjacent to both
                for (var k = 0; k < n && !filled; k++)
                {
                    if (k == li || k == lj) continue;
                    if (adj.Contains(EdgeKey(li, k)) && adj.Contains(EdgeKey(lj, k)))
                        filled = true;
                }

                if (filled)
                {
                    intervals.Add(new PersistenceInterval(1, birth, e.Distance));
                    pendingLoops.RemoveAt(p);
                }
            }
        }

        // Remaining loops persist to maxDist
        foreach (var (birth, _, _) in pendingLoops)
            intervals.Add(new PersistenceInterval(1, birth, maxDist));

        return intervals;
    }

    /// <summary>Persistence landscape: for each scale, max persistence of intervals alive at that scale.</summary>
    private static float[] ComputeLandscape(List<PersistenceInterval> diagram, float maxDist, int resolution)
    {
        var landscape = new float[resolution];
        var step = maxDist / resolution;

        for (var bin = 0; bin < resolution; bin++)
        {
            var t = (bin + 0.5f) * step;
            float maxPers = 0;
            foreach (var interval in diagram)
            {
                if (interval.Birth <= t && interval.Death >= t)
                    maxPers = MathF.Max(maxPers, interval.Persistence);
            }
            landscape[bin] = maxPers;
        }

        return landscape;
    }

    private static int Find(int[] parent, int i)
    {
        while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; }
        return i;
    }

    private static long EdgeKey(int i, int j) => i < j ? (long)i * 100000 + j : (long)j * 100000 + i;
}
