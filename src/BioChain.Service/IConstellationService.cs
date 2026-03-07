namespace BioChain.Service;

public interface IConstellationService
{
    /// <summary>
    /// Fast graph data from DB: nodes, edges, communities, loops, cascades, bridges, geometry.
    /// </summary>
    Task<ConstellationGraphResponse> GetGraphAsync(Guid subjectId, CancellationToken ct = default);

    /// <summary>
    /// LLM-powered deep analysis: narratives, contradictions, compensators, motifs, architecture, perturbations.
    /// </summary>
    Task<ConstellationAnalysisResponse> AnalyzeAsync(Guid subjectId, CancellationToken ct = default);
}
