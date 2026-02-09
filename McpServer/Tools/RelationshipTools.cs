using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Repository;

namespace McpAgentServer.Tools;

[McpServerToolType]
public class RelationshipTools(RelationshipRepository relationshipRepo)
{
    [McpServerTool(Name = "list_relationship_types")]
    [Description("List all relationship types available in the system (dating, friend, coworker, mentor, family, collaborator).")]
    public async Task<string> ListRelationshipTypes()
    {
        var types = await relationshipRepo.ListRelationshipTypesAsync();
        return JsonSerializer.Serialize(new { types });
    }

    [McpServerTool(Name = "get_relationship_profile")]
    [Description("Get a relationship profile for a person and relationship type. Returns the compatibility vector and staleness info.")]
    public async Task<string> GetRelationshipProfile(
        [Description("Person name")] string person,
        [Description("Relationship type (e.g., dating, friend, coworker, mentor, family, collaborator)")] string relationshipType)
    {
        var profile = await relationshipRepo.GetRelationshipProfileAsync(person, relationshipType);
        if (profile == null)
            return JsonSerializer.Serialize(new { error = "No relationship profile found", person, relationshipType });

        return JsonSerializer.Serialize(new
        {
            person = profile.Person,
            relationshipType = profile.RelationshipType,
            hasVector = profile.CompatibilityVector != null,
            createdAt = profile.CreatedAt,
            updatedAt = profile.UpdatedAt
        });
    }

    [McpServerTool(Name = "list_relationship_profiles")]
    [Description("List all relationship profiles for a person, with staleness indicators.")]
    public async Task<string> ListRelationshipProfiles(
        [Description("Person name")] string person)
    {
        var profiles = await relationshipRepo.ListRelationshipProfilesAsync(person);
        var stale = await relationshipRepo.GetStaleRelationshipProfilesAsync(person);

        var staleTypes = stale.Select(s => s.RelationshipType).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var result = profiles.Select(p => new
        {
            p.RelationshipType,
            p.UpdatedAt,
            p.HasVector,
            isStale = staleTypes.Contains(p.RelationshipType)
        }).ToList();

        return JsonSerializer.Serialize(new { person, profiles = result });
    }
}
