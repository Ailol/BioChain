using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using BioChain.Service;

namespace BioChain.Server.Tools;

[McpServerToolType]
public class PersonalityTools(PersonService personService, ProfileService profileService)
{
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    [McpServerTool(Name = "list_persons")]
    [Description("List all persons in the personality system.")]
    public async Task<string> ListPersons()
    {
        var persons = await personService.ListAsync();
        return JsonSerializer.Serialize(new { persons }, IndentedJson);
    }

    [McpServerTool(Name = "create_personality")]
    [Description("Create a new person in the personality system.")]
    public async Task<string> CreatePersonality([Description("Person name")] string name)
    {
        var (personId, personalityId) = await personService.EnsureAsync(name);
        return JsonSerializer.Serialize(new { personId, personalityId, message = $"Created personality for {name}" }, IndentedJson);
    }

    [McpServerTool(Name = "get_personality")]
    [Description("Get personality profile with biochemical profile and communication style.")]
    public async Task<string> GetPersonality([Description("Person name")] string person)
    {
        var style = await profileService.GetCommunicationStyleAsync(person);
        var counts = await profileService.GetSignalCountsAsync(person);
        var profiles = await profileService.GetProfileAsync(person);
        return JsonSerializer.Serialize(new
        {
            person,
            communicationStyle = style,
            signalCounts = counts.Select(c => new { c.Signal, c.Count }),
            profiles = profiles.Select(p => new { p.Signal, p.Formula })
        }, IndentedJson);
    }

}
