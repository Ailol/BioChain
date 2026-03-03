using System.Text;
using Microsoft.Extensions.AI;

namespace BioChain.AgentFramework;

/// <summary>
/// Builds evolution context from primitive data and calls the LLM.
/// No Repository or Entity dependencies — accepts strings, returns string.
/// Extracted from AgentEcosystemService.BuildEvolutionContext + LLM call.
/// </summary>
public sealed class EvolutionEngine(IChatClient engine)
{
    private static readonly string EvolutionPrompt = PromptLoader.LoadOrDefault(
        "SIGNALS_EVOLUTION_PROMPT.txt",
        "You are a signal graph evolution agent. Analyze the graph and output Signals Kernel DSL predictions and updates.");

    /// <summary>
    /// Builds the user context string from module metadata and calls the LLM.
    /// Returns raw LLM output text.
    /// </summary>
    public async Task<string> EvolveAsync(
        string moduleCode,
        string agentType,
        int generation,
        double utility,
        int hitCount,
        int evalCount,
        string[] watchSignals,
        string subgraphDsl,
        string[] predictionFormulas,
        CancellationToken ct = default)
    {
        var userContext = BuildContext(
            moduleCode, agentType, generation, utility,
            hitCount, evalCount, watchSignals, subgraphDsl, predictionFormulas);

        var response = await engine.GetResponseAsync(
        [
            new ChatMessage(ChatRole.System, EvolutionPrompt),
            new ChatMessage(ChatRole.User, userContext),
        ], cancellationToken: ct);

        return response.Text ?? "";
    }

    /// <summary>
    /// Pure function: builds the context string for the evolution LLM call.
    /// </summary>
    internal static string BuildContext(
        string moduleCode, string agentType, int generation,
        double utility, int hitCount, int evalCount,
        string[] watchSignals, string subgraphDsl,
        string[] predictionFormulas)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## MODULE");
        sb.AppendLine($"Code: {moduleCode}");
        sb.AppendLine($"Agent Type: {agentType}");
        sb.AppendLine($"Generation: {generation}");
        sb.AppendLine($"Utility: {utility:F2} ({hitCount}/{evalCount} hits)");
        if (watchSignals.Length > 0)
            sb.AppendLine($"Watch Signals: {string.Join(", ", watchSignals)}");
        sb.AppendLine();

        sb.AppendLine("## CURRENT GRAPH STATE");
        sb.AppendLine(subgraphDsl);
        sb.AppendLine();

        if (predictionFormulas.Length > 0)
        {
            sb.AppendLine("## PREDICTION HISTORY");
            foreach (var p in predictionFormulas.Take(20))
                sb.AppendLine($"- {p}");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
