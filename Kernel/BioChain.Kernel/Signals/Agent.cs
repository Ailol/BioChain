using BioChain.Kernel.Agents;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace BioChain.Kernel.Signals;

// ──────────────────────── SIDE EFFECT MESSAGES ────────────────────────

// Wolverine messages — one per side effect type
public sealed record ResolveLlmGate(Guid WorldId, int GateId, string Prompt, string Model,
    string? ParseMap, string? Fallback, int TimeoutMs, bool Cache);

public sealed record ResolveToolInvoke(Guid WorldId, string ToolCode, string Invoke,
    string[] InputCodes, string[] OutputCodes, int TimeoutMs, int RetryCount, string? Fallback);

// Result messages — injected back into next tick
public sealed record LlmGateResolved(Guid WorldId, int GateId, bool Fired, double Confidence);
public sealed record ToolInvokeResolved(Guid WorldId, string ToolCode, Dictionary<string, double> Outputs);

// ──────────────────────── SIDE EFFECT DISPATCHER ────────────────────────

/// <summary>
/// Routes SideEffect records from a tick to Wolverine messages.
/// Called by WorldGrain after each tick completes.
/// </summary>
public static class SideEffectDispatcher
{
    public static async Task DispatchAsync(IMessageBus bus, Guid worldId, SideEffect[] effects)
    {
        foreach (var effect in effects)
        {
            switch (effect)
            {
                case SideEffect.LlmGate llm:
                    await bus.PublishAsync(new ResolveLlmGate(
                        worldId, llm.GateId, llm.Prompt, llm.Model,
                        llm.ParseMap, llm.Fallback, llm.TimeoutMs, llm.Cache));
                    break;

                case SideEffect.ToolInvoke tool:
                    await bus.PublishAsync(new ResolveToolInvoke(
                        worldId, tool.ToolCode, tool.Invoke,
                        tool.InputCodes, tool.OutputCodes,
                        tool.TimeoutMs, tool.RetryCount, tool.Fallback));
                    break;
            }
        }
    }
}

// ──────────────────────── LLM BRIDGE ────────────────────────

/// <summary>
/// Wolverine handler: receives ResolveLlmGate, calls ILlmEngine, returns result.
/// Wolverine's cascading messages pattern: return value is published automatically.
/// </summary>
public sealed class LlmBridge
{
    public static async Task<LlmGateResolved> HandleAsync(
        ResolveLlmGate msg, ILlmEngine engine, ILogger<LlmBridge> log)
    {
        try
        {
            using var cts = new CancellationTokenSource(msg.TimeoutMs);
            var response = await engine.ProcessAsync(msg.Prompt, "", cts.Token);

            var fired = ParseDecision(response, msg.ParseMap);
            var confidence = ParseConfidence(response, msg.ParseMap);

            log.LogDebug("[LlmBridge] Gate {Id}: fired={Fired}, conf={Conf}", msg.GateId, fired, confidence);
            return new LlmGateResolved(msg.WorldId, msg.GateId, fired, confidence);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "[LlmBridge] Gate {Id} failed, using fallback", msg.GateId);
            var fallbackFired = msg.Fallback is not null && msg.Fallback != "false";
            return new LlmGateResolved(msg.WorldId, msg.GateId, fallbackFired, 0.5);
        }
    }

    private static bool ParseDecision(string response, string? parseMap)
    {
        var lower = response.ToLowerInvariant();
        // Try JSON: {"decision": true/false}
        if (lower.Contains("\"decision\""))
            return lower.Contains("\"decision\": true") || lower.Contains("\"decision\":true");
        // Fallback: any "true" in response
        return lower.Contains("true");
    }

    private static double ParseConfidence(string response, string? parseMap)
    {
        // Try JSON: {"confidence": 0.N}
        var idx = response.IndexOf("\"confidence\"", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var start = response.IndexOf(':', idx) + 1;
            var end = response.IndexOfAny([',', '}'], start);
            if (end > start && double.TryParse(response[start..end].Trim(), out var conf))
                return Math.Clamp(conf, 0, 1);
        }
        return 1.0;
    }
}

// ──────────────────────── TOOL BRIDGE ────────────────────────

/// <summary>
/// Wolverine handler: receives ResolveToolInvoke, routes by invoke type.
/// Supports: wasm (Extism), http (REST endpoint), native (C# delegate).
/// </summary>
public sealed class ToolBridge
{
    public static async Task<ToolInvokeResolved> HandleAsync(
        ResolveToolInvoke msg, ILogger<ToolBridge> log)
    {
        var outputs = new Dictionary<string, double>();

        try
        {
            if (msg.Invoke.EndsWith(".wasm"))
            {
                // WASM plugin invocation
                var pluginName = Path.GetFileNameWithoutExtension(msg.Invoke);
                if (!ExtismHost.Plugins.ContainsKey(pluginName))
                    ExtismHost.RegisterPlugin(pluginName, msg.Invoke);

                var input = string.Join(",", msg.InputCodes);
                var result = ExtismHost.Plugins[pluginName].Call(msg.ToolCode, input);

                // Parse result into output signals
                foreach (var code in msg.OutputCodes)
                    if (double.TryParse(result, out var val))
                        outputs[code] = val;
            }
            else if (msg.Invoke.StartsWith("http"))
            {
                // HTTP tool invocation — future
                log.LogDebug("[ToolBridge] HTTP tool not yet implemented: {Invoke}", msg.Invoke);
            }
            else
            {
                // Native tool — future
                log.LogDebug("[ToolBridge] Native tool not yet implemented: {Invoke}", msg.Invoke);
            }
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "[ToolBridge] Tool {Code} failed", msg.ToolCode);
            // Apply fallback values
            if (msg.Fallback is not null)
                foreach (var code in msg.OutputCodes)
                    outputs[code] = 0;
        }

        return new ToolInvokeResolved(msg.WorldId, msg.ToolCode, outputs);
    }
}
