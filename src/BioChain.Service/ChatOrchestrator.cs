using System.Text;
using System.Text.Json;
using BioChain.Agent;
using BioChain.Models;
using Microsoft.Extensions.Logging;

namespace BioChain.Service;

/// <summary>
/// Agentic chat orchestrator. Manages the multi-turn tool-use loop
/// where the LLM can call tools to inspect, simulate, and analyze
/// the biochemical network.
/// </summary>
public sealed class ChatOrchestrator
{
    private readonly SpacetimeService _stdb;
    private readonly LlmClient _llm;
    private readonly ToolExecutor _tools;
    private readonly PromptStore _prompts;
    private readonly ILogger<ChatOrchestrator> _logger;

    private const int MaxToolRounds = 5;

    public ChatOrchestrator(
        SpacetimeService stdb, LlmClient llm, ToolExecutor tools, PromptStore prompts, ILogger<ChatOrchestrator> logger)
    {
        _stdb = stdb;
        _llm = llm;
        _tools = tools;
        _prompts = prompts;
        _logger = logger;
    }

    public async Task<ChatResult> ChatAsync(
        ulong programId, string message, List<ChatTurn>? history = null, CancellationToken ct = default)
    {
        _logger.LogInformation("Agentic chat on program {ProgramId}: {Message}",
            programId, message[..Math.Min(80, message.Length)]);

        var context = _stdb.GetProgramContext(programId);
        _logger.LogInformation("Program context: {Len} chars", context.Length);

        var systemPrompt = _prompts.GetChatPrompt(context);

        var messages = new List<Dictionary<string, object>>
        {
            new() { ["role"] = "system", ["content"] = systemPrompt }
        };

        if (history is not null)
        {
            foreach (var turn in history)
                messages.Add(new Dictionary<string, object> { ["role"] = turn.Role, ["content"] = turn.Content });
        }

        messages.Add(new Dictionary<string, object> { ["role"] = "user", ["content"] = message });

        var toolActions = new List<ToolAction>();
        var finalResponse = await RunToolLoopAsync(programId, messages, toolActions, ct);

        return new ChatResult
        {
            ProgramId = programId,
            Response = finalResponse,
            ContextLength = context.Length,
            ToolActions = toolActions
        };
    }

    private async Task<string> RunToolLoopAsync(
        ulong programId,
        List<Dictionary<string, object>> messages,
        List<ToolAction> toolActions,
        CancellationToken ct)
    {
        for (int round = 0; round < MaxToolRounds; round++)
        {
            var content = await _llm.ChatAsync(messages, ToolRegistry.Definitions, ct);
            var toolCalls = ToolCallParser.Parse(content);

            if (toolCalls.Count == 0)
            {
                _logger.LogInformation("Agentic loop completed after {Rounds} tool rounds", round);
                return content.Trim();
            }

            _logger.LogInformation("Round {Round}: LLM requested {Count} tool call(s)",
                round + 1, toolCalls.Count);

            messages.Add(new Dictionary<string, object>
            {
                ["role"] = "assistant",
                ["content"] = content
            });

            var resultSb = new StringBuilder();
            foreach (var tc in toolCalls)
            {
                _logger.LogInformation("Executing tool: {Tool}({Args})",
                    tc.Name, tc.Arguments[..Math.Min(200, tc.Arguments.Length)]);

                string toolResult;
                try
                {
                    toolResult = await _tools.ExecuteAsync(programId, tc.Name, tc.Arguments, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Tool execution failed: {Tool}", tc.Name);
                    toolResult = JsonSerializer.Serialize(new { error = ex.Message });
                }

                toolActions.Add(new ToolAction
                {
                    Tool = tc.Name,
                    Arguments = tc.Arguments,
                    Result = toolResult.Length > 500 ? toolResult[..500] + "..." : toolResult
                });

                resultSb.AppendLine($"[Tool result for {tc.Name}]:");
                resultSb.AppendLine(toolResult);
                resultSb.AppendLine();
            }

            messages.Add(new Dictionary<string, object>
            {
                ["role"] = "user",
                ["content"] = $"Here are the tool results:\n\n{resultSb}\n\nPlease analyze these results and continue. If you need more information, call another tool. Otherwise, provide your final analysis."
            });
        }

        _logger.LogWarning("Agentic loop hit max rounds ({Max})", MaxToolRounds);
        return "I've reached the maximum number of tool-use rounds. Here's what I found so far based on the tool results above.";
    }
}
