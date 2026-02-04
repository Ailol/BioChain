using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Agents;
using Models;

namespace McpAgentServer.Tools;

[McpServerToolType]
public class PersonalityTools(Agents.PersonalityService svc)
{
    [McpServerTool(Name = "create_personality")]
    [Description("Create a new person in the personality system.")]
    public async Task<string> CreatePersonality([Description("Person name")] string name)
    {
        var created = await svc.CreatePersonalityAsync(name);
        return created ? $"{{\"created\":\"{name}\"}}" : $"{{\"exists\":\"{name}\"}}";
    }

    [McpServerTool(Name = "get_personality")]
    [Description("Get personality profile with behavior traits and neurotransmitter mappings.")]
    public async Task<string> GetPersonality([Description("Person name")] string person)
    {
        var result = await svc.GetPersonalityAsync(person);
        if (result.Profile is not null)
            return JsonSerializer.Serialize(result.Profile);

        return result.Suggestions is { Count: > 0 }
            ? JsonSerializer.Serialize(new { error = "Not found", suggestions = result.Suggestions })
            : "{\"error\":\"Not found\"}";
    }

    [McpServerTool(Name = "full_personality_scan")]
    [Description("Get full personality scan including traits, related hormones and peptides based on neurotransmitter interactions.")]
    public async Task<string> FullPersonalityScan([Description("Person name")] string person)
    {
        var result = await svc.GetFullPersonalityScanAsync(person);
        return result is not null
            ? JsonSerializer.Serialize(result)
            : "{\"error\":\"Not found\"}";
    }

    [McpServerTool(Name = "update_personality")]
    [Description("Submit a behavior/event for the NeuroGroupChat to evaluate. Each neurotransmitter agent decides if it's relevant to them and adds their own explanation if so. You do NOT decide - the agents do.")]
    public async Task<string> UpdatePersonality(
        [Description("Person name")] string person,
        [Description("Topic/event name (e.g., Programming, Morning Routine)")] string topic,
        [Description("Context/description of the behavior or event")] string context)
        => JsonSerializer.Serialize(await svc.UpdatePersonalityAsync(person, topic, context));

    [McpServerTool(Name = "scan_chat_update_personality")]
    [Description("Analyze chat for behavior patterns, optionally auto-add as traits.")]
    public async Task<string> ScanChat(
        [Description("Person name")] string person,
        [Description("Chat JSON: [{\"role\":\"user\",\"content\":\"...\"}]")] string chatJson,
        [Description("Auto-add traits")] bool autoAdd = false)
    {
        var msgs = JsonSerializer.Deserialize<List<OllamaMessage>>(chatJson);
        return msgs?.Count > 0
            ? JsonSerializer.Serialize(await svc.ScanChatAsync(person, msgs, autoAdd))
            : "{\"error\":\"Invalid chat\"}";
    }
}
