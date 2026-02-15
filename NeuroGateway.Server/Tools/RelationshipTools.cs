using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using NeuroGateway.Repository;

namespace NeuroGateway.Server.Tools;

[McpServerToolType]
public class RelationshipTools(RelationshipRepository relationshipRepo)
{
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    [McpServerTool(Name = "list_relationship_types")]
    [Description("List all relationship types available in the system.")]
    public async Task<string> ListRelationshipTypes()
    {
        var types = await relationshipRepo.ListAsync();
        return JsonSerializer.Serialize(new
        {
            relationshipTypes = types.Select(t => new { t.Name, t.Description })
        }, IndentedJson);
    }
}
