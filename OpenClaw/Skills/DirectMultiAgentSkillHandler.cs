using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenClaw.Gateway;

namespace OpenClaw.Skills;

/// <summary>
/// Direct skill handler that invokes MultiAgentService and PersonalityService in-process.
/// Use this when OpenClaw integration runs in the same process as the MCP server.
/// </summary>
/// <typeparam name="TMultiAgentService">Type implementing multi-agent chat methods</typeparam>
/// <typeparam name="TPersonalityService">Type implementing personality methods</typeparam>
public class DirectMultiAgentSkillHandler<TMultiAgentService, TPersonalityService> : IMultiAgentSkillHandler
    where TMultiAgentService : class
    where TPersonalityService : class
{
    private readonly TMultiAgentService _multiAgentService;
    private readonly TPersonalityService _personalityService;
    private readonly ILogger<DirectMultiAgentSkillHandler<TMultiAgentService, TPersonalityService>> _logger;

    public DirectMultiAgentSkillHandler(
        TMultiAgentService multiAgentService,
        TPersonalityService personalityService,
        ILogger<DirectMultiAgentSkillHandler<TMultiAgentService, TPersonalityService>> logger)
    {
        _multiAgentService = multiAgentService;
        _personalityService = personalityService;
        _logger = logger;
    }

    public async Task<object> HandleAsync(SkillInvocation invocation, CancellationToken cancellationToken)
    {
        var args = invocation.Arguments ?? new Dictionary<string, object>();

        _logger.LogInformation("Handling skill: {Tool} with {ArgCount} arguments", invocation.Tool, args.Count);

        return invocation.Tool switch
        {
            "hats_chat" => await InvokeHatsChatAsync(args),
            "neuro_chat" => await InvokeNeuroChatAsync(args),
            "group_chat" => await InvokeGroupChatAsync(args),
            "get_personality" => await InvokeGetPersonalityAsync(args),
            "update_personality" => await InvokeUpdatePersonalityAsync(args),
            "full_personality_scan" => await InvokeFullScanAsync(args),
            "create_personality" => await InvokeCreatePersonalityAsync(args),
            "scan_chat_update_personality" => await InvokeScanChatAsync(args),
            _ => throw new InvalidOperationException($"Unknown tool: {invocation.Tool}")
        };
    }

    private async Task<object> InvokeHatsChatAsync(Dictionary<string, object> args)
    {
        var topic = GetRequiredArg<string>(args, "topic");
        var maxIterations = GetOptionalArg(args, "maxIterations", 8);

        var method = _multiAgentService.GetType().GetMethod("RunHatsGroupChatAsync");
        if (method == null) throw new InvalidOperationException("RunHatsGroupChatAsync method not found");

        var task = (Task<string>)method.Invoke(_multiAgentService, new object[] { topic, maxIterations })!;
        return new { result = await task };
    }

    private async Task<object> InvokeNeuroChatAsync(Dictionary<string, object> args)
    {
        var topic = GetRequiredArg<string>(args, "topic");
        var maxIterations = GetOptionalArg(args, "maxIterations", 8);

        var method = _multiAgentService.GetType().GetMethod("RunNeuroGroupChatDiscussionAsync");
        if (method == null) throw new InvalidOperationException("RunNeuroGroupChatDiscussionAsync method not found");

        var task = (Task<string>)method.Invoke(_multiAgentService, new object[] { topic, maxIterations })!;
        return new { result = await task };
    }

    private async Task<object> InvokeGroupChatAsync(Dictionary<string, object> args)
    {
        var topic = GetRequiredArg<string>(args, "topic");
        var maxIterations = GetOptionalArg(args, "maxIterations", 6);

        var method = _multiAgentService.GetType().GetMethod("RunGroupChatAsync");
        if (method == null) throw new InvalidOperationException("RunGroupChatAsync method not found");

        var task = (Task<string>)method.Invoke(_multiAgentService, new object[] { topic, maxIterations })!;
        return new { result = await task };
    }

    private async Task<object> InvokeGetPersonalityAsync(Dictionary<string, object> args)
    {
        var person = GetRequiredArg<string>(args, "person");

        var method = _personalityService.GetType().GetMethod("GetPersonalityAsync");
        if (method == null) throw new InvalidOperationException("GetPersonalityAsync method not found");

        var task = (Task)method.Invoke(_personalityService, new object[] { person })!;
        await task;

        var resultProperty = task.GetType().GetProperty("Result");
        return resultProperty?.GetValue(task) ?? new { error = "No result" };
    }

    private async Task<object> InvokeUpdatePersonalityAsync(Dictionary<string, object> args)
    {
        var person = GetRequiredArg<string>(args, "person");
        var topic = GetRequiredArg<string>(args, "topic");
        var context = GetRequiredArg<string>(args, "context");

        var method = _personalityService.GetType().GetMethod("UpdatePersonalityAsync");
        if (method == null) throw new InvalidOperationException("UpdatePersonalityAsync method not found");

        var task = (Task)method.Invoke(_personalityService, new object[] { person, topic, context })!;
        await task;

        var resultProperty = task.GetType().GetProperty("Result");
        return resultProperty?.GetValue(task) ?? new { error = "No result" };
    }

    private async Task<object> InvokeFullScanAsync(Dictionary<string, object> args)
    {
        var person = GetRequiredArg<string>(args, "person");

        var method = _personalityService.GetType().GetMethod("GetFullPersonalityScanAsync");
        if (method == null) throw new InvalidOperationException("GetFullPersonalityScanAsync method not found");

        var task = (Task)method.Invoke(_personalityService, new object[] { person })!;
        await task;

        var resultProperty = task.GetType().GetProperty("Result");
        return resultProperty?.GetValue(task) ?? new { error = "No result" };
    }

    private async Task<object> InvokeCreatePersonalityAsync(Dictionary<string, object> args)
    {
        var name = GetRequiredArg<string>(args, "name");

        var method = _personalityService.GetType().GetMethod("CreatePersonalityAsync");
        if (method == null) throw new InvalidOperationException("CreatePersonalityAsync method not found");

        var task = (Task<bool>)method.Invoke(_personalityService, new object[] { name })!;
        var created = await task;

        return new { created, name };
    }

    private async Task<object> InvokeScanChatAsync(Dictionary<string, object> args)
    {
        var person = GetRequiredArg<string>(args, "person");
        var chatJson = GetRequiredArg<string>(args, "chatJson");
        var autoAdd = GetOptionalArg(args, "autoAdd", false);

        // Parse chat JSON
        var messages = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(chatJson);
        if (messages == null) throw new ArgumentException("Invalid chat JSON format");

        // Convert to OllamaMessage format
        var ollamaMessages = messages.Select(m => new
        {
            Role = m.GetValueOrDefault("role", "user"),
            Content = m.GetValueOrDefault("content", "")
        }).ToList();

        var method = _personalityService.GetType().GetMethod("ScanChatAsync");
        if (method == null) throw new InvalidOperationException("ScanChatAsync method not found");

        var task = (Task)method.Invoke(_personalityService, new object[] { person, ollamaMessages, autoAdd })!;
        await task;

        var resultProperty = task.GetType().GetProperty("Result");
        return resultProperty?.GetValue(task) ?? new { error = "No result" };
    }

    private static T GetRequiredArg<T>(Dictionary<string, object> args, string name)
    {
        if (!args.TryGetValue(name, out var value))
            throw new ArgumentException($"Missing required argument: {name}");

        if (value is JsonElement element)
        {
            return typeof(T) == typeof(string) 
                ? (T)(object)element.GetString()! 
                : element.Deserialize<T>()!;
        }

        return (T)Convert.ChangeType(value, typeof(T));
    }

    private static T GetOptionalArg<T>(Dictionary<string, object> args, string name, T defaultValue)
    {
        if (!args.TryGetValue(name, out var value))
            return defaultValue;

        if (value is JsonElement element)
        {
            return typeof(T) == typeof(string)
                ? (T)(object)(element.GetString() ?? defaultValue?.ToString() ?? "")
                : element.Deserialize<T>() ?? defaultValue;
        }

        return (T)Convert.ChangeType(value, typeof(T));
    }
}
