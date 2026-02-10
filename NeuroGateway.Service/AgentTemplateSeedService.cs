using Microsoft.EntityFrameworkCore;
using NeuroGateway.Models;
using NeuroGateway.Repository;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace NeuroGateway.Service;

/// <summary>
/// Startup service: loads YAML agent templates from disk and upserts them into the agent_template table.
/// Replaces the old init.sql INSERT INTO agent_template blocks.
/// Safe to call on every startup — uses INSERT ... ON CONFLICT DO UPDATE.
/// </summary>
public class AgentTemplateSeedService(IDbContextFactory<PersonalityDbContext> factory)
{
    private static readonly IDeserializer Yaml = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    /// <summary>
    /// Seed all agent templates from YAML files. Safe to call on every startup.
    /// </summary>
    public async Task SeedAsync()
    {
        var basePath = FindAgentTemplatesPath();
        if (basePath == null)
        {
            Console.Error.WriteLine("AgentTemplateSeedService: AgentTemplates directory not found — skipping seed.");
            return;
        }

        var groupPath = Path.Combine(basePath, "GroupAgents");
        var layerPath = Path.Combine(basePath, "LayerAgents");

        await using var ctx = await factory.CreateDbContextAsync();
        var conn = ctx.Database.GetDbConnection();
        await conn.OpenAsync();

        int count = 0;

        // 1. Seed analyzing agents (GroupAgents/*.yaml)
        if (Directory.Exists(groupPath))
        {
            foreach (var file in Directory.GetFiles(groupPath, "*.yaml"))
            {
                var yaml = await File.ReadAllTextAsync(file);
                var group = Yaml.Deserialize<GroupAgentsYaml>(yaml);
                foreach (var agent in group.Agents)
                {
                    await UpsertTemplateAsync(conn,
                        category: group.Category,
                        groupName: null,
                        name: agent.Name,
                        layer: null,
                        role: agent.Role,
                        responsibilities: agent.Responsibilities,
                        style: agent.Style,
                        maxWords: agent.MaxWords,
                        isSynthesizer: false,
                        sortOrder: agent.SortOrder);
                    count++;
                }
            }
        }

        // 2. Seed neurochat agents (LayerAgents/agents.yaml — unified config with relationship modes)
        if (Directory.Exists(layerPath))
        {
            var agentsFile = Path.Combine(layerPath, "agents.yaml");
            if (File.Exists(agentsFile))
            {
                var yaml = await File.ReadAllTextAsync(agentsFile);
                var config = Yaml.Deserialize<UnifiedLayerAgentsYaml>(yaml);

                foreach (var (_, mode) in config.RelationshipModes)
                {
                    foreach (var agent in config.Agents)
                    {
                        // Expand {mode.*} placeholders in style
                        var expandedStyle = agent.Style
                            .Replace("{mode.tone}", mode.Tone)
                            .Replace("{mode.goal}", mode.Goal)
                            .Replace("{mode.synth_instruction}", mode.SynthInstruction);

                        var maxWords = agent.IsSynthesizer ? mode.SynthMaxWords : mode.LayerMaxWords;

                        await UpsertTemplateAsync(conn,
                            category: "neurochat",
                            groupName: mode.Label,
                            name: agent.Name,
                            layer: agent.Layer,
                            role: agent.Role,
                            responsibilities: null,
                            style: expandedStyle,
                            maxWords: maxWords,
                            isSynthesizer: agent.IsSynthesizer,
                            sortOrder: agent.SortOrder);
                        count++;
                    }
                }
            }
        }

        Console.Error.WriteLine($"AgentTemplateSeedService: Seeded {count} agent templates from YAML.");
    }

    /// <summary>
    /// Upsert a single agent_template row. Uses different SQL for NULL vs non-NULL group_name
    /// because PostgreSQL UNIQUE constraints treat NULLs as distinct.
    /// </summary>
    private static async Task UpsertTemplateAsync(
        System.Data.Common.DbConnection conn,
        string category, string? groupName, string name, string? layer,
        string role, List<string>? responsibilities, string style,
        int maxWords, bool isSynthesizer, int sortOrder)
    {
        await using var cmd = conn.CreateCommand();

        if (groupName != null)
        {
            // Neurochat agents: group_name is NOT NULL, standard ON CONFLICT works
            cmd.CommandText = """
                INSERT INTO agent_template (category, group_name, name, layer, role, responsibilities, style, max_words, is_synthesizer, sort_order, created_at, updated_at)
                VALUES (@category, @groupName, @name, @layer, @role, @responsibilities, @style, @maxWords, @isSynthesizer, @sortOrder, NOW(), NOW())
                ON CONFLICT (category, group_name, name) DO UPDATE SET
                    layer = EXCLUDED.layer,
                    role = EXCLUDED.role,
                    responsibilities = EXCLUDED.responsibilities,
                    style = EXCLUDED.style,
                    max_words = EXCLUDED.max_words,
                    is_synthesizer = EXCLUDED.is_synthesizer,
                    sort_order = EXCLUDED.sort_order,
                    updated_at = NOW()
                """;
            AddParam(cmd, "@groupName", groupName);
        }
        else
        {
            // Analyzing agents: group_name IS NULL — use UPDATE ... WHERE + INSERT fallback
            cmd.CommandText = """
                WITH existing AS (
                    UPDATE agent_template SET
                        layer = @layer,
                        role = @role,
                        responsibilities = @responsibilities,
                        style = @style,
                        max_words = @maxWords,
                        is_synthesizer = @isSynthesizer,
                        sort_order = @sortOrder,
                        updated_at = NOW()
                    WHERE category = @category AND group_name IS NULL AND name = @name
                    RETURNING id
                )
                INSERT INTO agent_template (category, group_name, name, layer, role, responsibilities, style, max_words, is_synthesizer, sort_order, created_at, updated_at)
                SELECT @category, NULL, @name, @layer, @role, @responsibilities, @style, @maxWords, @isSynthesizer, @sortOrder, NOW(), NOW()
                WHERE NOT EXISTS (SELECT 1 FROM existing)
                """;
        }

        AddParam(cmd, "@category", category);
        AddParam(cmd, "@name", name);
        AddParam(cmd, "@layer", layer ?? (object)DBNull.Value);
        AddParam(cmd, "@role", role);

        var respParam = cmd.CreateParameter();
        respParam.ParameterName = "@responsibilities";
        respParam.Value = responsibilities is { Count: > 0 }
            ? responsibilities.ToArray()
            : DBNull.Value;
        cmd.Parameters.Add(respParam);

        AddParam(cmd, "@style", style);
        AddParam(cmd, "@maxWords", maxWords);
        AddParam(cmd, "@isSynthesizer", isSynthesizer);
        AddParam(cmd, "@sortOrder", sortOrder);

        await cmd.ExecuteNonQueryAsync();
    }

    private static void AddParam(System.Data.Common.DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }

    /// <summary>
    /// Find the AgentTemplates directory — works from both build output and project root.
    /// </summary>
    private static string? FindAgentTemplatesPath()
    {
        var candidate = Path.Combine(AppContext.BaseDirectory, "AgentTemplates");
        if (Directory.Exists(candidate)) return candidate;

        candidate = Path.Combine(Directory.GetCurrentDirectory(), "AgentTemplates");
        if (Directory.Exists(candidate)) return candidate;

        return null;
    }
}
