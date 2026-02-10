using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using NeuroGateway.Repository;

namespace NeuroGateway.Server.Tools;

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
}
